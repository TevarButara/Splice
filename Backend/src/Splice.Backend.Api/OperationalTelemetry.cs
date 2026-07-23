using System.Diagnostics;
using System.Diagnostics.Metrics;
using Npgsql;

namespace Splice.Backend.Api;

public sealed record OperationalMetricsSnapshot(
    long Requests, long ServerErrors, long ReplayBlobWrites,
    long ReplayBlobReads, long ReplayBlobFailures,
    long ReconciledRaids, long OrphanBlobsDeleted,
    double LastRequestDurationMs, string LastBlobMaintenanceUtc);

public sealed class OperationalMetrics
{
    private static readonly Meter Meter = new("Splice.Backend", "1.0.0");
    private static readonly Counter<long> RequestCounter =
        Meter.CreateCounter<long>("splice.api.requests");
    private static readonly Counter<long> ServerErrorCounter =
        Meter.CreateCounter<long>("splice.api.server_errors");
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("splice.api.request.duration", "ms");
    private static readonly Counter<long> ReplayWriteCounter =
        Meter.CreateCounter<long>("splice.replay.blob.writes");
    private static readonly Counter<long> ReplayReadCounter =
        Meter.CreateCounter<long>("splice.replay.blob.reads");
    private static readonly Counter<long> ReplayFailureCounter =
        Meter.CreateCounter<long>("splice.replay.blob.failures");
    private static readonly Counter<long> ReconciliationCounter =
        Meter.CreateCounter<long>("splice.raid.reconciled");
    private static readonly Counter<long> OrphanDeletedCounter =
        Meter.CreateCounter<long>("splice.replay.orphans.deleted");

    private long requests;
    private long serverErrors;
    private long replayBlobWrites;
    private long replayBlobReads;
    private long replayBlobFailures;
    private long reconciledRaids;
    private long orphanBlobsDeleted;
    private long lastRequestDurationBits;
    private long lastBlobMaintenanceUnixMilliseconds;

    public void RecordRequest(string routeGroup, string method, int statusCode, double durationMs)
    {
        Interlocked.Increment(ref requests);
        Interlocked.Exchange(ref lastRequestDurationBits,
            BitConverter.DoubleToInt64Bits(durationMs));
        var tags = new TagList
        {
            { "route.group", routeGroup },
            { "http.method", method },
            { "http.status_code", statusCode },
        };
        RequestCounter.Add(1, tags);
        RequestDuration.Record(durationMs, tags);
        if (statusCode >= 500)
        {
            Interlocked.Increment(ref serverErrors);
            ServerErrorCounter.Add(1, tags);
        }
    }

    public void RecordReplayWrite() =>
        Record(ref replayBlobWrites, ReplayWriteCounter);

    public void RecordReplayRead() =>
        Record(ref replayBlobReads, ReplayReadCounter);

    public void RecordReplayFailure(string operation, string code)
    {
        Interlocked.Increment(ref replayBlobFailures);
        ReplayFailureCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error.code", code));
    }

    public void RecordReconciledRaids(int count)
    {
        if (count <= 0) return;
        Interlocked.Add(ref reconciledRaids, count);
        ReconciliationCounter.Add(count);
    }

    public void RecordBlobMaintenance(int deleted, DateTimeOffset completedAt)
    {
        if (deleted > 0)
        {
            Interlocked.Add(ref orphanBlobsDeleted, deleted);
            OrphanDeletedCounter.Add(deleted);
        }
        Interlocked.Exchange(ref lastBlobMaintenanceUnixMilliseconds,
            completedAt.ToUnixTimeMilliseconds());
    }

    public OperationalMetricsSnapshot Snapshot()
    {
        var maintenance = Interlocked.Read(ref lastBlobMaintenanceUnixMilliseconds);
        return new OperationalMetricsSnapshot(
            Interlocked.Read(ref requests),
            Interlocked.Read(ref serverErrors),
            Interlocked.Read(ref replayBlobWrites),
            Interlocked.Read(ref replayBlobReads),
            Interlocked.Read(ref replayBlobFailures),
            Interlocked.Read(ref reconciledRaids),
            Interlocked.Read(ref orphanBlobsDeleted),
            BitConverter.Int64BitsToDouble(Interlocked.Read(ref lastRequestDurationBits)),
            maintenance > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(maintenance).ToString("O")
                : string.Empty);
    }

    private static void Record(ref long value, Counter<long> counter)
    {
        Interlocked.Increment(ref value);
        counter.Add(1);
    }
}

