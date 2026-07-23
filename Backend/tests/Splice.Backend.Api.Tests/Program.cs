using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Splice.Backend.Api;

if (args.Length != 1) throw new ArgumentException("PostgreSQL connection string is required.");
var connectionString = args[0];

const string attackerId = "11000000-0000-0000-0000-000000000001";
const string fairTargetId = "41000000-0000-0000-0000-000000000001";
const string highTargetAId = "41000000-0000-0000-0000-000000000002";
const string highTargetBId = "41000000-0000-0000-0000-000000000003";
const string selfTargetId = "41000000-0000-0000-0000-000000000004";
const string staleTargetId = "41000000-0000-0000-0000-000000000005";
const string loadoutId = "51000000-0000-0000-0000-000000000001";
const string attackerGearId = "61000000-0000-0000-0000-000000000001";
const string foreignGearId = "61000000-0000-0000-0000-000000000002";
const string trustedServerId = "test-authoritative-raid-1";
const string trustedServerKey = "test-only-c4-trusted-key-2026";

await SeedAsync(connectionString);
var host = await StartAsync(connectionString);
try
{
    var health = await SendAsync(host.Client, HttpMethod.Get, "/health", null, null, false);
    Equal(HttpStatusCode.OK, health.Status, "database health probe");
    Equal("ok", String(health, "database"), "database health status");

    var unauthorized = await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null, false);
    Equal(HttpStatusCode.Unauthorized, unauthorized.Status, "wallet requires authentication");
    ErrorCode(unauthorized, "AUTH_REQUIRED");

    var initialWallet = await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null);
    Equal(HttpStatusCode.OK, initialWallet.Status, "wallet read");
    Equal(1000L, Long(initialWallet, "warGemBalance"), "initial War Gem balance");
    False(Bool(initialWallet, "hasPendingRaid"), "initial pending raid");

    var forgedLoadout = await SendAsync(host.Client, HttpMethod.Put,
        $"/v1/attacker-loadouts/{loadoutId}",
        new
        {
            factionId = "1",
            heroId = "hero_test",
            gearInstanceIds = Array.Empty<string>(),
            entries = new[] { new { cardId = "1/unknown", count = 1 } }
        },
        "loadout:forged");
    Equal(HttpStatusCode.Conflict, forgedLoadout.Status, "unknown attacker card rejected");
    ErrorCode(forgedLoadout, "LOADOUT_CONTENT_INVALID");
    var validLoadoutBody = new
    {
        factionId = "1",
        heroId = "hero_test",
        gearInstanceIds = new[] { attackerGearId },
        entries = new[] { new { cardId = "1/1", count = 2 } },
    };
    var validLoadout = await SendAsync(host.Client, HttpMethod.Put,
        $"/v1/attacker-loadouts/{loadoutId}", validLoadoutBody, "loadout:valid");
    Equal(HttpStatusCode.OK, validLoadout.Status, "server validates attacker loadout");
    Equal(78L, Long(validLoadout, "armyPower"), "server computes army power from combat catalog");
    Equal(2830L, Long(validLoadout, "heroPower"), "server computes owned Hero power from catalog");
    Equal(200L, Long(validLoadout, "gearPower"), "server computes owned gear power from catalog");
    Equal(3108L, Long(validLoadout, "raidPower"), "server composes total authoritative raid power");

    var staleTarget = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(staleTargetId), "quote:stale-combat-snapshot");
    Equal(HttpStatusCode.Conflict, staleTarget.Status,
        "legacy town snapshot rejected before stake can be reserved");
    ErrorCode(staleTarget, "TARGET_COMBAT_SNAPSHOT_STALE");

    var unownedHero = await SendAsync(host.Client, HttpMethod.Put,
        $"/v1/attacker-loadouts/{Guid.NewGuid():D}",
        new
        {
            factionId = "1",
            heroId = "not_owned",
            gearInstanceIds = Array.Empty<string>(),
            entries = new[] { new { cardId = "1/1", count = 1 } }
        }, "loadout:unowned-hero");
    Equal(HttpStatusCode.Conflict, unownedHero.Status, "unowned Hero rejected");
    ErrorCode(unownedHero, "HERO_NOT_OWNED");

    var foreignGear = await SendAsync(host.Client, HttpMethod.Put,
        $"/v1/attacker-loadouts/{Guid.NewGuid():D}",
        new
        {
            factionId = "1",
            heroId = "hero_test",
            gearInstanceIds = new[] { foreignGearId },
            entries = new[] { new { cardId = "1/1", count = 1 } }
        }, "loadout:foreign-gear");
    Equal(HttpStatusCode.Conflict, foreignGear.Status, "gear owned by another player rejected");
    ErrorCode(foreignGear, "GEAR_NOT_OWNED");

    var missingKey = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(fairTargetId), null);
    Equal(HttpStatusCode.BadRequest, missingKey.Status, "mutation requires idempotency key");
    ErrorCode(missingKey, "IDEMPOTENCY_KEY_REQUIRED");

    var selfQuote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(selfTargetId), "quote:self");
    Equal(HttpStatusCode.Conflict, selfQuote.Status, "self target rejected");
    ErrorCode(selfQuote, "SELF_TARGET_FORBIDDEN");

    var quoteBody = QuoteBody(fairTargetId, clientStake: 1);
    var quote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        quoteBody, "quote:fair:1");
    Equal(HttpStatusCode.Created, quote.Status, "quote created");
    Equal("Defender Alpha", String(quote, "targetName"), "server target name overrides client");
    Equal("FAIR", String(quote, "difficultyBand"), "server difficulty overrides client");
    Equal(100L, Long(quote, "entryStake"), "server stake ignores client amount");
    var quoteId = String(quote, "quoteId");

    var quoteReplay = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        quoteBody, "quote:fair:1");
    Equal(HttpStatusCode.Created, quoteReplay.Status, "quote replay status");
    Equal(quoteId, String(quoteReplay, "quoteId"), "quote replay identity");

    var quoteConflict = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(highTargetAId), "quote:fair:1");
    Equal(HttpStatusCode.Conflict, quoteConflict.Status, "changed idempotency payload rejected");
    ErrorCode(quoteConflict, "IDEMPOTENCY_KEY_REUSED");

    var confirmBody = new { quoteId, stake = 1, payout = 999999 };
    var confirm = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        confirmBody, "raid:confirm:fair");
    Equal(HttpStatusCode.Created, confirm.Status, "raid funded");
    Equal(900L, Long(confirm, "wallet", "warGemBalance"), "server debits quoted stake");
    var raidId = String(confirm, "raidId");

    var confirmReplay = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        confirmBody, "raid:confirm:fair");
    Equal(raidId, String(confirmReplay, "raidId"), "confirm replay identity");
    Equal(900L, Long(confirmReplay, "wallet", "warGemBalance"), "confirm replay not double-debited");

    var secondKeySameQuote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        confirmBody, "raid:confirm:fair:second-key");
    Equal(HttpStatusCode.OK, secondKeySameQuote.Status, "same quote different key returns existing raid");
    Equal(raidId, String(secondKeySameQuote, "raidId"), "one quote creates one raid");
    Equal(900L, Long(secondKeySameQuote, "wallet", "warGemBalance"), "one quote funds once");

    await host.DisposeAsync();
    host = await StartAsync(connectionString);

    var recoveredWallet = await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null);
    Equal(900L, Long(recoveredWallet, "warGemBalance"), "fund survives API restart");
    True(Bool(recoveredWallet, "hasPendingRaid"), "pending escrow survives API restart");

    var refundBody = new { raidId, reasonCode = "CLIENT_START_FAILED" };
    var refund = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/startup-refund", refundBody, "raid:refund:fair");
    Equal(HttpStatusCode.OK, refund.Status, "startup refund");
    Equal(1000L, Long(refund, "wallet", "warGemBalance"), "startup refund restores full stake");

    var refundReplay = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/startup-refund", refundBody, "raid:refund:fair");
    Equal(1000L, Long(refundReplay, "wallet", "warGemBalance"), "refund replay not double-credited");

    var refundConflict = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/startup-refund",
        new { raidId, reasonCode = "STARTUP_TIMEOUT" }, "raid:refund:fair");
    Equal(HttpStatusCode.Conflict, refundConflict.Status, "changed refund payload rejected");
    ErrorCode(refundConflict, "IDEMPOTENCY_KEY_REUSED");

    var refundSecondKey = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/startup-refund", refundBody, "raid:refund:fair:second-key");
    Equal(1000L, Long(refundSecondKey, "wallet", "warGemBalance"), "refunded raid stays idempotent across keys");

    var sameQuote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(fairTargetId), "quote:same-concurrent");
    var sameQuoteId = String(sameQuote, "quoteId");
    var sameConfirmA = SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = sameQuoteId }, "raid:same-concurrent:a");
    var sameConfirmB = SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = sameQuoteId }, "raid:same-concurrent:b");
    var sameResults = await Task.WhenAll(sameConfirmA, sameConfirmB);
    True(sameResults.All(result => result.Status is HttpStatusCode.Created or HttpStatusCode.OK),
        "concurrent same-quote requests both succeed");
    Equal(String(sameResults[0], "raidId"), String(sameResults[1], "raidId"),
        "concurrent same-quote requests return one raid");
    Equal(900L, Long(sameResults[0], "wallet", "warGemBalance"),
        "concurrent same-quote debits once");
    var sameRaidId = String(sameResults[0], "raidId");
    var sameRefund = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{sameRaidId}/startup-refund",
        new { raidId = sameRaidId, reasonCode = "CLIENT_START_FAILED" }, "raid:refund:same-concurrent");
    Equal(1000L, Long(sameRefund, "wallet", "warGemBalance"), "same-quote race refund");

    var highQuoteA = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(highTargetAId, clientStake: 1), "quote:high:a");
    var highQuoteB = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(highTargetBId, clientStake: 1), "quote:high:b");
    Equal(600L, Long(highQuoteA, "entryStake"), "high stake server price A");
    Equal(600L, Long(highQuoteB, "entryStake"), "high stake server price B");

    var concurrentA = SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = String(highQuoteA, "quoteId") }, "raid:concurrent:a");
    var concurrentB = SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = String(highQuoteB, "quoteId") }, "raid:concurrent:b");
    var concurrent = await Task.WhenAll(concurrentA, concurrentB);
    var success = concurrent.Single(result => result.Status == HttpStatusCode.Created);
    var rejected = concurrent.Single(result => result.Status == HttpStatusCode.Conflict);
    ErrorCode(rejected, "PENDING_RAID_EXISTS");
    Equal(400L, Long(success, "wallet", "warGemBalance"), "concurrent confirm debits once");

    var winningRaidId = String(success, "raidId");
    var concurrentRefund = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{winningRaidId}/startup-refund",
        new { raidId = winningRaidId, reasonCode = "ALLOCATION_FAILED" }, "raid:refund:concurrent");
    Equal(1000L, Long(concurrentRefund, "wallet", "warGemBalance"), "concurrent winner refund");

    var recoveryQuote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(fairTargetId), "quote:reconciliation");
    var recoveryConfirm = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = String(recoveryQuote, "quoteId") }, "raid:reconciliation");
    var recoveryRaidId = String(recoveryConfirm, "raidId");
    Equal(900L, Long(recoveryConfirm, "wallet", "warGemBalance"), "reconciliation fixture funded");
    await BackdateRaidAsync(connectionString, recoveryRaidId);
    var reconciled = await host.App.Services.GetRequiredService<RaidReconciliationService>()
        .ReconcileOnceAsync(CancellationToken.None);
    Equal(1, reconciled, "abandoned escrow reconciled");
    var reconciledWallet = await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null);
    Equal(1000L, Long(reconciledWallet, "warGemBalance"), "reconciliation restores stake");
    False(Bool(reconciledWallet, "hasPendingRaid"), "reconciliation closes pending raid");

    var expiringQuote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(fairTargetId), "quote:expired");
    var expiringQuoteId = String(expiringQuote, "quoteId");
    await ExpireQuoteAsync(connectionString, expiringQuoteId);
    var expiredConfirm = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = expiringQuoteId }, "raid:confirm:expired");
    Equal(HttpStatusCode.Conflict, expiredConfirm.Status, "expired quote rejected");
    ErrorCode(expiredConfirm, "QUOTE_EXPIRED");

    await AssertDatabaseAsync(connectionString);
    Console.WriteLine("C2 API integration tests: PASS (auth, wallet, quote, escrow, restart, reconciliation, idempotency, concurrency)");
    await RunC3Async(host, connectionString);
    Console.WriteLine("C3 town integration tests: PASS (draft, validation, checkout, immutable snapshot, deployment, escrow, concurrency)");
    await RunC4Async(host, connectionString);
    Console.WriteLine("C4 raid authority tests: PASS (allocation, trusted start/result, settlement, immutable result, active recovery)");
}
finally
{
    await host.DisposeAsync();
}

