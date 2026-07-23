using Npgsql;

namespace Splice.Backend.Api;

public sealed record ReplayBlobMaintenanceResult(
    int Candidates, int Referenced, int Deleted,
    int ChangedDuringScan, int TemporaryDeleted, string CompletedUtc);

public sealed class ReplayBlobMaintenanceService(
    NpgsqlDataSource dataSource,
    IRaidReplayBlobMaintenance maintenance,
    IConfiguration configuration,
    OperationalMetrics metrics)
{
    public async Task<ReplayBlobMaintenanceResult> RunOnceAsync(
        CancellationToken cancellationToken)
    {
        // A newly written immutable blob is intentionally older than its DB pointer by a few
        // milliseconds. The grace period prevents cleanup from racing that transaction.
        var graceSeconds = Math.Max(60,
            configuration.GetValue("ReplayStorage:OrphanGraceSeconds", 3600));
        var batchSize = Math.Clamp(
            configuration.GetValue("ReplayStorage:MaintenanceBatchSize", 250),
            1, 1000);
        var olderThan = DateTimeOffset.UtcNow.AddSeconds(-graceSeconds);
        var candidates = await maintenance.ListCandidatesAsync(
            olderThan, batchSize, cancellationToken);
        var referenced = 0;
        var deleted = 0;
        var changed = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsReferencedAsync(candidate.Key, cancellationToken))
            {
                referenced++;
                continue;
            }

            // DeleteIfUnchanged rechecks length and mtime after the DB lookup. A concurrent
            // rewrite therefore survives this pass and is reconsidered only after the grace.
            if (await maintenance.DeleteIfUnchangedAsync(candidate, cancellationToken))
                deleted++;
            else
                changed++;
        }

        var temporaryDeleted = await maintenance.DeleteStaleTemporaryFilesAsync(
            olderThan, batchSize, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;
        metrics.RecordBlobMaintenance(deleted, completedAt);
        return new ReplayBlobMaintenanceResult(
            candidates.Count, referenced, deleted, changed,
            temporaryDeleted, completedAt.ToString("O"));
    }

    private async Task<bool> IsReferencedAsync(
        string key, CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1
                  FROM splice.raid_replays
                 WHERE storage_provider = @provider
                   AND storage_key = @key)
            """, connection);
        command.Parameters.AddWithValue("provider", maintenance.Provider);
        command.Parameters.AddWithValue("key", key);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

public sealed class ReplayBlobMaintenanceWorker(
    ReplayBlobMaintenanceService maintenance,
    IConfiguration configuration,
    ILogger<ReplayBlobMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Opt-in because a local directory must never be swept by a process pointed at
        // another database (for example an integration-test database).
        if (!configuration.GetValue(
                "ReplayStorage:MaintenanceEnabled", false))
            return;
        var intervalSeconds = Math.Max(60,
            configuration.GetValue(
                "ReplayStorage:MaintenanceIntervalSeconds", 900));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await maintenance.RunOnceAsync(stoppingToken);
                if (result.Deleted > 0 || result.TemporaryDeleted > 0)
                    logger.LogInformation(
                        "Replay blob maintenance deleted {OrphanCount} orphan blobs and {TemporaryCount} temporary files.",
                        result.Deleted, result.TemporaryDeleted);
            }
            catch (OperationCanceledException) when (
                stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Replay blob maintenance failed; the next interval will retry.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
