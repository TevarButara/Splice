#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Splice.Backend;
using Splice.Base;
using Splice.Core;
using Splice.Combat;

namespace Splice.Tests.EditMode
{
    public sealed class BackendBoundaryEditModeTests
    {
        private string factionId;
        private string reportScope;

        [SetUp]
        public void SetUp()
        {
            factionId = "c0_boundary_" + Guid.NewGuid().ToString("N");
            reportScope = "c0_report_" + Guid.NewGuid().ToString("N");
            LocalRaidReportStore.UseIsolatedStorageForTests(reportScope);
            SpliceServiceHub.ResetToLocalDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            TownSnapshotStore.DeleteFactionSnapshotsForTests(factionId);
            LocalRaidReportStore.DeleteIsolatedStorageForTests();
            SpliceServiceHub.ResetToLocalDefaults();
        }

        [Test]
        public void ServiceHub_DefaultsToLocalOnlyAdapters()
        {
            Assert.That(SpliceServiceHub.Wallet, Is.TypeOf<LocalWalletService>());
            Assert.That(SpliceServiceHub.TownSnapshots, Is.TypeOf<LocalTownSnapshotService>());
            Assert.That(SpliceServiceHub.RaidContracts, Is.TypeOf<LocalRaidContractService>());
            Assert.That(SpliceServiceHub.RaidReports, Is.TypeOf<LocalRaidReportService>());
            Assert.That(SpliceServiceHub.RaidSettlement, Is.TypeOf<LocalRaidSettlementService>());
            Assert.That(typeof(RaidFundingRequest).GetField("localPrototypeOffer"), Is.Null,
                "Client funding DTO must never carry a stake or payout offer.");
        }

        [Test]
        public async Task LocalTownBoundary_DeploysAndReadsImmutableSnapshot()
        {
            var service = new LocalTownSnapshotService();
            var layout = new BaseLayout
            {
                ownerAccountId = "c0_owner",
                factionId = factionId,
                storedGold = 300,
            };
            layout.garrison.Add(new GarrisonMonsterData
            {
                cardId = factionId + "/guard",
            });

            var request = new DeployTownRequest
            {
                checkedOutLayout = layout,
                usedCapacity = 1,
                maxCapacity = 10,
            };
            var deployKey = Guid.NewGuid().ToString("N");
            var deployed = await service.DeployAsync(request, deployKey, CancellationToken.None);
            var replay = await service.DeployAsync(request, deployKey, CancellationToken.None);

            Assert.That(deployed.success, Is.True, deployed.error);
            Assert.That(deployed.snapshot.snapshotId, Is.Not.Empty);
            Assert.That(replay.snapshot.snapshotId, Is.EqualTo(deployed.snapshot.snapshotId),
                "Retrying the same deployment key must not create a second snapshot revision.");
            Assert.That(deployed.snapshot.revision, Is.EqualTo(1));
            Assert.That((await service.GetByIdAsync(deployed.snapshot.snapshotId, CancellationToken.None))?.snapshotId,
                Is.EqualTo(deployed.snapshot.snapshotId));
            Assert.That((await service.GetLatestAsync(factionId, CancellationToken.None))?.revision, Is.EqualTo(1));
            var batch = await service.GetLatestManyAsync(new[] { factionId, factionId + "_missing" },
                CancellationToken.None);
            Assert.That(batch.Count, Is.EqualTo(1));
            Assert.That(batch[0].snapshotId, Is.EqualTo(deployed.snapshot.snapshotId));
        }

        [Test]
        public async Task RaidReportBoundary_IsIdempotentAndMergesLaterLoot()
        {
            var service = new LocalRaidReportService();
            var session = new RaidSessionIdentity
            {
                raidId = "c0_raid_" + Guid.NewGuid().ToString("N"),
                phase = RaidSessionPhase.Completed,
                targetId = "snapshot:c0_target",
                targetDisplayName = "C0 Target",
                targetFactionId = "natural",
                snapshotId = "c0_snapshot",
                snapshotRevision = 2,
                attackerAccountId = "c0_attacker",
                defenderAccountId = "c0_defender",
                outcome = RaidOutcome.FullVictory,
                breachedRings = 3,
                completedUtc = DateTime.UtcNow.ToString("O"),
            };
            var transaction = new RaidStakeTransaction
            {
                raidId = session.raidId,
                offer = new RaidStakeOffer { entryStake = 100 },
                settled = true,
                outcome = RaidOutcome.FullVictory,
                breachedRings = 3,
                payout = 180,
            };

            var first = await service.RecordCompletedAsync(session, transaction, 0, CancellationToken.None);
            var replay = await service.RecordCompletedAsync(session, transaction, 75, CancellationToken.None);

            Assert.That(first.success, Is.True, first.error);
            Assert.That(replay.success, Is.True, replay.error);
            Assert.That(replay.report.reportId, Is.EqualTo(first.report.reportId));
            Assert.That(replay.report.goldLoot, Is.EqualTo(75));
        }