static object QuoteBody(string targetId, int? clientStake = null) => new
{
    targetId,
    targetName = "Spoofed Client Name",
    difficultyBand = "FREE",
    attackerLoadoutId = loadoutId,
    entryStake = clientStake,
};

static async Task<TestHost> StartAsync(string connectionString)
{
    var app = SpliceApi.Build([
        "--environment=Development",
        "--urls=http://127.0.0.1:0",
        $"--ConnectionStrings:Splice={connectionString}",
        "--Logging:LogLevel:Default=Warning",
        "--Reconciliation:Enabled=false",
        "--Reconciliation:TimeoutSeconds=1",
        "--Reconciliation:ActiveTimeoutSeconds=1",
        $"--RaidServer:DevelopmentKey={trustedServerKey}",
        $"--RaidServer:DefaultServerId={trustedServerId}",
        "--RaidServer:WorkerLeaseSeconds=90",
    ]);
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    return new TestHost(app, new HttpClient { BaseAddress = new Uri(address) });
}

static async Task<ApiResult> SendAsync(HttpClient client, HttpMethod method, string path,
    object? body, string? idempotencyKey, bool authenticated = true)
{
    using var request = new HttpRequestMessage(method, path);
    if (authenticated) request.Headers.Authorization = new("Bearer", $"dev:{attackerId}");
    request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("D"));
    if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
    if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await client.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    return new ApiResult(response.StatusCode, JsonDocument.Parse(text));
}

static async Task<ApiResult> SendTrustedAsync(HttpClient client, HttpMethod method, string path,
    object? body, string? idempotencyKey, string key = trustedServerKey)
{
    using var request = new HttpRequestMessage(method, path);
    request.Headers.Add("X-Raid-Server-Key", key);
    request.Headers.Add("X-Raid-Server-Id", trustedServerId);
    request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("D"));
    if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
    if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await client.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    return new ApiResult(response.StatusCode, JsonDocument.Parse(text));
}

