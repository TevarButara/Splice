using System;
using Splice.Backend;
using Splice.Base;
using Splice.Combat;

namespace Splice.Core
{
    public enum RaidSessionPhase
    {
        None,
        Prepared,
        Started,
        Aborted,
        Completed,
    }

    [Serializable]
    public sealed class RaidSessionIdentity
    {
        public string preparationId;
        public string raidId;
        public RaidSessionPhase phase;
        public string targetId;
        public RaidTargetSource targetSource;
        public string targetDisplayName;
        public string targetFactionId;
        public string snapshotId;
        public int snapshotRevision;
        public string attackerAccountId;
        public string attackerLoadoutId;
        public string allocationId;
        public string raidServerId;
        public string raidTicket;
        public string defenderAccountId;
        public string attackerTownSnapshotId;
        public int attackerTownSnapshotRevision;
        public bool isRevenge;
        public string revengeReportId;
        public string revengeRequestId;
        public bool isIncomingDefense;
        public string sceneName;
        public string sceneContractVersion;
        public int validatedPathCorners;
        public float validatedPathDistance;
        public string preparedUtc;
        public string startedUtc;
        public string completedUtc;
        public RaidOutcome outcome;
        public int breachedRings;
        public string note;
    }

    // One immutable identity chain for target selection -> scene validation -> stake transaction -> result.
    // The economy remains the authority that creates raidId; this context binds that exact ID after debit.
    public static class RaidSessionContext
    {
        public static RaidSessionIdentity Current { get; private set; }
        public static bool IsPrepared => Current != null && Current.phase == RaidSessionPhase.Prepared;
        public static bool IsStarted => Current != null && Current.phase == RaidSessionPhase.Started;

        public static bool TryPrepare(RaidTarget target, string attackerAccountId, string sceneName,
            RaidSceneContractReport contract, out string error)
            => TryPrepare(target, attackerAccountId, sceneName, contract, null, out error);

        public static bool TryPrepare(RaidTarget target, string attackerAccountId, string sceneName,
            RaidSceneContractReport contract, TownDefenseSnapshot attackerTown, out string error)
        {
            error = string.Empty;
            if (target == null || !target.CanRaid(attackerAccountId, out error)) return false;
            if (contract == null || !contract.valid)
            {
                error = contract?.ErrorSummary ?? "Raid scene contract was not validated.";
                return false;
            }
            if (Current != null && Current.phase == RaidSessionPhase.Started)
            {
                error = "A raid session is already running.";
                return false;
            }

            if (attackerTown != null && attackerTown.ownerAccountId != attackerAccountId)
                attackerTown = null;
            Current = new RaidSessionIdentity
            {
                preparationId = Guid.NewGuid().ToString("N"),
                raidId = string.Empty,
                phase = RaidSessionPhase.Prepared,
                targetId = target.targetId,
                targetSource = target.source,
                targetDisplayName = target.displayName,
                targetFactionId = target.factionId,
                snapshotId = target.snapshotId ?? string.Empty,
                snapshotRevision = target.snapshotRevision,
                attackerAccountId = attackerAccountId ?? string.Empty,
                attackerLoadoutId = AttackerLoadoutIdentity.ForFaction(
                    string.IsNullOrWhiteSpace(PlayerProfile.ActiveFactionId)
                        ? target.factionId
                        : PlayerProfile.ActiveFactionId),
                defenderAccountId = !string.IsNullOrWhiteSpace(target.ownerAccountId)
                    ? target.ownerAccountId
                    : target.layout?.ownerAccountId ?? string.Empty,
                attackerTownSnapshotId = target.isIncomingDefense
                    ? target.simulatedAttackerSnapshotId ?? string.Empty
                    : attackerTown?.snapshotId ?? string.Empty,
                attackerTownSnapshotRevision = target.isIncomingDefense
                    ? target.simulatedAttackerSnapshotRevision
                    : attackerTown?.revision ?? 0,
                isRevenge = target.isRevenge,
                revengeReportId = target.revengeReportId ?? string.Empty,
                revengeRequestId = target.revengeRequestId ?? string.Empty,
                isIncomingDefense = target.isIncomingDefense,
                sceneName = sceneName ?? string.Empty,
                sceneContractVersion = contract.contractVersion,
                validatedPathCorners = contract.pathCornerCount,
                validatedPathDistance = contract.pathDistance,
                preparedUtc = DateTime.UtcNow.ToString("O"),
                outcome = RaidOutcome.InProgress,
                note = "Raid target and scene contract prepared before stake debit.",
            };
            return true;
        }

