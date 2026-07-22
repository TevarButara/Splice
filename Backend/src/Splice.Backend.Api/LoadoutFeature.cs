using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed record AttackerLoadoutEntryRequest(string CardId, int Count);
public sealed record PutAttackerLoadoutRequest(string FactionId, string HeroId,
    IReadOnlyList<string>? GearInstanceIds, IReadOnlyList<AttackerLoadoutEntryRequest>? Entries);
public sealed record AttackerLoadoutView(bool Success, string Error, string LoadoutId,
    long Revision, string FactionId, string HeroId, IReadOnlyList<string> GearInstanceIds,
    IReadOnlyList<AttackerLoadoutEntryRequest> Entries, long ArmyPower, long HeroPower,
    long GearPower, long RaidPower, string ContentVersion, string PayloadSha256, string UpdatedUtc);

public static class LoadoutFeature
{
    public const string ContentVersion = "content-c4c1-v1";
    public const string ValidatorVersion = "server-loadout-c4c1-v1";
    private const int MaxUniqueCards = 20;
    private const int MaxUnits = 50;
    private const int MaxGearItems = 6;
    private static readonly Regex IdentityPattern = new("^[A-Za-z0-9][A-Za-z0-9._/-]{0,79}$",
        RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record HeroAuthority(string ContentId, int Level, long BasePower,
        long ScaledPower, JsonElement Combat);
    private sealed record GearAuthority(string InstanceId, string ContentId, int Level,
        long BasePower, long ScaledPower, JsonElement Combat);

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

                var army = await ValidateArmyAsync(connection, transaction, normalized!,
                    context, cancellationToken);
                if (army.Error is not null) return army.Error;

                var hero = await ValidateHeroAsync(connection, transaction, playerId,
                    normalized!.HeroId, context, cancellationToken);
                if (hero.Error is not null) return hero.Error;

                var gear = await ValidateGearAsync(connection, transaction, playerId,
                    normalized.GearInstanceIds!, context, cancellationToken);
                if (gear.Error is not null) return gear.Error;

                var gearPower = gear.Items!.Sum(item => item.ScaledPower);
                var heroPayload = JsonSerializer.Serialize(hero.Value, JsonOptions);
                var gearPayload = JsonSerializer.Serialize(gear.Items, JsonOptions);
                var payload = JsonSerializer.Serialize(new
                {
                    factionId = normalized.FactionId,
                    hero = hero.Value,
                    gear = gear.Items,
                    entries = normalized.Entries,
                }, JsonOptions);
                var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

                await using var write = new NpgsqlCommand("""
                    INSERT INTO splice.attacker_loadouts
                        (id, owner_player_id, faction_id, revision, hero_id, entries,
                         payload_sha256, army_power, hero_power, gear_power, hero_payload,
                         gear_items, content_version, updated_at)
                    VALUES (@id, @owner, @faction, 1, @hero, @entries, @hash, @armyPower,
                            @heroPower, @gearPower, @heroPayload, @gearItems, @version,
                            clock_timestamp())
                    ON CONFLICT (id) DO UPDATE SET
                        faction_id=EXCLUDED.faction_id,
                        revision=splice.attacker_loadouts.revision + 1,
                        hero_id=EXCLUDED.hero_id,
                        entries=EXCLUDED.entries,
                        payload_sha256=EXCLUDED.payload_sha256,
                        army_power=EXCLUDED.army_power,
                        hero_power=EXCLUDED.hero_power,
                        gear_power=EXCLUDED.gear_power,
                        hero_payload=EXCLUDED.hero_payload,
                        gear_items=EXCLUDED.gear_items,
                        content_version=EXCLUDED.content_version,
                        updated_at=clock_timestamp()
                    WHERE splice.attacker_loadouts.owner_player_id = @owner
                    RETURNING revision, raid_power, updated_at
                    """, connection, transaction);
                write.Parameters.AddWithValue("id", loadoutId);
                write.Parameters.AddWithValue("owner", playerId);
                write.Parameters.AddWithValue("faction", normalized.FactionId);
                write.Parameters.AddWithValue("hero", hero.Value!.ContentId);
                write.Parameters.AddWithValue("entries", NpgsqlDbType.Jsonb,
                    JsonSerializer.Serialize(normalized.Entries, JsonOptions));
                write.Parameters.AddWithValue("hash", hash);
                write.Parameters.AddWithValue("armyPower", army.Power);
                write.Parameters.AddWithValue("heroPower", hero.Value.ScaledPower);
                write.Parameters.AddWithValue("gearPower", gearPower);
                write.Parameters.AddWithValue("heroPayload", NpgsqlDbType.Jsonb, heroPayload);
                write.Parameters.AddWithValue("gearItems", NpgsqlDbType.Jsonb, gearPayload);
                write.Parameters.AddWithValue("version", ContentVersion);
                await using var written = await write.ExecuteReaderAsync(cancellationToken);
                if (!await written.ReadAsync(cancellationToken))
                    return ApiErrors.Reply(context, StatusCodes.Status403Forbidden,
                        "LOADOUT_OWNER_MISMATCH", "Attacker loadout belongs to another player.");
                var revision = written.GetInt64(0);
                var raidPower = written.GetInt64(1);
                var updatedAt = written.GetFieldValue<DateTimeOffset>(2);

                return new ApiReply(StatusCodes.Status200OK,
                    new AttackerLoadoutView(true, string.Empty, loadoutId.ToString("D"), revision,
                        normalized.FactionId, hero.Value.ContentId, normalized.GearInstanceIds!,
                        normalized.Entries!, army.Power, hero.Value.ScaledPower, gearPower,
                        raidPower, ContentVersion, hash, updatedAt.ToString("O")));
            });
    }

    private static async Task<(long Power, ApiReply? Error)> ValidateArmyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PutAttackerLoadoutRequest request,
        HttpContext context, CancellationToken cancellationToken)
    {
        var ids = request.Entries!.Select(entry => entry.CardId).ToArray();
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
                definitions[reader.GetString(0)] =
                    (reader.GetString(1), reader.GetInt32(2), reader.GetString(3));
        }

        long power = 0;
        foreach (var entry in request.Entries!)
        {
            if (!definitions.TryGetValue(entry.CardId, out var definition) ||
                definition.Faction != request.FactionId || definition.Power <= 0)
                return (0, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                    "LOADOUT_CONTENT_INVALID", $"Unknown, disabled, or mismatched army card: {entry.CardId}"));
            if (definition.Version != ContentVersion)
                return (0, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                    "LOADOUT_CONTENT_STALE", $"Army card content is stale: {entry.CardId}"));
            power = checked(power + (long)definition.Power * entry.Count);
        }
        return (power, null);
    }

    private static async Task<(HeroAuthority? Value, ApiReply? Error)> ValidateHeroAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid playerId, string heroContentId,
        HttpContext context, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT h.level, c.raid_power, c.content_version, c.combat_payload::text
              FROM splice.player_heroes h
              JOIN splice.content_definitions c
                ON c.content_id=h.hero_content_id AND c.content_kind=h.content_kind
             WHERE h.player_id=@player AND h.hero_content_id=@hero
               AND c.content_kind='HERO' AND c.enabled
            """, connection, transaction);
        command.Parameters.AddWithValue("player", playerId);
        command.Parameters.AddWithValue("hero", heroContentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return (null, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "HERO_NOT_OWNED", "Selected Hero is not unlocked for this player."));
        var level = reader.GetInt32(0);
        var basePower = reader.GetInt32(1);
        var version = reader.GetString(2);
        var combat = JsonDocument.Parse(reader.GetString(3)).RootElement.Clone();
        if (version != ContentVersion || basePower <= 0)
            return (null, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "HERO_CONTENT_STALE", "Selected Hero must be revalidated against current content."));
        var scaled = ScalePower(basePower, level, 5);
        return (new HeroAuthority(heroContentId, level, basePower, scaled, combat), null);
    }

    private static async Task<(IReadOnlyList<GearAuthority>? Items, ApiReply? Error)> ValidateGearAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid playerId,
        IReadOnlyList<string> instanceIds, HttpContext context, CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0) return (Array.Empty<GearAuthority>(), null);
        var ids = instanceIds.Select(Guid.Parse).ToArray();
        var items = new List<GearAuthority>();
        await using var command = new NpgsqlCommand("""
            SELECT g.id, g.gear_content_id, g.level, c.raid_power,
                   c.content_version, c.combat_payload::text
              FROM splice.player_gear_items g
              JOIN splice.content_definitions c
                ON c.content_id=g.gear_content_id AND c.content_kind=g.content_kind
             WHERE g.owner_player_id=@player AND g.id = ANY(@ids)
               AND c.content_kind='GEAR' AND c.enabled
             ORDER BY g.id
            """, connection, transaction);
        command.Parameters.AddWithValue("player", playerId);
        command.Parameters.AddWithValue("ids", ids);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var version = reader.GetString(4);
            var basePower = reader.GetInt32(3);
            if (version != ContentVersion || basePower <= 0)
                return (null, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                    "GEAR_CONTENT_STALE", "Equipped gear must be revalidated against current content."));
            var level = reader.GetInt32(2);
            items.Add(new GearAuthority(reader.GetGuid(0).ToString("D"), reader.GetString(1), level,
                basePower, ScalePower(basePower, level, 10),
                JsonDocument.Parse(reader.GetString(5)).RootElement.Clone()));
        }
        if (items.Count != ids.Length)
            return (null, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "GEAR_NOT_OWNED", "One or more equipped gear items are missing or owned by another player."));
        return (items, null);
    }

    private static long ScalePower(long basePower, int level, int percentPerLevel) =>
        checked(basePower * (100L + Math.Max(0, level - 1) * percentPerLevel) / 100L);

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
        if (hero.StartsWith("hero/", StringComparison.Ordinal)) hero = hero[5..];
        if (!IdentityPattern.IsMatch(hero))
        {
            error = "A valid Hero ID is required.";
            return null;
        }
        hero = "hero/" + hero;

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

        var gear = request.GearInstanceIds ?? Array.Empty<string>();
        if (gear.Count > MaxGearItems)
        {
            error = $"Hero cannot equip more than {MaxGearItems} gear items.";
            return null;
        }
        var normalizedGear = new List<string>();
        foreach (var value in gear)
        {
            if (!Guid.TryParse(value, out var parsed))
            {
                error = "Gear instance IDs must be UUIDs.";
                return null;
            }
            normalizedGear.Add(parsed.ToString("D"));
        }
        normalizedGear.Sort(StringComparer.Ordinal);
        if (normalizedGear.Distinct(StringComparer.Ordinal).Count() != normalizedGear.Count)
        {
            error = "Duplicate gear instances are forbidden.";
            return null;
        }
        return new PutAttackerLoadoutRequest(faction, hero, normalizedGear, entries);
    }
}