static async Task SeedAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SET search_path = splice, public;
        INSERT INTO players (id, display_name) VALUES
          ('11000000-0000-0000-0000-000000000001', 'Attacker'),
          ('11000000-0000-0000-0000-000000000002', 'Defender Alpha'),
          ('11000000-0000-0000-0000-000000000003', 'Defender Beta'),
          ('11000000-0000-0000-0000-000000000004', 'Legacy Defender')
        ON CONFLICT (id) DO NOTHING;

        INSERT INTO content_definitions
          (content_id, faction_id, content_kind, raid_power, enabled, content_version, combat_payload)
        VALUES ('gear/test-blade','','GEAR',200,true,'content-c4c2-v1',
                '{"slot":"weapon","attackBonus":20}'::jsonb)
        ON CONFLICT (content_id, content_kind) DO UPDATE SET
          raid_power=EXCLUDED.raid_power, enabled=true,
          content_version=EXCLUDED.content_version, combat_payload=EXCLUDED.combat_payload;
        INSERT INTO player_heroes (player_id, hero_content_id, level) VALUES
          ('11000000-0000-0000-0000-000000000001','hero/hero_test',1);
        INSERT INTO player_gear_items (id, owner_player_id, gear_content_id, level) VALUES
          ('61000000-0000-0000-0000-000000000001','11000000-0000-0000-0000-000000000001','gear/test-blade',1),
          ('61000000-0000-0000-0000-000000000002','11000000-0000-0000-0000-000000000002','gear/test-blade',1);

        INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code) VALUES
          ('21000000-0000-0000-0000-000000000001', 'test:c2:attacker:war-gem', 'PLAYER',
           '11000000-0000-0000-0000-000000000001', 'WAR_GEM')
        ON CONFLICT (id) DO NOTHING;

        SELECT post_ledger_transaction(
          'test:c2:mint:1000', 'TEST_MINT', 'TEST', NULL,
          jsonb_build_array(
            jsonb_build_object('account_id','00000000-0000-0000-0000-000000000201','amount',-1000),
            jsonb_build_object('account_id','21000000-0000-0000-0000-000000000001','amount',1000)));

        INSERT INTO towns (id, owner_player_id, faction_id) VALUES
          ('31000000-0000-0000-0000-000000000001','11000000-0000-0000-0000-000000000002','human'),
          ('31000000-0000-0000-0000-000000000002','11000000-0000-0000-0000-000000000002','natural'),
          ('31000000-0000-0000-0000-000000000003','11000000-0000-0000-0000-000000000003','darkside'),
          ('31000000-0000-0000-0000-000000000004','11000000-0000-0000-0000-000000000001','human'),
          ('31000000-0000-0000-0000-000000000005','11000000-0000-0000-0000-000000000004','human')
        ON CONFLICT (id) DO NOTHING;

        INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code) VALUES
          ('22000000-0000-0000-0000-000000000001','test:c4:town:fair','TOWN','31000000-0000-0000-0000-000000000001','WAR_GEM'),
          ('22000000-0000-0000-0000-000000000002','test:c4:town:high-a','TOWN','31000000-0000-0000-0000-000000000002','WAR_GEM'),
          ('22000000-0000-0000-0000-000000000003','test:c4:town:high-b','TOWN','31000000-0000-0000-0000-000000000003','WAR_GEM'),
          ('22000000-0000-0000-0000-000000000004','test:c4:town:self','TOWN','31000000-0000-0000-0000-000000000004','WAR_GEM')
        ON CONFLICT (id) DO NOTHING;
        SELECT post_ledger_transaction(
          'test:c4:mint:town-backing', 'TEST_MINT', 'TEST', NULL,
          jsonb_build_array(
            jsonb_build_object('account_id','00000000-0000-0000-0000-000000000201','amount',-1400),
            jsonb_build_object('account_id','22000000-0000-0000-0000-000000000001','amount',100),
            jsonb_build_object('account_id','22000000-0000-0000-0000-000000000002','amount',600),
            jsonb_build_object('account_id','22000000-0000-0000-0000-000000000003','amount',600),
            jsonb_build_object('account_id','22000000-0000-0000-0000-000000000004','amount',100)));
        INSERT INTO town_escrows
          (id, town_id, ledger_account_id, currency_code, funded_amount, state, funded_transaction_id) VALUES
          ('42000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001','WAR_GEM',100,'ACTIVE',(SELECT id FROM ledger_transactions WHERE idempotency_key='test:c4:mint:town-backing')),
          ('42000000-0000-0000-0000-000000000002','31000000-0000-0000-0000-000000000002','22000000-0000-0000-0000-000000000002','WAR_GEM',600,'ACTIVE',(SELECT id FROM ledger_transactions WHERE idempotency_key='test:c4:mint:town-backing')),
          ('42000000-0000-0000-0000-000000000003','31000000-0000-0000-0000-000000000003','22000000-0000-0000-0000-000000000003','WAR_GEM',600,'ACTIVE',(SELECT id FROM ledger_transactions WHERE idempotency_key='test:c4:mint:town-backing')),
          ('42000000-0000-0000-0000-000000000004','31000000-0000-0000-0000-000000000004','22000000-0000-0000-0000-000000000004','WAR_GEM',100,'ACTIVE',(SELECT id FROM ledger_transactions WHERE idempotency_key='test:c4:mint:town-backing'))
        ON CONFLICT (id) DO NOTHING;

        WITH valid_snapshot(payload) AS (
          VALUES ('{"schemaVersion":2,"layout":{"version":1},"defenseUnits":[{"actorId":"core","contentId":"core/default","contentKind":"CORE","count":1,"ring":3,"position":{"x":0,"y":0,"z":0},"raidPower":100,"combat":{"maxHealth":5000,"armor":100,"attackDamage":50,"attackCooldownMs":1000,"moveSpeedMilli":0,"attackRangeMilli":10000,"maxTargets":1}}]}'::jsonb)
        )
        INSERT INTO town_snapshots
          (id, town_id, revision, payload, payload_sha256, faction_id, base_level,
           base_power, content_version, validator_version)
        SELECT row_data.id::uuid, row_data.town_id::uuid, 1, valid_snapshot.payload,
               repeat(row_data.hash_character, 64), row_data.faction_id, 1,
               row_data.base_power, 'test', 'test'
          FROM (VALUES
            ('32000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001','a','human',100),
            ('32000000-0000-0000-0000-000000000002','31000000-0000-0000-0000-000000000002','b','natural',600),
            ('32000000-0000-0000-0000-000000000003','31000000-0000-0000-0000-000000000003','c','darkside',600),
            ('32000000-0000-0000-0000-000000000004','31000000-0000-0000-0000-000000000004','d','human',100)
          ) AS row_data(id, town_id, hash_character, faction_id, base_power)
          CROSS JOIN valid_snapshot
        UNION ALL
        SELECT '32000000-0000-0000-0000-000000000005',
               '31000000-0000-0000-0000-000000000005', 1, '{}',
               repeat('e',64), 'human', 1, 100, 'legacy', 'legacy'
        ON CONFLICT (id) DO NOTHING;

        INSERT INTO town_deployments
          (id, town_id, active_snapshot_id, town_escrow_id, status, stake_band) VALUES
          ('41000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000001','42000000-0000-0000-0000-000000000001','ACTIVE','FAIR'),
          ('41000000-0000-0000-0000-000000000002','31000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000002','42000000-0000-0000-0000-000000000002','ACTIVE','HIGH'),
          ('41000000-0000-0000-0000-000000000003','31000000-0000-0000-0000-000000000003','32000000-0000-0000-0000-000000000003','42000000-0000-0000-0000-000000000003','ACTIVE','HIGH'),
          ('41000000-0000-0000-0000-000000000004','31000000-0000-0000-0000-000000000004','32000000-0000-0000-0000-000000000004','42000000-0000-0000-0000-000000000004','ACTIVE','FAIR'),
          ('41000000-0000-0000-0000-000000000005','31000000-0000-0000-0000-000000000005','32000000-0000-0000-0000-000000000005',NULL,'ACTIVE','FAIR')
        ON CONFLICT (id) DO NOTHING;
        """, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task ExpireQuoteAsync(string connectionString, string quoteId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(
        "UPDATE splice.raid_quotes SET expires_at = clock_timestamp() - interval '1 minute' WHERE id = @id", connection);
    command.Parameters.AddWithValue("id", Guid.Parse(quoteId));
    await command.ExecuteNonQueryAsync();
}

static async Task BackdateRaidAsync(string connectionString, string raidId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(
        "UPDATE splice.raid_sessions SET created_at = clock_timestamp() - interval '10 minutes' WHERE id = @id", connection);
    command.Parameters.AddWithValue("id", Guid.Parse(raidId));
    await command.ExecuteNonQueryAsync();
}

static async Task AssertDatabaseAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT
          (SELECT balance FROM splice.ledger_accounts WHERE id = '21000000-0000-0000-0000-000000000001'),
          (SELECT count(*) FROM splice.ledger_transactions t WHERE t.status = 'POSTED' AND
             (SELECT COALESCE(sum(p.amount),0) FROM splice.ledger_postings p WHERE p.ledger_transaction_id=t.id) <> 0),
          (SELECT count(*) FROM splice.ledger_postings p JOIN splice.ledger_accounts a ON a.id=p.ledger_account_id
             WHERE a.currency_code='PREMIUM_DIAMOND'),
          (SELECT count(*) FROM splice.raid_sessions WHERE state IN ('PREPARED','FUNDED','ACTIVE','SETTLING'))
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Equal(1000L, reader.GetInt64(0), "final wallet balance");
    Equal(0L, reader.GetInt64(1), "all posted transactions balanced");
    Equal(0L, reader.GetInt64(2), "Premium Diamond excluded from raid ledger");
    Equal(0L, reader.GetInt64(3), "no open raid remains after refunds");
}

static async Task RunC4Async(TestHost host, string connectionString)
{
    var quote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(fairTargetId), "quote:c4:full");
    var changedAfterQuote = await SendAsync(host.Client, HttpMethod.Put,
        $"/v1/attacker-loadouts/{loadoutId}",
        new
        {
            factionId = "1",
            heroId = "hero_test",
            gearInstanceIds = new[] { attackerGearId },
            entries = new[] { new { cardId = "1/3", count = 2 } }
        },
        "loadout:changed-after-quote");
    Equal(3170L, Long(changedAfterQuote, "raidPower"), "mutable loadout advances after quote");
    var confirm = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = String(quote, "quoteId") }, "raid:c4:full");
    var raidId = String(confirm, "raidId");
    Equal(800L, Long(confirm, "wallet", "warGemBalance"), "C4 fund reserves attacker stake");

    var allocationBody = new { raidId };
    var allocation = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/allocation", allocationBody, "allocate:c4:full");
    Equal(HttpStatusCode.Created, allocation.Status, "funded raid allocated");
    var allocationId = String(allocation, "allocationId");
    var ticket = String(allocation, "ticket");
    True(ticket.Length >= 64, "allocation returns opaque one-time ticket");
    var allocationReplay = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/allocation", allocationBody, "allocate:c4:full");
    Equal(ticket, String(allocationReplay, "ticket"), "allocation idempotency replays ticket");
    var allocationSecondKey = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/allocation", allocationBody, "allocate:c4:full:other");
    Equal(HttpStatusCode.Conflict, allocationSecondKey.Status, "ticket cannot be reissued with another key");
    ErrorCode(allocationSecondKey, "RAID_ALREADY_ALLOCATED");

    var startBody = new { allocationId, ticket };
    var untrustedStart = await SendAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{raidId}/start", startBody, "trusted:start:blocked");
    Equal(HttpStatusCode.Unauthorized, untrustedStart.Status, "player bearer cannot claim trusted route");
    ErrorCode(untrustedStart, "TRUSTED_RAID_SERVER_AUTH_REQUIRED");
    var wrongTrustedStart = await SendTrustedAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{raidId}/start", startBody, "trusted:start:wrong", "wrong-server-key-value");
    Equal(HttpStatusCode.Unauthorized, wrongTrustedStart.Status, "wrong Raid Server key rejected");
    var untrustedClaim = await SendAsync(host.Client, HttpMethod.Post,
        "/internal/v1/raid-jobs/claim", new { workerId = "worker-c4-a" }, "worker:claim:untrusted");
    Equal(HttpStatusCode.Unauthorized, untrustedClaim.Status, "player cannot claim a worker job");
    var claim = await SendTrustedAsync(host.Client, HttpMethod.Post,
        "/internal/v1/raid-jobs/claim", new { workerId = "worker-c4-a" }, "worker:claim:c4:full");
    Equal(HttpStatusCode.OK, claim.Status, "trusted worker claims queued raid");
    True(Bool(claim, "hasJob"), "claim returns an authoritative raid job");
    Equal(raidId, String(claim, "raidId"), "worker receives funded raid");
    Equal(allocationId, String(claim, "allocationId"), "worker receives assigned allocation");
    Equal(3108L, Long(claim, "attackerPower"),
        "job uses quoted immutable loadout power after mutable loadout changes");
    Equal(78L, Long(claim, "armyPower"), "job exposes immutable army power breakdown");
    Equal(1, Element(claim, "armyUnits").GetArrayLength(),
        "job includes immutable per-unit army authority");
    Equal(450L, Element(claim, "armyUnits")[0].GetProperty("combat")
            .GetProperty("maxHealth").GetInt64(),
        "army authority includes server combat stats");
    Equal(2830L, Long(claim, "heroPower"), "job exposes immutable Hero power breakdown");
    Equal(200L, Long(claim, "gearPower"), "job exposes immutable gear power breakdown");
    Equal("hero/hero_test", String(claim, "hero", "contentId"),
        "job includes the immutable authoritative Hero identity");
    Equal(30000L, Long(claim, "hero", "combat", "maxHealth"),
        "job includes the immutable authoritative Hero combat payload");
    Equal(1, Element(claim, "gearItems").GetArrayLength(),
        "job includes the immutable owned gear instances");
    Equal("32000000-0000-0000-0000-000000000001", String(claim, "targetSnapshotId"),
        "worker job pins immutable target snapshot");
    var loadoutSnapshotId = String(claim, "loadoutSnapshotId");
    True(Guid.TryParse(loadoutSnapshotId, out _),
        "worker job pins immutable loadout snapshot");

    var lateRefund = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{raidId}/startup-refund",
        new { raidId, reasonCode = "CLIENT_START_FAILED" }, "refund:c4:after-start");
    Equal(HttpStatusCode.Conflict, lateRefund.Status, "started raid cannot use client startup refund");
    ErrorCode(lateRefund, "RAID_ALREADY_STARTED");

    var resultId = Guid.NewGuid().ToString("D");
    var resultBody = new
    {
        allocationId,
        workerId = "worker-c4-a",
        resultId,
        outcome = "FULL_VICTORY",
        breachedRings = 3,
        durationMs = 60000,
        simulationHash = new string('a', 64),
    };
    var stolenResult = await SendTrustedAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{raidId}/result",
        new
        {
            allocationId,
            workerId = "worker-c4-other",
            resultId,
            outcome = "FULL_VICTORY",
            breachedRings = 3,
            durationMs = 60000,
            simulationHash = new string('a', 64),
        }, "trusted:result:c4:stolen");
    Equal(HttpStatusCode.Forbidden, stolenResult.Status, "another worker cannot steal active lease");
    ErrorCode(stolenResult, "RAID_WORKER_AUTH_INVALID");
    var result = await SendTrustedAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{raidId}/result", resultBody, "trusted:result:c4:full");
    Equal(HttpStatusCode.Created, result.Status, "trusted result settles raid");
    Equal(180L, Long(result, "warGemPayout"), "server computes full-victory payout");
    Equal(980L, Long(result, "attackerWallet", "warGemBalance"), "settlement credits backed payout");
    True(Bool(result, "defenderDeploymentPaused"), "depleted defender backing pauses deployment");

    var resultReplay = await SendTrustedAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{raidId}/result", resultBody, "trusted:result:c4:full:other");
    Equal(HttpStatusCode.OK, resultReplay.Status, "same immutable result replays across keys");
    Equal(980L, Long(resultReplay, "attackerWallet", "warGemBalance"), "result replay does not double-credit");
    var conflictBody = new
    {
        allocationId,
        workerId = "worker-c4-a",
        resultId = Guid.NewGuid().ToString("D"),
        outcome = "DEFEAT",
        breachedRings = 0,
        durationMs = 60000,
        simulationHash = new string('b', 64),
    };
    var conflict = await SendTrustedAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{raidId}/result", conflictBody, "trusted:result:c4:conflict");
    Equal(HttpStatusCode.Conflict, conflict.Status, "different second result rejected");
    ErrorCode(conflict, "RAID_RESULT_CONFLICT");

    var lifecycle = await SendAsync(host.Client, HttpMethod.Get, $"/v1/raids/{raidId}", null, null);
    Equal("SETTLED", String(lifecycle, "state"), "player reads authoritative settled state");
    Equal(resultId, String(lifecycle, "resultId"), "lifecycle exposes immutable result identity");
    await AssertImmutableRaidResultAsync(connectionString, resultId);
    await AssertImmutableLoadoutSnapshotAsync(connectionString, loadoutSnapshotId);
    var noJob = await SendTrustedAsync(host.Client, HttpMethod.Post,
        "/internal/v1/raid-jobs/claim", new { workerId = "worker-c4-a" }, "worker:claim:none");
    False(Bool(noJob, "hasJob"), "worker polling returns an explicit empty queue response");

    var recoveryQuote = await SendAsync(host.Client, HttpMethod.Post, "/v1/raid-quotes",
        QuoteBody(highTargetAId), "quote:c4:active-recovery");
    var recoveryConfirm = await SendAsync(host.Client, HttpMethod.Post, "/v1/raids",
        new { quoteId = String(recoveryQuote, "quoteId") }, "raid:c4:active-recovery");
    var recoveryRaidId = String(recoveryConfirm, "raidId");
    var recoveryAllocation = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/raids/{recoveryRaidId}/allocation", new { raidId = recoveryRaidId },
        "allocate:c4:active-recovery");
    var recoveryStartBody = new
    {
        allocationId = String(recoveryAllocation, "allocationId"),
        ticket = String(recoveryAllocation, "ticket"),
    };
    await SendTrustedAsync(host.Client, HttpMethod.Post,
        $"/internal/v1/raids/{recoveryRaidId}/start", recoveryStartBody,
        "trusted:start:c4:active-recovery");
    await BackdateActiveRaidAsync(connectionString, recoveryRaidId);
    var recovered = await host.App.Services.GetRequiredService<RaidReconciliationService>()
        .ReconcileOnceAsync(CancellationToken.None);
    Equal(1, recovered, "timed-out active Raid Server session reconciled");
    Equal(980L, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "infrastructure timeout refunds attacker stake");
    await AssertC4DatabaseAsync(connectionString, raidId, recoveryRaidId);
}

static async Task BackdateActiveRaidAsync(string connectionString, string raidId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(
        "UPDATE splice.raid_sessions SET started_at=clock_timestamp()-interval '10 minutes' WHERE id=@raid",
        connection);
    command.Parameters.AddWithValue("raid", Guid.Parse(raidId));
    await command.ExecuteNonQueryAsync();
}

static async Task AssertImmutableRaidResultAsync(string connectionString, string resultId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    try
    {
        await using var command = new NpgsqlCommand(
            "UPDATE splice.raid_results SET breached_rings=0 WHERE id=@result", connection);
        command.Parameters.AddWithValue("result", Guid.Parse(resultId));
        await command.ExecuteNonQueryAsync();
        throw new Exception("TEST_FAILED: immutable raid result accepted direct update");
    }
    catch (PostgresException exception)
    {
        True(exception.MessageText.StartsWith("IMMUTABLE_RAID_RESULT", StringComparison.Ordinal),
            "database immutable raid result trigger");
    }
}

static async Task AssertImmutableLoadoutSnapshotAsync(string connectionString, string snapshotId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    try
    {
        await using var command = new NpgsqlCommand(
            "UPDATE splice.attacker_loadout_snapshots SET hero_power=hero_power+1 WHERE id=@snapshot", connection);
        command.Parameters.AddWithValue("snapshot", Guid.Parse(snapshotId));
        await command.ExecuteNonQueryAsync();
        throw new Exception("TEST_FAILED: immutable attacker loadout snapshot accepted direct update");
    }
    catch (PostgresException exception)
    {
        True(exception.MessageText.StartsWith("IMMUTABLE_LOADOUT_SNAPSHOT", StringComparison.Ordinal),
            "database immutable attacker loadout snapshot trigger");
    }
}

static async Task AssertC4DatabaseAsync(string connectionString, string settledRaidId, string recoveredRaidId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT
          (SELECT balance FROM splice.ledger_accounts WHERE id='21000000-0000-0000-0000-000000000001'),
          (SELECT balance FROM splice.ledger_accounts WHERE id='22000000-0000-0000-0000-000000000001'),
          (SELECT balance FROM splice.ledger_accounts a JOIN splice.raid_escrows e ON e.ledger_account_id=a.id
             WHERE e.raid_id=@settled),
          (SELECT count(*) FROM splice.raid_results WHERE raid_id=@settled),
          (SELECT count(*) FROM splice.raid_results WHERE result_payload ? 'ticket'),
          (SELECT state FROM splice.raid_sessions WHERE id=@recovered),
          (SELECT count(*) FROM splice.raid_sessions WHERE state IN ('PREPARED','FUNDED','ACTIVE','SETTLING')),
          (SELECT count(*) FROM splice.ledger_transactions t WHERE t.status='POSTED' AND
             (SELECT COALESCE(sum(p.amount),0) FROM splice.ledger_postings p
               WHERE p.ledger_transaction_id=t.id)<>0)
        """, connection);
    command.Parameters.AddWithValue("settled", Guid.Parse(settledRaidId));
    command.Parameters.AddWithValue("recovered", Guid.Parse(recoveredRaidId));
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Equal(980L, reader.GetInt64(0), "C4 final attacker balance");
    Equal(20L, reader.GetInt64(1), "full victory removes only backed defender loss");
    Equal(0L, reader.GetInt64(2), "settled raid escrow drains to zero");
    Equal(1L, reader.GetInt64(3), "one immutable result per raid");
    Equal(0L, reader.GetInt64(4), "immutable result artifact never stores raw allocation ticket");
    Equal("REFUNDED", reader.GetString(5), "active infrastructure timeout closes raid");
    Equal(0L, reader.GetInt64(6), "C4 leaves no open raid");
    Equal(0L, reader.GetInt64(7), "C4 ledger remains double-entry balanced");
}

