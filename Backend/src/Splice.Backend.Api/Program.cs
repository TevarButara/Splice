using Npgsql;
using Splice.Backend.Api;

var app = SpliceApi.Build(args);
await app.RunAsync();

public static class SpliceApi
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("Splice")
            ?? throw new InvalidOperationException("ConnectionStrings:Splice is required.");

        builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
        builder.Services.AddSingleton<OperationalMetrics>();
        builder.Services.AddSingleton<OperationalStatusService>();
        builder.Services.AddSingleton<LocalFileRaidReplayBlobStore>();
        builder.Services.AddSingleton<IRaidReplayBlobStore>(services =>
            services.GetRequiredService<LocalFileRaidReplayBlobStore>());
        builder.Services.AddSingleton<IRaidReplayBlobMaintenance>(services =>
            services.GetRequiredService<LocalFileRaidReplayBlobStore>());
        builder.Services.AddSingleton<IRaidReplayBlobHealthCheck>(services =>
            services.GetRequiredService<LocalFileRaidReplayBlobStore>());
        builder.Services.AddSingleton<IdempotencyExecutor>();
        builder.Services.AddSingleton<RaidReconciliationService>();
        builder.Services.AddHostedService<RaidReconciliationWorker>();
        builder.Services.AddSingleton<ReplayBlobMaintenanceService>();
        builder.Services.AddHostedService<ReplayBlobMaintenanceWorker>();

        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("C2 development bearer authentication cannot run outside Development.");

        app.UseMiddleware<RequestTelemetryMiddleware>();
        app.UseMiddleware<RequestIdentityMiddleware>();
        app.MapHealthAndMetricsEndpoints();
        app.MapWalletEndpoints();
        app.MapLoadoutEndpoints();
        app.MapRaidEndpoints();
        app.MapRaidAuthorityEndpoints();
        app.MapRaidHistoryEndpoints();
        app.MapTownEndpoints();
        app.MapOperationalEndpoints();
        return app;
    }
}

public partial class Program;
