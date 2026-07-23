using System;
using NUnit.Framework;
using Splice.Backend;
using Splice.Core;
using Splice.RaidWorker;

namespace Splice.Editor.Tests
{
    public sealed class RaidLifecycleReplayEditModeTests
    {
        [Test]
        public void VerifiedReplay_MatchesLifecycleAndImmutableIdentity()
        {
            var input = DeterministicRaidSimulatorEditModeTests.FixedInput();
            var result = FixedTickRaidSimulator.Simulate(input);
            var resultId = Guid.NewGuid().ToString("D");
            var lifecycle = new RaidLifecycleDto
            {
                raidId = input.raidId,
                state = "SETTLED",
                resultId = resultId,
                replayAvailable = true,
                targetSnapshotId = input.targetSnapshotId,
                outcome = result.outcome,
                breachedRings = result.breachedRings,
                simulationVersion = result.simulationVersion,
                commandStreamHash = result.commandStreamHash,
            };
            var replay = new RaidReplayDto
            {
                raidId = input.raidId,
                resultId = resultId,
                input = input,
                result = result,
            };

            Assert.That(RaidLifecycleReplayController.TryValidateReplay(
                replay, lifecycle, out var error), Is.True, error);
        }

        [Test]
        public void TamperedCommand_IsRejectedEvenWhenStoredHashLooksValid()
        {
            var input = DeterministicRaidSimulatorEditModeTests.FixedInput();
            var result = FixedTickRaidSimulator.Simulate(input);
            result.commands[0].value++;

            Assert.That(RaidCommandStreamPresentationController.TryValidateStream(
                result, out var error), Is.False);
            StringAssert.Contains("completion/hash", error);
        }

        [Test]
        public void ReplayIdentityMismatch_IsRejected()
        {
            var input = DeterministicRaidSimulatorEditModeTests.FixedInput();
            var result = FixedTickRaidSimulator.Simulate(input);
            var lifecycle = new RaidLifecycleDto
            {
                raidId = Guid.NewGuid().ToString("D"),
                resultId = Guid.NewGuid().ToString("D"),
                simulationVersion = result.simulationVersion,
                commandStreamHash = result.commandStreamHash,
            };
            var replay = new RaidReplayDto
            {
                raidId = input.raidId,
                resultId = Guid.NewGuid().ToString("D"),
                input = input,
                result = result,
            };

            Assert.That(RaidLifecycleReplayController.TryValidateReplay(
                replay, lifecycle, out _), Is.False);
        }

        [Test]
        public void ReplayCompletionMetadataMismatch_IsRejected()
        {
            var input = DeterministicRaidSimulatorEditModeTests.FixedInput();
            var result = FixedTickRaidSimulator.Simulate(input);
            result.durationMs += FixedTickRaidSimulator.TickMilliseconds;

            Assert.That(RaidCommandStreamPresentationController.TryValidateStream(
                result, out _), Is.False);
        }

        [Test]
        public void PollBackoff_IsBoundedAndRefundStatesAreTerminal()
        {
            Assert.That(RaidLifecycleReplayController.NextDelaySeconds(.25f, 4f), Is.EqualTo(.4f));
            Assert.That(RaidLifecycleReplayController.NextDelaySeconds(4f, 4f), Is.EqualTo(4f));
            Assert.That(RaidLifecycleReplayController.IsRefundedState("REFUNDED"), Is.True);
            Assert.That(RaidLifecycleReplayController.IsRefundedState("ACTIVE"), Is.False);
        }

        [Test]
        public void SimulatedIncomingDefense_DoesNotPollAttackerLifecycle()
        {
            var incoming = new RaidSessionIdentity
            {
                raidId = Guid.NewGuid().ToString("D"),
                phase = RaidSessionPhase.Started,
                isIncomingDefense = true,
            };
            Assert.That(RaidLifecycleReplayController.IsEligibleSession(incoming, true), Is.False);
            incoming.isIncomingDefense = false;
            Assert.That(RaidLifecycleReplayController.IsEligibleSession(incoming, true), Is.True);
        }
    }
}