static async Task RunC3Async(TestHost host, string connectionString)
{
    const string faction = "c3-natural";
    await SeedC3Async(connectionString);

    var noSnapshot = await SendAsync(host.Client, HttpMethod.Get,
        $"/v1/towns/{faction}/snapshots/latest", null, null);
    Equal(HttpStatusCode.OK, noSnapshot.Status, "missing latest snapshot is not an error");
    True(noSnapshot.Json.RootElement.ValueKind == JsonValueKind.Null, "missing latest snapshot is null");

    var emptyDraft = await SendAsync(host.Client, HttpMethod.Get, $"/v1/towns/{faction}/draft", null, null);
    False(Bool(emptyDraft, "exists"), "draft initially absent");

    var collisionLayout = new BaseLayoutView
    {
        OwnerAccountId = attackerId,
        FactionId = faction,
        Towers = [new PlacedTowerView { TowerId = "c3-natural/shared-1", Position = new Vector3View() }],
        Garrison = [new GarrisonMonsterView
        {
            CardId = "c3-natural/shared-1", Position = new Vector3View { X = 2 },
        }],
    };
    var collisionDraft = await SendAsync(host.Client, HttpMethod.Put,
        $"/v1/towns/{faction}/draft", collisionLayout, "draft:c3:kind-collision");
    Equal(HttpStatusCode.OK, collisionDraft.Status,
        "tower and garrison may share a legacy composite id when content kind differs");

    var spoofedOwner = C3Layout(ownerId: "11000000-0000-0000-0000-000000000002");
    var ownerRejected = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        spoofedOwner, "draft:owner-spoof");
    Equal(HttpStatusCode.Forbidden, ownerRejected.Status, "spoofed town owner rejected");
    ErrorCode(ownerRejected, "TOWN_OWNER_MISMATCH");

    var unknownContent = C3Layout(unknownContent: true);
    var contentRejected = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        unknownContent, "draft:unknown-content");
    Equal(HttpStatusCode.UnprocessableEntity, contentRejected.Status, "unknown content rejected");
    ErrorCode(contentRejected, "CONTENT_VALIDATION_FAILED");

    var layoutV1 = C3Layout();
    var saveDraft = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        layoutV1, "draft:c3:v1");
    Equal(HttpStatusCode.OK, saveDraft.Status, "draft saved");
    True(Bool(saveDraft, "success"), "draft acknowledgement");
    var saveReplay = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        layoutV1, "draft:c3:v1");
    Equal(HttpStatusCode.OK, saveReplay.Status, "draft replay");

    var changedLayout = C3Layout();
    changedLayout.Towers[0].Position.X = 9;
    var draftConflict = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        changedLayout, "draft:c3:v1");
    Equal(HttpStatusCode.Conflict, draftConflict.Status, "changed draft idempotency payload rejected");
    ErrorCode(draftConflict, "IDEMPOTENCY_KEY_REUSED");

    var loadedDraft = await SendAsync(host.Client, HttpMethod.Get, $"/v1/towns/{faction}/draft", null, null);
    True(Bool(loadedDraft, "exists"), "saved draft exists");
    Equal(50L, Long(loadedDraft, "checkedOutLayout", "storedGold"), "saved draft payload");

    var warGemBefore = Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance");
    var goldBefore = await PlayerBalanceAsync(connectionString, "GOLD");
    var badCapacity = await SendAsync(host.Client, HttpMethod.Post, $"/v1/towns/{faction}/deployments",
        new { checkedOutLayout = layoutV1, usedCapacity = 4, maxCapacity = 100 }, "deploy:c3:bad-capacity");
    Equal(HttpStatusCode.UnprocessableEntity, badCapacity.Status, "forged capacity rejected");
    ErrorCode(badCapacity, "CONTENT_VALIDATION_FAILED");
    Equal(warGemBefore, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "validation happens before War Gem debit");
    Equal(goldBefore, await PlayerBalanceAsync(connectionString, "GOLD"),
        "validation happens before Gold debit");

    var draftMismatch = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/towns/{faction}/deployments",
        new { checkedOutLayout = changedLayout, usedCapacity = 5, maxCapacity = 100 },
        "deploy:c3:draft-mismatch");
    Equal(HttpStatusCode.Conflict, draftMismatch.Status, "non-checked-out payload rejected");
    ErrorCode(draftMismatch, "DRAFT_VERSION_CONFLICT");
    Equal(warGemBefore, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "draft mismatch happens before War Gem debit");
    Equal(goldBefore, await PlayerBalanceAsync(connectionString, "GOLD"),
        "draft mismatch happens before Gold debit");

    var deployBodyV1 = new { checkedOutLayout = layoutV1, usedCapacity = 5, maxCapacity = 100 };
    var deployV1 = await SendAsync(host.Client, HttpMethod.Post, $"/v1/towns/{faction}/deployments",
        deployBodyV1, "deploy:c3:v1");
    Equal(HttpStatusCode.Created, deployV1.Status, "snapshot V1 deployed");
    True(Bool(deployV1, "success"), "snapshot commit success");
    Equal(1L, Long(deployV1, "snapshot", "revision"), "snapshot V1 revision");
    Equal(5L, Long(deployV1, "snapshot", "usedCapacity"), "server capacity calculation");
    Equal(100L, Long(deployV1, "snapshot", "maxCapacity"), "server max capacity");
    Equal(405L, Long(deployV1, "snapshot", "basePowerRating"), "server base power");
    Equal(3, Element(deployV1, "snapshot", "defenseUnits").GetArrayLength(),
        "snapshot pins tower, garrison, and Core combat authority");
    var snapshotV1Id = String(deployV1, "snapshot", "snapshotId");
    var deploymentV1Id = String(deployV1, "snapshot", "deploymentId");
    True(Guid.TryParse(deploymentV1Id, out _), "snapshot response includes deployment UUID for quote");
    Equal(900L, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "town stake debited once");
    Equal(390L, await PlayerBalanceAsync(connectionString, "GOLD"),
        "build checkout and vault deposit debited");

    var deployReplay = await SendAsync(host.Client, HttpMethod.Post, $"/v1/towns/{faction}/deployments",
        deployBodyV1, "deploy:c3:v1");
    Equal(snapshotV1Id, String(deployReplay, "snapshot", "snapshotId"), "deployment replay identity");
    Equal(deploymentV1Id, String(deployReplay, "snapshot", "deploymentId"),
        "deployment replay preserves quote target identity");
    Equal(900L, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "deployment replay does not debit town stake");
    Equal(390L, await PlayerBalanceAsync(connectionString, "GOLD"),
        "deployment replay does not debit Gold");

    var layoutV2 = C3Layout(revisionTwo: true);
    var saveV2 = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        layoutV2, "draft:c3:v2");
    Equal(HttpStatusCode.OK, saveV2.Status, "draft V2 saved");
    var deployBodyV2 = new { checkedOutLayout = layoutV2, usedCapacity = 6, maxCapacity = 100 };
    var concurrentV2A = SendAsync(host.Client, HttpMethod.Post, $"/v1/towns/{faction}/deployments",
        deployBodyV2, "deploy:c3:v2:a");
    var concurrentV2B = SendAsync(host.Client, HttpMethod.Post, $"/v1/towns/{faction}/deployments",
        deployBodyV2, "deploy:c3:v2:b");
    var v2Results = await Task.WhenAll(concurrentV2A, concurrentV2B);
    True(v2Results.All(result => result.Status is HttpStatusCode.Created or HttpStatusCode.OK),
        "concurrent deployment requests succeed/replay");
    var snapshotV2Id = String(v2Results[0], "snapshot", "snapshotId");
    var deploymentV2Id = String(v2Results[0], "snapshot", "deploymentId");
    Equal(snapshotV2Id, String(v2Results[1], "snapshot", "snapshotId"),
        "concurrent deployment creates one snapshot");
    Equal(deploymentV2Id, String(v2Results[1], "snapshot", "deploymentId"),
        "concurrent deployment returns one deployment identity");
    Equal(2L, Long(v2Results[0], "snapshot", "revision"), "snapshot V2 revision");
    Equal(6L, Long(v2Results[0], "snapshot", "usedCapacity"), "snapshot V2 capacity");
    Equal(530L, Long(v2Results[0], "snapshot", "basePowerRating"), "snapshot V2 power");
    Equal(900L, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "town escrow reused for V2");
    Equal(360L, await PlayerBalanceAsync(connectionString, "GOLD"),
        "V2 charges only build/vault delta");

    var latest = await SendAsync(host.Client, HttpMethod.Get,
        $"/v1/towns/{faction}/snapshots/latest", null, null);
    Equal(snapshotV2Id, String(latest, "snapshotId"), "latest pointer moves to V2");
    Equal(deploymentV2Id, String(latest, "deploymentId"), "latest exposes quote target deployment");
    var oldSnapshot = await SendAsync(host.Client, HttpMethod.Get,
        $"/v1/town-snapshots/{snapshotV1Id}", null, null);
    Equal(1L, Long(oldSnapshot, "revision"), "V1 remains readable");
    Equal(deploymentV1Id, String(oldSnapshot, "deploymentId"),
        "retired snapshot retains its historical deployment identity");
    Equal(50L, Long(oldSnapshot, "layout", "storedGold"), "V1 payload remains unchanged");
    Equal(1, Element(oldSnapshot, "layout", "towers").GetArrayLength(), "V1 tower list remains unchanged");

    var batch = await SendAsync(host.Client, HttpMethod.Post, "/v1/town-snapshots/latest/query",
        new { factionIds = new[] { faction } }, null);
    Equal(HttpStatusCode.OK, batch.Status, "snapshot batch query");
    var batchItems = Element(batch, "snapshots");
    Equal(1, batchItems.GetArrayLength(), "batch returns one active C3 town");
    Equal(snapshotV2Id, batchItems[0].GetProperty("snapshotId").GetString()!, "batch returns V2");
    Equal(deploymentV2Id, batchItems[0].GetProperty("deploymentId").GetString()!,
        "target pool batch exposes C2 quote deployment UUID");

    var revertDraft = await SendAsync(host.Client, HttpMethod.Put, $"/v1/towns/{faction}/draft",
        layoutV1, "draft:c3:revert-v1");
    Equal(HttpStatusCode.OK, revertDraft.Status, "old layout saved as a new draft");
    var revertDeploy = await SendAsync(host.Client, HttpMethod.Post,
        $"/v1/towns/{faction}/deployments", deployBodyV1, "deploy:c3:revert-v1");
    Equal(HttpStatusCode.Created, revertDeploy.Status, "retired layout creates a new deployment revision");
    Equal(3L, Long(revertDeploy, "snapshot", "revision"), "reverted layout becomes V3");
    var snapshotV3Id = String(revertDeploy, "snapshot", "snapshotId");
    True(snapshotV3Id != snapshotV1Id, "reverted layout does not reuse retired snapshot identity");
    var latestV3 = await SendAsync(host.Client, HttpMethod.Get,
        $"/v1/towns/{faction}/snapshots/latest", null, null);
    Equal(snapshotV3Id, String(latestV3, "snapshotId"), "latest pointer moves to reverted V3");
    Equal(900L, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "reverted revision keeps existing town escrow");
    Equal(385L, await PlayerBalanceAsync(connectionString, "GOLD"),
        "reverted revision applies build refund and vault withdrawal");

    var expensiveLayout = new BaseLayoutView
    {
        OwnerAccountId = attackerId,
        FactionId = "c3-expensive",
        Towers =
        [
            new PlacedTowerView
            {
                TowerId = "c3-expensive/tower-1",
                Position = new Vector3View(),
            },
        ],
    };
    var expensiveDraft = await SendAsync(host.Client, HttpMethod.Put,
        "/v1/towns/c3-expensive/draft", expensiveLayout, "draft:c3:expensive");
    Equal(HttpStatusCode.OK, expensiveDraft.Status, "expensive draft saved");
    var expensiveDeploy = await SendAsync(host.Client, HttpMethod.Post,
        "/v1/towns/c3-expensive/deployments",
        new { checkedOutLayout = expensiveLayout, usedCapacity = 1, maxCapacity = 100 },
        "deploy:c3:insufficient-gold");
    Equal(HttpStatusCode.Conflict, expensiveDeploy.Status, "insufficient Gold blocks deployment");
    ErrorCode(expensiveDeploy, "INSUFFICIENT_FUNDS");
    Equal(900L, Long(await SendAsync(host.Client, HttpMethod.Get, "/v1/wallet", null, null),
        "warGemBalance"), "failed Gold checkout rolls back War Gem stake");
    Equal(385L, await PlayerBalanceAsync(connectionString, "GOLD"),
        "failed Gold checkout leaves Gold unchanged");

    await AssertImmutableSnapshotAsync(connectionString, snapshotV1Id);
    await AssertC3DatabaseAsync(connectionString);
}

