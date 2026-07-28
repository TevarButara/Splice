using NUnit.Framework;
using Splice.Backend;

namespace Splice.Tests.EditMode
{
    public sealed class RaidObserverPolicyEditModeTests
    {
        [TestCase("FUNDED", RaidObserverPhase.Incoming, true)]
        [TestCase("ACTIVE", RaidObserverPhase.InProgress, true)]
        [TestCase("SETTLING", RaidObserverPhase.InProgress, true)]
        [TestCase("SETTLED", RaidObserverPhase.CompletedWithoutReplay, false)]
        public void ObserverPolicy_MapsAuthoritativeLifecycleWithoutFakeLiveFeed(string state,
            RaidObserverPhase expected, bool notification)
        {
            var decision = RaidObserverPolicy.Resolve(new IncomingDefenseRaidDto
            {
                state = state,
                attackerDisplayName = "Raider",
            });

            Assert.That(decision.phase, Is.EqualTo(expected));
            Assert.That(decision.showIncomingNotification, Is.EqualTo(notification));
            Assert.That(decision.canOpenReplay, Is.False);
            Assert.That(decision.claimsLiveCommandStream, Is.False);
        }

        [Test]
        public void ObserverPolicy_OnlyEnablesReplayAfterVerifiedArtifactExists()
        {
            var decision = RaidObserverPolicy.Resolve(new IncomingDefenseRaidDto
            {
                state = "SETTLED",
                replayAvailable = true,
            });

            Assert.That(decision.phase, Is.EqualTo(RaidObserverPhase.ReplayReady));
            Assert.That(decision.canOpenReplay, Is.True);
            Assert.That(decision.claimsLiveCommandStream, Is.False);
        }
    }
}
