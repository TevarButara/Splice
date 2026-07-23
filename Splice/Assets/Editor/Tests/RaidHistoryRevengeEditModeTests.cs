#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Splice.Backend;
using Splice.Base;
using Splice.Core;
using Splice.RaidWorker;
using Splice.UI;

namespace Splice.Tests.EditMode
{
    public sealed class RaidHistoryRevengeEditModeTests
    {
        [TearDown]
        public void TearDown() => RaidReplayLaunchContext.Clear();

        [Test]
        public void HistoryReplayLaunch_IsUuidOnlyAndOneShot()
        {
            var raidId = Guid.NewGuid().ToString("D");

            Assert.That(RaidReplayLaunchContext.TryPrepare("client-owned-report"), Is.False);
            Assert.That(RaidReplayLaunchContext.TryPrepare(raidId), Is.True);
            Assert.That(RaidReplayLaunchContext.HasPendingHistoryReplay, Is.True);
            Assert.That(RaidReplayLaunchContext.TryConsume(out var consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(raidId));
            Assert.That(RaidReplayLaunchContext.TryConsume(out _), Is.False,
                "History navigation must not leak into the next RaidArena session.");
        }

        [Test]
        public void HistoryReplayLaunch_WinsOverStaleStartedSession()
        {
            var staleRaidId = Guid.NewGuid().ToString("D");
            var selectedHistoryRaidId = Guid.NewGuid().ToString("D");
            var staleSession = new RaidSessionIdentity
            {
                phase = RaidSessionPhase.Started,
                raidId = staleRaidId,
            };
            Assert.That(RaidReplayLaunchContext.TryPrepare(selectedHistoryRaidId), Is.True);

            Assert.That(RaidLifecycleReplayController.TryResolveRaidId(
                staleSession, true, out var resolved, out var isHistory), Is.True);
            Assert.That(resolved, Is.EqualTo(selectedHistoryRaidId));
            Assert.That(isHistory, Is.True);
            Assert.That(RaidReplayLaunchContext.HasPendingHistoryReplay, Is.False);
        }

        [Test]
        public void RevengeTarget_RequiresPinnedServerIdentitiesAndSnapshotOwnership()
        {
            var deploymentId = Guid.NewGuid().ToString("D");
            var snapshotId = Guid.NewGuid().ToString("D");
            var ownerId = Guid.NewGuid().ToString("D");
            var prepared = new RaidRevengeTargetDto
            {
                success = true,
                sourceRaidId = Guid.NewGuid().ToString("D"),
                requestId = Guid.NewGuid().ToString("D"),
                targetDeploymentId = deploymentId,
                targetSnapshotId = snapshotId,
                targetOwnerAccountId = ownerId,
                targetDisplayName = "Original Attacker",
            };
            var snapshot = new TownDefenseSnapshot
            {
                snapshotId = snapshotId,
                deploymentId = deploymentId,
                ownerAccountId = ownerId,
                factionId = "human",
                revision = 3,
                matchmakingEligible = true,
                layout = new BaseLayout(),
            };

            Assert.That(RaidHistoryController.TryBuildRevengeTarget(
                prepared, snapshot, true, out var target, out var error), Is.True, error);
            Assert.That(target.isRevenge, Is.True);
            Assert.That(target.targetId, Is.EqualTo(deploymentId));
            Assert.That(target.revengeRequestId, Is.EqualTo(prepared.requestId));

            snapshot.ownerAccountId = Guid.NewGuid().ToString("D");
            Assert.That(RaidHistoryController.TryBuildRevengeTarget(
                prepared, snapshot, true, out _, out _), Is.False,
                "Client must reject a revenge snapshot owned by a different player.");
        }

        [Test]
        public void QuoteContract_CarriesOnlyOpaqueRevengeRequestIdentity()
        {
            Assert.That(typeof(CreateRaidQuoteRequest).GetField("revengeRequestId"), Is.Not.Null);
            Assert.That(typeof(CreateRaidQuoteRequest).GetField("revengePayout"), Is.Null);
            Assert.That(typeof(CreateRaidQuoteRequest).GetField("revengeCooldown"), Is.Null);
        }
    }
}
#endif