static BaseLayoutView C3Layout(bool revisionTwo = false, string ownerId = attackerId,
    bool unknownContent = false)
{
    var layout = new BaseLayoutView
    {
        OwnerAccountId = ownerId,
        FactionId = "c3-natural",
        StoredGold = revisionTwo ? 70 : 50,
        MinerCardIds = ["c3-natural/miner-1"],
        Towers =
        [
            new PlacedTowerView
            {
                TowerId = unknownContent ? "c3-natural/unknown" : "c3-natural/tower-1",
                Position = new Vector3View { X = 0, Y = 0, Z = 0 },
            },
        ],
        Garrison =
        [
            new GarrisonMonsterView
            {
                CardId = "c3-natural/garrison-1",
                Position = new Vector3View { X = 2, Y = 0, Z = 0 },
            },
        ],
    };
    if (revisionTwo)
        layout.Towers.Add(new PlacedTowerView
        {
            TowerId = "c3-natural/tower-2",
            Position = new Vector3View { X = 4, Y = 0, Z = 0 },
        });
    return layout;
}

static async Task SeedC3Async(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SET search_path = splice, public;
        INSERT INTO ledger_accounts (id, account_key, owner_type, owner_id, currency_code) VALUES
          ('21000000-0000-0000-0000-000000000002', 'test:c3:attacker:gold', 'PLAYER',
           '11000000-0000-0000-0000-000000000001', 'GOLD')
        ON CONFLICT (id) DO NOTHING;
        SELECT post_ledger_transaction(
          'test:c3:mint:gold:500', 'TEST_MINT', 'TEST', NULL,
          jsonb_build_array(
            jsonb_build_object('account_id','00000000-0000-0000-0000-000000000101','amount',-500),
            jsonb_build_object('account_id','21000000-0000-0000-0000-000000000002','amount',500)));
        INSERT INTO content_definitions
          (content_id, faction_id, content_kind, defense_capacity_cost, gold_cost,
           raid_power, content_version, combat_payload) VALUES
          ('c3-natural/tower-1','c3-natural','TOWER',2,20,120,'content-c4c2-v1',
           '{"maxHealth":1000,"armor":20,"attackDamage":25,"attackCooldownMs":1000,"attackRangeMilli":15000,"moveSpeedMilli":0,"abilityId":"","abilityDamage":0,"abilityCooldownMs":0,"abilityCastRangeMilli":0,"abilityRadiusMilli":0,"maxTargets":1}'),
          ('c3-natural/tower-2','c3-natural','TOWER',1,10,100,'content-c4c2-v1',
           '{"maxHealth":800,"armor":10,"attackDamage":20,"attackCooldownMs":1000,"attackRangeMilli":12000,"moveSpeedMilli":0,"abilityId":"","abilityDamage":0,"abilityCooldownMs":0,"abilityCastRangeMilli":0,"abilityRadiusMilli":0,"maxTargets":1}'),
          ('c3-natural/garrison-1','c3-natural','GARRISON',3,30,80,'content-c4c2-v1',
           '{"maxHealth":500,"armor":0,"attackDamage":30,"attackCooldownMs":1000,"attackRangeMilli":3000,"moveSpeedMilli":5000,"abilityId":"","abilityDamage":0,"abilityCooldownMs":0,"abilityCastRangeMilli":0,"abilityRadiusMilli":0,"maxTargets":1}'),
          ('c3-natural/miner-1','c3-natural','MINER',0,10,0,'content-c4c2-v1','{}'),
          ('c3-expensive/tower-1','c3-expensive','TOWER',1,1000,120,'content-c4c2-v1',
           '{"maxHealth":1000,"armor":20,"attackDamage":25,"attackCooldownMs":1000,"attackRangeMilli":15000,"moveSpeedMilli":0,"abilityId":"","abilityDamage":0,"abilityCooldownMs":0,"abilityCastRangeMilli":0,"abilityRadiusMilli":0,"maxTargets":1}'),
          ('c3-natural/shared-1','c3-natural','TOWER',2,20,120,'content-c4c2-v1',
           '{"maxHealth":1000,"armor":20,"attackDamage":25,"attackCooldownMs":1000,"attackRangeMilli":15000,"moveSpeedMilli":0,"abilityId":"","abilityDamage":0,"abilityCooldownMs":0,"abilityCastRangeMilli":0,"abilityRadiusMilli":0,"maxTargets":1}'),
          ('c3-natural/shared-1','c3-natural','GARRISON',3,30,80,'content-c4c2-v1',
           '{"maxHealth":500,"armor":0,"attackDamage":30,"attackCooldownMs":1000,"attackRangeMilli":3000,"moveSpeedMilli":5000,"abilityId":"","abilityDamage":0,"abilityCooldownMs":0,"abilityCastRangeMilli":0,"abilityRadiusMilli":0,"maxTargets":1}')
        ON CONFLICT (content_id, content_kind) DO UPDATE SET
          faction_id=EXCLUDED.faction_id,
          defense_capacity_cost=EXCLUDED.defense_capacity_cost,
          gold_cost=EXCLUDED.gold_cost,
          raid_power=EXCLUDED.raid_power,
          combat_payload=EXCLUDED.combat_payload,
          enabled=true,
          content_version=EXCLUDED.content_version;
        """, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task<long> PlayerBalanceAsync(string connectionString, string currency)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT balance FROM splice.ledger_accounts
         WHERE owner_type='PLAYER' AND owner_id=@player AND currency_code=@currency
        """, connection);
    command.Parameters.AddWithValue("player", Guid.Parse(attackerId));
    command.Parameters.AddWithValue("currency", currency);
    return (long)(await command.ExecuteScalarAsync())!;
}

