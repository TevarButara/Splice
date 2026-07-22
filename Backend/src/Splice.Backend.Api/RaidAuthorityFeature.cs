using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed record AllocateRaidRequest(string RaidId);
public sealed record RaidAllocationView(bool Success, string Error, string RaidId,
    string AllocationId, string RaidServerId, string Ticket, string TargetSnapshotId,
    string SceneContractVersion, string ExpiresUtc);
public sealed record TrustedRaidStartRequest(string AllocationId, string Ticket);
public sealed record TrustedRaidStartView(bool Success, string Error, string RaidId,
    string AllocationId, string TargetSnapshotId, string AttackerLoadoutId,
    string SceneContractVersion, JsonElement TargetSnapshot, string StartedUtc);
public sealed record TrustedRaidResultRequest(string AllocationId, string Ticket, string ResultId,
    string Outcome, int BreachedRings, int DurationMs, string SimulationHash);
public sealed record TrustedRaidResultView(bool Success, string Error, string RaidId,
    string ResultId, string Outcome, int BreachedRings, long WarGemPayout,
    bool DefenderDeploymentPaused, WalletView AttackerWallet, string SettledUtc);
public sealed record RaidLifecycleView(string RaidId, string State, string TargetSnapshotId,
    string AllocationState, string ResultId, string Outcome, int BreachedRings,
    long WarGemPayout, string UpdatedUtc);

