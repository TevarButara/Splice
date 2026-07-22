#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Splice.Base;
using Splice.Backend;
using Splice.Combat;

namespace Splice.Tests.EditMode
{
    public sealed class BackendTransportEditModeTests
    {
        [TearDown]
        public void TearDown() => SpliceServiceHub.ResetToLocalDefaults();

        [Test]
        public void Serializer_RoundTripsIntentWithoutClientControlledEconomyAmounts()
        {
            var serializer = UnityBackendJsonSerializer.Instance;
            var request = new CreateRaidQuoteRequest
            {
                targetId = "snapshot:target_1",
                targetName = "Target One",
                difficultyBand = "FAIR",
                attackerLoadoutId = "loadout_1",
            };

            var json = serializer.ToJson(request);
            var replay = serializer.FromJson<CreateRaidQuoteRequest>(json);

            Assert.That(replay.targetId, Is.EqualTo(request.targetId));
            Assert.That(replay.attackerLoadoutId, Is.EqualTo(request.attackerLoadoutId));
            StringAssert.DoesNotContain("entryStake", json);
            StringAssert.DoesNotContain("payout", json.ToLowerInvariant());
            Assert.That(typeof(CreateRaidQuoteRequest).GetField("entryStake"), Is.Null);
            Assert.That(typeof(ConfirmRaidRequest).GetField("payout"), Is.Null);
        }

        [Test]
        public void RequestPolicy_RequiresIdempotencyAndBlocksInternalRaidAuthorityRoutes()
        {
            var missingKey = Assert.Throws<BackendServiceException>(() =>
                BackendApiClient.CreateRequest(BackendHttpMethods.Post, BackendRoutes.Raids,
                    "{}", string.Empty, true));
            var internalRoute = Assert.Throws<BackendServiceException>(() =>
                BackendApiClient.CreateRequest(BackendHttpMethods.Post,
                    "/internal/v1/raids/raid_1/results", "{}", "key_1", true));

            Assert.That(missingKey.Code, Is.EqualTo(BackendErrorCodes.IdempotencyKeyRequired));
            Assert.That(internalRoute.Code, Is.EqualTo(BackendErrorCodes.ClientAuthorityForbidden));
        }

        [Test]
        public async Task LoopbackTransport_ReplaysSameMutationAndRejectsChangedBody()
        {
            var transport = new LoopbackBackendTransport();
            transport.Register(BackendHttpMethods.Post, BackendRoutes.RaidQuotes, (request, _) =>
            {
                Assert.That(request.requiresAuthentication, Is.True);
                Assert.That(request.requestId, Is.Not.Empty);
                Assert.That(request.idempotencyKey, Is.EqualTo("quote_key"));
                return Task.FromResult(LoopbackBackendTransport.Json(200, new RaidQuoteDto
                {
                    quoteId = "quote_1",
                    targetId = "target_1",
                    entryStake = 100,
                    fullVictoryPayout = 180,
                }));
            });
            var client = new BackendApiClient(transport);
            var body = new CreateRaidQuoteRequest { targetId = "target_1" };

            var first = await client.SendAsync<CreateRaidQuoteRequest, RaidQuoteDto>(
                BackendHttpMethods.Post, BackendRoutes.RaidQuotes, body,
                "quote_key", true, CancellationToken.None);
            var replay = await client.SendAsync<CreateRaidQuoteRequest, RaidQuoteDto>(
                BackendHttpMethods.Post, BackendRoutes.RaidQuotes, body,
                "quote_key", true, CancellationToken.None);
            var conflict = Assert.ThrowsAsync<BackendServiceException>(async () =>
                await client.SendAsync<CreateRaidQuoteRequest, RaidQuoteDto>(
                    BackendHttpMethods.Post, BackendRoutes.RaidQuotes,
                    new CreateRaidQuoteRequest { targetId = "target_changed" },
                    "quote_key", true, CancellationToken.None));

            Assert.That(first.quoteId, Is.EqualTo("quote_1"));
            Assert.That(replay.quoteId, Is.EqualTo(first.quoteId));
            Assert.That(transport.HandlerCalls, Is.EqualTo(1));
            Assert.That(conflict.Code, Is.EqualTo(BackendErrorCodes.IdempotencyKeyReused));
            Assert.That(conflict.Retryable, Is.False);
        }

        [Test]
        public void ErrorMapping_PreservesRequestIdAndSafeRetryPolicy()
        {
            var transport = new LoopbackBackendTransport();
            transport.Register(BackendHttpMethods.Get, BackendRoutes.Wallet, (request, _) =>
                Task.FromResult(LoopbackBackendTransport.Error(503,
                    BackendErrorCodes.ServiceUnavailable, "Try again later.",
                    request.requestId, true)));
            var client = new BackendApiClient(transport);

            var exception = Assert.ThrowsAsync<BackendServiceException>(async () =>
                await client.GetAsync<WalletView>(BackendRoutes.Wallet, CancellationToken.None));
            var request = BackendApiClient.CreateRequest(BackendHttpMethods.Get,
                BackendRoutes.Wallet, string.Empty, string.Empty, false);

            Assert.That(exception.StatusCode, Is.EqualTo(503));
            Assert.That(exception.Code, Is.EqualTo(BackendErrorCodes.ServiceUnavailable));
            Assert.That(exception.RequestId, Is.Not.Empty);
            Assert.That(exception.Retryable, Is.True);
            Assert.That(BackendErrorPolicy.CanRetry(request, exception), Is.True);
        }