        [Test]
        public void RaidReadiness_RejectsMissingOrMismatchedSnapshotBeforeStakeDebit()
        {
            var target = new RaidTarget
            {
                source = RaidTargetSource.PlayerSnapshot,
                snapshotId = "snapshot_expected",
                snapshotRevision = 4,
            };

            var missing = RaidSceneAdapter.ValidateResolvedTargetSnapshot(target, null);
            var mismatched = RaidSceneAdapter.ValidateResolvedTargetSnapshot(target,
                new TownDefenseSnapshot { snapshotId = "snapshot_other", revision = 4 });
            var valid = RaidSceneAdapter.ValidateResolvedTargetSnapshot(target,
                new TownDefenseSnapshot { snapshotId = "snapshot_expected", revision = 4 });

            Assert.That(missing.success, Is.False);
            StringAssert.Contains("before stake debit", missing.error);
            Assert.That(mismatched.success, Is.False);
            Assert.That(valid.success, Is.True, valid.error);
        }

        [Test]
        public async Task RaidContract_UsesHundredsScaleAndConfirmsQuoteOnlyOnce()
        {
            var wallet = new FakeWalletService();
            var service = new LocalRaidContractService(wallet);
            var quote = await service.CreateQuoteAsync(new CreateRaidQuoteRequest
            {
                targetId = "target_a",
                targetName = "Target A",
                difficultyBand = "fair",
            }, Guid.NewGuid().ToString("N"), CancellationToken.None);

            Assert.That(quote.entryStake, Is.EqualTo(100));
            Assert.That(quote.fullVictoryPayout, Is.EqualTo(180));
            Assert.That(quote.outerExtractionPayout, Is.EqualTo(60));
            Assert.That(quote.innerExtractionPayout, Is.EqualTo(90));
            Assert.That(quote.coreExtractionPayout, Is.EqualTo(120));

            var key = Guid.NewGuid().ToString("N");
            var first = await service.ConfirmAsync(quote.quoteId, key, CancellationToken.None);
            var replay = await service.ConfirmAsync(quote.quoteId, key, CancellationToken.None);
            var secondKey = await service.ConfirmAsync(quote.quoteId, Guid.NewGuid().ToString("N"),
                CancellationToken.None);

            Assert.That(first.success, Is.True, first.error);
            Assert.That(replay.raidId, Is.EqualTo(first.raidId));
            Assert.That(secondKey.raidId, Is.EqualTo(first.raidId), "One quote must create at most one raid.");
            Assert.That(wallet.FundCalls, Is.EqualTo(1), "Duplicate confirmation must not debit twice.");
        }

        [Test]
        public async Task RaidContract_RejectsIdempotencyKeyReuseAcrossQuotes()
        {
            var wallet = new FakeWalletService();
            var service = new LocalRaidContractService(wallet);
            var firstQuote = await Quote(service, "target_a");
            var secondQuote = await Quote(service, "target_b");
            var key = Guid.NewGuid().ToString("N");

            Assert.That((await service.ConfirmAsync(firstQuote.quoteId, key, CancellationToken.None)).success, Is.True);
            var conflict = await service.ConfirmAsync(secondQuote.quoteId, key, CancellationToken.None);

            Assert.That(conflict.success, Is.False);
            StringAssert.Contains("another quote", conflict.error);
            Assert.That(wallet.FundCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task RaidContract_QuoteCreationReplaysSameBodyAndRejectsChangedBody()
        {
            var service = new LocalRaidContractService(new FakeWalletService());
            var key = Guid.NewGuid().ToString("N");
            var request = new CreateRaidQuoteRequest
            {
                targetId = "target_a",
                targetName = "Target A",
                difficultyBand = "FAIR",
            };

            var first = await service.CreateQuoteAsync(request, key, CancellationToken.None);
            var replay = await service.CreateQuoteAsync(request, key, CancellationToken.None);
            var conflict = Assert.ThrowsAsync<BackendServiceException>(async () =>
                await service.CreateQuoteAsync(new CreateRaidQuoteRequest
                {
                    targetId = "target_changed",
                    targetName = "Changed",
                    difficultyBand = "HARD",
                }, key, CancellationToken.None));

            Assert.That(replay.quoteId, Is.EqualTo(first.quoteId));
            Assert.That(conflict.Code, Is.EqualTo(BackendErrorCodes.IdempotencyKeyReused));
        }

        private static Task<RaidQuoteDto> Quote(LocalRaidContractService service, string targetId) =>
            service.CreateQuoteAsync(new CreateRaidQuoteRequest
            {
                targetId = targetId,
                targetName = targetId,
                difficultyBand = "FAIR",
            }, Guid.NewGuid().ToString("N"), CancellationToken.None);

        private sealed class FakeWalletService : IWalletService
        {
            public int FundCalls { get; private set; }

            public Task<WalletView> GetWalletAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new WalletView { warGemBalance = 1000 });

            public Task<RaidFundingResult> FundRaidAsync(RaidFundingRequest request, string idempotencyKey,
                CancellationToken cancellationToken)
            {
                FundCalls++;
                var transaction = new RaidStakeTransaction
                {
                    raidId = "fake_raid_" + FundCalls,
                    offer = new RaidStakeOffer { entryStake = 100 },
                    balanceAfter = 900,
                };
                return Task.FromResult(new RaidFundingResult
                {
                    success = true,
                    transaction = transaction,
                    wallet = new WalletView { warGemBalance = 900, hasPendingRaid = true },
                });
            }

            public Task<RaidFundingResult> CancelRaidBeforeStartAsync(string raidId, string reasonCode,
                string idempotencyKey, CancellationToken cancellationToken) =>
                Task.FromResult(new RaidFundingResult());

            public Task<RaidFundingResult> ResolveAbandonedRaidAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new RaidFundingResult { wallet = new WalletView { warGemBalance = 1000 } });
        }
    }
}
#endif
