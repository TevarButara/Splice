using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

var baseUrl = Required("SPLICE_LOAD_BASE_URL").TrimEnd('/');
var endpoint = new Uri(baseUrl, UriKind.Absolute);
var allowRemote = string.Equals(Environment.GetEnvironmentVariable("SPLICE_LOAD_ALLOW_REMOTE"),
    "true", StringComparison.OrdinalIgnoreCase);
if (!endpoint.IsLoopback && !allowRemote)
    throw new InvalidOperationException(
        "Load harness is local-only unless SPLICE_LOAD_ALLOW_REMOTE=true is explicitly set.");

var playerId = Required("SPLICE_LOAD_PLAYER_ID");
if (!Guid.TryParse(playerId, out _)) throw new ArgumentException("SPLICE_LOAD_PLAYER_ID must be a UUID.");
var serverId = Required("SPLICE_LOAD_RAID_SERVER_ID");
var serverKey = Required("SPLICE_LOAD_RAID_SERVER_KEY");
var requestCount = Integer("SPLICE_LOAD_REQUESTS", 240, 30, 100000);
var concurrency = Integer("SPLICE_LOAD_CONCURRENCY", 16, 1, 512);
var minimumRequestsPerSecond = Integer("SPLICE_LOAD_MIN_RPS", 20, 1, 100000);

using var client = new HttpClient
{
    BaseAddress = endpoint,
    Timeout = TimeSpan.FromSeconds(10),
};

for (var index = 0; index < 12; index++)
{
    using var warmup = await CreateRequestAsync(index, playerId, serverId, serverKey);
    using var response = await client.SendAsync(warmup);
    response.EnsureSuccessStatusCode();
}

var samples = new ConcurrentBag<Sample>();
var failures = new ConcurrentBag<string>();
var wallClock = Stopwatch.StartNew();
await Parallel.ForEachAsync(Enumerable.Range(0, requestCount),
    new ParallelOptions { MaxDegreeOfParallelism = concurrency },
    async (index, cancellationToken) =>
    {
        var started = Stopwatch.GetTimestamp();
        var operation = Operation(index);
        try
        {
            using var request = await CreateRequestAsync(index, playerId, serverId, serverKey);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                failures.Add($"{operation}:{(int)response.StatusCode}:{ErrorCode(body)}");
            }
        }
        catch (Exception exception)
        {
            failures.Add($"{operation}:{exception.GetType().Name}");
        }
        finally
        {
            samples.Add(new Sample(operation, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        }
    });
wallClock.Stop();

var operationBudgets = new Dictionary<string, double>(StringComparer.Ordinal)
{
    ["health"] = 200,
    ["wallet"] = 300,
    ["empty-worker-claim"] = 600,
};
var reports = samples.GroupBy(sample => sample.Operation, StringComparer.Ordinal)
    .OrderBy(group => group.Key, StringComparer.Ordinal)
    .Select(group =>
    {
        var ordered = group.Select(sample => sample.Milliseconds).Order().ToArray();
        return new OperationReport(group.Key, ordered.Length, Percentile(ordered, 0.50),
            Percentile(ordered, 0.95), Percentile(ordered, 0.99), ordered[^1],
            operationBudgets[group.Key]);
    }).ToArray();
var requestsPerSecond = requestCount / Math.Max(0.001, wallClock.Elapsed.TotalSeconds);
var passed = failures.IsEmpty && requestsPerSecond >= minimumRequestsPerSecond &&
             reports.All(report => report.P95Milliseconds <= report.P95BudgetMilliseconds);
var report = new LoadReport(passed, requestCount, concurrency, wallClock.Elapsed.TotalMilliseconds,
    requestsPerSecond, minimumRequestsPerSecond, failures.Count, reports,
    failures.Take(10).Order(StringComparer.Ordinal).ToArray());
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return passed ? 0 : 1;

static string Operation(int index) => (index % 5) switch
{
    0 => "health",
    4 => "empty-worker-claim",
    _ => "wallet",
};

static Task<HttpRequestMessage> CreateRequestAsync(int index, string playerId,
    string serverId, string serverKey)
{
    var operation = Operation(index);
    if (operation == "health")
        return Task.FromResult(new HttpRequestMessage(HttpMethod.Get, "/health"));
    if (operation == "wallet")
    {
        var wallet = new HttpRequestMessage(HttpMethod.Get, "/v1/wallet");
        wallet.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "dev:" + playerId);
        return Task.FromResult(wallet);
    }

    var claim = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/raid-jobs/claim");
    claim.Headers.Add("X-Raid-Server-Id", serverId);
    claim.Headers.Add("X-Raid-Server-Key", serverKey);
    claim.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("D"));
    claim.Headers.Add("Idempotency-Key", "load-worker-claim-" + Guid.NewGuid().ToString("N"));
    claim.Content = JsonContent.Create(new { workerId = "load-worker-" + (index % 32) });
    return Task.FromResult(claim);
}

static double Percentile(double[] ordered, double percentile)
{
    if (ordered.Length == 0) return 0;
    var index = Math.Clamp((int)Math.Ceiling(ordered.Length * percentile) - 1, 0, ordered.Length - 1);
    return Math.Round(ordered[index], 3);
}

static string ErrorCode(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("error").GetProperty("code").GetString() ?? "UNKNOWN";
    }
    catch
    {
        return "UNPARSEABLE";
    }
}

static string Required(string name) =>
    Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(name + " is required.");

static int Integer(string name, int fallback, int minimum, int maximum)
{
    var raw = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(raw)) return fallback;
    if (!int.TryParse(raw, out var value) || value < minimum || value > maximum)
        throw new ArgumentOutOfRangeException(name, $"{name} must be between {minimum} and {maximum}.");
    return value;
}

internal sealed record Sample(string Operation, double Milliseconds);
internal sealed record OperationReport(string Operation, int Count, double P50Milliseconds,
    double P95Milliseconds, double P99Milliseconds, double MaximumMilliseconds,
    double P95BudgetMilliseconds);
internal sealed record LoadReport(bool Passed, int Requests, int Concurrency,
    double DurationMilliseconds, double RequestsPerSecond, int MinimumRequestsPerSecond,
    int Failures, OperationReport[] Operations, string[] FailureSamples);
