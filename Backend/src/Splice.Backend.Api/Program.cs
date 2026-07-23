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
        builder.Services.AddSingleton<IRaidReplayBlobStore, LocalFileRaidReplayBlobStore>();
        builder.Services.AddSingleton<IdempotencyExecutor>();
        builder.Services.AddSingleton<RaidReconciliationService>();
        builder.Services.AddHostedService<RaidReconciliationWorker>();

        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("C2 development bearer authentication cannot run outside Development.");

        app.UseMiddleware<RequestIdentityMiddleware>();
        app.MapGet("/health", async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return Results.Ok(new { status = "ok", database = "ok" });
        });
        app.MapWalletEndpoints();
        app.MapLoadoutEndpoints();
        app.MapRaidEndpoints();
        app.MapRaidAuthorityEndpoints();
        app.MapRaidHistoryEndpoints();
        app.MapTownEndpoints();
        return app;
    }
}

public partial class Program;