public sealed class RequestTelemetryMiddleware(
    RequestDelegate next,
    OperationalMetrics metrics,
    ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var routeGroup = RouteGroup(context.Request.Path);
        Activity.Current?.SetTag("splice.route_group", routeGroup);
        try
        {
            await next(context);
        }
        finally
        {
            var durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            metrics.RecordRequest(routeGroup, context.Request.Method,
                context.Response.StatusCode, durationMs);
            Activity.Current?.SetTag("http.response.status_code",
                context.Response.StatusCode);
            if (context.Response.StatusCode >= 500)
                logger.LogWarning(
                    "Server error. request_id={RequestId} route_group={RouteGroup} status={Status} duration_ms={DurationMs:F2}",
                    context.TraceIdentifier, routeGroup,
                    context.Response.StatusCode, durationMs);
        }
    }

    private static string RouteGroup(PathString path) =>
        path.StartsWithSegments("/internal/v1") ? "internal" :
        path.StartsWithSegments("/v1") ? "player" :
        path.StartsWithSegments("/health") ? "health" : "other";
}

public sealed record OperationalQueueSnapshot(
    long FundedRaids, long AllocatedJobs, long ClaimedJobs,
    long ExpiredWorkerLeases, long StuckActiveRaids,
    long UnpublishedOutboxEvents, long OldestOutboxAgeSeconds);

public sealed record OperationalStatusView(
    string Status, IReadOnlyList<string> Alerts,
    OperationalQueueSnapshot Queue, OperationalMetricsSnapshot Metrics,
    string ObservedUtc);

public sealed class OperationalStatusService(
    NpgsqlDataSource dataSource,
    IConfiguration configuration,
    OperationalMetrics metrics)
{
    public async Task<OperationalStatusView> GetAsync(
        CancellationToken cancellationToken)
    {
        var activeTimeoutSeconds = Math.Max(60,
            configuration.GetValue("Ops:ActiveRaidWarningSeconds", 1800));
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM splice.raid_sessions WHERE state='FUNDED'),
              (SELECT count(*) FROM splice.raid_allocations allocation
                JOIN splice.raid_sessions raid ON raid.id=allocation.raid_id
               WHERE allocation.state='ALLOCATED' AND raid.state='FUNDED'
                 AND allocation.expires_at > clock_timestamp()),
              (SELECT count(*) FROM splice.raid_allocations allocation
                JOIN splice.raid_sessions raid ON raid.id=allocation.raid_id
               WHERE allocation.state='CLAIMED' AND raid.state='ACTIVE'
                 AND allocation.lease_expires_at >= clock_timestamp()),
              (SELECT count(*) FROM splice.raid_allocations allocation
                JOIN splice.raid_sessions raid ON raid.id=allocation.raid_id
               WHERE allocation.state='CLAIMED' AND raid.state='ACTIVE'
                 AND allocation.lease_expires_at < clock_timestamp()),
              (SELECT count(*) FROM splice.raid_sessions
               WHERE state='ACTIVE' AND started_at <
                     clock_timestamp()-make_interval(secs => @activeTimeout)),
              (SELECT count(*) FROM splice.outbox_events WHERE published_at IS NULL),
              (SELECT COALESCE(EXTRACT(EPOCH FROM
                    (clock_timestamp()-min(created_at)))::bigint,0)
                 FROM splice.outbox_events WHERE published_at IS NULL)
            """, connection);
        command.Parameters.AddWithValue("activeTimeout", activeTimeoutSeconds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var queue = new OperationalQueueSnapshot(
            reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5),
            reader.GetInt64(6));

        var alerts = new List<string>();
        if (queue.ExpiredWorkerLeases >
            Math.Max(0, configuration.GetValue("Ops:ExpiredLeaseWarningCount", 0)))
            alerts.Add("EXPIRED_WORKER_LEASES");
        if (queue.StuckActiveRaids > 0) alerts.Add("STUCK_ACTIVE_RAIDS");
        if (queue.UnpublishedOutboxEvents >
            Math.Max(1, configuration.GetValue("Ops:OutboxWarningCount", 10000)))
            alerts.Add("OUTBOX_BACKLOG");
        return new OperationalStatusView(
            alerts.Count == 0 ? "HEALTHY" : "DEGRADED",
            alerts, queue, metrics.Snapshot(), DateTimeOffset.UtcNow.ToString("O"));
    }
}

public static class OperationalEndpoints
{
    public static void MapOperationalEndpoints(this WebApplication app)
    {
        app.MapGet("/internal/v1/ops/status",
            async (OperationalStatusService service,
                    CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(cancellationToken)));
    }
}
