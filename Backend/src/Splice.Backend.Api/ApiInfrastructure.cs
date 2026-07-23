using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed record ApiReply(int StatusCode, object Body);
public sealed record ErrorEnvelope(ErrorBody Error);
public sealed record ErrorBody(string Code, string Message, string RequestId, bool Retryable);

public static class ApiErrors
{
    public static ApiReply Reply(HttpContext context, int status, string code, string message, bool retryable = false) =>
        new(status, new ErrorEnvelope(new ErrorBody(code, message, context.TraceIdentifier, retryable)));

    public static Task WriteAsync(HttpContext context, int status, string code, string message, bool retryable = false)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(
            new ErrorEnvelope(new ErrorBody(code, message, context.TraceIdentifier, retryable)));
    }
}

public sealed class RequestIdentityMiddleware(RequestDelegate next, IConfiguration configuration,
    ILogger<RequestIdentityMiddleware> logger)
{
    public const string PlayerItem = "Splice.PlayerId";
    public const string RaidServerItem = "Splice.RaidServerId";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        context.TraceIdentifier = Guid.TryParse(requestId, out var parsedRequestId)
            ? parsedRequestId.ToString("D")
            : Guid.NewGuid().ToString("D");
        context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;

        if (context.Request.Path.StartsWithSegments("/internal/v1"))
        {
            var configuredKey = configuration["RaidServer:DevelopmentKey"];
            var providedKey = context.Request.Headers["X-Raid-Server-Key"].FirstOrDefault();
            var serverId = context.Request.Headers["X-Raid-Server-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                await ApiErrors.WriteAsync(context, StatusCodes.Status503ServiceUnavailable,
                    "TRUSTED_RAID_SERVER_DISABLED", "Trusted Raid Server access is not configured.");
                return;
            }
            if (string.IsNullOrWhiteSpace(providedKey) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(configuredKey), Encoding.UTF8.GetBytes(providedKey)) ||
                string.IsNullOrWhiteSpace(serverId) || serverId.Length > 80)
            {
                await ApiErrors.WriteAsync(context, StatusCodes.Status401Unauthorized,
                    "TRUSTED_RAID_SERVER_AUTH_REQUIRED", "A trusted Raid Server identity is required.");
                return;
            }
            context.Items[RaidServerItem] = serverId.Trim();
        }
        else if (context.Request.Path.StartsWithSegments("/v1"))
        {
            var authorization = context.Request.Headers.Authorization.FirstOrDefault();
            const string prefix = "Bearer dev:";
            if (authorization is null || !authorization.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParse(authorization[prefix.Length..], out var playerId))
            {
                await ApiErrors.WriteAsync(context, StatusCodes.Status401Unauthorized,
                    "AUTH_REQUIRED", "A valid local development bearer identity is required.");
                return;
            }
            context.Items[PlayerItem] = playerId;
        }

        try
        {
            await next(context);
        }
        catch (NpgsqlException exception) when (!context.Response.HasStarted)
        {
            logger.LogWarning("Database request failed. request_id={RequestId} path={Path} sql_state={SqlState}",
                context.TraceIdentifier, context.Request.Path, exception.SqlState);
            await ApiErrors.WriteAsync(context, StatusCodes.Status503ServiceUnavailable,
                "DATABASE_UNAVAILABLE", "The database is temporarily unavailable.", true);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception,
                "Unhandled API request failure. request_id={RequestId} path={Path}",
                context.TraceIdentifier, context.Request.Path);
            await ApiErrors.WriteAsync(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", "The server could not complete the request.");
        }
    }

    public static Guid PlayerId(HttpContext context) =>
        context.Items.TryGetValue(PlayerItem, out var value) && value is Guid playerId
            ? playerId
            : throw new InvalidOperationException("Authenticated player identity is missing.");

    public static string RaidServerId(HttpContext context) =>
        context.Items.TryGetValue(RaidServerItem, out var value) && value is string serverId
            ? serverId
            : throw new InvalidOperationException("Trusted Raid Server identity is missing.");
}

