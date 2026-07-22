#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using Splice.Base;
using Splice.Combat;
using Splice.Core;
using Splice.Scenes;
using Splice.UI;
using UnityEngine;

namespace Splice.Tests.EditMode
{
    public sealed class IncomingRaidScenarioContractEditModeTests
    {
        private string defenderFaction;
        private string attackerFaction;

        [SetUp]
        public void SetUp()
        {
            defenderFaction = "step6e_def_" + Guid.NewGuid().ToString("N");
            attackerFaction = "step6e_atk_" + Guid.NewGuid().ToString("N");
            RaidContext.Clear();
            RaidSessionContext.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            TownSnapshotStore.DeleteFactionSnapshotsForTests(defenderFaction);
            TownSnapshotStore.DeleteFactionSnapshotsForTests(attackerFaction);
            RaidContext.Clear();
            RaidSessionContext.Clear();
        }

        [Test]
        public void BuildTarget_UsesDefenderTown_AndRetainsAttackerSnapshotForRevenge()
        {
            var defender = Commit(defenderFaction, "local_defender");
            var attacker = Commit(attackerFaction, "remote_attacker");

            Assert.That(IncomingRaidScenarioContract.TryBuildTarget(defender, attacker,
                "remote_attacker", out var target, out var error), Is.True, error);
            Assert.That(target.isIncomingDefense, Is.True);
            Assert.That(target.snapshotId, Is.EqualTo(defender.snapshotId));
            Assert.That(target.ownerAccountId, Is.EqualTo("local_defender"));
            Assert.That(target.simulatedAttackerSnapshotId, Is.EqualTo(attacker.snapshotId));
            Assert.That(target.simulatedAttackerSnapshotRevision, Is.EqualTo(attacker.revision));
        }

        [Test]
        public void EmptyActiveFaction_FallsBackToFirstRegisteredTown()
        {
            var ids = new[] { "empty_faction", "town_faction", "later_faction" };
            var resolved = IncomingRaidScenarioContract.ResolveDefenderFactionId(
                string.Empty, ids, id => id == "town_faction" || id == "later_faction");
            Assert.That(resolved, Is.EqualTo("town_faction"));

            resolved = IncomingRaidScenarioContract.ResolveDefenderFactionId(
                "explicit_faction", ids, _ => true);
            Assert.That(resolved, Is.EqualTo("explicit_faction"));
        }

        [Test]
        public void DefenderCamera_MirrorsMonCameraFromOppositeSideWithoutChangingViewScale()
        {
            var monCameraPosition = new Vector3(10.4f, 46.3f, -107.2f);
            var monCameraEuler = new Vector3(51.2f, 358.97f, 0f);
            var attackerSpawn = new Vector3(10.82f, 3.72f, -92.6f);
            var defenderCore = new Vector3(6.6f, 3.08f, 60.59f);

            RaidPresentationCameraContract.CalculateMirroredPose(monCameraPosition, monCameraEuler,
                attackerSpawn, defenderCore, out var position, out var euler);

            Assert.That(position.x, Is.EqualTo(7.02f).Within(.01f));
            Assert.That(position.y, Is.EqualTo(45.66f).Within(.01f));
            Assert.That(position.z, Is.EqualTo(75.19f).Within(.01f));
            Assert.That(euler.x, Is.EqualTo(monCameraEuler.x).Within(.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(euler.y, monCameraEuler.y)), Is.EqualTo(180f).Within(.01f));
        }