        public static bool BindAllocation(RaidAllocationDto allocation)
        {
            if (!IsPrepared || allocation?.success != true ||
                string.IsNullOrWhiteSpace(allocation.raidId) ||
                string.IsNullOrWhiteSpace(allocation.allocationId) ||
                string.IsNullOrWhiteSpace(allocation.ticket) ||
                (!string.IsNullOrWhiteSpace(Current.raidId) && Current.raidId != allocation.raidId))
                return false;
            Current.raidId = allocation.raidId;
            Current.allocationId = allocation.allocationId;
            Current.raidServerId = allocation.raidServerId ?? string.Empty;
            Current.raidTicket = allocation.ticket;
            Current.note = "Raid Server allocation ticket bound before scene startup.";
            return true;
        }

        public static bool TryBindAndStart(string economyRaidId, RaidTarget target,
            RaidSceneContractReport contract, out string error)
        {
            error = string.Empty;
            if (!IsPrepared)
            {
                error = "Raid session is not prepared.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(economyRaidId))
            {
                error = "Economy raid ID is missing.";
                return false;
            }
            if (target == null || target.targetId != Current.targetId ||
                (target.snapshotId ?? string.Empty) != Current.snapshotId ||
                target.snapshotRevision != Current.snapshotRevision ||
                target.isRevenge != Current.isRevenge ||
                (target.revengeReportId ?? string.Empty) != Current.revengeReportId ||
                (target.revengeRequestId ?? string.Empty) != Current.revengeRequestId ||
                target.isIncomingDefense != Current.isIncomingDefense ||
                (target.isIncomingDefense &&
                 ((target.simulatedAttackerSnapshotId ?? string.Empty) != Current.attackerTownSnapshotId ||
                  target.simulatedAttackerSnapshotRevision != Current.attackerTownSnapshotRevision)))
            {
                error = "Selected target identity changed after preparation.";
                return false;
            }
            if (contract == null || !contract.valid || contract.contractVersion != Current.sceneContractVersion)
            {
                error = "Raid scene contract changed or became invalid after preparation.";
                return false;
            }

            Current.raidId = economyRaidId;
            Current.phase = RaidSessionPhase.Started;
            Current.startedUtc = DateTime.UtcNow.ToString("O");
            Current.note = "Economy raid ID bound; immutable target locked for gameplay.";
            return true;
        }

        public static void AbortBeforeGameplay(string reason)
        {
            if (Current == null || Current.phase == RaidSessionPhase.Completed) return;
            Current.phase = RaidSessionPhase.Aborted;
            Current.completedUtc = DateTime.UtcNow.ToString("O");
            Current.note = reason ?? "Raid preparation cancelled.";
        }

        public static void MarkCompleted(RaidOutcome outcome, int breachedRings)
        {
            if (Current == null ||
                (Current.phase != RaidSessionPhase.Started && Current.phase != RaidSessionPhase.Completed)) return;
            Current.phase = RaidSessionPhase.Completed;
            Current.outcome = outcome;
            Current.breachedRings = Math.Max(0, Math.Min(3, breachedRings));
            Current.completedUtc = DateTime.UtcNow.ToString("O");
            Current.note = "Raid result linked to the prepared target and economy raid ID.";
        }

        public static void Clear() => Current = null;
    }
}
