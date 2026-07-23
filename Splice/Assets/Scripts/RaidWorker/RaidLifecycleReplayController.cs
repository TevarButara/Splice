using System;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Combat;
using Splice.Core;
using UnityEngine;

namespace Splice.RaidWorker
{
    // C4C2D read-only player boundary. It may poll public lifecycle/replay routes but has no
    // capability to submit a result or mutate shared economy (architecture §4.1 / §5.10).
    [DisallowMultipleComponent]
    public sealed class RaidLifecycleReplayController : MonoBehaviour
    {
        [Min(.25f)] [SerializeField] private float initialPollSeconds = .75f;
        [Min(1f)] [SerializeField] private float maximumPollSeconds = 4f;
        [Min(30f)] [SerializeField] private float timeoutSeconds = 600f;

        private CancellationTokenSource lifetimeCancellation;
        private RaidCommandStreamPresentationController presentation;
        private IRaidContractService service;
        private string raidId;

        public static bool ShouldHandleCurrentRaid =>
            IsEligibleSession(RaidSessionContext.Current, SpliceServiceHub.IsRemoteMeta) ||
            (SpliceServiceHub.IsRemoteMeta && RaidReplayLaunchContext.HasPendingHistoryReplay);

        public bool IsPolling { get; private set; }
        public bool ReplayStarted { get; private set; }
        public int PollCount { get; private set; }
        public string LastState { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public bool IsHistoryReplay { get; private set; }

        public void Begin(RaidCommandStreamPresentationController target)
        {
            if (IsPolling || target == null || !ShouldHandleCurrentRaid) return;
            presentation = target;
            service = SpliceServiceHub.RaidContracts;
            if (!TryResolveRaidId(RaidSessionContext.Current, SpliceServiceHub.IsRemoteMeta,
                    out raidId, out var isHistoryReplay)) return;
            IsHistoryReplay = isHistoryReplay;
            lifetimeCancellation = new CancellationTokenSource();
            IsPolling = true;
            _ = PollAsync(lifetimeCancellation.Token);
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddSeconds(Mathf.Max(30f, timeoutSeconds));
            var delaySeconds = Mathf.Max(.25f, initialPollSeconds);
            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RaidLifecycleDto lifecycle;
                    try
                    {
                        lifecycle = await service.GetLifecycleAsync(raidId, cancellationToken);
                        PollCount++;
                    }
                    catch (BackendServiceException exception) when (exception.Retryable)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        delaySeconds = NextDelaySeconds(delaySeconds, maximumPollSeconds);
                        continue;
                    }

                    if (lifecycle == null || lifecycle.raidId != raidId)
                        throw new InvalidOperationException("Lifecycle identity does not match the active raid.");
                    LastState = (lifecycle.state ?? string.Empty).ToUpperInvariant();
                    presentation.ShowLifecycleStatus(lifecycle);

                    if (LastState == "SETTLED")
                    {
                        if (!lifecycle.replayAvailable)
                            throw new InvalidOperationException("Settled raid has no verified replay.");
                        var replay = await service.GetReplayAsync(raidId, cancellationToken);
                        if (!TryValidateReplay(replay, lifecycle, out var validationError))
                            throw new InvalidOperationException(validationError);
                        ReplayStarted = true;
                        IsPolling = false;
                        if (!IsHistoryReplay)
                            RaidSessionContext.MarkCompleted(ToOutcome(replay.result.outcome),
                                replay.result.breachedRings);
                        presentation.Play(replay.input, replay.result);
                        return;
                    }
                    if (IsRefundedState(LastState))
                    {
                        IsPolling = false;
                        if (!IsHistoryReplay)
                            RaidSessionContext.AbortBeforeGameplay("Authoritative raid was refunded.");
                        else
                            presentation.ShowLifecycleError(
                                "Defense history replay is unavailable because the raid was refunded.");
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    delaySeconds = LastState == "ACTIVE"
                        ? Mathf.Max(.5f, initialPollSeconds)
                        : NextDelaySeconds(delaySeconds, maximumPollSeconds);
                }
                throw new TimeoutException("Authoritative raid lifecycle polling timed out.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                IsPolling = false;
            }
            catch (Exception exception)
            {
                IsPolling = false;
                LastError = exception.Message;
                presentation?.ShowLifecycleError(LastError);
                Debug.LogError("[RaidLifecycle] " + LastError, this);
            }
        }

        public static bool TryValidateReplay(RaidReplayDto replay, RaidLifecycleDto lifecycle,
            out string error)
        {
            if (replay?.input == null || replay.result == null || lifecycle == null ||
                replay.raidId != lifecycle.raidId || replay.resultId != lifecycle.resultId ||
                replay.input.raidId != replay.raidId ||
                (!string.IsNullOrWhiteSpace(lifecycle.targetSnapshotId) &&
                 replay.input.targetSnapshotId != lifecycle.targetSnapshotId) ||
                replay.result.outcome != lifecycle.outcome ||
                replay.result.breachedRings != lifecycle.breachedRings ||
                replay.result.simulationVersion != lifecycle.simulationVersion ||
                replay.result.commandStreamHash != lifecycle.commandStreamHash)
            {
                error = "Replay identity or lifecycle metadata does not match.";
                return false;
            }
            if (!RaidCommandStreamPresentationController.TryValidateStream(replay.result, out error))
                return false;
            error = string.Empty;
            return true;
        }

        public static float NextDelaySeconds(float current, float maximum) =>
            Mathf.Min(Mathf.Max(1f, maximum), Mathf.Max(.25f, current) * 1.6f);

        public static bool IsRefundedState(string state) =>
            state is "REFUNDED" or "CANCELLED" or "FAILED";

        public static bool IsEligibleSession(RaidSessionIdentity session, bool remoteMeta) =>
            remoteMeta && session != null && session.phase == RaidSessionPhase.Started &&
            !session.isIncomingDefense && Guid.TryParse(session.raidId, out _);

        public static bool TryResolveRaidId(RaidSessionIdentity session, bool remoteMeta,
            out string resolvedRaidId, out bool isHistoryReplay)
        {
            // An explicit history navigation must win over any stale completed/started session
            // retained across scene loads, otherwise the player could see the wrong replay.
            if (remoteMeta && RaidReplayLaunchContext.TryConsume(out resolvedRaidId))
            {
                isHistoryReplay = true;
                return true;
            }
            resolvedRaidId = IsEligibleSession(session, remoteMeta)
                ? session.raidId
                : string.Empty;
            isHistoryReplay = false;
            return !string.IsNullOrWhiteSpace(resolvedRaidId);
        }

        private static RaidOutcome ToOutcome(string outcome) => outcome switch
        {
            "FULL_VICTORY" => RaidOutcome.FullVictory,
            "EXTRACTED" => RaidOutcome.Extracted,
            _ => RaidOutcome.Defeat,
        };
    }
}
