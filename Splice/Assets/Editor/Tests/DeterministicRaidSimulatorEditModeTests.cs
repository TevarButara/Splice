#if UNITY_EDITOR
using NUnit.Framework;
using Splice.RaidWorker;

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
    }
}
#endif
