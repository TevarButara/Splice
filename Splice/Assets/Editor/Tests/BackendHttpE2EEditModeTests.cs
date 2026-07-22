#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Splice.Backend;
using Splice.Base;
using UnityEngine;
using UnityEngine.TestTools;

namespace Splice.Tests.EditMode
{
    public sealed class BackendHttpE2EEditModeTests
    {
        private const string AttackerId = "11000000-0000-0000-0000-000000000001";
        private const string DefenderDeploymentId = "41000000-0000-0000-0000-000000000001";
        private const string DefenderSnapshotId = "32000000-0000-0000-0000-000000000001";
        private const string OwnDeploymentId = "41000000-0000-0000-0000-000000000099";
        private const string OwnSnapshotId = "32000000-0000-0000-0000-000000000099";
        private const string LoadoutId = "51000000-0000-0000-0000-000000000001";
        private const string QuoteId = "61000000-0000-0000-0000-000000000001";
        private const string RaidId = "71000000-0000-0000-0000-000000000001";

        [UnityTest]
        public IEnumerator RemoteServices_RunCheckoutDeployTargetQuoteFundOverRealHttp()
        {
            var port = FreePort();
            using var server = new ContractServer(port, 6);
            server.Start();
            var flow = RunFlowAsync(port);
            while (!flow.IsCompleted) yield return null;
            if (flow.IsFaulted) throw flow.Exception?.InnerException ?? flow.Exception;
            while (!server.Completion.IsCompleted) yield return null;
            if (server.Completion.IsFaulted)
                throw server.Completion.Exception?.InnerException ?? server.Completion.Exception;

            Assert.That(server.Paths, Is.EqualTo(new[]
            {
                "/v1/towns/1/draft",
                "/v1/towns/1/deployments",
                "/v1/town-snapshots/latest/query",
                "/v1/raid-quotes",
                "/v1/raids",
                "/v1/wallet",
            }));
            Assert.That(server.AuthorizationHeaders,
                Has.All.EqualTo("Bearer dev:" + AttackerId));
            Assert.That(server.MutationIdempotencyHeaders,
                Is.EqualTo(new[] { "checkout-e2e", "deploy-e2e", "quote-e2e", "fund-e2e" }));
        }

