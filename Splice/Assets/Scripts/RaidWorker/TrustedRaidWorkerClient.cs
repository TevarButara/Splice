using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Splice.RaidWorker
{
    [Serializable]
    public sealed class RaidWorkerLoadoutEntry { public string cardId; public int count; }

    [Serializable]
    public sealed class RaidWorkerCombatPayload
    {
        public int maxHealth;
        public int armor;
        public int attackDamage;
        public int attackCooldownMs;
        public int attackRangeMilli;
        public int moveSpeedMilli;
        public string abilityId;
        public int abilityDamage;
        public int abilityCooldownMs;
        public int abilityCastRangeMilli;
        public int abilityRadiusMilli;
    }

    [Serializable]
    public sealed class RaidWorkerHeroAuthority
    {
        public string contentId;
        public int level;
        public long basePower;
        public long scaledPower;
        public RaidWorkerCombatPayload combat;
    }

    [Serializable]
    public sealed class RaidWorkerGearAuthority
    {
        public string instanceId;
        public string contentId;
        public int level;
        public long basePower;
        public long scaledPower;
        public RaidWorkerCombatPayload combat;
    }

    [Serializable]
    public sealed class RaidJobResponse
    {
        public bool hasJob;
        public string error;
        public string raidId;
        public string allocationId;
        public string workerId;
        public string targetSnapshotId;
        public string loadoutSnapshotId;
        public string sceneContractVersion;
        public long attackerPower;
        public long armyPower;
        public long heroPower;
        public long gearPower;
        public long defenderPower;
        public List<RaidWorkerLoadoutEntry> loadoutEntries;
        public RaidWorkerHeroAuthority hero;
        public List<RaidWorkerGearAuthority> gearItems;
        public string leaseExpiresUtc;
    }

    [Serializable]
    internal sealed class ClaimJobRequest { public string workerId; }

    [Serializable]
    internal sealed class SubmitRaidResultRequest
    {
        public string allocationId;
        public string ticket = string.Empty;
        public string workerId;
        public string resultId;
        public string outcome;
        public int breachedRings;
        public int durationMs;
        public string simulationHash;
    }

    [Serializable]
    internal sealed class SubmitRaidResultResponse { public bool success; public string error; }

    public sealed class TrustedRaidWorkerClient
    {
        private readonly string baseUrl;
        private readonly string serverId;
        private readonly string serverKey;

        public TrustedRaidWorkerClient(string baseUrl, string serverId, string serverKey)
        {
            this.baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            this.serverId = serverId ?? string.Empty;
            this.serverKey = serverKey ?? string.Empty;
            if (!Uri.TryCreate(this.baseUrl, UriKind.Absolute, out _) ||
                string.IsNullOrWhiteSpace(this.serverId) || string.IsNullOrWhiteSpace(this.serverKey))
                throw new ArgumentException("Trusted worker endpoint and identity are required.");
        }

        public async Task<RaidJobResponse> ClaimAsync(string workerId, CancellationToken cancellationToken) =>
            await SendAsync<ClaimJobRequest, RaidJobResponse>("/internal/v1/raid-jobs/claim",
                new ClaimJobRequest { workerId = workerId }, cancellationToken);

        public async Task SubmitResultAsync(RaidJobResponse job, RaidSimulationResult result,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync<SubmitRaidResultRequest, SubmitRaidResultResponse>(
                "/internal/v1/raids/" + job.raidId + "/result",
                new SubmitRaidResultRequest
                {
                    allocationId = job.allocationId,
                    workerId = job.workerId,
                    resultId = Guid.NewGuid().ToString("D"),
                    outcome = result.outcome,
                    breachedRings = result.breachedRings,
                    durationMs = result.durationMs,
                    simulationHash = result.simulationHash,
                }, cancellationToken);
            if (response?.success != true)
                throw new InvalidOperationException(response?.error ?? "Raid result settlement was rejected.");
        }

        public static bool IsAllowedInternalPath(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.StartsWith("/internal/v1/", StringComparison.Ordinal) &&
            !path.Contains("..", StringComparison.Ordinal);

        private async Task<TResponse> SendAsync<TRequest, TResponse>(string path, TRequest request,
            CancellationToken cancellationToken) where TResponse : class
        {
            if (!IsAllowedInternalPath(path))
                throw new InvalidOperationException("Trusted worker may call only internal v1 routes.");
            using var webRequest = new UnityWebRequest(baseUrl + path, UnityWebRequest.kHttpVerbPOST);
            var json = JsonUtility.ToJson(request);
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("X-Raid-Server-Id", serverId);
            webRequest.SetRequestHeader("X-Raid-Server-Key", serverKey);
            webRequest.SetRequestHeader("X-Request-Id", Guid.NewGuid().ToString("D"));
            webRequest.SetRequestHeader("Idempotency-Key", Guid.NewGuid().ToString("N"));
            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"Trusted worker HTTP {webRequest.responseCode}: " +
                                                    webRequest.downloadHandler.text);
            return JsonUtility.FromJson<TResponse>(webRequest.downloadHandler.text);
        }
    }
}
