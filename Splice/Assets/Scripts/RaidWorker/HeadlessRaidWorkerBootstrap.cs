using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Splice.RaidWorker
{
    public sealed class HeadlessRaidWorkerBootstrap : MonoBehaviour
    {
        private CancellationTokenSource lifetime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void TryStart()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!Application.isBatchMode || !arguments.Contains("-spliceRaidWorker")) return;
            var root = new GameObject("[Splice Headless Raid Worker]");
            DontDestroyOnLoad(root);
            root.AddComponent<HeadlessRaidWorkerBootstrap>();
        }

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
            _ = RunAsync(lifetime.Token);
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }

        private static async Task RunAsync(CancellationToken cancellationToken)
        {
            var arguments = Environment.GetCommandLineArgs();
            var once = arguments.Contains("-spliceRaidWorkerOnce");
            var crashAfterClaim = arguments.Contains("-spliceRaidWorkerCrashAfterClaim");
            var duplicateSubmit = arguments.Contains("-spliceRaidWorkerDuplicateSubmit");
            var workerId = Environment.GetEnvironmentVariable("SPLICE_RAID_WORKER_ID") ??
                           "unity-worker-" + SystemInfo.deviceUniqueIdentifier;
            var endpoint = Environment.GetEnvironmentVariable("SPLICE_RAID_SERVER_URL") ??
                           "http://127.0.0.1:8080";
            var serverId = Environment.GetEnvironmentVariable("SPLICE_RAID_SERVER_ID") ?? string.Empty;
            var serverKey = Environment.GetEnvironmentVariable("SPLICE_RAID_SERVER_KEY") ?? string.Empty;
            try
            {
                var client = new TrustedRaidWorkerClient(endpoint, serverId, serverKey);
                do
                {
                    var job = await client.ClaimAsync(workerId, cancellationToken);
                    if (job?.hasJob == true)
                    {
                        if (crashAfterClaim)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogError($"[RaidWorker] C4C2E crash injection after claim {job.raidId}");
                            Application.Quit(77);
                            return;
#else
                            throw new InvalidOperationException(
                                "Crash injection is allowed only in Editor or Development builds.");
#endif
                        }

                        var result = await SimulateWithHeartbeatAsync(client, job, cancellationToken);
                        await client.SubmitResultAsync(job, result, cancellationToken);
                        if (duplicateSubmit)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            await client.SubmitResultAsync(job, result, cancellationToken);
#else
                            throw new InvalidOperationException(
                                "Duplicate-submit injection is allowed only in Editor or Development builds.");
#endif
                        }
                        Debug.Log($"[RaidWorker] settled {job.raidId}: {result.outcome}/" +
                                  $"{result.breachedRings}, ticks={result.tickCount}, " +
                                  $"commands={result.commandCount}, hash={result.commandStreamHash}, " +
                                  $"result={TrustedRaidWorkerClient.ComputeDeterministicResultId(job.raidId)}");
                    }
                    if (once) break;
                    await Task.Delay(job?.hasJob == true ? 250 : 2000, cancellationToken);
                } while (!cancellationToken.IsCancellationRequested);
                if (once) Application.Quit(0);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (once) Application.Quit(1);
            }
        }

        private static async Task<RaidSimulationResult> SimulateWithHeartbeatAsync(
            TrustedRaidWorkerClient client, RaidJobResponse job, CancellationToken cancellationToken)
        {
            var heartbeatSeconds = 10;
            if (int.TryParse(Environment.GetEnvironmentVariable("SPLICE_RAID_WORKER_HEARTBEAT_SECONDS"),
                    out var configuredSeconds))
                heartbeatSeconds = Math.Clamp(configuredSeconds, 5, 30);

            var simulation = Task.Run(() => FixedTickRaidSimulator.Simulate(
                FixedTickRaidSimulationInput.FromJob(job)), cancellationToken);
            while (!simulation.IsCompleted)
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(heartbeatSeconds), cancellationToken);
                if (await Task.WhenAny(simulation, delay) == simulation) break;
                await client.HeartbeatAsync(job, cancellationToken);
            }
            return await simulation;
        }
    }
}
