using Npgsql;

namespace Splice.Backend.Api;

public sealed record RaidDefenseHistoryItemView(
    string RaidId, string ReportId, string AttackerPlayerId, string AttackerDisplayName,
    string DefenderPlayerId, string TargetSnapshotId, string State, string Outcome,
    int BreachedRings, long EntryStake, long AttackerPayout, long DefenderWarGemDelta,
    bool ReplayAvailable, string CompletedUtc, bool RevengeAvailable,
    string RevengeState, string RevengeCooldownUntilUtc);

public sealed record RaidDefenseHistoryPageView(
    IReadOnlyList<RaidDefenseHistoryItemView> Items,
    string NextBeforeUtc, string NextBeforeRaidId);

public sealed record PrepareRaidRevengeRequest(string SourceRaidId);

public sealed record RaidRevengeTargetView(
    bool Success, string Error, string SourceRaidId, string RequestId,
    string TargetDeploymentId, string TargetSnapshotId, string TargetOwnerAccountId,
    string TargetDisplayName, string TargetFactionId, long TargetPower,
    string ExpiresUtc);

public static class RaidHistoryFeature
{
    private static readonly TimeSpan RevengeCooldown = TimeSpan.FromHours(4);
    private static readonly TimeSpan RevengeRequestLifetime = TimeSpan.FromMinutes(10);

