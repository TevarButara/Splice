using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Splice.Backend.Api;

public static class HealthAndMetricsEndpoints
{
    private const string PrometheusContentType = "text/plain; version=0.0.4; charset=utf-8";

    public static void MapHealthAndMetricsEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health/ready", ReadinessAsync);
        // Backwards-compatible alias for existing clients and local load tests.
        app.MapGet("/health", ReadinessAsync);
        app.MapGet("/metrics", MetricsAsync);
    }

    private static async Task<IResult> ReadinessAsync(
        NpgsqlDataSource dataSource,
        IRaidReplayBlobHealthCheck replayStorage,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            await replayStorage.ProbeWritableAsync(cancellationToken);
            return Results.Ok(new
            {
                status = "ok",
                database = "ok",
                replayStorage = "ok",
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("Splice.Readiness").LogWarning(
                "Readiness dependency probe failed. type={FailureType}",
                exception.GetType().Name);
            return Results.Json(new
            {
                status = "unavailable",
                database = "unknown",
                replayStorage = "unknown",
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> MetricsAsync(
        HttpContext context,
        IConfiguration configuration,
        OperationalStatusService statusService,
        CancellationToken cancellationToken)
    {
        var configuredToken = configuration["Ops:MetricsBearerToken"];
        if (string.IsNullOrWhiteSpace(configuredToken) || configuredToken.Length < 24)
            return Results.Text(
                "# metrics endpoint disabled: configure a bearer token of at least 24 characters\n",
                PrometheusContentType, Encoding.UTF8,
                StatusCodes.Status503ServiceUnavailable);

        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        const string prefix = "Bearer ";
        var suppliedToken = authorization is not null &&
                            authorization.StartsWith(prefix, StringComparison.Ordinal)
            ? authorization[prefix.Length..]
            : string.Empty;
        if (!SecretEquals(configuredToken, suppliedToken))
        {
            context.Response.Headers.WWWAuthenticate = "Bearer realm=\"splice-metrics\"";
            return Results.Text("# authentication required\n", PrometheusContentType,
                Encoding.UTF8, StatusCodes.Status401Unauthorized);
        }

        var status = await statusService.GetAsync(cancellationToken);
        return Results.Text(RenderPrometheus(status), PrometheusContentType, Encoding.UTF8);
    }

    private static bool SecretEquals(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public static string RenderPrometheus(OperationalStatusView status)
    {
        var metrics = status.Metrics;
        var queue = status.Queue;
        var maintenanceTimestamp = DateTimeOffset.TryParse(
            metrics.LastBlobMaintenanceUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var maintenance)
            ? maintenance.ToUnixTimeSeconds()
            : 0;
        var builder = new StringBuilder(2048);
        Append(builder, "splice_api_requests_total",
            "Total HTTP requests observed by this API process.", "counter", metrics.Requests);
        Append(builder, "splice_api_server_errors_total",
            "Total HTTP 5xx responses observed by this API process.", "counter", metrics.ServerErrors);
        Append(builder, "splice_replay_blob_writes_total",
            "Total successful replay blob writes.", "counter", metrics.ReplayBlobWrites);
        Append(builder, "splice_replay_blob_reads_total",
            "Total successful replay blob reads.", "counter", metrics.ReplayBlobReads);
        Append(builder, "splice_replay_blob_failures_total",
            "Total replay blob operation failures.", "counter", metrics.ReplayBlobFailures);
        Append(builder, "splice_raid_reconciled_total",
            "Total abandoned raids reconciled by this API process.", "counter", metrics.ReconciledRaids);
        Append(builder, "splice_replay_orphan_blobs_deleted_total",
            "Total orphan replay blobs deleted by this API process.", "counter", metrics.OrphanBlobsDeleted);
        Append(builder, "splice_api_last_request_duration_milliseconds",
            "Duration of the last completed HTTP request.", "gauge", metrics.LastRequestDurationMs);
        Append(builder, "splice_replay_last_maintenance_timestamp_seconds",
            "Unix timestamp of the last completed replay maintenance cycle, or zero.", "gauge",
            maintenanceTimestamp);
        Append(builder, "splice_raid_funded_queue",
            "Raids waiting in FUNDED state.", "gauge", queue.FundedRaids);
        Append(builder, "splice_raid_allocated_jobs",
            "Unexpired allocated raid jobs.", "gauge", queue.AllocatedJobs);
        Append(builder, "splice_raid_claimed_jobs",
            "Active raid jobs with a valid worker lease.", "gauge", queue.ClaimedJobs);
        Append(builder, "splice_raid_expired_worker_leases",
            "Active raid jobs with an expired worker lease.", "gauge", queue.ExpiredWorkerLeases);
        Append(builder, "splice_raid_stuck_active",
            "Active raids older than the configured warning threshold.", "gauge", queue.StuckActiveRaids);
        Append(builder, "splice_outbox_unpublished_events",
            "Outbox events not yet marked as published.", "gauge", queue.UnpublishedOutboxEvents);
        Append(builder, "splice_outbox_oldest_age_seconds",
            "Age of the oldest unpublished outbox event.", "gauge", queue.OldestOutboxAgeSeconds);
        Append(builder, "splice_ops_healthy",
            "One when operational status is healthy, otherwise zero.", "gauge",
            status.Status == "HEALTHY" ? 1 : 0);
        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder, string name, string help, string type, double value)
    {
        builder.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        builder.Append("# TYPE ").Append(name).Append(' ').AppendLine(type);
        builder.Append(name).Append(' ')
            .AppendLine(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
