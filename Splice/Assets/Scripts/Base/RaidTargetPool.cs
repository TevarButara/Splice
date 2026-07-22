using System.Collections.Generic;

namespace Splice.Base
{
    public sealed class RaidTargetPoolResult
    {
        public readonly List<RaidTarget> targets = new();
        public readonly List<string> warnings = new();
        public int playerSnapshotTargets;
        public int botTargets;
        public int inspectionTargets;
        public int rejectedSnapshots;
        public int RaidableCount => targets.Count - inspectionTargets;
    }

    // Pure composition layer shared by the current greybox provider and the future world-map service.
    // Remote snapshots are preferred, bots fill every missing raidable slot, and the current account's latest
    // deployed town can be appended for inspection without ever becoming a legal raid target.
    public static class RaidTargetPool
    {
        public const string ValidationVersion = "prototype-b.6b.pool.v1";

        public static RaidTargetPoolResult Compose(IEnumerable<TownDefenseSnapshot> deployedSnapshots,
            IEnumerable<RaidTarget> botFallbacks, string attackerAccountId, int desiredRaidableCount,
            bool includeOwnSnapshotForInspection = true)
        {
            var result = new RaidTargetPoolResult();
            var remote = new List<RaidTarget>();
            var own = new List<RaidTarget>();
            var seen = new HashSet<string>();

            if (deployedSnapshots != null)
            {
                foreach (var snapshot in deployedSnapshots)
                {
                    if (!IsPoolEligible(snapshot, out var rejection))
                    {
                        result.rejectedSnapshots++;
                        result.warnings.Add(rejection);
                        continue;
                    }

                    var isOwn = !string.IsNullOrWhiteSpace(attackerAccountId) &&
                                snapshot.ownerAccountId == attackerAccountId;
                    var label = $"Deployed {snapshot.factionId} V{snapshot.revision}";
                    var target = RaidTarget.FromSnapshot(snapshot, label, isOwn);
                    if (target == null || !seen.Add(target.targetId)) continue;
                    if (isOwn) own.Add(target);
                    else remote.Add(target);
                }
            }

            var desired = System.Math.Max(0, desiredRaidableCount);
            foreach (var target in remote)
            {
                if (result.RaidableCount >= desired) break;
                result.targets.Add(target);
                result.playerSnapshotTargets++;
            }

            if (botFallbacks != null)
            {
                foreach (var target in botFallbacks)
                {
                    if (result.RaidableCount >= desired) break;
                    if (target == null || string.IsNullOrWhiteSpace(target.targetId) || !seen.Add(target.targetId)) continue;
                    if (!target.CanRaid(attackerAccountId, out _)) continue;
                    result.targets.Add(target);
                    result.botTargets++;
                }
            }

            if (result.RaidableCount < desired)
                result.warnings.Add($"Target pool has {result.RaidableCount}/{desired} raidable targets after bot fallback.");

            if (includeOwnSnapshotForInspection)
            {
                foreach (var target in own)
                {
                    result.targets.Add(target);
                    result.inspectionTargets++;
                }
            }

            return result;
        }

        public static bool IsPoolEligible(TownDefenseSnapshot snapshot, out string rejection)
        {
            if (snapshot == null)
            {
                rejection = "Rejected null town snapshot.";
                return false;
            }
            if (!snapshot.matchmakingEligible)
            {
                rejection = $"Snapshot {snapshot.snapshotId} is not matchmaking eligible.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(snapshot.snapshotId) || snapshot.revision <= 0)
            {
                rejection = "Snapshot identity or revision is invalid.";
                return false;
            }
            if (snapshot.layout == null || string.IsNullOrWhiteSpace(snapshot.ownerAccountId) ||
                string.IsNullOrWhiteSpace(snapshot.factionId))
            {
                rejection = $"Snapshot {snapshot.snapshotId} is missing ownership or layout data.";
                return false;
            }
            if ((snapshot.layout.towers?.Count ?? 0) + (snapshot.layout.garrison?.Count ?? 0) == 0)
            {
                rejection = $"Snapshot {snapshot.snapshotId} has no defensive units.";
                return false;
            }

            rejection = string.Empty;
            return true;
        }
    }
}