        [Test]
        public void IncomingDefenseView_IgnoresDestroyedLegacyCameraReference()
        {
            var host = new GameObject("SideSelectionRegressionHost");
            var legacyCameraObject = new GameObject("RemovedMonCamera");
            var legacyCamera = legacyCameraObject.AddComponent<Camera>();
            var controller = host.AddComponent<SideSelectionController>();
            typeof(SideSelectionController)
                .GetField("monsterCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, legacyCamera);
            UnityEngine.Object.DestroyImmediate(legacyCameraObject);

            try
            {
                Assert.DoesNotThrow(controller.EnterIncomingDefenseView,
                    "The split-scene migration must tolerate destroyed Fort/Mon camera references.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BuildTarget_RejectsSelfRaidAndAttackerOwnershipMismatch()
        {
            var defender = Commit(defenderFaction, "same_owner");
            var attacker = Commit(attackerFaction, "same_owner");
            Assert.That(IncomingRaidScenarioContract.TryBuildTarget(defender, attacker,
                "same_owner", out _, out var error), Is.False);
            StringAssert.Contains("different accounts", error.ToLowerInvariant());

            attacker.ownerAccountId = "tampered_owner";
            Assert.That(IncomingRaidScenarioContract.TryBuildTarget(defender, attacker,
                "remote_attacker", out _, out error), Is.False);
            StringAssert.Contains("ownership", error.ToLowerInvariant());
        }

        [TestCase(RaidOutcome.FullVictory, 3, 180)]
        [TestCase(RaidOutcome.Extracted, 1, 60)]
        [TestCase(RaidOutcome.Extracted, 2, 90)]
        [TestCase(RaidOutcome.Extracted, 3, 120)]
        [TestCase(RaidOutcome.Defeat, 0, 0)]
        public void SimulatedSettlement_UsesHundredsScaleWithoutWalletMutation(
            RaidOutcome outcome, int rings, int expectedPayout)
        {
            var before = LocalWarGemEconomy.Balance;
            var transaction = IncomingRaidScenarioContract.BuildSimulatedAttackerSettlement(
                "incoming_test", outcome, rings, "Defender Town");

            Assert.That(transaction.offer.entryStake, Is.EqualTo(100));
            Assert.That(transaction.payout, Is.EqualTo(expectedPayout));
            Assert.That(transaction.settled, Is.True);
            Assert.That(transaction.outcome, Is.EqualTo(outcome));
            Assert.That(LocalWarGemEconomy.Balance, Is.EqualTo(before));
        }

        [Test]
        public void SessionBinding_LocksIncomingAttackerSnapshotIdentity()
        {
            var defender = Commit(defenderFaction, "local_defender");
            var attacker = Commit(attackerFaction, "remote_attacker");
            Assert.That(IncomingRaidScenarioContract.TryBuildTarget(defender, attacker,
                "remote_attacker", out var target, out var error), Is.True, error);
            var contract = ValidContract();

            Assert.That(RaidSessionContext.TryPrepare(target, "remote_attacker", "Bootstrap",
                contract, out error), Is.True, error);
            Assert.That(RaidSessionContext.Current.isIncomingDefense, Is.True);
            Assert.That(RaidSessionContext.Current.attackerTownSnapshotId, Is.EqualTo(attacker.snapshotId));

            var originalId = target.simulatedAttackerSnapshotId;
            target.simulatedAttackerSnapshotId = "tampered_snapshot";
            Assert.That(RaidSessionContext.TryBindAndStart("incoming_economy_id", target,
                contract, out error), Is.False);
            StringAssert.Contains("identity changed", error.ToLowerInvariant());

            target.simulatedAttackerSnapshotId = originalId;
            Assert.That(RaidSessionContext.TryBindAndStart("incoming_economy_id", target,
                contract, out error), Is.True, error);
        }

        private static RaidSceneContractReport ValidContract() => new()
        {
            valid = true,
            contractVersion = RaidSceneContract.Version,
            pathCornerCount = 4,
            pathDistance = 100f,
        };

        private static TownDefenseSnapshot Commit(string factionId, string owner)
        {
            var layout = new BaseLayout
            {
                ownerAccountId = owner,
                factionId = factionId,
                storedGold = 400,
            };
            layout.garrison.Add(new GarrisonMonsterData
            {
                cardId = factionId + "/guard",
                position = Vector3.zero,
            });
            return TownSnapshotStore.Commit(layout, 1, 10);
        }
    }
}
#endif
