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
                        var result = DeterministicRaidSimulator.Simulate(new RaidSimulationInput
                        {
                            raidId = job.raidId,
                            targetSnapshotId = job.targetSnapshotId,
                            loadoutSnapshotId = job.loadoutSnapshotId,
                            attackerPower = job.attackerPower,
                            defenderPower = job.defenderPower,
                        });
                        await client.SubmitResultAsync(job, result, cancellationToken);
                        Debug.Log($"[RaidWorker] settled {job.raidId}: {result.outcome}/{result.breachedRings}");
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
    }
}
