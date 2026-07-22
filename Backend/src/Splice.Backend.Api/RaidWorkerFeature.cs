using System.Text.Json;
using Npgsql;

namespace Splice.Backend.Api;

public sealed record ClaimRaidJobRequest(string WorkerId);
public sealed record RaidJobView(bool HasJob, string Error, string RaidId, string AllocationId,
    string WorkerId, string TargetSnapshotId, string LoadoutSnapshotId, string SceneContractVersion,
    long AttackerPower, long DefenderPower, JsonElement TargetSnapshot, JsonElement LoadoutEntries,
    string LeaseExpiresUtc);
public sealed record HeartbeatRaidJobRequest(string WorkerId);
public sealed record RaidJobHeartbeatView(bool Success, string Error, string AllocationId,
    string LeaseExpiresUtc);

public static partial class RaidAuthorityFeature
{
    private static void MapRaidWorkerEndpoints(WebApplication app)
    {
        app.MapPost("/internal/v1/raid-jobs/claim", ClaimJobAsync);
        app.MapPost("/internal/v1/raid-jobs/{allocationId:guid}/heartbeat", HeartbeatJobAsync);
    }

    private static async Task<IResult> ClaimJobAsync(HttpContext context, ClaimRaidJobRequest request,
        IdempotencyExecutor idempotency, IConfiguration configuration)
    {
        var serverId = RequestIdentityMiddleware.RaidServerId(context);
        return await idempotency.ExecuteAsync(context, TrustedActorId, request,
            async (connection, transaction, cancellationToken) =>
            {
                var workerId = NormalizeWorker(request.WorkerId);
                if (workerId is null)
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "WORKER_ID_INVALID", "Worker ID must contain 1-80 safe characters.");

                await using var query = new NpgsqlCommand("""
                    SELECT a.id, a.raid_id, r.target_snapshot_id, q.attacker_loadout_snapshot_id,
                           r.scene_contract_version, s.payload::text, s.base_power,
                           ls.entries::text, ls.raid_power
                      FROM splice.raid_allocations a
                      JOIN splice.raid_sessions r ON r.id=a.raid_id
                      JOIN splice.raid_quotes q ON q.id=r.quote_id
                      JOIN splice.town_snapshots s ON s.id=r.target_snapshot_id
                      JOIN splice.attacker_loadout_snapshots ls ON ls.id=q.attacker_loadout_snapshot_id
                     WHERE a.raid_server_id=@server
                       AND ((a.state='ALLOCATED' AND a.expires_at > clock_timestamp() AND r.state='FUNDED')
                         OR (a.state='CLAIMED' AND a.lease_expires_at < clock_timestamp()
                             AND r.state='ACTIVE'))
                     ORDER BY a.created_at
                     FOR UPDATE OF a, r SKIP LOCKED
                     LIMIT 1
                    """, connection, transaction);
                query.Parameters.AddWithValue("server", serverId);
                await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    var empty = JsonDocument.Parse("{}").RootElement.Clone();
                    return new ApiReply(StatusCodes.Status200OK,
                        new RaidJobView(false, string.Empty, string.Empty, string.Empty, workerId,
                            string.Empty, string.Empty, string.Empty, 0, 0, empty, empty, string.Empty));
                }

                var allocationId = reader.GetGuid(0);
                var raidId = reader.GetGuid(1);
                var targetSnapshotId = reader.GetGuid(2);
                var loadoutSnapshotId = reader.GetGuid(3);
                var sceneContract = reader.GetString(4);
                var target = JsonDocument.Parse(reader.GetString(5)).RootElement.Clone();
                var defenderPower = reader.GetInt64(6);
                var entries = JsonDocument.Parse(reader.GetString(7)).RootElement.Clone();
                var attackerPower = reader.GetInt64(8);
                await reader.DisposeAsync();

                var leaseSeconds = Math.Clamp(configuration.GetValue("RaidServer:WorkerLeaseSeconds", 90), 30, 300);
                var now = DateTimeOffset.UtcNow;
                var lease = now.AddSeconds(leaseSeconds);
                await using var claim = new NpgsqlCommand("""
                    UPDATE splice.raid_allocations
                       SET state='CLAIMED', worker_id=@worker, claimed_at=COALESCE(claimed_at,@now),
                           heartbeat_at=@now, lease_expires_at=@lease, claim_attempt=claim_attempt+1
                     WHERE id=@allocation;
                    UPDATE splice.raid_sessions
                       SET state='ACTIVE', started_at=COALESCE(started_at,@now)
                     WHERE id=@raid;
                    UPDATE splice.raid_escrows SET state='ACTIVE'
                     WHERE raid_id=@raid AND state='FUNDED'
                    """, connection, transaction);
                claim.Parameters.AddWithValue("worker", workerId);
                claim.Parameters.AddWithValue("now", now);
                claim.Parameters.AddWithValue("lease", lease);
                claim.Parameters.AddWithValue("allocation", allocationId);
                claim.Parameters.AddWithValue("raid", raidId);
                await claim.ExecuteNonQueryAsync(cancellationToken);

                return new ApiReply(StatusCodes.Status200OK,
                    new RaidJobView(true, string.Empty, raidId.ToString("D"), allocationId.ToString("D"),
                        workerId, targetSnapshotId.ToString("D"), loadoutSnapshotId.ToString("D"),
                        sceneContract, attackerPower, defenderPower, target, entries, lease.ToString("O")));
            });
    }

    private static async Task<IResult> HeartbeatJobAsync(HttpContext context, Guid allocationId,
        HeartbeatRaidJobRequest request, IdempotencyExecutor idempotency, IConfiguration configuration)
    {
        var serverId = RequestIdentityMiddleware.RaidServerId(context);
        return await idempotency.ExecuteAsync(context, TrustedActorId, request,
            async (connection, transaction, cancellationToken) =>
            {
                var workerId = NormalizeWorker(request.WorkerId);
                if (workerId is null)
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "WORKER_ID_INVALID", "Worker ID is invalid.");
                var leaseSeconds = Math.Clamp(configuration.GetValue("RaidServer:WorkerLeaseSeconds", 90), 30, 300);
                var now = DateTimeOffset.UtcNow;
                var lease = now.AddSeconds(leaseSeconds);
                await using var heartbeat = new NpgsqlCommand("""
                    UPDATE splice.raid_allocations
                       SET heartbeat_at=@now, lease_expires_at=@lease
                     WHERE id=@allocation AND raid_server_id=@server AND worker_id=@worker
                       AND state='CLAIMED' AND lease_expires_at >= clock_timestamp()
                    """, connection, transaction);
                heartbeat.Parameters.AddWithValue("now", now);
                heartbeat.Parameters.AddWithValue("lease", lease);
                heartbeat.Parameters.AddWithValue("allocation", allocationId);
                heartbeat.Parameters.AddWithValue("server", serverId);
                heartbeat.Parameters.AddWithValue("worker", workerId);
                if (await heartbeat.ExecuteNonQueryAsync(cancellationToken) != 1)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "RAID_LEASE_LOST", "Worker no longer owns an active lease for this raid.");
                return new ApiReply(StatusCodes.Status200OK,
                    new RaidJobHeartbeatView(true, string.Empty, allocationId.ToString("D"), lease.ToString("O")));
            });
    }

    private static string? NormalizeWorker(string? workerId)
    {
        var value = workerId?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80) return null;
        return value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            ? value : null;
    }
}
