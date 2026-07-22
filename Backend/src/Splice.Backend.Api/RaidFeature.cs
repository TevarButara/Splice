using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed record CreateRaidQuoteRequest(string TargetId, string TargetName,
    string DifficultyBand, string AttackerLoadoutId);
public sealed record RaidQuoteView(string QuoteId, string TargetId, string TargetName,
    string DifficultyBand, long EntryStake, long FullVictoryPayout, long OuterExtractionPayout,
    long InnerExtractionPayout, long CoreExtractionPayout, string ExpiresUtc);
public sealed record ConfirmRaidRequest(string QuoteId);
public sealed record StartupRefundRequest(string RaidId, string ReasonCode);
public sealed record RaidStartView(bool Success, string Error, string RaidId, string QuoteId, WalletView Wallet);
public sealed record RaidFundingView(bool Success, string Error, RaidStakeTransactionView? Transaction, WalletView Wallet);

public static class RaidFeature
{
    private const string RulesVersion = "raid-economy-c2-v1";
    private const string SceneContractVersion = "raid-scene-c2-v1";

    public static void MapRaidEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/raid-quotes", CreateQuoteAsync);
        app.MapPost("/v1/raids", ConfirmRaidAsync);
        app.MapPost("/v1/raids/{raidId:guid}/startup-refund", StartupRefundAsync);
    }

    private static async Task<IResult> CreateQuoteAsync(HttpContext context, CreateRaidQuoteRequest request,
        IdempotencyExecutor idempotency)
    {
        var attackerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, attackerId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.TargetId, out var deploymentId) ||
                    !Guid.TryParse(request.AttackerLoadoutId, out var loadoutId))
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "INVALID_REQUEST", "Target and loadout IDs must be UUIDs.");

                await using var targetCommand = new NpgsqlCommand("""
                    SELECT d.active_snapshot_id, d.stake_band, d.status,
                           t.owner_player_id, p.display_name
                      FROM splice.town_deployments d
                      JOIN splice.towns t ON t.id = d.town_id
                      JOIN splice.players p ON p.id = t.owner_player_id
                     WHERE d.id = @deployment
                     FOR SHARE OF d
                    """, connection, transaction);
                targetCommand.Parameters.AddWithValue("deployment", deploymentId);
                await using var reader = await targetCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "TARGET_INELIGIBLE", "Raid target was not found.");

                var snapshotId = reader.GetGuid(0);
                var band = reader.GetString(1);
                var status = reader.GetString(2);
                var defenderId = reader.GetGuid(3);
                var targetName = reader.GetString(4);
                await reader.DisposeAsync();

                if (defenderId == attackerId)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "SELF_TARGET_FORBIDDEN", "A player cannot raid their own town.");
                if (status is not ("READY" or "ACTIVE"))
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "TARGET_INELIGIBLE", "Raid target is not currently active.");

                var stake = band switch { "HIGH" => 600L, "RISKY" => 300L, _ => 100L };
                var quoteId = Guid.NewGuid();
                var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
                var full = stake * 18 / 10;
                var outer = stake * 6 / 10;
                var inner = stake * 9 / 10;
                var core = stake * 12 / 10;

                await using var insert = new NpgsqlCommand("""
                    INSERT INTO splice.raid_quotes (
                        id, attacker_player_id, target_deployment_id, target_snapshot_id,
                        attacker_loadout_id, difficulty_band, attacker_stake, defender_max_loss,
                        full_victory_payout, outer_payout, inner_payout, core_payout,
                        rules_version, expires_at)
                    VALUES (@id, @attacker, @deployment, @snapshot, @loadout, @band, @stake, @loss,
                            @full, @outer, @inner, @core, @rules, @expires)
                    """, connection, transaction);
                insert.Parameters.AddWithValue("id", quoteId);
                insert.Parameters.AddWithValue("attacker", attackerId);
                insert.Parameters.AddWithValue("deployment", deploymentId);
                insert.Parameters.AddWithValue("snapshot", snapshotId);
                insert.Parameters.AddWithValue("loadout", loadoutId);
                insert.Parameters.AddWithValue("band", band);
                insert.Parameters.AddWithValue("stake", stake);
                insert.Parameters.AddWithValue("loss", full - stake);
                insert.Parameters.AddWithValue("full", full);
                insert.Parameters.AddWithValue("outer", outer);
                insert.Parameters.AddWithValue("inner", inner);
                insert.Parameters.AddWithValue("core", core);
                insert.Parameters.AddWithValue("rules", RulesVersion);
                insert.Parameters.AddWithValue("expires", expiresAt);
                await insert.ExecuteNonQueryAsync(cancellationToken);

                return new ApiReply(StatusCodes.Status201Created,
                    new RaidQuoteView(quoteId.ToString("D"), deploymentId.ToString("D"), targetName, band,
                        stake, full, outer, inner, core, expiresAt.ToString("O")));
            });
    }

    private static async Task<IResult> ConfirmRaidAsync(HttpContext context, ConfirmRaidRequest request,
        IdempotencyExecutor idempotency)
    {
        var attackerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, attackerId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.QuoteId, out var quoteId))
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "INVALID_REQUEST", "Quote ID must be a UUID.");

                await using var quoteCommand = new NpgsqlCommand("""
                    SELECT q.target_snapshot_id, q.attacker_stake, q.expires_at,
                           t.owner_player_id, q.full_victory_payout,
                           te.id, te.ledger_account_id, te.state
                      FROM splice.raid_quotes q
                      JOIN splice.town_deployments d ON d.id = q.target_deployment_id
                      JOIN splice.towns t ON t.id = d.town_id
                      LEFT JOIN splice.town_escrows te ON te.id = d.town_escrow_id
                     WHERE q.id = @quote AND q.attacker_player_id = @attacker
                     FOR UPDATE OF q
                    """, connection, transaction);
                quoteCommand.Parameters.AddWithValue("quote", quoteId);
                quoteCommand.Parameters.AddWithValue("attacker", attackerId);
                await using var reader = await quoteCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "QUOTE_NOT_FOUND", "Raid quote was not found.");
                var snapshotId = reader.GetGuid(0);
                var stake = reader.GetInt64(1);
                var expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
                var defenderId = reader.GetGuid(3);
                var fullPayout = reader.GetInt64(4);
                var defenderTownEscrowId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
                var defenderTownAccountId = reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6);
                var defenderTownEscrowState = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                await reader.DisposeAsync();

                // Re-check only after locking the quote: concurrent keys for one quote must replay one raid.
                var existingRaid = await FindRaidByQuoteAsync(connection, transaction, quoteId,
                    attackerId, cancellationToken);
                if (existingRaid is not null)
                {
                    var replayWallet = await WalletFeature.LoadAsync(connection, transaction, attackerId, cancellationToken);
                    return new ApiReply(StatusCodes.Status200OK,
                        new RaidStartView(true, string.Empty, existingRaid.Value.ToString("D"),
                            quoteId.ToString("D"), replayWallet));
                }

                if (expiresAt <= DateTimeOffset.UtcNow)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "QUOTE_EXPIRED", "Raid quote has expired.");
                if (attackerId == defenderId)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "SELF_TARGET_FORBIDDEN", "A player cannot raid their own town.");

                if (await HasOpenRaidAsync(connection, transaction, attackerId, cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "PENDING_RAID_EXISTS", "Another funded raid is still open.");

                var defenderReserve = Math.Max(0, fullPayout - stake);
                if (defenderReserve > 0 && (defenderTownEscrowId is null || defenderTownAccountId is null ||
                                            defenderTownEscrowState != "ACTIVE"))
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "TARGET_ESCROW_UNAVAILABLE", "Target town does not have active War Gem backing.");
                if (defenderTownAccountId is not null)
                {
                    var defenderBalance = await AccountBalanceAsync(connection, transaction,
                        defenderTownAccountId.Value, cancellationToken);
                    if (defenderBalance < defenderReserve)
                        return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                            "TARGET_ESCROW_UNAVAILABLE", "Target town War Gem backing is already reserved.");
                }

                var playerAccountId = await PlayerWarGemAccountAsync(connection, transaction,
                    attackerId, cancellationToken);
                var raidId = Guid.NewGuid();
                var escrowAccountId = Guid.NewGuid();

                await ExecuteAsync(connection, transaction, """
                    INSERT INTO splice.raid_sessions (
                        id, quote_id, attacker_player_id, defender_player_id,
                        target_snapshot_id, state, scene_contract_version)
                    VALUES (@raid, @quote, @attacker, @defender, @snapshot, 'FUNDED', @scene)
                    """, cancellationToken,
                    ("raid", raidId), ("quote", quoteId), ("attacker", attackerId),
                    ("defender", defenderId), ("snapshot", snapshotId), ("scene", SceneContractVersion));

                await ExecuteAsync(connection, transaction, """
                    INSERT INTO splice.ledger_accounts
                        (id, account_key, owner_type, owner_id, currency_code)
                    VALUES (@account, @key, 'RAID_ESCROW', @raid, 'WAR_GEM')
                    """, cancellationToken,
                    ("account", escrowAccountId), ("key", $"raid:{raidId:D}:escrow"), ("raid", raidId));

                var postingItems = new List<Dictionary<string, object>>
                {
                    new() { ["account_id"] = playerAccountId, ["amount"] = -stake },
                    new() { ["account_id"] = escrowAccountId, ["amount"] = stake + defenderReserve },
                };
                if (defenderReserve > 0)
                    postingItems.Add(new()
                    {
                        ["account_id"] = defenderTownAccountId!.Value,
                        ["amount"] = -defenderReserve,
                    });
                var postings = JsonSerializer.Serialize(postingItems);
                var fundedTransactionId = await PostLedgerAsync(connection, transaction,
                    $"raid:{raidId:D}:fund", "RAID_FUND", raidId, postings, cancellationToken);

                await ExecuteAsync(connection, transaction, """
                    INSERT INTO splice.raid_escrows (
                        id, raid_id, ledger_account_id, currency_code, funded_amount,
                        state, funded_transaction_id, defender_town_escrow_id,
                        defender_reserved_amount)
                    VALUES (@id, @raid, @account, 'WAR_GEM', @amount, 'FUNDED', @transaction,
                            @townEscrow, @reserve)
                    """, cancellationToken,
                    ("id", Guid.NewGuid()), ("raid", raidId), ("account", escrowAccountId),
                    ("amount", stake), ("transaction", fundedTransactionId),
                    ("townEscrow", (object?)defenderTownEscrowId ?? DBNull.Value),
                    ("reserve", defenderReserve));

                var wallet = await WalletFeature.LoadAsync(connection, transaction, attackerId, cancellationToken);
                return new ApiReply(StatusCodes.Status201Created,
                    new RaidStartView(true, string.Empty, raidId.ToString("D"), quoteId.ToString("D"), wallet));
            });
    }

    private static async Task<IResult> StartupRefundAsync(HttpContext context, Guid raidId,
        StartupRefundRequest request, IdempotencyExecutor idempotency)
    {
        var attackerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, attackerId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.RaidId, out var bodyRaidId) || bodyRaidId != raidId)
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "RAID_ID_MISMATCH", "Route and body raid IDs must match.");
                if (request.ReasonCode is not ("CLIENT_START_FAILED" or "ALLOCATION_FAILED" or "STARTUP_TIMEOUT"))
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "REFUND_REASON_INVALID", "Startup refund reason is not allowed.");

                await using var query = new NpgsqlCommand("""
                    SELECT r.state, r.started_at, e.id, e.ledger_account_id,
                           e.funded_amount, e.defender_reserved_amount, te.ledger_account_id,
                           e.state, e.refunded_transaction_id,
                           q.difficulty_band, q.attacker_stake, q.full_victory_payout,
                           q.outer_payout, q.inner_payout, q.core_payout, defender.display_name
                      FROM splice.raid_sessions r
                      JOIN splice.raid_escrows e ON e.raid_id = r.id
                      JOIN splice.raid_quotes q ON q.id = r.quote_id
                      JOIN splice.players defender ON defender.id = r.defender_player_id
                      LEFT JOIN splice.town_escrows te ON te.id = e.defender_town_escrow_id
                     WHERE r.id = @raid AND r.attacker_player_id = @attacker
                     FOR UPDATE OF r, e
                    """, connection, transaction);
                query.Parameters.AddWithValue("raid", raidId);
                query.Parameters.AddWithValue("attacker", attackerId);
                await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "RAID_NOT_FOUND", "Raid was not found.");

                var raidState = reader.GetString(0);
                var started = !reader.IsDBNull(1);
                var escrowId = reader.GetGuid(2);
                var escrowAccountId = reader.GetGuid(3);
                var amount = reader.GetInt64(4);
                var defenderReserve = reader.GetInt64(5);
                var defenderTownAccountId = reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6);
                var escrowState = reader.GetString(7);
                var offer = new RaidStakeOfferView(reader.GetString(15), reader.GetString(9), reader.GetInt64(10),
                    reader.GetInt64(11), reader.GetInt64(12), reader.GetInt64(13), reader.GetInt64(14));
                await reader.DisposeAsync();

                if (raidState == "REFUNDED" || escrowState == "REFUNDED")
                {
                    var existingWallet = await WalletFeature.LoadAsync(connection, transaction, attackerId, cancellationToken);
                    return new ApiReply(StatusCodes.Status200OK,
                        new RaidFundingView(true, string.Empty, null, existingWallet));
                }
                if (raidState != "FUNDED" || started)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_ALREADY_STARTED", "A started raid cannot receive a startup refund.");

                var playerAccountId = await PlayerWarGemAccountAsync(connection, transaction,
                    attackerId, cancellationToken);
                var postingItems = new List<Dictionary<string, object>>
                {
                    new() { ["account_id"] = escrowAccountId, ["amount"] = -(amount + defenderReserve) },
                    new() { ["account_id"] = playerAccountId, ["amount"] = amount },
                };
                if (defenderReserve > 0 && defenderTownAccountId is not null)
                    postingItems.Add(new()
                    {
                        ["account_id"] = defenderTownAccountId.Value,
                        ["amount"] = defenderReserve,
                    });
                var postings = JsonSerializer.Serialize(postingItems);
                var refundTransactionId = await PostLedgerAsync(connection, transaction,
                    $"raid:{raidId:D}:startup_refund", "RAID_STARTUP_REFUND", raidId,
                    postings, cancellationToken);

                await ExecuteAsync(connection, transaction, """
                    UPDATE splice.raid_sessions SET state = 'REFUNDED', completed_at = clock_timestamp()
                     WHERE id = @raid;
                    UPDATE splice.raid_escrows
                       SET state = 'REFUNDED', refunded_transaction_id = @transaction,
                           settled_at = clock_timestamp()
                     WHERE id = @escrow
                    """, cancellationToken,
                    ("raid", raidId), ("transaction", refundTransactionId), ("escrow", escrowId));

                var wallet = await WalletFeature.LoadAsync(connection, transaction, attackerId, cancellationToken);
                var transactionView = new RaidStakeTransactionView(raidId.ToString("D"), offer, true,
                    "Aborted", 0, amount, wallet.WarGemBalance, string.Empty,
                    DateTimeOffset.UtcNow.ToString("O"), request.ReasonCode);
                return new ApiReply(StatusCodes.Status200OK,
                    new RaidFundingView(true, string.Empty, transactionView, wallet));
            });
    }

    private static async Task<Guid?> FindRaidByQuoteAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid quoteId, Guid attackerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id FROM splice.raid_sessions
             WHERE quote_id = @quote AND attacker_player_id = @attacker
            """, connection, transaction);
        command.Parameters.AddWithValue("quote", quoteId);
        command.Parameters.AddWithValue("attacker", attackerId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task<bool> HasOpenRaidAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid attackerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM splice.raid_sessions
                 WHERE attacker_player_id = @attacker
                   AND state IN ('PREPARED', 'FUNDED', 'ACTIVE', 'SETTLING'))
            """, connection, transaction);
        command.Parameters.AddWithValue("attacker", attackerId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<Guid> PlayerWarGemAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid playerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id FROM splice.ledger_accounts
             WHERE owner_type = 'PLAYER' AND owner_id = @player AND currency_code = 'WAR_GEM'
             FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("player", playerId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not Guid accountId)
            throw new PostgresException("LEDGER_ACCOUNT_NOT_FOUND", "P0001", "P0001", "LEDGER_ACCOUNT_NOT_FOUND");
        return accountId;
    }

    private static async Task<long> AccountBalanceAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT balance FROM splice.ledger_accounts WHERE id=@account FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("account", accountId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<Guid> PostLedgerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string key, string type, Guid referenceId, string postings, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT splice.post_ledger_transaction(
                @key, @type, 'RAID', @reference, @postings, '{}'::jsonb)
            """, connection, transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("reference", referenceId);
        command.Parameters.AddWithValue("postings", NpgsqlDbType.Jsonb, postings);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