public sealed class IdempotencyExecutor(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumTransactionAttempts = 5;

    public async Task<IResult> ExecuteAsync<TRequest>(HttpContext context, Guid actorId, TRequest request,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<ApiReply>> operation,
        IsolationLevel isolationLevel = IsolationLevel.Serializable)
    {
        var validation = ValidateRequest(context);
        if (validation is not null) return ToResult(validation);
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault()!;

        var scope = $"{actorId:D}:{context.Request.Method}:{context.Request.Path}";
        var canonicalRequest = JsonSerializer.Serialize(request, JsonOptions);
        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));

        for (var attempt = 1; attempt <= MaximumTransactionAttempts; attempt++)
        {
            await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
            await using var transaction = await connection.BeginTransactionAsync(
                isolationLevel, context.RequestAborted);
            try
            {
                await AdvisoryLockAsync(connection, transaction, scope + ":" + key, context.RequestAborted);
                var replay = await ReadExistingAsync(connection, transaction, scope, key, context.RequestAborted);
                if (replay is not null)
                {
                    await transaction.CommitAsync(context.RequestAborted);
                    if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.ASCII.GetBytes(replay.Value.Hash), Encoding.ASCII.GetBytes(requestHash)))
                        return ToResult(ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                            "IDEMPOTENCY_KEY_REUSED", "Idempotency key was already used with another request."));
                    return Results.Text(replay.Value.Json, "application/json", Encoding.UTF8, replay.Value.Status);
                }

                var reply = await operation(connection, transaction, context.RequestAborted);
                var responseJson = JsonSerializer.Serialize(reply.Body, JsonOptions);
                await StoreAsync(connection, transaction, scope, key, requestHash,
                    reply.StatusCode, responseJson, context.RequestAborted);
                await transaction.CommitAsync(context.RequestAborted);
                return Results.Text(responseJson, "application/json", Encoding.UTF8, reply.StatusCode);
            }
            catch (PostgresException exception) when (
                (exception.SqlState == PostgresErrorCodes.SerializationFailure ||
                 exception.SqlState == PostgresErrorCodes.DeadlockDetected) &&
                attempt < MaximumTransactionAttempts)
            {
                // PostgreSQL may already mark the transaction completed after an abort.
                // await using disposes/rolls back when still active; a second explicit rollback can itself throw.
                await RollbackIfActiveAsync(transaction);
                var delayMilliseconds = Math.Min(100, (5 << (attempt - 1)) + Random.Shared.Next(0, 11));
                await Task.Delay(delayMilliseconds, context.RequestAborted);
            }
            catch (PostgresException exception)
            {
                return ToResult(MapPostgres(context, exception));
            }
        }

        return ToResult(ApiErrors.Reply(context, StatusCodes.Status503ServiceUnavailable,
            "SERIALIZATION_RETRY_REQUIRED", "The transaction must be retried.", true));
    }

    public static ApiReply? ValidateRequest(HttpContext context)
    {
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key is required.");
        return key.Length > 160
            ? ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                "IDEMPOTENCY_KEY_INVALID", "Idempotency-Key is too long.")
            : null;
    }

    private static async Task RollbackIfActiveAsync(NpgsqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // An aborted transaction can already be completed by Npgsql; disposal is then sufficient.
        }
    }

    public static IResult ToResult(ApiReply reply) => Results.Json(reply.Body, statusCode: reply.StatusCode);

    private static ApiReply MapPostgres(HttpContext context, PostgresException exception)
    {
        if (exception.MessageText.StartsWith("INSUFFICIENT_FUNDS", StringComparison.Ordinal))
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "INSUFFICIENT_FUNDS", "The wallet does not contain enough currency.");
        if (exception.MessageText.StartsWith("LEDGER_ACCOUNT_NOT_FOUND", StringComparison.Ordinal))
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "WALLET_NOT_FOUND", "The required server wallet account does not exist.");
        if (exception.MessageText.StartsWith("STAKE_POLICY_MIGRATION_REQUIRED", StringComparison.Ordinal))
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "STAKE_POLICY_MIGRATION_REQUIRED", "Town escrow must be migrated before redeployment.");
        if (exception.ConstraintName == "one_open_raid_per_attacker_idx")
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "PENDING_RAID_EXISTS", "Another funded raid is still open.");
        if (exception.MessageText.StartsWith("IDEMPOTENCY_KEY_REUSED", StringComparison.Ordinal))
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "IDEMPOTENCY_KEY_REUSED", "Idempotency key was already used with another request.");
        if (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            return ApiErrors.Reply(context, StatusCodes.Status503ServiceUnavailable,
                "SERIALIZATION_RETRY_REQUIRED", "The transaction must be retried.", true);
        return ApiErrors.Reply(context, StatusCodes.Status500InternalServerError,
            "DATABASE_ERROR", "The database rejected the operation.", true);
    }

    private static async Task AdvisoryLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string value, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@value, 0))", connection, transaction);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string Hash, int Status, string Json)?> ReadExistingAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string scope, string key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT request_hash, response_status, response_body::text
              FROM splice.idempotency_requests
             WHERE scope = @scope AND idempotency_key = @key
            """, connection, transaction);
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (reader.GetString(0), reader.GetInt32(1), reader.GetString(2));
    }

    private static async Task StoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string scope, string key, string hash, int status, string json, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO splice.idempotency_requests
                (scope, idempotency_key, request_hash, response_status, response_body, expires_at)
            VALUES (@scope, @key, @hash, @status, @body, clock_timestamp() + interval '24 hours')
            """, connection, transaction);
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("body", NpgsqlDbType.Jsonb, json);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
