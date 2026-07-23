using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Splice.Backend.Api;

public sealed class Vector3View { public float X { get; set; } public float Y { get; set; } public float Z { get; set; } }
public sealed class PlacedTowerView
{
    public string TowerId { get; set; } = string.Empty;
    public Vector3View Position { get; set; } = new();
    public int AttackLevel { get; set; }
    public int HealthLevel { get; set; }
    public int ArmorLevel { get; set; }
    public int RangeLevel { get; set; }
    public int TargetsLevel { get; set; }
}
public sealed class GarrisonMonsterView
{
    public string CardId { get; set; } = string.Empty;
    public Vector3View Position { get; set; } = new();
}
public sealed class BaseLayoutView
{
    public int Version { get; set; } = 1;
    public string OwnerAccountId { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public List<PlacedTowerView> Towers { get; set; } = [];
    public List<GarrisonMonsterView> Garrison { get; set; } = [];
    public List<string> MinerCardIds { get; set; } = [];
    public int StoredGold { get; set; }
}
public sealed record DeployTownRequest(BaseLayoutView CheckedOutLayout, int UsedCapacity, int MaxCapacity);
public sealed record TownDraftView(bool Exists, BaseLayoutView? CheckedOutLayout);
public sealed record BackendAck(bool Success = true);
public sealed record SnapshotCommitView(bool Success, string Error, TownDefenseSnapshotView? Snapshot);
public sealed record SnapshotBatchRequest(IReadOnlyList<string>? FactionIds);
public sealed record SnapshotBatchView(IReadOnlyList<TownDefenseSnapshotView> Snapshots);
public sealed record TownDefenseSnapshotView(
    int SchemaVersion, string SnapshotId, string DeploymentId, int Revision, string CommittedUtc,
    string OwnerAccountId, string FactionId, int BaseLevel, long BasePowerRating,
    int UsedCapacity, int MaxCapacity, bool MatchmakingEligible,
    string ValidationVersion, IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<CombatUnitAuthorityView> DefenseUnits, BaseLayoutView Layout,
    string ArmyShowcasePresetName, string HeroAppearanceId);

public static partial class TownFeature
{
    private const string ValidatorVersion = "server-town-c4c2-v1";
    private const string ContentVersion = LoadoutFeature.ContentVersion;
    private const int MaxCitySlots = 3;
    private const int MaxDefensePieces = 200;
    private const int MaxPayloadBytes = 512 * 1024;
    private static readonly Regex FactionPattern = FactionRegex();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapTownEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/towns/{factionId}/draft", GetDraftAsync);
        app.MapPut("/v1/towns/{factionId}/draft", PutDraftAsync);
        app.MapPost("/v1/towns/{factionId}/deployments", DeployAsync);
        app.MapGet("/v1/towns/{factionId}/snapshots/latest", GetLatestAsync);
        app.MapPost("/v1/town-snapshots/latest/query", QueryLatestAsync);
        app.MapGet("/v1/town-snapshots/{snapshotId:guid}", GetByIdAsync);
    }

