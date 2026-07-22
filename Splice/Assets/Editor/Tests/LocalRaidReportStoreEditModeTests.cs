#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Splice.Base;
using Splice.Combat;
using Splice.Core;
using UnityEngine;

namespace Splice.Tests.EditMode
{
    public sealed class LocalRaidReportStoreEditModeTests
    {
        private string storageScope;
        private string snapshotFaction;

        [SetUp]
        public void SetUp()
        {
            storageScope = "step6d_" + Guid.NewGuid().ToString("N");
            LocalRaidReportStore.UseIsolatedStorageForTests(storageScope);
            RaidContext.Clear();
            RaidSessionContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(snapshotFaction))
                TownSnapshotStore.DeleteFactionSnapshotsForTests(snapshotFaction);
            LocalRaidReportStore.DeleteIsolatedStorageForTests();
            RaidContext.Clear();
            RaidSessionContext.Clear();
        }

        [Test]
        public void CompletedResult_IsIdempotent_AndConflictingReplayIsRejected()
        {
            var session = CompletedSession("raid_idempotent", "attacker", "defender");
            var transaction = SettledTransaction("raid_idempotent", RaidOutcome.FullVictory, 180);

            Assert.That(LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, 25,
                out var first, out var error), Is.True, error);
            Assert.That(LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, 40,
                out var replay, out error), Is.True, error);
            Assert.That(replay.reportId, Is.EqualTo(first.reportId));
            Assert.That(replay.goldLoot, Is.EqualTo(40));
            Assert.That(LocalRaidReportStore.LoadAll().Count, Is.EqualTo(1));

            transaction.payout = 999;
            Assert.That(LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, 40,
                out _, out error), Is.False);
            StringAssert.Contains("conflicting", error.ToLowerInvariant());
            Assert.That(LocalRaidReportStore.LoadAll().Count, Is.EqualTo(1));
        }

        [Test]
        public void SelfTargetReport_IsRejected()
        {
            var session = CompletedSession("raid_self", "same_account", "same_account");
            var transaction = SettledTransaction("raid_self", RaidOutcome.Defeat, 0);
            Assert.That(LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, 0,
                out _, out var error), Is.False);
            StringAssert.Contains("self-target", error.ToLowerInvariant());
            Assert.That(LocalRaidReportStore.LoadAll().Count, Is.Zero);
        }

        [Test]
        public void Revenge_UsesAttackerSnapshot_FreshRaidId_AndCooldown()
        {
            const string attacker = "enemy_attacker";
            const string defender = "local_defender";
            var attackerSnapshot = CommitSnapshot(attacker);
            var session = CompletedSession("raid_incoming", attacker, defender);
            session.attackerTownSnapshotId = attackerSnapshot.snapshotId;
            session.attackerTownSnapshotRevision = attackerSnapshot.revision;
            var transaction = SettledTransaction("raid_incoming", RaidOutcome.FullVictory, 180);
            Assert.That(LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, 50,
                out var report, out var error), Is.True, error);

            var now = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
            Assert.That(LocalRaidReportStore.TryPrepareRevenge(report.reportId, attacker, now,
                out _, out error), Is.False);
            StringAssert.Contains("original defender", error.ToLowerInvariant());

            Assert.That(LocalRaidReportStore.TryPrepareRevenge(report.reportId, defender, now,
                out var target, out error), Is.True, error);
            Assert.That(target.isRevenge, Is.True);
            Assert.That(target.snapshotId, Is.EqualTo(attackerSnapshot.snapshotId));
            Assert.That(target.ownerAccountId, Is.EqualTo(attacker));
            Assert.That(target.ownerAccountId, Is.Not.EqualTo(defender));

            Assert.That(LocalRaidReportStore.TryMarkRevengeStarted(report.reportId,
                target.revengeRequestId, defender, report.sourceRaidId, now, out error), Is.False);
            StringAssert.Contains("fresh", error.ToLowerInvariant());

            const string freshRaidId = "fresh_revenge_transaction";
            Assert.That(LocalRaidReportStore.TryMarkRevengeStarted(report.reportId,
                target.revengeRequestId, defender, freshRaidId, now, out error), Is.True, error);
            Assert.That(LocalRaidReportStore.TryMarkRevengeStarted(report.reportId,
                target.revengeRequestId, defender, freshRaidId, now, out error), Is.True,
                "Retrying the accepted start must be idempotent. " + error);

            Assert.That(LocalRaidReportStore.TryPrepareRevenge(report.reportId, defender,
                now.AddMinutes(30), out _, out error), Is.False);
            StringAssert.Contains("cooldown", error.ToLowerInvariant());

            Assert.That(LocalRaidReportStore.TryPrepareRevenge(report.reportId, defender,
                now.Add(LocalRaidReportStore.RevengeCooldown).AddSeconds(1), out var laterTarget, out error),
                Is.True, error);
            Assert.That(laterTarget.revengeRequestId, Is.Not.EqualTo(target.revengeRequestId));
        }

        [Test]
        public void RevengeWithoutAttackerSnapshot_IsRejected()
        {
            var session = CompletedSession("raid_missing_snapshot", "enemy", "defender");
            session.outcome = RaidOutcome.Defeat;
            session.breachedRings = 0;
            var transaction = SettledTransaction("raid_missing_snapshot", RaidOutcome.Defeat, 0);
            Assert.That(LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, 0,
                out var report, out var error), Is.True, error);

            Assert.That(LocalRaidReportStore.TryPrepareRevenge(report.reportId, "defender", DateTime.UtcNow,
                out _, out error), Is.False);
            StringAssert.Contains("snapshot", error.ToLowerInvariant());
        }

        [Test]
        public void RaidSession_PreservesRevengeIdentityThroughEconomyBinding()
        {
            var target = new RaidTarget
            {
                targetId = "revenge:report_1:snapshot_1",
                displayName = "Revenge Target",
                source = RaidTargetSource.Bot,
                ownerAccountId = "enemy_owner",
                factionId = "natural",
                matchmakingEligible = true,
                isRevenge = true,
                revengeReportId = "report_1",
                revengeRequestId = "request_1",
                layout = new BaseLayout
                {
                    ownerAccountId = "enemy_owner",
                    factionId = "natural",
                },
            };
            var contract = new RaidSceneContractReport
            {
                valid = true,
                contractVersion = RaidSceneContract.Version,
                pathCornerCount = 4,
                pathDistance = 100f,
            };

            Assert.That(RaidSessionContext.TryPrepare(target, "local_attacker", "Bootstrap",
                contract, out var error), Is.True, error);
            Assert.That(RaidSessionContext.Current.isRevenge, Is.True);
            Assert.That(RaidSessionContext.Current.revengeReportId, Is.EqualTo("report_1"));
            Assert.That(RaidSessionContext.Current.revengeRequestId, Is.EqualTo("request_1"));

            target.revengeRequestId = "tampered_request";
            Assert.That(RaidSessionContext.TryBindAndStart("fresh_economy_raid", target,
                contract, out error), Is.False);
            StringAssert.Contains("identity changed", error.ToLowerInvariant());

            target.revengeRequestId = "request_1";
            Assert.That(RaidSessionContext.TryBindAndStart("fresh_economy_raid", target,
                contract, out error), Is.True, error);
            Assert.That(RaidSessionContext.Current.raidId, Is.EqualTo("fresh_economy_raid"));
            Assert.That(RaidSessionContext.Current.revengeReportId, Is.EqualTo("report_1"));
            Assert.That(RaidSessionContext.Current.revengeRequestId, Is.EqualTo("request_1"));
        }

        private TownDefenseSnapshot CommitSnapshot(string owner)
        {
            snapshotFaction = "step6d_faction_" + Guid.NewGuid().ToString("N");
            var layout = new BaseLayout
            {
                ownerAccountId = owner,
                factionId = snapshotFaction,
                storedGold = 400,
            };
            layout.garrison.Add(new GarrisonMonsterData
            {
                cardId = snapshotFaction + "/guard",
                position = Vector3.zero,
            });
            return TownSnapshotStore.Commit(layout, 1, 10);
        }

        private static RaidSessionIdentity CompletedSession(string raidId, string attacker, string defender)
        {
            return new RaidSessionIdentity
            {
                raidId = raidId,
                phase = RaidSessionPhase.Completed,
                targetId = "snapshot:defender_town",
                targetDisplayName = "Defender Town",
                targetFactionId = "natural",
                snapshotId = "defender_snapshot",
                snapshotRevision = 3,
                attackerAccountId = attacker,
                defenderAccountId = defender,
                outcome = RaidOutcome.FullVictory,
                breachedRings = 3,
                completedUtc = "2026-07-22T09:00:00.0000000Z",
            };
        }

        private static RaidStakeTransaction SettledTransaction(string raidId, RaidOutcome outcome, int payout)
        {
            return new RaidStakeTransaction
            {
                raidId = raidId,
                settled = true,
                outcome = outcome,
                breachedRings = outcome == RaidOutcome.FullVictory ? 3 : 0,
                payout = payout,
                offer = new RaidStakeOffer { entryStake = 100 },
                settledUtc = "2026-07-22T09:00:00.0000000Z",
            };
        }
    }
}
#endif
