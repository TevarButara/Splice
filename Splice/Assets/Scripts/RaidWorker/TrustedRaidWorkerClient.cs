using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
    public sealed class RaidWorkerVector3 { public float x; public float y; public float z; }

    [Serializable]
    public sealed class RaidWorkerTower
    {
        public string towerId;
        public RaidWorkerVector3 position = new();
        public int attackLevel;
        public int healthLevel;
        public int armorLevel;
        public int rangeLevel;
        public int targetsLevel;
    }

    [Serializable]
    public sealed class RaidWorkerGarrison
    {
        public string cardId;
        public RaidWorkerVector3 position = new();
    }

    [Serializable]
    public sealed class RaidWorkerBaseLayout
    {
        public int version;
        public string ownerAccountId;
        public string factionId;
        public List<RaidWorkerTower> towers = new();
        public List<RaidWorkerGarrison> garrison = new();
        public List<string> minerCardIds = new();
        public int storedGold;
    }

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
        public int maxTargets = 1;
    }

    [Serializable]
    public sealed class RaidWorkerUnitAuthority
    {
        public string actorId;
        public string contentId;
        public string unitKind;
        public int count = 1;
        public long basePower;
        public long scaledPower;
        public RaidWorkerCombatPayload combat;
        public RaidWorkerVector3 position = new();
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
        public RaidWorkerBaseLayout targetSnapshot;
        public List<RaidWorkerLoadoutEntry> loadoutEntries;
        public List<RaidWorkerUnitAuthority> armyUnits;
        public List<RaidWorkerUnitAuthority> defenseUnits;
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
        public string simulationVersion;
        public int tickCount;
        public int commandCount;
        public string commandStreamHash;
        public List<RaidSimulationCommand> commands = new();
    }

    [Serializable]
    internal sealed class SubmitRaidResultResponse { public bool success; public string error; }

    [Serializable]
    internal sealed class HeartbeatRaidJobRequest { public string workerId; }

    [Serializable]
    internal sealed class HeartbeatRaidJobResponse
    {
        public bool success;
        public string error;
        public string allocationId;
        public string leaseExpiresUtc;
    }

    public sealed class TrustedRaidWorkerClient
    {
        public const int MaximumAttempts = 4;
        public const int MaximumRetryDelayMilliseconds = 2000;
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
                new ClaimJobRequest { workerId = workerId }, "worker-claim-" + Guid.NewGuid().ToString("N"),
                cancellationToken);

        public async Task HeartbeatAsync(RaidJobResponse job, CancellationToken cancellationToken)
        {
            if (job == null || !Guid.TryParse(job.allocationId, out _))
                throw new ArgumentException("Worker heartbeat requires an authoritative allocation.", nameof(job));
            var response = await SendAsync<HeartbeatRaidJobRequest, HeartbeatRaidJobResponse>(
                "/internal/v1/raid-jobs/" + job.allocationId + "/heartbeat",
                new HeartbeatRaidJobRequest { workerId = job.workerId },
                "worker-heartbeat-" + Guid.NewGuid().ToString("N"), cancellationToken);
            if (response?.success != true)
                throw new InvalidOperationException(response?.error ?? "Raid worker heartbeat was rejected.");
            job.leaseExpiresUtc = response.leaseExpiresUtc;
        }

        public async Task SubmitResultAsync(RaidJobResponse job, RaidSimulationResult result,
            CancellationToken cancellationToken)
        {
            if (job == null || result == null || !Guid.TryParse(job.raidId, out _))
                throw new ArgumentException("Authoritative job and result are required.");
            var resultId = ComputeDeterministicResultId(job.raidId);
            var response = await SendAsync<SubmitRaidResultRequest, SubmitRaidResultResponse>(
                "/internal/v1/raids/" + job.raidId + "/result",
                new SubmitRaidResultRequest
                {
                    allocationId = job.allocationId,
                    workerId = job.workerId,
                    resultId = resultId,
                    outcome = result.outcome,
                    breachedRings = result.breachedRings,
                    durationMs = result.durationMs,
                    simulationHash = result.simulationHash,
                    simulationVersion = result.simulationVersion,
                    tickCount = result.tickCount,
                    commandCount = result.commandCount,
                    commandStreamHash = result.commandStreamHash,
                    commands = result.commands,
                }, "worker-result-" + resultId.Replace("-", string.Empty), cancellationToken);
            if (response?.success != true)
                throw new InvalidOperationException(response?.error ?? "Raid result settlement was rejected.");
        }

        public static string ComputeDeterministicResultId(string raidId)
        {
            if (!Guid.TryParse(raidId, out var parsed))
                throw new ArgumentException("Raid ID must be a UUID.", nameof(raidId));
            var canonical = "splice-authoritative-raid-result-v1|" + parsed.ToString("D");
            byte[] hash;
            using (var algorithm = SHA256.Create())
                hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Guid.ParseExact(BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty),
                "N").ToString("D");
        }

        public static bool IsAllowedInternalPath(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.StartsWith("/internal/v1/", StringComparison.Ordinal) &&
            !path.Contains("..", StringComparison.Ordinal);

        public static bool IsRetryableHttpStatus(long status) =>
            status is 408 or 425 or 429 || status is >= 500 and <= 599;

        public static int RetryDelayMilliseconds(int failedAttempt)
        {
            var exponent = Math.Clamp(failedAttempt - 1, 0, 3);
            return Math.Min(MaximumRetryDelayMilliseconds, 250 << exponent);
        }

        private async Task<TResponse> SendAsync<TRequest, TResponse>(string path, TRequest request,
            string idempotencyKey, CancellationToken cancellationToken) where TResponse : class
        {
            if (!IsAllowedInternalPath(path))
                throw new InvalidOperationException("Trusted worker may call only internal v1 routes.");
            var json = JsonUtility.ToJson(request);
            Exception lastError = null;
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                using var webRequest = new UnityWebRequest(baseUrl + path, UnityWebRequest.kHttpVerbPOST);
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Accept", "application/json");
                webRequest.SetRequestHeader("X-Raid-Server-Id", serverId);
                webRequest.SetRequestHeader("X-Raid-Server-Key", serverKey);
                webRequest.SetRequestHeader("X-Request-Id", Guid.NewGuid().ToString("D"));
                webRequest.SetRequestHeader("Idempotency-Key", idempotencyKey);
                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                    return JsonUtility.FromJson<TResponse>(webRequest.downloadHandler.text);

                var retryable = webRequest.result is UnityWebRequest.Result.ConnectionError or
                                    UnityWebRequest.Result.DataProcessingError ||
                                IsRetryableHttpStatus(webRequest.responseCode);
                lastError = new InvalidOperationException($"Trusted worker HTTP {webRequest.responseCode}: " +
                                                          webRequest.downloadHandler.text);
                if (!retryable || attempt == MaximumAttempts) throw lastError;
                await Task.Delay(RetryDelayMilliseconds(attempt), cancellationToken);
            }
            throw lastError ?? new InvalidOperationException("Trusted worker request failed.");
        }
    }
}