    public static void MapRaidHistoryEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/raid-history/defense", GetDefenseHistoryAsync);
        app.MapPost("/v1/raid-history/{sourceRaidId:guid}/revenge", PrepareRevengeAsync);
    }

    private static async Task<IResult> GetDefenseHistoryAsync(
        HttpContext context, NpgsqlDataSource dataSource, int? limit,
        DateTimeOffset? beforeUtc, Guid? beforeRaidId)
    {
        if (beforeUtc.HasValue != beforeRaidId.HasValue)
            return IdempotencyExecutor.ToResult(ApiErrors.Reply(context,
                StatusCodes.Status400BadRequest, "HISTORY_CURSOR_INVALID",
                "Both beforeUtc and beforeRaidId are required for stable pagination."));

        var playerId = RequestIdentityMiddleware.PlayerId(context);
        var pageSize = Math.Clamp(limit ?? 20, 1, 50);
        var cursorPredicate = beforeUtc.HasValue
            ? "AND (r.completed_at, r.id) < (@beforeUtc, @beforeRaidId)"
            : string.Empty;
        await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand($$"""
            SELECT r.id, r.attacker_player_id, attacker.display_name,
                   r.defender_player_id, r.target_snapshot_id, r.state,
                   rr.outcome, rr.breached_rings, q.attacker_stake,
                   rr.war_gem_payout, rp.result_id IS NOT NULL, r.completed_at,
                   revenge_target.id IS NOT NULL, cooldown.started_at,
                   pending.expires_at
              FROM splice.raid_sessions r
              JOIN splice.players attacker ON attacker.id = r.attacker_player_id
              JOIN splice.raid_quotes q ON q.id = r.quote_id
              JOIN splice.raid_results rr ON rr.raid_id = r.id
              LEFT JOIN splice.raid_replays rp ON rp.raid_id = r.id
              LEFT JOIN LATERAL (
                  SELECT d.id
                    FROM splice.towns town
                    JOIN splice.town_deployments d ON d.town_id = town.id
                    JOIN splice.town_snapshots snapshot ON snapshot.id = d.active_snapshot_id
                    JOIN splice.town_escrows escrow ON escrow.id = d.town_escrow_id
                   WHERE town.owner_player_id = r.attacker_player_id
                     AND d.status IN ('READY', 'ACTIVE')
                     AND snapshot.matchmaking_eligible
                     AND COALESCE((snapshot.payload->>'schemaVersion')::integer, 0) >= 2
                     AND escrow.state = 'ACTIVE'
                   ORDER BY d.activated_at DESC
                   LIMIT 1
              ) revenge_target ON true
              LEFT JOIN LATERAL (
                  SELECT started_at
                    FROM splice.raid_revenge_requests request
                   WHERE request.source_raid_id = r.id
                     AND request.requester_player_id = @player
                     AND request.state = 'STARTED'
                   ORDER BY request.started_at DESC
                   LIMIT 1
              ) cooldown ON true
              LEFT JOIN LATERAL (
                  SELECT expires_at
                    FROM splice.raid_revenge_requests request
                   WHERE request.source_raid_id = r.id
                     AND request.requester_player_id = @player
                     AND (
                         (request.state IN ('PREPARED', 'QUOTED')
                          AND request.expires_at > clock_timestamp())
                         OR request.state = 'FUNDED'
                     )
                   ORDER BY request.created_at DESC
                   LIMIT 1
              ) pending ON true
             WHERE r.defender_player_id = @player
               AND r.state = 'SETTLED'
               AND r.completed_at IS NOT NULL
               {{cursorPredicate}}
             ORDER BY r.completed_at DESC, r.id DESC
             LIMIT @take
            """, connection);
        command.Parameters.AddWithValue("player", playerId);
        command.Parameters.AddWithValue("take", pageSize + 1);
        if (beforeUtc.HasValue)
        {
            command.Parameters.AddWithValue("beforeUtc", beforeUtc.Value);
            command.Parameters.AddWithValue("beforeRaidId", beforeRaidId!.Value);
        }

        var items = new List<RaidDefenseHistoryItemView>(pageSize + 1);
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        while (await reader.ReadAsync(context.RequestAborted))
        {
            var completedAt = reader.GetFieldValue<DateTimeOffset>(11);
            var targetAvailable = reader.GetBoolean(12);
            var cooldownStarted = reader.IsDBNull(13)
                ? (DateTimeOffset?)null
                : reader.GetFieldValue<DateTimeOffset>(13);
            var pending = !reader.IsDBNull(14);
            var cooldownUntil = cooldownStarted?.Add(RevengeCooldown);
            var onCooldown = cooldownUntil > DateTimeOffset.UtcNow;
            var revengeState = !targetAvailable ? "TARGET_UNAVAILABLE" :
                onCooldown ? "COOLDOWN" :
                pending ? "PENDING" : "READY";
            var stake = reader.GetInt64(8);
            var payout = reader.GetInt64(9);
            var raidId = reader.GetGuid(0).ToString("D");
            items.Add(new RaidDefenseHistoryItemView(
                raidId, raidId, reader.GetGuid(1).ToString("D"), reader.GetString(2),
                reader.GetGuid(3).ToString("D"), reader.GetGuid(4).ToString("D"),
                reader.GetString(5), reader.GetString(6), reader.GetInt32(7),
                stake, payout, stake - payout, reader.GetBoolean(10),
                completedAt.ToString("O"), revengeState == "READY", revengeState,
                onCooldown ? cooldownUntil!.Value.ToString("O") : string.Empty));
        }

        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        var tail = hasMore && items.Count > 0 ? items[^1] : null;
        return Results.Ok(new RaidDefenseHistoryPageView(items,
            tail?.CompletedUtc ?? string.Empty, tail?.RaidId ?? string.Empty));
    }

    private static async Task<IResult> PrepareRevengeAsync(
        HttpContext context, Guid sourceRaidId, PrepareRaidRevengeRequest request,
        IdempotencyExecutor idempotency)
    {
        var requesterId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, requesterId, request,
            async (connection, transaction, cancellationToken) =>
            {
                if (!Guid.TryParse(request.SourceRaidId, out var bodyId) || bodyId != sourceRaidId)
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "RAID_ID_MISMATCH", "Route and body source raid IDs must match.");

                await using var source = new NpgsqlCommand("""
                    SELECT r.attacker_player_id, r.defender_player_id, r.state
                      FROM splice.raid_sessions r
                     WHERE r.id = @source
                     FOR UPDATE
                    """, connection, transaction);
                source.Parameters.AddWithValue("source", sourceRaidId);
                await using var sourceReader = await source.ExecuteReaderAsync(cancellationToken);
                if (!await sourceReader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                        "DEFENSE_REPORT_NOT_FOUND", "Defense history entry was not found.");
                var targetPlayerId = sourceReader.GetGuid(0);
                var defenderPlayerId = sourceReader.GetGuid(1);
                var sourceState = sourceReader.GetString(2);
                await sourceReader.DisposeAsync();

                if (defenderPlayerId != requesterId)
                    return ApiErrors.Reply(context, StatusCodes.Status403Forbidden,
                        "REVENGE_OWNER_REQUIRED", "Only the original defender can prepare this revenge.");
                if (targetPlayerId == requesterId)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "SELF_TARGET_FORBIDDEN", "Revenge cannot target the same player.");
                if (sourceState != "SETTLED")
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "REVENGE_SOURCE_NOT_SETTLED", "Only a settled defense report can start revenge.");

                await using var expireStale = new NpgsqlCommand("""
                    UPDATE splice.raid_revenge_requests
                       SET state = 'CANCELLED',
                           cancelled_at = clock_timestamp()
                     WHERE source_raid_id = @source
                       AND requester_player_id = @requester
                       AND state IN ('PREPARED', 'QUOTED')
                       AND expires_at <= clock_timestamp()
                    """, connection, transaction);
                expireStale.Parameters.AddWithValue("source", sourceRaidId);
                expireStale.Parameters.AddWithValue("requester", requesterId);
                await expireStale.ExecuteNonQueryAsync(cancellationToken);

                await using var pending = new NpgsqlCommand("""
                    SELECT EXISTS (
                        SELECT 1 FROM splice.raid_revenge_requests
                         WHERE source_raid_id = @source AND requester_player_id = @requester
                           AND (
                               (state IN ('PREPARED', 'QUOTED')
                                AND expires_at > clock_timestamp())
                               OR state = 'FUNDED'
                           ))
                    """, connection, transaction);
                pending.Parameters.AddWithValue("source", sourceRaidId);
                pending.Parameters.AddWithValue("requester", requesterId);
                if ((bool)(await pending.ExecuteScalarAsync(cancellationToken))!)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "REVENGE_REQUEST_PENDING", "A live revenge request already exists for this report.");

                await using var cooldown = new NpgsqlCommand("""
                    SELECT max(started_at)
                      FROM splice.raid_revenge_requests
                     WHERE source_raid_id = @source AND requester_player_id = @requester
                       AND state = 'STARTED'
                    """, connection, transaction);
                cooldown.Parameters.AddWithValue("source", sourceRaidId);
                cooldown.Parameters.AddWithValue("requester", requesterId);
                await using var cooldownReader =
                    await cooldown.ExecuteReaderAsync(cancellationToken);
                if (await cooldownReader.ReadAsync(cancellationToken) &&
                    !cooldownReader.IsDBNull(0))
                {
                    var lastStarted =
                        cooldownReader.GetFieldValue<DateTimeOffset>(0);
                    if (lastStarted.Add(RevengeCooldown) > DateTimeOffset.UtcNow)
                        return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                            "REVENGE_COOLDOWN_ACTIVE", "Revenge cooldown is still active.");
                }
                await cooldownReader.DisposeAsync();

                await using var target = new NpgsqlCommand("""
                    SELECT d.id, d.active_snapshot_id, target.display_name, town.faction_id,
                           snapshot.base_power
                      FROM splice.towns town
                      JOIN splice.players target ON target.id = town.owner_player_id
                      JOIN splice.town_deployments d ON d.town_id = town.id
                      JOIN splice.town_snapshots snapshot ON snapshot.id = d.active_snapshot_id
                      JOIN splice.town_escrows escrow ON escrow.id = d.town_escrow_id
                     WHERE town.owner_player_id = @target
                       AND d.status IN ('READY', 'ACTIVE')
                       AND snapshot.matchmaking_eligible
                       AND COALESCE((snapshot.payload->>'schemaVersion')::integer, 0) >= 2
                       AND jsonb_array_length(COALESCE(snapshot.payload->'defenseUnits','[]'::jsonb)) > 0
                       AND escrow.state = 'ACTIVE'
                     ORDER BY d.activated_at DESC
                     LIMIT 1
                     FOR SHARE OF d
                    """, connection, transaction);
                target.Parameters.AddWithValue("target", targetPlayerId);
                await using var targetReader = await target.ExecuteReaderAsync(cancellationToken);
                if (!await targetReader.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "REVENGE_TARGET_UNAVAILABLE",
                        "The original attacker has no currently raidable deployed town.");
                var targetDeploymentId = targetReader.GetGuid(0);
                var targetSnapshotId = targetReader.GetGuid(1);
                var targetName = targetReader.GetString(2);
                var targetFaction = targetReader.GetString(3);
                var targetPower = targetReader.GetInt64(4);
                await targetReader.DisposeAsync();

                var requestId = Guid.NewGuid();
                var expiresAt = DateTimeOffset.UtcNow.Add(RevengeRequestLifetime);
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO splice.raid_revenge_requests
                        (id, source_raid_id, requester_player_id, target_player_id,
                         target_deployment_id, target_snapshot_id, state, expires_at)
                    VALUES (@id, @source, @requester, @target, @deployment, @snapshot,
                            'PREPARED', @expires);
                    INSERT INTO splice.outbox_events
                        (aggregate_type, aggregate_id, event_type, payload)
                    VALUES ('RAID_REVENGE', @id, 'RaidRevengePrepared',
                            jsonb_build_object('sourceRaidId', @source,
                                               'targetDeploymentId', @deployment))
                    """, connection, transaction);
                insert.Parameters.AddWithValue("id", requestId);
                insert.Parameters.AddWithValue("source", sourceRaidId);
                insert.Parameters.AddWithValue("requester", requesterId);
                insert.Parameters.AddWithValue("target", targetPlayerId);
                insert.Parameters.AddWithValue("deployment", targetDeploymentId);
                insert.Parameters.AddWithValue("snapshot", targetSnapshotId);
                insert.Parameters.AddWithValue("expires", expiresAt);
                await insert.ExecuteNonQueryAsync(cancellationToken);

                return new ApiReply(StatusCodes.Status201Created,
                    new RaidRevengeTargetView(true, string.Empty, sourceRaidId.ToString("D"),
                        requestId.ToString("D"), targetDeploymentId.ToString("D"),
                        targetSnapshotId.ToString("D"), targetPlayerId.ToString("D"),
                        targetName, targetFaction, targetPower, expiresAt.ToString("O")));
            });
    }
}