static async Task AssertImmutableSnapshotAsync(string connectionString, string snapshotId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    try
    {
        await using var command = new NpgsqlCommand(
            "UPDATE splice.town_snapshots SET payload='{}'::jsonb WHERE id=@id", connection);
        command.Parameters.AddWithValue("id", Guid.Parse(snapshotId));
        await command.ExecuteNonQueryAsync();
        throw new Exception("TEST_FAILED: immutable snapshot accepted direct update");
    }
    catch (PostgresException exception)
    {
        True(exception.MessageText.StartsWith("IMMUTABLE_TOWN_RECORD", StringComparison.Ordinal),
            "database immutable snapshot trigger");
    }
}

static async Task AssertC3DatabaseAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT
          (SELECT count(*) FROM splice.town_layout_commits c JOIN splice.towns t ON t.id=c.town_id
             WHERE t.owner_player_id='11000000-0000-0000-0000-000000000001' AND t.faction_id='c3-natural'),
          (SELECT count(*) FROM splice.town_snapshots s JOIN splice.towns t ON t.id=s.town_id
             WHERE t.owner_player_id='11000000-0000-0000-0000-000000000001' AND t.faction_id='c3-natural'),
          (SELECT count(*) FROM splice.town_deployments d JOIN splice.towns t ON t.id=d.town_id
             WHERE t.faction_id='c3-natural' AND d.status='ACTIVE'),
          (SELECT count(*) FROM splice.town_deployments d JOIN splice.towns t ON t.id=d.town_id
             WHERE t.faction_id='c3-natural' AND d.status='RETIRED'),
          (SELECT a.balance FROM splice.town_escrows e JOIN splice.ledger_accounts a ON a.id=e.ledger_account_id
             JOIN splice.towns t ON t.id=e.town_id WHERE t.faction_id='c3-natural' AND e.state='ACTIVE'),
          (SELECT a.balance FROM splice.town_vaults v JOIN splice.ledger_accounts a ON a.id=v.ledger_account_id
             JOIN splice.towns t ON t.id=v.town_id WHERE t.faction_id='c3-natural' AND v.currency_code='GOLD'),
          (SELECT count(*) FROM splice.town_snapshots
             WHERE layout_commit_id IS NOT NULL
               AND payload_sha256 <> encode(public.digest(payload::text,'sha256'),'hex')),
          (SELECT count(*) FROM splice.ledger_transactions t WHERE t.status='POSTED' AND
             (SELECT COALESCE(sum(p.amount),0) FROM splice.ledger_postings p WHERE p.ledger_transaction_id=t.id)<>0)
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Equal(3L, reader.GetInt64(0), "three immutable layout commits");
    Equal(3L, reader.GetInt64(1), "three immutable snapshots");
    Equal(1L, reader.GetInt64(2), "one active deployment");
    Equal(2L, reader.GetInt64(3), "old deployments retired");
    Equal(100L, reader.GetInt64(4), "town escrow remains fully backed");
    Equal(50L, reader.GetInt64(5), "town vault matches reverted V3 stored Gold");
    Equal(0L, reader.GetInt64(6), "snapshot hashes match immutable payloads");
    Equal(0L, reader.GetInt64(7), "ledger remains balanced after C3");
}

static string String(ApiResult result, params string[] path) => Element(result, path).GetString()!;
static long Long(ApiResult result, params string[] path) => Element(result, path).GetInt64();
static bool Bool(ApiResult result, params string[] path) => Element(result, path).GetBoolean();
static JsonElement Element(ApiResult result, params string[] path)
{
    var element = result.Json.RootElement;
    foreach (var segment in path) element = element.GetProperty(segment);
    return element;
}
static void ErrorCode(ApiResult result, string code) => Equal(code, String(result, "error", "code"), $"error code {code}");
static void True(bool value, string name) { if (!value) throw new Exception($"TEST_FAILED: {name}"); }
static void False(bool value, string name) => True(!value, name);
static void Equal<T>(T expected, T actual, string name) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"TEST_FAILED: {name}; expected={expected}, actual={actual}");
}

sealed record ApiResult(HttpStatusCode Status, JsonDocument Json);
sealed class TestHost : IAsyncDisposable
{
    public TestHost(WebApplication app, HttpClient client)
    {
        App = app;
        Client = client;
    }

    public WebApplication App { get; }
    public HttpClient Client { get; }
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
    }
}
