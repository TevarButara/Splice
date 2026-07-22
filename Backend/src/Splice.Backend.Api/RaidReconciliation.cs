using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed class RaidReconciliationService(NpgsqlDataSource dataSource, IConfiguration configuration)
{
    public async Task<int> ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Max(1, configuration.GetValue("Reconciliation:TimeoutSeconds", 120));
        var activeTimeoutSeconds = Math.Max(timeoutSeconds,
            configuration.GetValue("Reconciliation:ActiveTimeoutSeconds", 1800));
        var batchSize = Math.Clamp(configuration.GetValue("Reconciliation:BatchSize", 50), 1, 200);
        var reconciled = 0;

        for (var index = 0; index < batchSize; index++)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var candidate = await FindCandidateAsync(connection, transaction, timeoutSeconds,
                activeTimeoutSeconds, cancellationToken);
            if (candidate is null)
            {
                await transaction.CommitAsync(cancellationToken);
                break;
            }

            var playerAccountId = await PlayerAccountAsync(connection, transaction,
                candidate.Value.AttackerId, cancellationToken);
            var postingItems = new List<Dictionary<string, object>>
            {
                new() { ["account_id"] = candidate.Value.EscrowAccountId,
                    ["amount"] = -(candidate.Value.Amount + candidate.Value.DefenderReserve) },
                new() { ["account_id"] = playerAccountId, ["amount"] = candidate.Value.Amount },
            };
            if (candidate.Value.DefenderReserve > 0 && candidate.Value.DefenderAccountId is not null)
                postingItems.Add(new()
                {
                    ["account_id"] = candidate.Value.DefenderAccountId.Value,
                    ["amount"] = candidate.Value.DefenderReserve,
                });
            var postings = JsonSerializer.Serialize(postingItems);
            var refundTransactionId = await PostRefundAsync(connection, transaction,
                candidate.Value.RaidId, candidate.Value.RaidState, postings, cancellationToken);

            await using var update = new NpgsqlCommand("""
                UPDATE splice.raid_sessions
                   SET state = 'REFUNDED', completed_at = clock_timestamp()
                 WHERE id = @raid AND state = @state;
                UPDATE splice.raid_escrows
                   SET state = 'REFUNDED', refunded_transaction_id = @transaction,
                       settled_at = clock_timestamp()
                 WHERE id = @escrow AND state IN ('FUNDED','ACTIVE');
                UPDATE splice.raid_allocations
                   SET state='EXPIRED', completed_at=clock_timestamp()
                 WHERE raid_id=@raid AND state IN ('ALLOCATED','CLAIMED')
                """, connection, transaction);
            update.Parameters.AddWithValue("raid", candidate.Value.RaidId);
            update.Parameters.AddWithValue("escrow", candidate.Value.EscrowId);
            update.Parameters.AddWithValue("transaction", refundTransactionId);
            update.Parameters.AddWithValue("state", candidate.Value.RaidState);
            await update.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            reconciled++;
        }

        return reconciled;
    }

    private static async Task<(Guid RaidId, Guid AttackerId, Guid EscrowId,
        Guid EscrowAccountId, long Amount, long DefenderReserve, Guid? DefenderAccountId,
        string RaidState)?> FindCandidateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int timeoutSeconds,
        int activeTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT r.id, r.attacker_player_id, e.id, e.ledger_account_id, e.funded_amount,
                   e.defender_reserved_amount, te.ledger_account_id, r.state
              FROM splice.raid_sessions r
              JOIN splice.raid_escrows e ON e.raid_id = r.id
              LEFT JOIN splice.town_escrows te ON te.id = e.defender_town_escrow_id
             WHERE ((r.state = 'FUNDED' AND r.started_at IS NULL AND e.state = 'FUNDED'
                       AND r.created_at < clock_timestamp() - make_interval(secs => @timeout))
                 OR (r.state = 'ACTIVE' AND e.state = 'ACTIVE' AND r.started_at IS NOT NULL
                       AND r.started_at < clock_timestamp() - make_interval(secs => @activeTimeout)))
             ORDER BY r.created_at
             FOR UPDATE OF r, e SKIP LOCKED
             LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue("timeout", timeoutSeconds);
        command.Parameters.AddWithValue("activeTimeout", activeTimeoutSeconds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
            reader.GetInt64(4), reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.GetString(7));
    }

    private static async Task<Guid> PlayerAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid playerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id FROM splice.ledger_accounts
             WHERE owner_type = 'PLAYER' AND owner_id = @player AND currency_code = 'WAR_GEM'
             FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("player", playerId);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<Guid> PostRefundAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid raidId, string raidState, string postings,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT splice.post_ledger_transaction(
                @key, @type, 'RAID', @raid, @postings, '{"source":"reconciliation"}'::jsonb)
            """, connection, transaction);
        var startup = raidState == "FUNDED";
        command.Parameters.AddWithValue("key", startup
            ? $"raid:{raidId:D}:startup_refund"
            : $"raid:{raidId:D}:infrastructure_refund");
        command.Parameters.AddWithValue("type", startup ? "RAID_STARTUP_REFUND" : "RAID_INFRA_REFUND");
        command.Parameters.AddWithValue("raid", raidId);
        command.Parameters.AddWithValue("postings", NpgsqlDbType.Jsonb, postings);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

public sealed class RaidReconciliationWorker(
    RaidReconciliationService reconciliation,
    IConfiguration configuration,
    ILogger<RaidReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Reconciliation:Enabled", true)) return;
        var intervalSeconds = Math.Max(1, configuration.GetValue("Reconciliation:IntervalSeconds", 30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = await reconciliation.ReconcileOnceAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Reconciled {Count} abandoned raid escrows.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Raid escrow reconciliation failed; the next interval will retry.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