    private static async Task<IResult> GetDraftAsync(HttpContext context, string factionId,
        NpgsqlDataSource dataSource)
    {
        if (!ValidFaction(factionId))
            return IdempotencyExecutor.ToResult(ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                "FACTION_ID_INVALID", "Faction ID is invalid."));
        await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("""
            SELECT d.payload::text
              FROM splice.towns t
              JOIN splice.town_drafts d ON d.town_id = t.id
             WHERE t.owner_player_id = @player AND t.faction_id = @faction
            """, connection);
        command.Parameters.AddWithValue("player", RequestIdentityMiddleware.PlayerId(context));
        command.Parameters.AddWithValue("faction", factionId);
        var payload = await command.ExecuteScalarAsync(context.RequestAborted) as string;
        return Results.Ok(payload is null
            ? new TownDraftView(false, null)
            : new TownDraftView(true, JsonSerializer.Deserialize<BaseLayoutView>(payload, JsonOptions)));
    }

    private static async Task<IResult> PutDraftAsync(HttpContext context, string factionId,
        BaseLayoutView layout, IdempotencyExecutor idempotency)
    {
        var playerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, playerId, layout,
            async (connection, transaction, cancellationToken) =>
            {
                var identityError = ValidateIdentity(context, playerId, factionId, layout);
                if (identityError is not null) return identityError;
                var canonical = Canonical(layout);
                if (Encoding.UTF8.GetByteCount(canonical) > MaxPayloadBytes)
                    return ApiErrors.Reply(context, StatusCodes.Status413PayloadTooLarge,
                        "DRAFT_TOO_LARGE", "Town draft exceeds the allowed size.");

                var structure = await ValidateLayoutAsync(connection, transaction, layout, null, null,
                    requireDefense: false, cancellationToken);
                if (!structure.Valid)
                    return ValidationError(context, structure.Errors);

                var town = await GetOrCreateTownAsync(connection, transaction, playerId, factionId,
                    context, cancellationToken);
                if (town.Error is not null) return town.Error;
                var payloadHash = Sha256(canonical);

                await using var command = new NpgsqlCommand("""
                    INSERT INTO splice.town_drafts (town_id, version, payload, payload_hash)
                    VALUES (@town, 1, @payload, @hash)
                    ON CONFLICT (town_id) DO UPDATE SET
                        version = splice.town_drafts.version + 1,
                        payload = EXCLUDED.payload,
                        payload_hash = EXCLUDED.payload_hash,
                        updated_at = clock_timestamp();
                    UPDATE splice.towns SET draft_version = draft_version + 1, updated_at = clock_timestamp()
                     WHERE id = @town
                    """, connection, transaction);
                command.Parameters.AddWithValue("town", town.Id);
                command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, canonical);
                command.Parameters.AddWithValue("hash", payloadHash);
                await command.ExecuteNonQueryAsync(cancellationToken);
                return new ApiReply(StatusCodes.Status200OK, new BackendAck());
            });
    }

    private static async Task<IResult> DeployAsync(HttpContext context, string factionId,
        DeployTownRequest request, IdempotencyExecutor idempotency)
    {
        var playerId = RequestIdentityMiddleware.PlayerId(context);
        return await idempotency.ExecuteAsync(context, playerId, request,
            async (connection, transaction, cancellationToken) =>
            {
                var layout = request.CheckedOutLayout;
                var identityError = ValidateIdentity(context, playerId, factionId, layout);
                if (identityError is not null) return identityError;

                var town = await FindTownForUpdateAsync(connection, transaction, playerId, factionId,
                    cancellationToken);
                if (town is null)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "DRAFT_NOT_FOUND", "Save a checked-out draft before deployment.");

                var canonical = Canonical(layout);
                var payloadHash = Sha256(canonical);
                var draft = await ReadDraftForUpdateAsync(connection, transaction, town.Value.Id, cancellationToken);
                if (draft is null)
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "DRAFT_NOT_FOUND", "Save a checked-out draft before deployment.");
                if (!string.Equals(draft.Value.Hash, payloadHash, StringComparison.Ordinal))
                    return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                        "DRAFT_VERSION_CONFLICT", "Deployment payload does not match the checked-out draft.");

                var validation = await ValidateLayoutAsync(connection, transaction, layout,
                    request.UsedCapacity, request.MaxCapacity, requireDefense: true, cancellationToken,
                    town.Value.BaseLevel);
                if (!validation.Valid) return ValidationError(context, validation.Errors);

                var existing = await FindCommittedSnapshotAsync(connection, transaction,
                    town.Value.Id, payloadHash, cancellationToken);
                if (existing is not null)
                    return new ApiReply(StatusCodes.Status200OK,
                        new SnapshotCommitView(true, string.Empty, existing));

                var revision = await NextRevisionAsync(connection, transaction, town.Value.Id, cancellationToken);
                var commitId = Guid.NewGuid();
                var snapshotId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();
                var committedAt = DateTimeOffset.UtcNow;
                var previousBuildValue = await PreviousBuildValueAsync(connection, transaction,
                    town.Value.Id, cancellationToken);
                var checkoutCost = validation.BuildValue >= previousBuildValue
                    ? validation.BuildValue - previousBuildValue
                    : -((previousBuildValue - validation.BuildValue) / 2);

                var goldAccountId = await PlayerAccountAsync(connection, transaction, playerId,
                    "GOLD", cancellationToken);
                Guid? checkoutTransactionId = null;
                if (checkoutCost != 0)
                {
                    var systemGoldId = checkoutCost > 0
                        ? Guid.Parse("00000000-0000-0000-0000-000000000102")
                        : Guid.Parse("00000000-0000-0000-0000-000000000101");
                    var amount = Math.Abs(checkoutCost);
                    var postings = checkoutCost > 0
                        ? Postings(goldAccountId, -amount, systemGoldId, amount)
                        : Postings(systemGoldId, -amount, goldAccountId, amount);
                    checkoutTransactionId = await PostLedgerAsync(connection, transaction,
                        $"checkout:{commitId:D}:meta_gold", "TOWN_CHECKOUT", commitId, postings,
                        cancellationToken);
                }

                await AdjustTownVaultAsync(connection, transaction, town.Value.Id, commitId,
                    goldAccountId, layout.StoredGold, cancellationToken);

                await using (var commitCommand = new NpgsqlCommand("""
                    INSERT INTO splice.town_layout_commits (
                        id, town_id, revision, draft_version, payload, payload_hash,
                        build_value, checkout_cost, checkout_transaction_id,
                        content_version, validator_version, committed_at)
                    VALUES (@id, @town, @revision, @draft_version, @payload, @hash,
                            @build_value, @checkout_cost, @transaction,
                            @content_version, @validator_version, @committed_at)
                    """, connection, transaction))
                {
                    commitCommand.Parameters.AddWithValue("id", commitId);
                    commitCommand.Parameters.AddWithValue("town", town.Value.Id);
                    commitCommand.Parameters.AddWithValue("revision", revision);
                    commitCommand.Parameters.AddWithValue("draft_version", draft.Value.Version);
                    commitCommand.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, canonical);
                    commitCommand.Parameters.AddWithValue("hash", payloadHash);
                    commitCommand.Parameters.AddWithValue("build_value", validation.BuildValue);
                    commitCommand.Parameters.AddWithValue("checkout_cost", checkoutCost);
                    commitCommand.Parameters.AddWithValue("transaction", NpgsqlDbType.Uuid,
                        checkoutTransactionId is null ? DBNull.Value : checkoutTransactionId.Value);
                    commitCommand.Parameters.AddWithValue("content_version", ContentVersion);
                    commitCommand.Parameters.AddWithValue("validator_version", ValidatorVersion);
                    commitCommand.Parameters.AddWithValue("committed_at", committedAt);
                    await commitCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                var townEscrow = await GetOrFundTownEscrowAsync(connection, transaction,
                    town.Value.Id, playerId, deploymentId, town.Value.BaseLevel, cancellationToken);
                var snapshot = new TownDefenseSnapshotView(
                    2, snapshotId.ToString("D"), deploymentId.ToString("D"), revision, committedAt.ToString("O"),
                    layout.OwnerAccountId, factionId, town.Value.BaseLevel, validation.BasePower,
                    validation.UsedCapacity, validation.MaxCapacity, true, ValidatorVersion,
                    validation.Warnings, validation.DefenseUnits, layout, string.Empty, string.Empty);
                var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);

                await using (var snapshotCommand = new NpgsqlCommand("""
                    INSERT INTO splice.town_snapshots (
                        id, town_id, layout_commit_id, revision, payload, payload_sha256,
                        faction_id, base_level, base_power, content_version, validator_version,
                        committed_at, used_capacity, max_capacity, tower_count, garrison_count,
                        matchmaking_eligible, validation_warnings)
                    VALUES (@id, @town, @commit, @revision, @payload,
                            encode(public.digest((@payload::jsonb)::text, 'sha256'), 'hex'),
                            @faction, @level, @power, @content_version, @validator_version,
                            @committed_at, @used, @max, @towers, @garrison, true, @warnings)
                    """, connection, transaction))
                {
                    snapshotCommand.Parameters.AddWithValue("id", snapshotId);
                    snapshotCommand.Parameters.AddWithValue("town", town.Value.Id);
                    snapshotCommand.Parameters.AddWithValue("commit", commitId);
                    snapshotCommand.Parameters.AddWithValue("revision", revision);
                    snapshotCommand.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, snapshotJson);
                    snapshotCommand.Parameters.AddWithValue("faction", factionId);
                    snapshotCommand.Parameters.AddWithValue("level", town.Value.BaseLevel);
                    snapshotCommand.Parameters.AddWithValue("power", validation.BasePower);
                    snapshotCommand.Parameters.AddWithValue("content_version", ContentVersion);
                    snapshotCommand.Parameters.AddWithValue("validator_version", ValidatorVersion);
                    snapshotCommand.Parameters.AddWithValue("committed_at", committedAt);
                    snapshotCommand.Parameters.AddWithValue("used", validation.UsedCapacity);
                    snapshotCommand.Parameters.AddWithValue("max", validation.MaxCapacity);
                    snapshotCommand.Parameters.AddWithValue("towers", layout.Towers.Count);
                    snapshotCommand.Parameters.AddWithValue("garrison", layout.Garrison.Count);
                    snapshotCommand.Parameters.AddWithValue("warnings", NpgsqlDbType.Jsonb,
                        JsonSerializer.Serialize(validation.Warnings, JsonOptions));
                    await snapshotCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var deploymentCommand = new NpgsqlCommand("""
                    UPDATE splice.town_deployments SET status = 'RETIRED', retired_at = clock_timestamp()
                     WHERE town_id = @town AND status IN ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED');
                    INSERT INTO splice.town_deployments (
                        id, town_id, active_snapshot_id, town_escrow_id, status, stake_band)
                    VALUES (@id, @town, @snapshot, @escrow, 'ACTIVE', @band)
                    """, connection, transaction))
                {
                    deploymentCommand.Parameters.AddWithValue("town", town.Value.Id);
                    deploymentCommand.Parameters.AddWithValue("id", deploymentId);
                    deploymentCommand.Parameters.AddWithValue("snapshot", snapshotId);
                    deploymentCommand.Parameters.AddWithValue("escrow", townEscrow.Id);
                    deploymentCommand.Parameters.AddWithValue("band", StakeBand(town.Value.BaseLevel));
                    await deploymentCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var outbox = new NpgsqlCommand("""
                    INSERT INTO splice.outbox_events (aggregate_type, aggregate_id, event_type, payload)
                    VALUES ('TOWN_SNAPSHOT', @snapshot, 'TownSnapshotDeployed',
                            jsonb_build_object('snapshotId', @snapshot, 'townId', @town, 'revision', @revision))
                    """, connection, transaction))
                {
                    outbox.Parameters.AddWithValue("snapshot", snapshotId);
                    outbox.Parameters.AddWithValue("town", town.Value.Id);
                    outbox.Parameters.AddWithValue("revision", revision);
                    await outbox.ExecuteNonQueryAsync(cancellationToken);
                }

                return new ApiReply(StatusCodes.Status201Created,
                    new SnapshotCommitView(true, string.Empty, snapshot));
            });
    }

    private static async Task<IResult> GetLatestAsync(HttpContext context, string factionId,
        NpgsqlDataSource dataSource)
    {
        if (!ValidFaction(factionId))
            return IdempotencyExecutor.ToResult(ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                "FACTION_ID_INVALID", "Faction ID is invalid."));
        await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("""
            SELECT d.id, s.payload::text
              FROM splice.towns t
              JOIN splice.town_deployments d ON d.town_id = t.id
              JOIN splice.town_snapshots s ON s.id = d.active_snapshot_id
             WHERE t.owner_player_id = @player AND t.faction_id = @faction
               AND d.status IN ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED')
             ORDER BY s.revision DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("player", RequestIdentityMiddleware.PlayerId(context));
        command.Parameters.AddWithValue("faction", factionId);
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        if (!await reader.ReadAsync(context.RequestAborted))
            return Results.Text("null", "application/json", Encoding.UTF8, StatusCodes.Status200OK);
        var snapshot = DeserializeSnapshot(reader.GetString(1), reader.GetGuid(0));
        return Results.Json(snapshot, JsonOptions, statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetByIdAsync(HttpContext context, Guid snapshotId,
        NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("""
            SELECT d.id, s.payload::text
              FROM splice.town_snapshots s
              LEFT JOIN LATERAL (
                  SELECT id FROM splice.town_deployments
                   WHERE active_snapshot_id = s.id
                   ORDER BY activated_at DESC LIMIT 1
              ) d ON true
             WHERE s.id = @id
            """, connection);
        command.Parameters.AddWithValue("id", snapshotId);
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        if (!await reader.ReadAsync(context.RequestAborted))
            return IdempotencyExecutor.ToResult(ApiErrors.Reply(context, StatusCodes.Status404NotFound,
                "SNAPSHOT_NOT_FOUND", "Town snapshot was not found."));
        var deploymentId = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0);
        return Results.Json(DeserializeSnapshot(reader.GetString(1), deploymentId), JsonOptions,
            statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> QueryLatestAsync(HttpContext context, SnapshotBatchRequest request,
        NpgsqlDataSource dataSource)
    {
        var factions = (request.FactionIds ?? []).Where(ValidFaction).Distinct(StringComparer.Ordinal).Take(20).ToArray();
        if (factions.Length == 0) return Results.Ok(new SnapshotBatchView([]));
        await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
        await using var command = new NpgsqlCommand("""
            SELECT d.id, s.payload::text
              FROM splice.town_deployments d
              JOIN splice.town_snapshots s ON s.id = d.active_snapshot_id
             WHERE d.status IN ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED')
               AND s.matchmaking_eligible AND s.faction_id = ANY(@factions)
             ORDER BY d.activated_at DESC
             LIMIT 50
            """, connection);
        command.Parameters.AddWithValue("factions", factions);
        var snapshots = new List<TownDefenseSnapshotView>();
        await using var reader = await command.ExecuteReaderAsync(context.RequestAborted);
        while (await reader.ReadAsync(context.RequestAborted))
        {
            var snapshot = DeserializeSnapshot(reader.GetString(1), reader.GetGuid(0));
            if (snapshot is not null) snapshots.Add(snapshot);
        }
        return Results.Ok(new SnapshotBatchView(snapshots));
    }

    private static ApiReply? ValidateIdentity(HttpContext context, Guid playerId, string factionId,
        BaseLayoutView? layout)
    {
        if (!ValidFaction(factionId))
            return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                "FACTION_ID_INVALID", "Faction ID is invalid.");
        if (layout is null)
            return ApiErrors.Reply(context, StatusCodes.Status400BadRequest,
                "DRAFT_REQUIRED", "Town layout is required.");
        if (layout.Version != 1)
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "LAYOUT_VERSION_UNSUPPORTED", "Town layout version is not supported.");
        if (!Guid.TryParse(layout.OwnerAccountId, out var ownerId) || ownerId != playerId)
            return ApiErrors.Reply(context, StatusCodes.Status403Forbidden,
                "TOWN_OWNER_MISMATCH", "Town layout owner does not match the authenticated player.");
        if (!string.Equals(layout.FactionId, factionId, StringComparison.Ordinal))
            return ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "TOWN_FACTION_MISMATCH", "Route and layout faction IDs must match.");
        return null;
    }

    private static async Task<LayoutValidation> ValidateLayoutAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, BaseLayoutView layout, int? claimedUsed, int? claimedMax,
        bool requireDefense, CancellationToken cancellationToken, int baseLevel = 1)
    {
        layout.Towers ??= [];
        layout.Garrison ??= [];
        layout.MinerCardIds ??= [];
        var result = new LayoutValidation();
        var pieceCount = layout.Towers.Count + layout.Garrison.Count;
        if (requireDefense && pieceCount == 0) result.Errors.Add("Place at least one tower or garrison unit.");
        if (pieceCount > MaxDefensePieces) result.Errors.Add("Town contains too many defense pieces.");
        if (layout.StoredGold < 0) result.Errors.Add("Stored Gold cannot be negative.");

        var ids = layout.Towers.Select(t => t.TowerId)
            .Concat(layout.Garrison.Select(g => g.CardId))
            .Concat(layout.MinerCardIds).Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        var definitions = new Dictionary<(string Id, string Kind), ContentDefinition>();
        if (ids.Length > 0)
        {
            await using var command = new NpgsqlCommand("""
                SELECT content_id, faction_id, content_kind, defense_capacity_cost, gold_cost,
                       content_version, raid_power, combat_payload::text
                  FROM splice.content_definitions
                 WHERE enabled AND content_id = ANY(@ids)
                """, connection, transaction);
            command.Parameters.AddWithValue("ids", ids);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var kind = reader.GetString(2);
                var combat = JsonSerializer.Deserialize<CombatStatsView>(reader.GetString(7), JsonOptions);
                if (combat is not null)
                    definitions[(id, kind)] = new(reader.GetString(1), kind,
                        reader.GetInt32(3), reader.GetInt64(4), reader.GetString(5),
                        reader.GetInt32(6), combat);
            }
        }

        var occupied = new HashSet<string>(StringComparer.Ordinal);
        for (var towerIndex = 0; towerIndex < layout.Towers.Count; towerIndex++)
        {
            var tower = layout.Towers[towerIndex];
            if (!ValidPosition(tower.Position) || !occupied.Add(PositionKey(tower.Position)))
                result.Errors.Add("Tower position is invalid or overlaps another defense piece.");
            if (!definitions.TryGetValue((tower.TowerId ?? string.Empty, "TOWER"), out var definition) ||
                definition.Kind != "TOWER" || definition.Faction != layout.FactionId)
            {
                result.Errors.Add($"Unknown or mismatched tower content: {tower.TowerId}");
                continue;
            }
            if (definition.Version != ContentVersion)
                result.Errors.Add($"Tower content version is stale: {tower.TowerId}");
            if (!LoadoutFeature.ValidCombat(definition.Combat, mobile: false))
                result.Errors.Add($"Tower combat payload is invalid: {tower.TowerId}");
            var upgrades = new[] { tower.AttackLevel, tower.HealthLevel, tower.ArmorLevel,
                tower.RangeLevel, tower.TargetsLevel };
            if (upgrades.Any(level => level is < 0 or > 10)) result.Errors.Add("Tower upgrade level is invalid.");
            var scaledPower = checked(definition.Power * (100L + upgrades.Sum() * 5L) / 100L);
            result.DefenseUnits.Add(new CombatUnitAuthorityView(
                $"tower:{towerIndex:D3}:{tower.TowerId}", tower.TowerId ?? string.Empty, "TOWER", 1,
                definition.Power, Math.Max(1, scaledPower),
                ScaleTowerCombat(definition.Combat, tower), tower.Position));
            result.UsedCapacity += definition.Capacity;
            result.BuildValue += definition.GoldCost + upgrades.Sum() * Math.Max(1, definition.GoldCost / 2);
            result.BasePower += 100 + upgrades.Sum() * 20;
        }
        for (var unitIndex = 0; unitIndex < layout.Garrison.Count; unitIndex++)
        {
            var unit = layout.Garrison[unitIndex];
            if (!ValidPosition(unit.Position) || !occupied.Add(PositionKey(unit.Position)))
                result.Errors.Add("Garrison position is invalid or overlaps another defense piece.");
            if (!definitions.TryGetValue((unit.CardId ?? string.Empty, "GARRISON"), out var definition) ||
                definition.Kind != "GARRISON" || definition.Faction != layout.FactionId)
            {
                result.Errors.Add($"Unknown or mismatched garrison content: {unit.CardId}");
                continue;
            }
            if (definition.Version != ContentVersion)
                result.Errors.Add($"Garrison content version is stale: {unit.CardId}");
            if (!LoadoutFeature.ValidCombat(definition.Combat, mobile: true))
                result.Errors.Add($"Garrison combat payload is invalid: {unit.CardId}");
            result.DefenseUnits.Add(new CombatUnitAuthorityView(
                $"garrison:{unitIndex:D3}:{unit.CardId}", unit.CardId ?? string.Empty, "GARRISON", 1,
                definition.Power, definition.Power, definition.Combat, unit.Position));
            result.UsedCapacity += definition.Capacity;
            result.BuildValue += definition.GoldCost;
            result.BasePower += 80;
        }

        if (requireDefense)
        {
            var coreCombat = CoreCombat(baseLevel);
            var corePower = CombatPower(coreCombat);
            result.DefenseUnits.Add(new CombatUnitAuthorityView("core", "town-core", "CORE", 1,
                corePower, corePower, coreCombat, new Vector3View()));
        }
        foreach (var minerId in layout.MinerCardIds)
        {
            if (!definitions.TryGetValue((minerId ?? string.Empty, "MINER"), out var definition) ||
                definition.Kind != "MINER" || definition.Faction != layout.FactionId)
                result.Errors.Add($"Unknown or mismatched miner content: {minerId}");
            else
            {
                if (definition.Version != ContentVersion)
                    result.Errors.Add($"Miner content version is stale: {minerId}");
                result.BuildValue += definition.GoldCost;
            }
        }

        result.MaxCapacity = Math.Max(1, baseLevel) * 100;
        result.BasePower += result.UsedCapacity * 25L + Math.Max(1, baseLevel) * 100L + layout.StoredGold / 100;
        if (claimedUsed is not null && claimedUsed != result.UsedCapacity)
            result.Errors.Add($"Used capacity must be {result.UsedCapacity}, not {claimedUsed}.");
        if (claimedMax is not null && claimedMax != result.MaxCapacity)
            result.Errors.Add($"Max capacity must be {result.MaxCapacity}, not {claimedMax}.");
        if (result.UsedCapacity > result.MaxCapacity) result.Errors.Add("Defense capacity is exceeded.");
        if (layout.Towers.Count == 0) result.Warnings.Add("No tower deployed: fast raiders may rush the Core.");
        if (layout.Garrison.Count == 0) result.Warnings.Add("No garrison deployed: the town has no mobile defense.");
        return result;
    }

    private static async Task<(Guid Id, int BaseLevel, ApiReply? Error)> GetOrCreateTownAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid playerId, string factionId,
        HttpContext context, CancellationToken cancellationToken)
    {
        var existing = await FindTownForUpdateAsync(connection, transaction, playerId, factionId, cancellationToken);
        if (existing is not null) return (existing.Value.Id, existing.Value.BaseLevel, null);
        await using var countCommand = new NpgsqlCommand(
            "SELECT count(*) FROM splice.towns WHERE owner_player_id = @player", connection, transaction);
        countCommand.Parameters.AddWithValue("player", playerId);
        if ((long)(await countCommand.ExecuteScalarAsync(cancellationToken))! >= MaxCitySlots)
            return (Guid.Empty, 0, ApiErrors.Reply(context, StatusCodes.Status409Conflict,
                "CITY_SLOT_LIMIT", "The account already owns the maximum number of towns."));
        var townId = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO splice.towns (id, owner_player_id, faction_id, base_level)
            VALUES (@id, @player, @faction, 1)
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", townId);
        insert.Parameters.AddWithValue("player", playerId);
        insert.Parameters.AddWithValue("faction", factionId);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return (townId, 1, null);
    }

    private static async Task<(Guid Id, int BaseLevel)?> FindTownForUpdateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid playerId, string factionId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, base_level FROM splice.towns
             WHERE owner_player_id = @player AND faction_id = @faction
             FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("player", playerId);
        command.Parameters.AddWithValue("faction", factionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetGuid(0), reader.GetInt32(1)) : null;
    }

    private static async Task<(long Version, string Hash)?> ReadDraftForUpdateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid townId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT version, payload_hash FROM splice.town_drafts WHERE town_id = @town FOR UPDATE",
            connection, transaction);
        command.Parameters.AddWithValue("town", townId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetInt64(0), reader.GetString(1)) : null;
    }

    private static async Task<TownDefenseSnapshotView?> FindCommittedSnapshotAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid townId, string payloadHash,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT d.id, s.payload::text
              FROM splice.town_layout_commits c
              JOIN splice.town_snapshots s ON s.layout_commit_id = c.id
              JOIN splice.town_deployments d ON d.active_snapshot_id = s.id
             WHERE c.town_id = @town AND c.payload_hash = @hash
               AND d.status IN ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED')
             ORDER BY c.revision DESC LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue("town", townId);
        command.Parameters.AddWithValue("hash", payloadHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? DeserializeSnapshot(reader.GetString(1), reader.GetGuid(0))
            : null;
    }

    private static async Task<int> NextRevisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid townId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(max(revision), 0) + 1 FROM splice.town_layout_commits WHERE town_id = @town",
            connection, transaction);
        command.Parameters.AddWithValue("town", townId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<long> PreviousBuildValueAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid townId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT build_value FROM splice.town_layout_commits
             WHERE town_id = @town ORDER BY revision DESC LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue("town", townId);
        return (await command.ExecuteScalarAsync(cancellationToken)) as long? ?? 0;
    }

    private static async Task<Guid> PlayerAccountAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid playerId, string currency, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id FROM splice.ledger_accounts
             WHERE owner_type = 'PLAYER' AND owner_id = @player AND currency_code = @currency
             FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("player", playerId);
        command.Parameters.AddWithValue("currency", currency);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not Guid accountId)
            throw new PostgresException("LEDGER_ACCOUNT_NOT_FOUND", "P0001", "P0001", "LEDGER_ACCOUNT_NOT_FOUND");
        return accountId;
    }

    private static async Task AdjustTownVaultAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid townId, Guid commitId, Guid playerGoldAccountId, long desiredBalance,
        CancellationToken cancellationToken)
    {
        Guid vaultAccountId;
        long currentBalance;
        await using (var query = new NpgsqlCommand("""
            SELECT v.ledger_account_id, a.balance
              FROM splice.town_vaults v
              JOIN splice.ledger_accounts a ON a.id = v.ledger_account_id
             WHERE v.town_id = @town AND v.currency_code = 'GOLD'
             FOR UPDATE OF v, a
            """, connection, transaction))
        {
            query.Parameters.AddWithValue("town", townId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                vaultAccountId = reader.GetGuid(0);
                currentBalance = reader.GetInt64(1);
            }
            else
            {
                await reader.DisposeAsync();
                vaultAccountId = Guid.NewGuid();
                currentBalance = 0;
                await using var create = new NpgsqlCommand("""
                    INSERT INTO splice.ledger_accounts
                        (id, account_key, owner_type, owner_id, currency_code)
                    VALUES (@account, @key, 'TOWN', @town, 'GOLD');
                    INSERT INTO splice.town_vaults
                        (town_id, currency_code, ledger_account_id, lootable_cap)
                    VALUES (@town, 'GOLD', @account, @cap)
                    """, connection, transaction);
                create.Parameters.AddWithValue("account", vaultAccountId);
                create.Parameters.AddWithValue("key", $"town:{townId:D}:vault:gold");
                create.Parameters.AddWithValue("town", townId);
                create.Parameters.AddWithValue("cap", desiredBalance);
                await create.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var delta = desiredBalance - currentBalance;
        if (delta != 0)
        {
            var postings = delta > 0
                ? Postings(playerGoldAccountId, -delta, vaultAccountId, delta)
                : Postings(vaultAccountId, delta, playerGoldAccountId, -delta);
            await PostLedgerAsync(connection, transaction, $"town:{commitId:D}:vault_adjust",
                "TOWN_VAULT_ADJUST", commitId, postings, cancellationToken);
        }
        await using var update = new NpgsqlCommand("""
            UPDATE splice.town_vaults
               SET lootable_cap = @cap, version = version + 1, updated_at = clock_timestamp()
             WHERE town_id = @town AND currency_code = 'GOLD'
            """, connection, transaction);
        update.Parameters.AddWithValue("cap", desiredBalance);
        update.Parameters.AddWithValue("town", townId);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(Guid Id, long Amount)> GetOrFundTownEscrowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid townId, Guid playerId,
        Guid deploymentId, int baseLevel, CancellationToken cancellationToken)
    {
        var expectedStake = TownStake(baseLevel);
        await using (var query = new NpgsqlCommand("""
            SELECT id, funded_amount FROM splice.town_escrows
             WHERE town_id = @town AND currency_code = 'WAR_GEM'
               AND state IN ('FUNDED', 'ACTIVE', 'RETIRING')
             FOR UPDATE
            """, connection, transaction))
        {
            query.Parameters.AddWithValue("town", townId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existing = (reader.GetGuid(0), reader.GetInt64(1));
                if (existing.Item2 != expectedStake)
                    throw new PostgresException("STAKE_POLICY_MIGRATION_REQUIRED", "P0001", "P0001",
                        "STAKE_POLICY_MIGRATION_REQUIRED");
                return existing;
            }
        }

        var playerAccountId = await PlayerAccountAsync(connection, transaction, playerId,
            "WAR_GEM", cancellationToken);
        var escrowId = Guid.NewGuid();
        var escrowAccountId = Guid.NewGuid();
        await using (var create = new NpgsqlCommand("""
            INSERT INTO splice.ledger_accounts
                (id, account_key, owner_type, owner_id, currency_code)
            VALUES (@account, @key, 'TOWN', @town, 'WAR_GEM')
            """, connection, transaction))
        {
            create.Parameters.AddWithValue("account", escrowAccountId);
            create.Parameters.AddWithValue("key", $"town:{townId:D}:escrow:war_gem");
            create.Parameters.AddWithValue("town", townId);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
        var transactionId = await PostLedgerAsync(connection, transaction,
            $"town:{deploymentId:D}:fund", "TOWN_ESCROW_FUND", deploymentId,
            Postings(playerAccountId, -expectedStake, escrowAccountId, expectedStake), cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO splice.town_escrows
                (id, town_id, ledger_account_id, currency_code, funded_amount, state, funded_transaction_id)
            VALUES (@id, @town, @account, 'WAR_GEM', @amount, 'ACTIVE', @transaction)
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", escrowId);
        insert.Parameters.AddWithValue("town", townId);
        insert.Parameters.AddWithValue("account", escrowAccountId);
        insert.Parameters.AddWithValue("amount", expectedStake);
        insert.Parameters.AddWithValue("transaction", transactionId);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return (escrowId, expectedStake);
    }

    private static async Task<Guid> PostLedgerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string key, string type, Guid referenceId, string postings, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT splice.post_ledger_transaction(@key, @type, 'TOWN', @reference, @postings, '{}'::jsonb)
            """, connection, transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("reference", referenceId);
        command.Parameters.AddWithValue("postings", NpgsqlDbType.Jsonb, postings);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static string Postings(Guid debit, long debitAmount, Guid credit, long creditAmount) =>
        JsonSerializer.Serialize(new[]
        {
            new { account_id = debit, amount = debitAmount },
            new { account_id = credit, amount = creditAmount },
        }, JsonOptions);

    private static ApiReply ValidationError(HttpContext context, IReadOnlyList<string> errors) =>
        ApiErrors.Reply(context, StatusCodes.Status422UnprocessableEntity,
            "CONTENT_VALIDATION_FAILED", string.Join(" ", errors));

    private static string Canonical<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static TownDefenseSnapshotView? DeserializeSnapshot(string payload, Guid? deploymentId)
    {
        var snapshot = JsonSerializer.Deserialize<TownDefenseSnapshotView>(payload, JsonOptions);
        return snapshot is null || deploymentId is null
            ? snapshot
            : snapshot with { DeploymentId = deploymentId.Value.ToString("D") };
    }
    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool ValidFaction(string value) => !string.IsNullOrWhiteSpace(value) && FactionPattern.IsMatch(value);
    private static bool ValidPosition(Vector3View? position) => position is not null &&
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        Math.Abs(position.X) <= 10000 && Math.Abs(position.Y) <= 10000 && Math.Abs(position.Z) <= 10000;
    private static string PositionKey(Vector3View position) =>
        $"{Math.Round(position.X * 100, MidpointRounding.AwayFromZero)}:" +
        $"{Math.Round(position.Z * 100, MidpointRounding.AwayFromZero)}";
    private static long TownStake(int baseLevel) => baseLevel >= 10 ? 600 : baseLevel >= 5 ? 300 : 100;
    private static string StakeBand(int baseLevel) => baseLevel >= 10 ? "HIGH" : baseLevel >= 5 ? "RISKY" : "FAIR";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex FactionRegex();

    private static CombatStatsView ScaleTowerCombat(CombatStatsView combat, PlacedTowerView tower) =>
        combat with
        {
            MaxHealth = ScaleStat(combat.MaxHealth, tower.HealthLevel, 10),
            AttackDamage = ScaleStat(combat.AttackDamage, tower.AttackLevel, 10),
            Armor = checked(combat.Armor + tower.ArmorLevel * 5),
            AttackRangeMilli = ScaleStat(combat.AttackRangeMilli, tower.RangeLevel, 5),
            MaxTargets = Math.Clamp(checked(combat.MaxTargets + tower.TargetsLevel), 1, 32),
        };

    private static int ScaleStat(int value, int level, int percent) =>
        checked((int)Math.Min(int.MaxValue, value * (100L + Math.Max(0, level) * percent) / 100L));

    private static CombatStatsView CoreCombat(int baseLevel) => new(
        checked(5000 + Math.Max(1, baseLevel) * 1000), 20 + Math.Max(1, baseLevel) * 2,
        40 + Math.Max(1, baseLevel) * 10, 1000, 10000, 0, string.Empty,
        0, 0, 0, 0, 1);

    private static long CombatPower(CombatStatsView combat) => Math.Max(1,
        checked(combat.MaxHealth / 20L + combat.Armor * 2L +
                combat.AttackDamage * 1000L / Math.Max(1, combat.AttackCooldownMs) +
                combat.AbilityDamage / 5L));

    private sealed record ContentDefinition(string Faction, string Kind, int Capacity,
        long GoldCost, string Version, int Power, CombatStatsView Combat);
    private sealed class LayoutValidation
    {
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
        public int UsedCapacity { get; set; }
        public int MaxCapacity { get; set; }
        public long BuildValue { get; set; }
        public long BasePower { get; set; }
        public List<CombatUnitAuthorityView> DefenseUnits { get; } = [];
        public bool Valid => Errors.Count == 0;
    }
}