public static partial class RaidAuthorityFeature
{
    private static readonly Guid TrustedActorId = Guid.Parse("00000000-0000-0000-0000-000000000401");
    private static readonly Regex HashPattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapRaidAuthorityEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/raids/{raidId:guid}/allocation", AllocateAsync);
        app.MapGet("/v1/raids/{raidId:guid}", GetLifecycleAsync);
        app.MapPost("/internal/v1/raids/{raidId:guid}/start", StartAsync);
        app.MapPost("/internal/v1/raids/{raidId:guid}/result", SubmitResultAsync);
    }

    private static async Task<IResult> AllocateAsync(HttpContext context, Guid raidId,
        AllocateRaidRequest request, IdempotencyExecutor idempotency, IConfiguration configuration)
    {
        var attackerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, attackerId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.RaidId, out var bodyRaidId) || bodyRaidId != raidId)
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "RAID_ID_MISMATCH", "Route and body raid IDs must match.");

                await using var query = new NpgsqlCommand("""
                    SELECT r.state, r.target_snapshot_id, r.scene_contract_version,
                           a.id, a.state
                      FROM splice.raid_sessions r
                      LEFT JOIN splice.raid_allocations a ON a.raid_id = r.id
                     WHERE r.id = @raid AND r.attacker_player_id = @attacker
                     FOR UPDATE OF r
                    """, connection, transaction);
                query.Parameters.AddWithValue("raid", raidId);
                query.Parameters.AddWithValue("attacker", attackerId);
                await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "RAID_NOT_FOUND", "Raid was not found.");
                var state = reader.GetString(0);
                var snapshotId = reader.GetGuid(1);
                var sceneContract = reader.GetString(2);
                var hasAllocation = !reader.IsDBNull(3);
                await reader.DisposeAsync();

                if (hasAllocation)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_ALREADY_ALLOCATED", "Use the original idempotency key to replay the allocation ticket.");
                if (state != "FUNDED")
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_NOT_ALLOCATABLE", "Only a funded raid can be allocated.");

                var allocationId = Guid.NewGuid();
                var ticket = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                var ticketHash = Sha256(ticket);
                var serverId = configuration["RaidServer:DefaultServerId"] ?? "local-authoritative-raid-1";
                var lifetime = Math.Clamp(configuration.GetValue("RaidServer:TicketLifetimeSeconds", 120), 30, 600);
                var expiresAt = DateTimeOffset.UtcNow.AddSeconds(lifetime);
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO splice.raid_allocations
                        (id, raid_id, raid_server_id, ticket_hash, state, expires_at)
                    VALUES (@id, @raid, @server, @hash, 'ALLOCATED', @expires);
                    UPDATE splice.raid_sessions SET raid_server_id = @server WHERE id = @raid
                    """, connection, transaction);
                insert.Parameters.AddWithValue("id", allocationId);
                insert.Parameters.AddWithValue("raid", raidId);
                insert.Parameters.AddWithValue("server", serverId);
                insert.Parameters.AddWithValue("hash", ticketHash);
                insert.Parameters.AddWithValue("expires", expiresAt);
                await insert.ExecuteNonQueryAsync(cancellationToken);

                return new ApiReply(StatusCodes.Status201Created,
                    new RaidAllocationView(true, string.Empty, raidId.ToString("D"),
                        allocationId.ToString("D"), serverId, ticket, snapshotId.ToString("D"),
                        sceneContract, expiresAt.ToString("O")));
            });
    }

    private static async Task<IResult> GetLifecycleAsync(HttpContext context, Guid raidId,
        NpgsqlDataSource dataSource)
    {
        var attackerId = RequestIdentityMiddleware.PlayerId(context);
        await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("""
            SELECT r.state, r.target_snapshot_id, COALESCE(a.state, ''),
                   COALESCE(rr.id::text, ''), COALESCE(rr.outcome, ''),
                   COALESCE(rr.breached_rings, 0), COALESCE(rr.war_gem_payout, 0),
                   COALESCE(r.completed_at, r.started_at, r.created_at)
              FROM splice.raid_sessions r
              LEFT JOIN splice.raid_allocations a ON a.raid_id = r.id
              LEFT JOIN splice.raid_results rr ON rr.raid_id = r.id
             WHERE r.id = @raid AND r.attacker_player_id = @attacker
            """, connection);
        command.Parameters.AddWithValue("raid", raidId);
        command.Parameters.AddWithValue("attacker", attackerId);
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        if (!await reader.ReadAsync(context.RequestAborted))
            return IdempotencyExecutor.ToResult(ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                "RAID_NOT_FOUND", "Raid was not found."));
        return Results.Ok(new RaidLifecycleView(raidId.ToString("D"), reader.GetString(0),
            reader.GetGuid(1).ToString("D"), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt32(5), reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(7).ToString("O")));
    }

    private static async Task<IResult> StartAsync(HttpContext context, Guid raidId,
        TrustedRaidStartRequest request, IdempotencyExecutor idempotency)
    {
        var serverId = RequestIdentityMiddleware.RaidServerId(context);
        return await idempotency.ExecuteAsync(context, TrustedActorId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.AllocationId, out var allocationId) ||
                    string.IsNullOrWhiteSpace(request.Ticket))
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "INVALID_REQUEST", "Allocation ID and ticket are required.");

                await using var query = new NpgsqlCommand("""
                    SELECT r.state, r.target_snapshot_id, r.scene_contract_version,
                           q.attacker_loadout_id, a.state, a.raid_server_id, a.ticket_hash,
                           a.expires_at, s.payload::text
                      FROM splice.raid_sessions r
                      JOIN splice.raid_quotes q ON q.id = r.quote_id
                      JOIN splice.raid_allocations a ON a.raid_id = r.id
                      JOIN splice.town_snapshots s ON s.id = r.target_snapshot_id
                     WHERE r.id = @raid AND a.id = @allocation
                     FOR UPDATE OF r, a
                    """, connection, transaction);
                query.Parameters.AddWithValue("raid", raidId);
                query.Parameters.AddWithValue("allocation", allocationId);
                await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "ALLOCATION_NOT_FOUND", "Raid allocation was not found.");
                var raidState = reader.GetString(0);
                var snapshotId = reader.GetGuid(1);
                var sceneContract = reader.GetString(2);
                var loadoutId = reader.GetGuid(3);
                var allocationState = reader.GetString(4);
                var assignedServer = reader.GetString(5);
                var ticketHash = reader.GetString(6);
                var expiresAt = reader.GetFieldValue<DateTimeOffset>(7);
                var snapshotPayload = JsonDocument.Parse(reader.GetString(8)).RootElement.Clone();
                await reader.DisposeAsync();

                if (assignedServer != serverId || !TicketMatches(ticketHash, request.Ticket))
                    return ApiErrors.Reply(context, StatusCodes.Status403Forbidden,
                        "RAID_TICKET_INVALID", "Raid ticket is invalid for this server.");
                if (expiresAt <= DateTimeOffset.UtcNow && allocationState == "ALLOCATED")
                {
                    await ExecuteAsync(connection, transaction,
                        "UPDATE splice.raid_allocations SET state='EXPIRED' WHERE id=@allocation",
                        cancellationToken, ("allocation", allocationId));
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_TICKET_EXPIRED", "Raid allocation ticket has expired.");
                }
                if (raidState == "ACTIVE" && allocationState == "CLAIMED")
                    return new ApiReply(StatusCodes.Status200OK,
                        StartView(raidId, allocationId, snapshotId, loadoutId, sceneContract,
                            snapshotPayload, DateTimeOffset.UtcNow));
                if (raidState != "FUNDED" || allocationState != "ALLOCATED")
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_CANNOT_START", "Raid is not in an allocatable state.");

                var startedAt = DateTimeOffset.UtcNow;
                await ExecuteAsync(connection, transaction, """
                    UPDATE splice.raid_allocations SET state='CLAIMED', claimed_at=@now WHERE id=@allocation;
                    UPDATE splice.raid_sessions SET state='ACTIVE', started_at=@now WHERE id=@raid;
                    UPDATE splice.raid_escrows SET state='ACTIVE' WHERE raid_id=@raid
                    """, cancellationToken, ("now", startedAt), ("allocation", allocationId), ("raid", raidId));
                return new ApiReply(StatusCodes.Status200OK,
                    StartView(raidId, allocationId, snapshotId, loadoutId, sceneContract,
                        snapshotPayload, startedAt));
            });
    }

    private static async Task<IResult> SubmitResultAsync(HttpContext context, Guid raidId,
        TrustedRaidResultRequest request, IdempotencyExecutor idempotency)
    {
        var serverId = RequestIdentityMiddleware.RaidServerId(context);
        return await idempotency.ExecuteAsync(context, TrustedActorId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.AllocationId, out var allocationId) ||
                    !Guid.TryParse(request.ResultId, out var resultId) ||
                    string.IsNullOrWhiteSpace(request.Ticket) ||
                    request.Outcome is not ("FULL_VICTORY" or "EXTRACTED" or "DEFEAT") ||
                    request.BreachedRings is < 0 or > 3 || request.DurationMs is < 1000 or > 3600000 ||
                    !HashPattern.IsMatch(request.SimulationHash ?? string.Empty))
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "RAID_RESULT_INVALID", "Trusted raid result failed schema validation.");

                var replay = await ReadExistingResultAsync(connection, transaction, raidId, cancellationToken);
                if (replay is not null)
                {
                    if (replay.Value.ResultId != resultId || replay.Value.Outcome != request.Outcome ||
                        replay.Value.Rings != request.BreachedRings || replay.Value.DurationMs != request.DurationMs ||
                        replay.Value.SimulationHash != request.SimulationHash)
                        return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                            "RAID_RESULT_CONFLICT", "Raid already has a different immutable result.");
                    var replayWallet = await WalletFeature.LoadAsync(connection, transaction,
                        replay.Value.AttackerId, cancellationToken);
                    return new ApiReply(StatusCodes.Status200OK,
                        new TrustedRaidResultView(true, string.Empty, raidId.ToString("D"),
                            resultId.ToString("D"), request.Outcome, request.BreachedRings,
                            replay.Value.Payout, replay.Value.DeploymentPaused, replayWallet,
                            replay.Value.SettledAt.ToString("O")));
                }

                await using var query = new NpgsqlCommand("""
                    SELECT r.state, r.attacker_player_id, a.state, a.raid_server_id, a.ticket_hash,
                           e.id, e.ledger_account_id, e.funded_amount, e.defender_reserved_amount,
                           e.defender_town_escrow_id, e.state, te.ledger_account_id,
                           q.attacker_stake, q.full_victory_payout, q.outer_payout,
                           q.inner_payout, q.core_payout, q.target_deployment_id
                      FROM splice.raid_sessions r
                      JOIN splice.raid_allocations a ON a.raid_id = r.id
                      JOIN splice.raid_escrows e ON e.raid_id = r.id
                      JOIN splice.raid_quotes q ON q.id = r.quote_id
                      LEFT JOIN splice.town_escrows te ON te.id = e.defender_town_escrow_id
                     WHERE r.id = @raid AND a.id = @allocation
                     FOR UPDATE OF r, a, e
                    """, connection, transaction);
                query.Parameters.AddWithValue("raid", raidId);
                query.Parameters.AddWithValue("allocation", allocationId);
                await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "RAID_NOT_FOUND", "Active raid was not found.");
                var raidState = reader.GetString(0);
                var attackerId = reader.GetGuid(1);
                var allocationState = reader.GetString(2);
                var assignedServer = reader.GetString(3);
                var ticketHash = reader.GetString(4);
                var escrowId = reader.GetGuid(5);
                var raidEscrowAccountId = reader.GetGuid(6);
                var attackerAmount = reader.GetInt64(7);
                var defenderReserve = reader.GetInt64(8);
                var defenderTownEscrowId = reader.IsDBNull(9) ? (Guid?)null : reader.GetGuid(9);
                var escrowState = reader.GetString(10);
                var defenderAccountId = reader.IsDBNull(11) ? (Guid?)null : reader.GetGuid(11);
                var requiredTownBacking = reader.GetInt64(12);
                var full = reader.GetInt64(13);
                var outer = reader.GetInt64(14);
                var inner = reader.GetInt64(15);
                var core = reader.GetInt64(16);
                var deploymentId = reader.GetGuid(17);
                await reader.DisposeAsync();

                if (assignedServer != serverId || !TicketMatches(ticketHash, request.Ticket))
                    return ApiErrors.Reply(context, StatusCodes.Status403Forbidden,
                        "RAID_TICKET_INVALID", "Raid ticket is invalid for this server.");
                if (raidState != "ACTIVE" || allocationState != "CLAIMED" || escrowState != "ACTIVE")
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_NOT_ACTIVE", "Only an active claimed raid can accept a result.");
                if (defenderReserve > 0 && (defenderTownEscrowId is null || defenderAccountId is null))
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "DEFENDER_ESCROW_MISSING", "Reserved defender backing is unavailable.");

                var payout = Payout(request.Outcome, request.BreachedRings, full, outer, inner, core);
                var totalEscrow = checked(attackerAmount + defenderReserve);
                if (payout > totalEscrow)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "PAYOUT_NOT_BACKED", "Raid payout exceeds its funded escrow.");
                var attackerAccountId = await PlayerWarGemAccountAsync(connection, transaction,
                    attackerId, cancellationToken);
                var remainder = totalEscrow - payout;
                var postings = new List<Dictionary<string, object>>
                {
                    new() { ["account_id"] = raidEscrowAccountId, ["amount"] = -totalEscrow },
                };
                if (payout > 0)
                    postings.Add(new() { ["account_id"] = attackerAccountId, ["amount"] = payout });
                if (remainder > 0 && defenderAccountId is not null)
                    postings.Add(new() { ["account_id"] = defenderAccountId.Value, ["amount"] = remainder });
                var settlementTransactionId = await PostLedgerAsync(connection, transaction,
                    $"raid:{raidId:D}:settlement", "RAID_SETTLEMENT", raidId,
                    JsonSerializer.Serialize(postings), cancellationToken);

                // Never persist the raw allocation ticket inside the immutable result artifact.
                var payload = JsonSerializer.Serialize(new
                {
                    request.AllocationId,
                    request.ResultId,
                    request.Outcome,
                    request.BreachedRings,
                    request.DurationMs,
                    request.SimulationHash,
                }, JsonOptions);
                var settledAt = DateTimeOffset.UtcNow;
                await using var write = new NpgsqlCommand("""
                    INSERT INTO splice.raid_results
                        (id, raid_id, allocation_id, raid_server_id, outcome, breached_rings,
                         duration_ms, simulation_hash, war_gem_payout, result_payload, received_at)
                    VALUES (@result, @raid, @allocation, @server, @outcome, @rings,
                            @duration, @hash, @payout, @payload, @now);
                    UPDATE splice.raid_sessions
                       SET state='SETTLED', result_id=@result, completed_at=@now
                     WHERE id=@raid;
                    UPDATE splice.raid_escrows
                       SET state='SETTLED', settlement_transaction_id=@transaction, settled_at=@now
                     WHERE id=@escrow;
                    UPDATE splice.raid_allocations
                       SET state='COMPLETED', completed_at=@now WHERE id=@allocation
                    """, connection, transaction);
                write.Parameters.AddWithValue("result", resultId);
                write.Parameters.AddWithValue("raid", raidId);
                write.Parameters.AddWithValue("allocation", allocationId);
                write.Parameters.AddWithValue("server", serverId);
                write.Parameters.AddWithValue("outcome", request.Outcome);
                write.Parameters.AddWithValue("rings", request.BreachedRings);
                write.Parameters.AddWithValue("duration", request.DurationMs);
                write.Parameters.AddWithValue("hash", request.SimulationHash!);
                write.Parameters.AddWithValue("payout", payout);
                write.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, payload);
                write.Parameters.AddWithValue("now", settledAt);
                write.Parameters.AddWithValue("transaction", settlementTransactionId);
                write.Parameters.AddWithValue("escrow", escrowId);
                await write.ExecuteNonQueryAsync(cancellationToken);

                var deploymentPaused = false;
                if (defenderAccountId is not null)
                {
                    var balance = await AccountBalanceAsync(connection, transaction,
                        defenderAccountId.Value, cancellationToken);
                    if (balance < requiredTownBacking)
                    {
                        deploymentPaused = await ExecuteAsync(connection, transaction,
                            "UPDATE splice.town_deployments SET status='PAUSED' WHERE id=@deployment AND status IN ('READY','ACTIVE')",
                            cancellationToken, ("deployment", deploymentId)) > 0;
                    }
                }

                var wallet = await WalletFeature.LoadAsync(connection, transaction, attackerId, cancellationToken);
                return new ApiReply(StatusCodes.Status201Created,
                    new TrustedRaidResultView(true, string.Empty, raidId.ToString("D"),
                        resultId.ToString("D"), request.Outcome, request.BreachedRings, payout,
                        deploymentPaused, wallet, settledAt.ToString("O")));
            });
    }

    private static TrustedRaidStartView StartView(Guid raidId, Guid allocationId, Guid snapshotId,
        Guid loadoutId, string sceneContract, JsonElement snapshot, DateTimeOffset startedAt) =>
        new(true, string.Empty, raidId.ToString("D"), allocationId.ToString("D"),
            snapshotId.ToString("D"), loadoutId.ToString("D"), sceneContract, snapshot, startedAt.ToString("O"));

    private static long Payout(string outcome, int rings, long full, long outer, long inner, long core) =>
        outcome switch
        {
            "FULL_VICTORY" => full,
            "EXTRACTED" when rings >= 3 => core,
            "EXTRACTED" when rings == 2 => inner,
            "EXTRACTED" when rings == 1 => outer,
            _ => 0,
        };

    private static bool TicketMatches(string expectedHash, string ticket)
    {
        var actual = Sha256(ticket ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash), Encoding.ASCII.GetBytes(actual));
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<(Guid ResultId, Guid AttackerId, string Outcome, int Rings,
        int DurationMs, string SimulationHash, long Payout, bool DeploymentPaused,
        DateTimeOffset SettledAt)?> ReadExistingResultAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid raidId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT rr.id, r.attacker_player_id, rr.outcome, rr.breached_rings,
                   rr.duration_ms, rr.simulation_hash, rr.war_gem_payout,
                   d.status='PAUSED', rr.received_at
              FROM splice.raid_results rr
              JOIN splice.raid_sessions r ON r.id = rr.raid_id
              JOIN splice.raid_quotes q ON q.id = r.quote_id
              JOIN splice.town_deployments d ON d.id = q.target_deployment_id
             WHERE rr.raid_id = @raid
            """, connection, transaction);
        command.Parameters.AddWithValue("raid", raidId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetString(5), reader.GetInt64(6), reader.GetBoolean(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    private static async Task<Guid> PlayerWarGemAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid playerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id FROM splice.ledger_accounts
             WHERE owner_type='PLAYER' AND owner_id=@player AND currency_code='WAR_GEM'
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
                @key, @type, 'RAID', @reference, @postings, '{"source":"trusted_raid_server"}'::jsonb)
            """, connection, transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("reference", referenceId);
        command.Parameters.AddWithValue("postings", NpgsqlDbType.Jsonb, postings);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
