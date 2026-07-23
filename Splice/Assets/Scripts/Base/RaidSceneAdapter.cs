using System;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Characters;
using Splice.Core;
using Splice.Network;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Base
{
    // Scene-specific bridge. UI/economy only ask this component whether the raid can be prepared/started;
    // they no longer need to know where the Core, spawn point, NavMesh, registry, or snapshot loader live.
    public class RaidSceneAdapter : MonoBehaviour
    {
        [SerializeField] private FortCore fortCore;
        [SerializeField] private RaidHeroSpawner heroSpawner;
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private RaidTargetProvider targetProvider;
        [SerializeField] private RaidSnapshotLoader snapshotLoader;
        [Min(0.25f)] [SerializeField] private float navMeshSampleRadius = 8f;

        public RaidTarget PreparedTarget => RaidSessionContext.IsPrepared ? RaidContext.Target : null;
        public bool HasPreparedRaid => RaidSessionContext.IsPrepared && RaidContext.Target != null;
        public RaidSceneContractReport LastContractReport { get; private set; }

        private void Awake() => ResolveReferences();
        private void OnValidate() => ResolveReferences();

        public bool ValidateScene(out RaidSceneContractReport report)
        {
            ResolveReferences();
            report = RaidSceneContract.Validate(fortCore, heroSpawner, navMeshSurface, navMeshSampleRadius);
            LastContractReport = report;
            return report.valid;
        }

        // Legacy synchronous wrapper for existing editor diagnostics. Runtime UI awaits PrepareRaidAsync.
        public bool TryPrepareRaid(out RaidTarget target, out string error)
        {
            var result = PrepareRaidAsync(CancellationToken.None).GetAwaiter().GetResult();
            target = result.target;
            error = result.error;
            return result.success;
        }

        public async Task<RaidPreparationResult> PrepareRaidAsync(CancellationToken cancellationToken)
        {
            if (!ValidateScene(out var contract))
                return PreparationFailed(contract.ErrorSummary);

            var target = RaidContext.Target;
            if (target == null || !target.CanRaid(PlayerProfile.AccountId, out _))
            {
                if (targetProvider == null)
                    return PreparationFailed("Raid target provider is missing.");

                var candidates = await targetProvider.GenerateTargetsAsync(cancellationToken);
                target = null;
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (!candidates[i].CanRaid(PlayerProfile.AccountId, out _)) continue;
                    target = candidates[i];
                    break;
                }
            }

            if (target == null)
                return PreparationFailed("No legal raid target is available.");
            if (!RaidContext.TrySelectTarget(target, PlayerProfile.ActiveFactionId, PlayerProfile.AccountId,
                    out var error)) return PreparationFailed(error);
            var attackerTown = string.IsNullOrWhiteSpace(PlayerProfile.ActiveFactionId)
                ? null
                : await SpliceServiceHub.TownSnapshots.GetLatestAsync(
                    PlayerProfile.ActiveFactionId, cancellationToken);
            if (!RaidSessionContext.TryPrepare(target, PlayerProfile.AccountId,
                    SceneManager.GetActiveScene().name, contract, attackerTown, out error))
                return PreparationFailed(error);

            Debug.Log($"[RaidSession] prepared {RaidSessionContext.Current.preparationId}: " +
                      $"target {target.targetId}, contract {contract.contractVersion}, " +
                      $"PathComplete ({contract.pathCornerCount} corners).", this);
            return new RaidPreparationResult
            {
                success = true,
                error = string.Empty,
                target = target,
            };
        }

        public bool CanStartPreparedRaid(out string error)
        {
            var result = CanStartPreparedRaidAsync(CancellationToken.None).GetAwaiter().GetResult();
            error = result.error;
            return result.success;
        }

        public async Task<BackendOperationResult> CanStartPreparedRaidAsync(
            CancellationToken cancellationToken)
        {
            if (!RaidSessionContext.IsPrepared || RaidContext.Target == null)
                return OperationFailed("Raid target is not prepared.");
            if (!ValidateScene(out var contract))
                return OperationFailed(contract.ErrorSummary);
            if (snapshotLoader == null)
                return OperationFailed("Raid snapshot loader is missing.");
            if (!snapshotLoader.CanLoadSelectedTarget(out var error))
                return OperationFailed(error);
            if (RaidContext.Target.IsSnapshotBacked)
            {
                var resolvedSnapshot = await SpliceServiceHub.TownSnapshots.GetByIdAsync(
                    RaidContext.Target.snapshotId, cancellationToken);
                var snapshotGate = ValidateResolvedTargetSnapshot(RaidContext.Target, resolvedSnapshot);
                if (!snapshotGate.success) return snapshotGate;
            }
            var session = RaidSessionContext.Current;
            if (session != null && session.isRevenge && !SpliceServiceHub.IsRemoteMeta)
            {
                var gate = await SpliceServiceHub.RaidReports.CanStartRevengeAsync(
                    session.revengeReportId, session.revengeRequestId,
                    PlayerProfile.AccountId, DateTime.UtcNow, cancellationToken);
                if (!gate.success) return OperationFailed(gate.error);
            }
            return OperationSucceeded();
        }

        public bool TryStartPreparedRaid(string economyRaidId, out string error)
        {
            var result = StartPreparedRaidAsync(economyRaidId, CancellationToken.None)
                .GetAwaiter().GetResult();
            error = result.error;
            return result.success;
        }

        public async Task<BackendOperationResult> StartPreparedRaidAsync(string economyRaidId,
            CancellationToken cancellationToken)
        {
            var readiness = await CanStartPreparedRaidAsync(cancellationToken);
            if (!readiness.success) return readiness;
            if (!RaidSessionContext.TryBindAndStart(economyRaidId, RaidContext.Target,
                    LastContractReport, out var error)) return OperationFailed(error);
            var session = RaidSessionContext.Current;
            if (session != null && session.isRevenge && !SpliceServiceHub.IsRemoteMeta)
            {
                var mark = await SpliceServiceHub.RaidReports.MarkRevengeStartedAsync(
                    session.revengeReportId, session.revengeRequestId, PlayerProfile.AccountId,
                    economyRaidId, DateTime.UtcNow, cancellationToken);
                if (!mark.success)
                {
                    RaidSessionContext.AbortBeforeGameplay("Revenge start contract failed: " + mark.error);
                    return OperationFailed(mark.error);
                }
            }
            if (!snapshotLoader.TryLoadSelectedTarget(out error))
            {
                RaidSessionContext.AbortBeforeGameplay("Snapshot loading failed after the stake was accepted: " + error);
                return OperationFailed(error);
            }

            Debug.Log($"[RaidSession] started raid {economyRaidId}: target {RaidSessionContext.Current.targetId}, " +
                      $"snapshot {RaidSessionContext.Current.snapshotId} v{RaidSessionContext.Current.snapshotRevision}.", this);
            return OperationSucceeded();
        }

        public void CancelPreparedRaid()
        {
            if (RaidSessionContext.IsPrepared)
                RaidSessionContext.AbortBeforeGameplay("Player cancelled before stake debit.");
        }

        public static string DifficultyBandFor(RaidTarget target)
        {
            if (target == null) return "UNKNOWN";
            if (target.basePowerRating >= 1400) return "HARD";
            if (target.basePowerRating >= 700) return "FAIR";
            return "EASY";
        }

        public static BackendOperationResult ValidateResolvedTargetSnapshot(
            RaidTarget target, TownDefenseSnapshot snapshot)
        {
            if (target == null) return OperationFailed("Raid target is missing.");
            if (!target.IsSnapshotBacked) return OperationSucceeded();
            if (snapshot == null)
                return OperationFailed("Selected immutable snapshot is unavailable before stake debit.");
            if (snapshot.snapshotId != target.snapshotId || snapshot.revision != target.snapshotRevision)
                return OperationFailed("Resolved snapshot identity does not match the prepared raid target.");
            return OperationSucceeded();
        }

        private static RaidPreparationResult PreparationFailed(string error) => new()
        {
            success = false,
            error = error,
        };

        private static BackendOperationResult OperationFailed(string error) => new()
        {
            success = false,
            error = error,
        };

        private static BackendOperationResult OperationSucceeded() => new()
        {
            success = true,
            error = string.Empty,
        };

        private void ResolveReferences()
        {
            if (fortCore == null) fortCore = FindFirstObjectByType<FortCore>();
            if (heroSpawner == null) heroSpawner = FindFirstObjectByType<RaidHeroSpawner>();
            if (navMeshSurface == null) navMeshSurface = FindFirstObjectByType<NavMeshSurface>();
            if (targetProvider == null) targetProvider = GetComponent<RaidTargetProvider>();
            if (targetProvider == null) targetProvider = FindFirstObjectByType<RaidTargetProvider>();
            if (snapshotLoader == null) snapshotLoader = GetComponent<RaidSnapshotLoader>();
            if (snapshotLoader == null) snapshotLoader = FindFirstObjectByType<RaidSnapshotLoader>();
            if (targetProvider != null && snapshotLoader != null)
                targetProvider.ConfigureRegistry(snapshotLoader.Registry);
        }
    }
}
