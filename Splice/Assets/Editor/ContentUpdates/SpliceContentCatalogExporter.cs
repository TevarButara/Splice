#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Splice.ContentUpdates;
using Splice.Data;
using Splice.Editor.Validation;
using Splice.Validation;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.ContentUpdates
{
    [Serializable]
    public sealed class SpliceHeroCombatCatalog
    {
        public int maxHealth;
        public int armor;
        public int attackDamage;
        public int attackCooldownMs;
        public int attackRangeMilli;
        public int moveSpeedMilli;
        public string abilityId;
        public int abilityDamage;
        public int abilityCooldownMs;
        public int abilityCastRangeMilli;
        public int abilityRadiusMilli;
    }

    [Serializable]
    public sealed class SpliceContentCatalogItem
    {
        public string contentId;
        public string factionId;
        public string contentKind;
        public int defenseCapacityCost;
        public long goldCost;
        public int raidPower;
        public SpliceHeroCombatCatalog heroCombat;
        public string serverContentVersion;
        public string addressablesLabel;
        public string address;
        public bool backendAuthoritative;
    }

    [Serializable]
    public sealed class SpliceContentCatalogDocument
    {
        public int schemaVersion = 1;
        public string liveContentVersion = LiveContentRuntime.EmbeddedContentVersion;
        public string serverContentVersion = SpliceContentCatalogExporter.ServerContentVersion;
        public string sourceSha256;
        public List<SpliceContentCatalogItem> items = new();
    }

    public static class SpliceContentCatalogExporter
    {
        public const string ServerContentVersion = "content-c4c1-v1";
        public const string CatalogRelativePath = "Backend/content/generated/splice-content-catalog.json";
        public const string SqlRelativePath = "Backend/database/seeds/002_splice_content_catalog.generated.sql";
        public const string ExportMenu = "Splice/Live Content/2. Validate + Export Backend Catalog";

        [MenuItem(ExportMenu, priority = 1801)]
        public static void ExportFromMenu()
        {
            var report = SpliceContentValidatorMenu.ValidateProject(false, false);
            if (!report.IsValid) throw new InvalidOperationException(report.DetailedSummary());
            ExportProject();
        }

        public static void ExportProject()
        {
            var document = BuildDocument();
            var json = JsonUtility.ToJson(document, true) + Environment.NewLine;
            var sql = BuildSql(document);
            WriteRepositoryFile(CatalogRelativePath, json);
            WriteRepositoryFile(SqlRelativePath, sql);
            AssetDatabase.Refresh();
            Debug.Log($"[LiveContent] Exported {document.items.Count} catalog items; " +
                      $"source {document.sourceSha256}.");
        }

        public static SpliceContentCatalogDocument BuildDocument()
        {
            var byId = new Dictionary<string, SpliceContentCatalogItem>(StringComparer.Ordinal);
            foreach (var faction in LoadAll<FactionSO>().OrderBy(f => f.factionId, StringComparer.Ordinal))
            {
                if (faction == null || string.IsNullOrWhiteSpace(faction.factionId)) continue;
                foreach (var tower in faction.towers)
                {
                    if (tower == null || string.IsNullOrWhiteSpace(tower.towerId)) continue;
                    Add(byId, Item(FactionRegistrySO.TowerId(faction, tower), faction.factionId,
                        "TOWER", tower.defenseCapacityCost, tower.goldCost));
                }
                foreach (var card in faction.cards.Concat(faction.minerCards))
                {
                    if (card == null || string.IsNullOrWhiteSpace(card.cardId)) continue;
                    if (card.cardType == CardType.Monster && card.linkedMonster != null)
                        Add(byId, Item(FactionRegistrySO.CardId(faction, card), faction.factionId,
                            "GARRISON", card.linkedMonster.defenseCapacityCost, card.goldCost));
                    else if (card.cardType == CardType.Miner && card.linkedMiner != null)
                        Add(byId, Item(FactionRegistrySO.CardId(faction, card), faction.factionId,
                            "MINER", 0, card.goldCost));
                }
            }

            foreach (var hero in LoadAll<HeroDefinitionSO>().OrderBy(h => h.heroId, StringComparer.Ordinal))
            {
                if (hero == null || string.IsNullOrWhiteSpace(hero.heroId)) continue;
                Add(byId, new SpliceContentCatalogItem
                {
                    contentId = "hero/" + hero.heroId,
                    factionId = string.Empty,
                    contentKind = "HERO",
                    raidPower = HeroRaidPower(hero),
                    heroCombat = HeroCombat(hero),
                    serverContentVersion = ServerContentVersion,
                    addressablesLabel = "hero/" + hero.heroId,
                    address = "hero/" + hero.heroId,
                    backendAuthoritative = true,
                });
            }

            var document = new SpliceContentCatalogDocument
            {
                items = byId.Values.OrderBy(item => item.contentId, StringComparer.Ordinal)
                    .ThenBy(item => item.contentKind, StringComparer.Ordinal).ToList(),
            };
            document.sourceSha256 = SourceHash(document.items);
            return document;
        }

        public static string CurrentJson() => JsonUtility.ToJson(BuildDocument(), true) + Environment.NewLine;

        public static void ValidateGenerated(ContentValidationReport report)
        {
            var path = RepositoryPath(CatalogRelativePath);
            if (!File.Exists(path))
            {
                report.Error("CONTENT_CATALOG_EXPORT_MISSING",
                    "Generated backend content catalog is missing. Run Splice/Live Content/2.");
                return;
            }
            if (!string.Equals(File.ReadAllText(path), CurrentJson(), StringComparison.Ordinal))
                report.Error("CONTENT_CATALOG_EXPORT_STALE",
                    "Generated backend content catalog is stale. Re-export before building.");
        }

        private static SpliceContentCatalogItem Item(string id, string factionId, string kind,
            int capacity, long goldCost) => new()
        {
            contentId = id,
            factionId = factionId,
            contentKind = kind,
            defenseCapacityCost = Math.Max(0, capacity),
            goldCost = Math.Max(0, goldCost),
            raidPower = kind == "GARRISON"
                ? Math.Max(1, checked((int)Math.Min(int.MaxValue, goldCost * 10L + capacity * 25L)))
                : 0,
            serverContentVersion = ServerContentVersion,
            addressablesLabel = "faction/" + factionId,
            address = id,
            backendAuthoritative = true,
        };

        private static void Add(IDictionary<string, SpliceContentCatalogItem> target,
            SpliceContentCatalogItem item)
        {
            var key = item.contentKind + "\n" + item.contentId;
            if (!target.TryAdd(key, item))
                throw new InvalidOperationException(
                    $"Duplicate exported content identity: {item.contentKind}/{item.contentId}");
        }

        private static string SourceHash(IEnumerable<SpliceContentCatalogItem> items)
        {
            var canonical = new StringBuilder();
            foreach (var item in items)
                canonical.Append(item.contentId).Append('|').Append(item.factionId).Append('|')
                    .Append(item.contentKind).Append('|').Append(item.defenseCapacityCost).Append('|')
                    .Append(item.goldCost).Append('|').Append(item.raidPower).Append('|')
                    .Append(CombatJson(item)).Append('|')
                    .Append(item.addressablesLabel).Append('\n');
            return LiveContentManifestValidator.Sha256(canonical.ToString());
        }

        private static string BuildSql(SpliceContentCatalogDocument document)
        {
            var rows = document.items.Where(item => item.backendAuthoritative).ToArray();
            var builder = new StringBuilder("-- Generated by Unity Splice Content Catalog Exporter. Do not edit by hand.\nBEGIN;\n");
            if (rows.Length > 0)
            {
                builder.Append("INSERT INTO splice.content_definitions\n")
                    .Append("  (content_id, faction_id, content_kind, defense_capacity_cost, gold_cost, raid_power, enabled, content_version, combat_payload) VALUES\n");
                for (var i = 0; i < rows.Length; i++)
                {
                    var item = rows[i];
                    builder.Append("  ('").Append(Sql(item.contentId)).Append("','")
                        .Append(Sql(item.factionId)).Append("','").Append(Sql(item.contentKind)).Append("',")
                        .Append(item.defenseCapacityCost).Append(',').Append(item.goldCost).Append(',')
                        .Append(item.raidPower)
                        .Append(",true,'").Append(Sql(item.serverContentVersion)).Append("','")
                        .Append(Sql(CombatJson(item))).Append("'::jsonb)")
                        .Append(i == rows.Length - 1 ? "\n" : ",\n");
                }
                builder.Append("ON CONFLICT (content_id, content_kind) DO UPDATE SET\n")
                    .Append("  faction_id=EXCLUDED.faction_id,\n")
                    .Append("  defense_capacity_cost=EXCLUDED.defense_capacity_cost, gold_cost=EXCLUDED.gold_cost,\n")
                    .Append("  raid_power=EXCLUDED.raid_power,\n")
                    .Append("  combat_payload=EXCLUDED.combat_payload,\n")
                    .Append("  enabled=true, content_version=EXCLUDED.content_version;\n");
            }
            builder.Append("COMMIT;\n");
            return builder.ToString();
        }

        private static string Sql(string value) => (value ?? string.Empty).Replace("'", "''");

        private static SpliceHeroCombatCatalog HeroCombat(HeroDefinitionSO hero)
        {
            var ability = hero.tacticalAbility;
            return new SpliceHeroCombatCatalog
            {
                maxHealth = hero.maxHealth,
                armor = hero.armor,
                attackDamage = hero.attackDamage,
                attackCooldownMs = Milliseconds(hero.attackCooldown),
                attackRangeMilli = Milli(hero.attackRange),
                moveSpeedMilli = Milli(hero.moveSpeed),
                abilityId = ability != null ? ability.abilityId : string.Empty,
                abilityDamage = ability != null ? ability.damage : 0,
                abilityCooldownMs = ability != null ? Milliseconds(ability.cooldownSeconds) : 0,
                abilityCastRangeMilli = ability != null ? Milli(ability.castRange) : 0,
                abilityRadiusMilli = ability != null ? Milli(ability.effectRadius) : 0,
            };
        }

        private static int HeroRaidPower(HeroDefinitionSO hero)
        {
            var cooldownMs = Math.Max(1, Milliseconds(hero.attackCooldown));
            var abilityPower = hero.tacticalAbility != null ? hero.tacticalAbility.damage / 5 : 0;
            return Math.Max(1, checked(hero.maxHealth / 20 + hero.armor * 2 +
                                       hero.attackDamage * 1000 / cooldownMs + abilityPower));
        }

        private static string CombatJson(SpliceContentCatalogItem item) =>
            item.heroCombat == null ? "{}" : JsonUtility.ToJson(item.heroCombat);

        private static int Milliseconds(float seconds) =>
            Math.Max(1, Mathf.RoundToInt(seconds * 1000f));

        private static int Milli(float value) => Mathf.RoundToInt(value * 1000f);

        private static List<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null).ToList();

        private static void WriteRepositoryFile(string relativePath, string content)
        {
            var path = RepositoryPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string RepositoryPath(string relativePath)
        {
            var unityRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                            throw new InvalidOperationException("Unity project root was not found.");
            var repositoryRoot = Directory.GetParent(unityRoot)?.FullName ??
                                 throw new InvalidOperationException("Repository root was not found.");
            return Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
