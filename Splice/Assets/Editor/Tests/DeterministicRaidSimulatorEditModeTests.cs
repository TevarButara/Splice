#if UNITY_EDITOR
using NUnit.Framework;
using Splice.RaidWorker;
using UnityEngine;

namespace Splice.Editor.Tests
{
    public sealed class DeterministicRaidSimulatorEditModeTests
    {
        private static RaidSimulationInput Input() => new()
        {
            raidId = "10000000-0000-0000-0000-000000000001",
            targetSnapshotId = "20000000-0000-0000-0000-000000000001",
            loadoutSnapshotId = "30000000-0000-0000-0000-000000000001",
            attackerPower = 1000,
            defenderPower = 800,
        };

        [Test]
        public void SameImmutableInputsProduceSameResultAndHash()
        {
            var first = DeterministicRaidSimulator.Simulate(Input());
            var second = DeterministicRaidSimulator.Simulate(Input());
            Assert.AreEqual(first.outcome, second.outcome);
            Assert.AreEqual(first.breachedRings, second.breachedRings);
            Assert.AreEqual(first.durationMs, second.durationMs);
            Assert.AreEqual(first.simulationHash, second.simulationHash);
            StringAssert.IsMatch("^[0-9a-f]{64}$", first.simulationHash);
        }

        [Test]
        public void StrongerArmyCannotProduceSchemaInvalidResult()
        {
            var input = Input();
            input.attackerPower = 10000;
            var result = DeterministicRaidSimulator.Simulate(input);
            Assert.AreEqual("FULL_VICTORY", result.outcome);
            Assert.AreEqual(3, result.breachedRings);
            Assert.That(result.durationMs, Is.InRange(1000, 3600000));
        }

        [Test]
        public void TrustedClientRejectsPublicAndTraversalRoutes()
        {
            Assert.IsTrue(TrustedRaidWorkerClient.IsAllowedInternalPath("/internal/v1/raid-jobs/claim"));
            Assert.IsFalse(TrustedRaidWorkerClient.IsAllowedInternalPath("/v1/wallet"));
            Assert.IsFalse(TrustedRaidWorkerClient.IsAllowedInternalPath("/internal/v1/../wallet"));
        }

        [Test]
        public void WorkerClaimDeserializesImmutableHeroAndGearPayload()
        {
            const string json = "{\"hasJob\":true,\"attackerPower\":3160," +
                "\"armyPower\":130,\"heroPower\":2830,\"gearPower\":200," +
                "\"hero\":{\"contentId\":\"hero/hero_test\",\"level\":1," +
                "\"basePower\":2830,\"scaledPower\":2830,\"combat\":{" +
                "\"maxHealth\":30000,\"attackDamage\":1000,\"abilityId\":\"breach_charge\"}}," +
                "\"gearItems\":[{\"instanceId\":\"61000000-0000-0000-0000-000000000001\"," +
                "\"contentId\":\"gear/test-blade\",\"level\":1,\"basePower\":200," +
                "\"scaledPower\":200,\"combat\":{}}]}";

            var job = JsonUtility.FromJson<RaidJobResponse>(json);

            Assert.That(job.attackerPower, Is.EqualTo(3160));
            Assert.That(job.hero.contentId, Is.EqualTo("hero/hero_test"));
            Assert.That(job.hero.combat.maxHealth, Is.EqualTo(30000));
            Assert.That(job.hero.combat.abilityId, Is.EqualTo("breach_charge"));
            Assert.That(job.gearItems, Has.Count.EqualTo(1));
            Assert.That(job.gearItems[0].instanceId,
                Is.EqualTo("61000000-0000-0000-0000-000000000001"));
        }
    }
}
#endif