        [Test]
        public async Task RemoteRaidContract_SendsQuoteAndConfirmAsIdempotentPublicIntents()
        {
            const string deploymentId = "41000000-0000-0000-0000-000000000001";
            const string loadoutId = "51000000-0000-0000-0000-000000000001";
            var transport = new LoopbackBackendTransport();
            transport.Register(BackendHttpMethods.Post, BackendRoutes.RaidQuotes, (request, _) =>
            {
                var body = UnityBackendJsonSerializer.Instance.FromJson<CreateRaidQuoteRequest>(request.bodyJson);
                Assert.That(body.targetId, Is.EqualTo(deploymentId));
                Assert.That(body.attackerLoadoutId, Is.EqualTo(loadoutId));
                return Task.FromResult(LoopbackBackendTransport.Json(200,
                    new RaidQuoteDto { quoteId = "quote_1", targetId = body.targetId }));
            });
            transport.Register(BackendHttpMethods.Post, BackendRoutes.Raids, (request, _) =>
            {
                var body = UnityBackendJsonSerializer.Instance.FromJson<ConfirmRaidRequest>(request.bodyJson);
                Assert.That(body.quoteId, Is.EqualTo("quote_1"));
                return Task.FromResult(LoopbackBackendTransport.Json(200,
                    new RaidStartDto { success = true, quoteId = body.quoteId, raidId = "raid_1" }));
            });
            var service = new RemoteRaidContractService(new BackendApiClient(transport));

            var quote = await service.CreateQuoteAsync(
                new CreateRaidQuoteRequest
                {
                    targetId = deploymentId,
                    attackerLoadoutId = loadoutId,
                },
                "quote_key", CancellationToken.None);
            var start = await service.ConfirmAsync(quote.quoteId, "raid_key", CancellationToken.None);

            Assert.That(start.success, Is.True, start.error);
            Assert.That(start.raidId, Is.EqualTo("raid_1"));
            Assert.That(transport.HandlerCalls, Is.EqualTo(2));
        }

        [Test]
        public void RemoteRaidContract_RejectsSnapshotAliasAndMissingLoadoutBeforeNetwork()
        {
            var transport = new LoopbackBackendTransport();
            var service = new RemoteRaidContractService(new BackendApiClient(transport));

            var exception = Assert.Throws<BackendServiceException>(() =>
                service.CreateQuoteAsync(new CreateRaidQuoteRequest
                {
                    targetId = "snapshot:legacy-id",
                    attackerLoadoutId = string.Empty,
                }, "quote_key", CancellationToken.None));

            Assert.That(exception.Code, Is.EqualTo(BackendErrorCodes.InvalidTransportRequest));
            Assert.That(transport.HandlerCalls, Is.Zero);
        }

        [Test]
        public void SnapshotTarget_UsesServerDeploymentIdentityForQuoteAndKeepsSnapshotLock()
        {
            var snapshot = new TownDefenseSnapshot
            {
                snapshotId = "32000000-0000-0000-0000-000000000001",
                deploymentId = "41000000-0000-0000-0000-000000000001",
                revision = 2,
                ownerAccountId = "11000000-0000-0000-0000-000000000002",
                factionId = "1",
                layout = new BaseLayout { factionId = "1" },
            };

            var target = RaidTarget.FromSnapshot(snapshot, "Defender", false);

            Assert.That(target.targetId, Is.EqualTo(snapshot.deploymentId));
            Assert.That(target.deploymentId, Is.EqualTo(snapshot.deploymentId));
            Assert.That(target.snapshotId, Is.EqualTo(snapshot.snapshotId));
            Assert.That(target.snapshotRevision, Is.EqualTo(2));
        }

        [Test]
        public void HttpTransport_AllowsLoopbackHttpButRejectsInsecureRemoteHost()
        {
            Assert.That(UnityWebRequestBackendTransport.ValidateBaseUri("http://127.0.0.1:5080").IsLoopback,
                Is.True);
            Assert.Throws<ArgumentException>(() =>
                UnityWebRequestBackendTransport.ValidateBaseUri("http://example.com/api"));
            Assert.That(UnityWebRequestBackendTransport.ValidateBaseUri("https://api.example.com/v1").Scheme,
                Is.EqualTo("https"));
        }

        [Test]
        public async Task RemoteComposition_GuardsReportAndSettlementAuthorityOnPlayerClient()
        {
            SpliceServiceHub.ConfigureRemoteMeta(new LoopbackBackendTransport());

            Assert.That(SpliceServiceHub.Wallet, Is.TypeOf<RemoteWalletService>());
            Assert.That(SpliceServiceHub.TownSnapshots, Is.TypeOf<RemoteTownSnapshotService>());
            Assert.That(SpliceServiceHub.RaidContracts, Is.TypeOf<RemoteRaidContractService>());
            var settlement = await SpliceServiceHub.RaidSettlement.SettleActiveRaidAsync(
                RaidOutcome.FullVictory, 3, CancellationToken.None);
            var report = await SpliceServiceHub.RaidReports.RecordCurrentCompletedAsync(
                100, CancellationToken.None);

            Assert.That(settlement.success, Is.False);
            Assert.That(report.success, Is.False);
            StringAssert.Contains(BackendErrorCodes.ClientAuthorityForbidden, settlement.error);
            StringAssert.Contains(BackendErrorCodes.ClientAuthorityForbidden, report.error);
        }
    }
}
#endif
