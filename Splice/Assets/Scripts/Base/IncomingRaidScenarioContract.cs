using System;
using System.Collections.Generic;
using Splice.Combat;
using Splice.Core;

namespace Splice.Base
{
    // Pure contract shared by runtime scenario setup and regression tests. The town being watched belongs
    // to the local defender; the separate attacker snapshot is retained only as a future Revenge target.
    public static class IncomingRaidScenarioContract
    {
        // Directly opening Bootstrap can legitimately happen before the faction selection scene has written
        // ActiveFactionId. Prefer the explicit selection, otherwise pick the first registered faction that
        // already owns a committed layout/snapshot. Never invent a faction with no town data.
        public static string ResolveDefenderFactionId(string activeFactionId,
            IReadOnlyList<string> registeredFactionIds, Func<string, bool> hasTown)
        {
            if (!string.IsNullOrWhiteSpace(activeFactionId)) return activeFactionId;
            if (registeredFactionIds == null || hasTown == null) return string.Empty;
            for (var i = 0; i < registeredFactionIds.Count; i++)
            {
                var id = registeredFactionIds[i];
                if (!string.IsNullOrWhiteSpace(id) && hasTown(id)) return id;
            }
            return string.Empty;
        }

        public static bool TryBuildTarget(TownDefenseSnapshot defenderSnapshot,
            TownDefenseSnapshot attackerSnapshot, string simulatedAttackerAccountId,
            out RaidTarget target, out string error)
        {
            target = null;
            if (!RaidTargetPool.IsPoolEligible(defenderSnapshot, out error)) return false;
            if (!RaidTargetPool.IsPoolEligible(attackerSnapshot, out error))
            {
                error = "Simulated attacker snapshot is invalid. " + error;
                return false;
            }
            if (string.IsNullOrWhiteSpace(simulatedAttackerAccountId))
            {
                error = "Simulated attacker account is missing.";
                return false;
            }
            if (defenderSnapshot.ownerAccountId == simulatedAttackerAccountId)
            {
                error = "Incoming raid attacker and defender must be different accounts.";
                return false;
            }
            if (attackerSnapshot.ownerAccountId != simulatedAttackerAccountId)
            {
                error = "Simulated attacker snapshot ownership does not match the attacker account.";
                return false;
            }

            target = RaidTarget.FromSnapshot(defenderSnapshot,
                $"Incoming Raid • {defenderSnapshot.factionId} Town", false);
            if (target == null)
            {
                error = "Defender target could not be created.";
                return false;
            }
            target.targetId = "incoming-defense:" + defenderSnapshot.snapshotId;
            target.isIncomingDefense = true;
            target.simulatedAttackerSnapshotId = attackerSnapshot.snapshotId;
            target.simulatedAttackerSnapshotRevision = attackerSnapshot.revision;
            error = string.Empty;
            return true;
        }

        public static RaidStakeTransaction BuildSimulatedAttackerSettlement(string raidId,
            RaidOutcome outcome, int breachedRings, string targetName)
        {
            var rings = UnityEngine.Mathf.Clamp(breachedRings, 0, 3);
            const int stake = 100;
            var payout = outcome switch
            {
                RaidOutcome.FullVictory => 180,
                RaidOutcome.Extracted when rings >= 3 => 120,
                RaidOutcome.Extracted when rings == 2 => 90,
                RaidOutcome.Extracted when rings == 1 => 60,
                _ => 0,
            };
            return new RaidStakeTransaction
            {
                raidId = raidId,
                offer = new RaidStakeOffer
                {
                    targetName = targetName,
                    difficultyBand = "SIMULATION",
                    entryStake = stake,
                    fullVictoryPayout = 180,
                    outerExtractionPayout = 60,
                    innerExtractionPayout = 90,
                    coreExtractionPayout = 120,
                },
                settled = true,
                outcome = outcome,
                breachedRings = rings,
                payout = payout,
                balanceAfter = 0,
                startedUtc = RaidSessionContext.Current?.startedUtc ?? string.Empty,
                settledUtc = System.DateTime.UtcNow.ToString("O"),
                settlementNote = "Local incoming-raid simulation; no real wallet mutation.",
            };
        }
    }
}
