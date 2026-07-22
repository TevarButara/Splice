using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed record AttackerLoadoutEntryRequest(string CardId, int Count);
public sealed record PutAttackerLoadoutRequest(string FactionId, string HeroId,
    IReadOnlyList<AttackerLoadoutEntryRequest>? Entries);
public sealed record AttackerLoadoutView(bool Success, string Error, string LoadoutId,
    long Revision, string FactionId, string HeroId, IReadOnlyList<AttackerLoadoutEntryRequest> Entries,
    long RaidPower, string ContentVersion, string PayloadSha256, string UpdatedUtc);

public static class LoadoutFeature
{
    public const string ContentVersion = "content-c4b-v1";
    public const string ValidatorVersion = "server-loadout-c4b-v1";
    private const int MaxUniqueCards = 20;
    private const int MaxUnits = 50;
    private static readonly Regex IdentityPattern = new("^[A-Za-z0-9][A-Za-z0-9._/-]{0,79}$",
        RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapLoadoutEndpoints(this WebApplication app) =>
        app.MapPut("/v1/attacker-loadouts/{loadoutId:guid}", PutAsync);

    private static async Task<IResult> PutAsync(HttpContext context, Guid loadoutId,
        PutAttackerLoadoutRequest request, IdempotencyExecutor idempotency)
    {
        var playerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, playerId, request,
            async (connection, transaction, cancellationToken) =>
            {
                var normalized = Normalize(request, out var validationError);
                if (validationError is not null)
                    return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                        "LOADOUT_INVALID", validationError);

                var ids = normalized!.Entries!.Select(entry => entry.CardId).ToArray();
                var definitions = new Dictionary<string, (string Faction, int Power, string Version)>(
                    StringComparer.Ordinal);
                await using (var content = new NpgsqlCommand("""
                    SELECT content_id, faction_id, raid_power, content_version
                      FROM splice.content_definitions
                     WHERE enabled AND content_kind='GARRISON' AND content_id = ANY(@ids)
                    """, connection, transaction))
                {
                    content.Parameters.AddWithValue("ids", ids);
                    await using var reader = await content.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                        definitions[reader.GetString(0)] = (reader.GetString(1), reader.GetInt32(2), reader.GetString(3));
                }

                long raidPower = 0;
                foreach (var entry in normalized.Entries!)
                {
                    if (!definitions.TryGetValue(entry.CardId, out var definition) ||
                        definition.Faction != normalized.FactionId || definition.Power <= 0)
                        return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                            "LOADOUT_CONTENT_INVALID", $"Unknown, disabled, or mismatched army card: {entry.CardId}");
                    if (definition.Version != ContentVersion)
                        return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                            "LOADOUT_CONTENT_STALE", $"Army card content is stale: {entry.CardId}");
                    raidPower = checked(raidPower + (long)definition.Power * entry.Count);
                }

                var payload = JsonSerializer.Serialize(new
                {
                    factionId = normalized.FactionId,
                    heroId = normalized.HeroId,
                    entries = normalized.Entries,
                }, JsonOptions);
                var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

                await using var write = new NpgsqlCommand("""
                    INSERT INTO splice.attacker_loadouts
                        (id, owner_player_id, faction_id, revision, hero_id, entries,
                         payload_sha256, raid_power, content_version, updated_at)
                    VALUES (@id, @owner, @faction, 1, @hero, @entries, @hash, @power, @version,
                            clock_timestamp())
                    ON CONFLICT (id) DO UPDATE SET
                        faction_id=EXCLUDED.faction_id,
                        revision=splice.attacker_loadouts.revision + 1,
                        hero_id=EXCLUDED.hero_id,
                        entries=EXCLUDED.entries,
                        payload_sha256=EXCLUDED.payload_sha256,
                        raid_power=EXCLUDED.raid_power,
                        content_version=EXCLUDED.content_version,
                        updated_at=clock_timestamp()
                    WHERE splice.attacker_loadouts.owner_player_id = @owner
                    RETURNING revision, updated_at
                    """, connection, transaction);
                write.Parameters.AddWithValue("id", loadoutId);
                write.Parameters.AddWithValue("owner", playerId);
                write.Parameters.AddWithValue("faction", normalized.FactionId);
                write.Parameters.AddWithValue("hero", normalized.HeroId);
                write.Parameters.AddWithValue("entries", NpgsqlDbType.Jsonb,
                    JsonSerializer.Serialize(normalized.Entries, JsonOptions));
                write.Parameters.AddWithValue("hash", hash);
                write.Parameters.AddWithValue("power", raidPower);
                write.Parameters.AddWithValue("version", ContentVersion);
                await using var written = await write.ExecuteReaderAsync(cancellationToken);
                if (!await written.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status403Forbidden,
                        "LOADOUT_OWNER_MISMATCH", "Attacker loadout belongs to another player.");
                var revision = written.GetInt64(0);
                var updatedAt = written.GetFieldValue<DateTimeOffset>(1);

                return new ApiReply(StatusCodes.Status200OK,
                    new AttackerLoadoutView(true, string.Empty, loadoutId.ToString("D"), revision,
                        normalized.FactionId, normalized.HeroId, normalized.Entries, raidPower,
                        ContentVersion, hash, updatedAt.ToString("O")));
            });
    }

    private static PutAttackerLoadoutRequest? Normalize(PutAttackerLoadoutRequest request,
        out string? error)
    {
        error = null;
        var faction = request.FactionId?.Trim() ?? string.Empty;
        var hero = request.HeroId?.Trim() ?? string.Empty;
        if (!IdentityPattern.IsMatch(faction))
        {
            error = "Faction ID is invalid.";
            return null;
        }
        if (hero.Length > 0 && !IdentityPattern.IsMatch(hero))
        {
            error = "Hero ID is invalid.";
            return null;
        }
        if (request.Entries is null || request.Entries.Count is < 1 or > MaxUniqueCards)
        {
            error = $"Army must contain 1-{MaxUniqueCards} unique card entries.";
            return null;
        }

        var entries = request.Entries
            .Select(entry => new AttackerLoadoutEntryRequest(entry.CardId?.Trim() ?? string.Empty, entry.Count))
            .OrderBy(entry => entry.CardId, StringComparer.Ordinal).ToArray();
        if (entries.Any(entry => !IdentityPattern.IsMatch(entry.CardId) || entry.Count is < 1 or > MaxUnits))
        {
            error = "Each army card ID and count must be valid.";
            return null;
        }
        if (entries.Select(entry => entry.CardId).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            error = "Duplicate army card entries are forbidden.";
            return null;
        }
        if (entries.Sum(entry => entry.Count) > MaxUnits)
        {
            error = $"Army cannot contain more than {MaxUnits} units.";
            return null;
        }
        return new PutAttackerLoadoutRequest(faction, hero, entries);
    }
}