        private static async Task RunFlowAsync(int port)
        {
            var transport = new UnityWebRequestBackendTransport($"http://127.0.0.1:{port}",
                new StaticBackendAccessTokenProvider("dev:" + AttackerId), 5);
            var client = new BackendApiClient(transport);
            var towns = new RemoteTownSnapshotService(client);
            var raids = new RemoteRaidContractService(client);
            var wallet = new RemoteWalletService(client);
            var layout = new BaseLayout
            {
                ownerAccountId = AttackerId,
                factionId = "1",
                towers = new List<PlacedTowerData>
                {
                    new() { towerId = "1/1", position = Vector3.zero },
                },
            };

            await towns.SaveCheckedOutDraftAsync(layout, "checkout-e2e", CancellationToken.None);
            var deployed = await towns.DeployAsync(new DeployTownRequest
            {
                checkedOutLayout = layout,
                usedCapacity = 2,
                maxCapacity = 100,
            }, "deploy-e2e", CancellationToken.None);
            Assert.That(deployed.success, Is.True, deployed.error);
            Assert.That(deployed.snapshot.deploymentId, Is.EqualTo(OwnDeploymentId));

            var snapshots = await towns.GetLatestManyAsync(new[] { "1" }, CancellationToken.None);
            Assert.That(snapshots, Has.Count.EqualTo(1));
            var target = RaidTarget.FromSnapshot(snapshots[0], "Defender", false);
            Assert.That(target.targetId, Is.EqualTo(DefenderDeploymentId));
            Assert.That(target.snapshotId, Is.EqualTo(DefenderSnapshotId));

            var quote = await raids.CreateQuoteAsync(new CreateRaidQuoteRequest
            {
                targetId = target.targetId,
                targetName = target.displayName,
                difficultyBand = "FAIR",
                attackerLoadoutId = LoadoutId,
            }, "quote-e2e", CancellationToken.None);
            Assert.That(quote.entryStake, Is.EqualTo(100));
            var funded = await raids.ConfirmAsync(quote.quoteId, "fund-e2e", CancellationToken.None);
            Assert.That(funded.success, Is.True, funded.error);
            Assert.That(funded.raidId, Is.EqualTo(RaidId));
            Assert.That(funded.wallet.warGemBalance, Is.EqualTo(900));
            var refreshed = await wallet.GetWalletAsync(CancellationToken.None);
            Assert.That(refreshed.hasPendingRaid, Is.True);
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed class ContractServer : IDisposable
        {
            private readonly HttpListener listener = new();
            private readonly int expectedRequests;
            public readonly List<string> Paths = new();
            public readonly List<string> AuthorizationHeaders = new();
            public readonly List<string> MutationIdempotencyHeaders = new();
            public Task Completion { get; private set; }

            public ContractServer(int port, int requestCount)
            {
                expectedRequests = requestCount;
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            }

            public void Start()
            {
                listener.Start();
                Completion = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                for (var i = 0; i < expectedRequests; i++)
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    Paths.Add(request.Url.AbsolutePath);
                    AuthorizationHeaders.Add(request.Headers["Authorization"] ?? string.Empty);
                    if (request.Url.AbsolutePath is "/v1/towns/1/draft" or
                        "/v1/towns/1/deployments" or "/v1/raid-quotes" or "/v1/raids")
                        MutationIdempotencyHeaders.Add(request.Headers["Idempotency-Key"] ?? string.Empty);
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        _ = await reader.ReadToEndAsync();
                    var json = ResponseFor(request.Url.AbsolutePath);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    context.Response.StatusCode = request.Url.AbsolutePath is "/v1/towns/1/deployments" or
                        "/v1/raid-quotes" or "/v1/raids" ? 201 : 200;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
            }

            private static string ResponseFor(string path) => path switch
            {
                "/v1/towns/1/draft" => "{\"success\":true}",
                "/v1/towns/1/deployments" => SnapshotCommit(OwnSnapshotId, OwnDeploymentId, AttackerId),
                "/v1/town-snapshots/latest/query" =>
                    "{\"snapshots\":[" + Snapshot(DefenderSnapshotId, DefenderDeploymentId,
                        "11000000-0000-0000-0000-000000000002") + "]}",
                "/v1/raid-quotes" => "{\"quoteId\":\"" + QuoteId + "\",\"targetId\":\"" +
                    DefenderDeploymentId + "\",\"targetName\":\"Defender\",\"difficultyBand\":\"FAIR\"," +
                    "\"entryStake\":100,\"fullVictoryPayout\":180,\"outerExtractionPayout\":60," +
                    "\"innerExtractionPayout\":90,\"coreExtractionPayout\":120,\"expiresUtc\":\"2099-01-01T00:00:00Z\"}",
                "/v1/raids" => "{\"success\":true,\"error\":\"\",\"raidId\":\"" + RaidId +
                    "\",\"quoteId\":\"" + QuoteId + "\",\"wallet\":" + Wallet() + "}",
                "/v1/wallet" => Wallet(),
                _ => "{\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"missing\",\"requestId\":\"test\",\"retryable\":false}}",
            };

            private static string SnapshotCommit(string snapshotId, string deploymentId, string owner) =>
                "{\"success\":true,\"error\":\"\",\"snapshot\":" +
                Snapshot(snapshotId, deploymentId, owner) + "}";

            private static string Snapshot(string snapshotId, string deploymentId, string owner) =>
                "{\"schemaVersion\":1,\"snapshotId\":\"" + snapshotId +
                "\",\"deploymentId\":\"" + deploymentId + "\",\"revision\":1," +
                "\"committedUtc\":\"2099-01-01T00:00:00Z\",\"ownerAccountId\":\"" + owner +
                "\",\"factionId\":\"1\",\"baseLevel\":1,\"basePowerRating\":405," +
                "\"usedCapacity\":2,\"maxCapacity\":100,\"matchmakingEligible\":true," +
                "\"validationVersion\":\"server-c3-v1\",\"validationWarnings\":[]," +
                "\"layout\":{\"version\":1,\"ownerAccountId\":\"" + owner +
                "\",\"factionId\":\"1\",\"towers\":[{\"towerId\":\"1/1\"," +
                "\"position\":{\"x\":0,\"y\":0,\"z\":0},\"attackLevel\":0,\"healthLevel\":0," +
                "\"armorLevel\":0,\"rangeLevel\":0,\"targetsLevel\":0}],\"garrison\":[]," +
                "\"minerCardIds\":[],\"storedGold\":50},\"armyShowcasePresetName\":\"\"," +
                "\"heroAppearanceId\":\"\"}";

            private static string Wallet() =>
                "{\"warGemBalance\":900,\"hasPendingRaid\":true,\"pendingRaid\":null," +
                "\"latestTransactionSummary\":\"RAID_FUND -100 => 900\"}";

            public void Dispose()
            {
                if (listener.IsListening) listener.Stop();
                listener.Close();
            }
        }
    }
}
#endif
