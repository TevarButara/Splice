using System;
using System.Collections.Generic;
using Splice.Data;
using UnityEngine;

namespace Splice.Base
{
    public sealed class TownRegionPrototypeRule
    {
        public readonly string regionId;
        public readonly string displayName;
        public readonly int goldCost;
        public readonly int additionalDefenseCapacity;
        public readonly string[] prerequisites;

        public TownRegionPrototypeRule(string regionId, string displayName, int goldCost,
            int additionalDefenseCapacity, params string[] prerequisites)
        {
            this.regionId = regionId;
            this.displayName = displayName;
            this.goldCost = goldCost;
            this.additionalDefenseCapacity = additionalDefenseCapacity;
            this.prerequisites = prerequisites ?? Array.Empty<string>();
        }
    }

    public static class TownExpansionPrototypeCatalog
    {
        public const string MapTemplateId = "town-default-v1";
        public const int MapVersion = 1;
        public const string CoreRegionId = "core";

        public static readonly IReadOnlyDictionary<string, TownRegionPrototypeRule> Regions =
            new Dictionary<string, TownRegionPrototypeRule>(StringComparer.Ordinal)
            {
                [CoreRegionId] = new(CoreRegionId, "Town Core", 0, 0),
                ["north"] = new("north", "North Ridge", 500, 20, CoreRegionId),
                ["east"] = new("east", "East Quarter", 500, 20, CoreRegionId),
                ["south"] = new("south", "South Gate", 700, 25, CoreRegionId),
                ["west"] = new("west", "West Quarter", 700, 25, CoreRegionId),
                ["outer-north"] = new("outer-north", "Outer North", 1200, 30, "north"),
            };

        public static bool Contains(Vector3 position, IReadOnlyCollection<string> unlockedRegionIds)
        {
            foreach (var id in unlockedRegionIds)
            {
                if (!TryGeometry(id, out var center, out var size)) continue;
                if (Mathf.Abs(position.x - center.x) <= size.x * 0.5f + 0.001f &&
                    Mathf.Abs(position.z - center.y) <= size.y * 0.5f + 0.001f)
                    return true;
            }
            return false;
        }

        private static bool TryGeometry(string regionId, out Vector2 center, out Vector2 size)
        {
            size = new Vector2(40f, 40f);
            center = regionId switch
            {
                CoreRegionId => Vector2.zero,
                "north" => new Vector2(0f, 40f),
                "east" => new Vector2(40f, 0f),
                "south" => new Vector2(0f, -40f),
                "west" => new Vector2(-40f, 0f),
                "outer-north" => new Vector2(0f, 80f),
                _ => default,
            };
            return Regions.ContainsKey(regionId);
        }
    }

    [Serializable]
    public sealed class TownExpansionState
    {
        public string factionId;
        public string mapTemplateId = "town-default-v1";
        public int mapVersion = 1;
        public long revision;
        public List<string> unlockedRegionIds = new();
    }

    public static class TownExpansionStore
    {
        private const string Prefix = "Splice.TownExpansion.v1.";

        public static TownExpansionState Load(string factionId, TownMapDefinitionSO definition = null)
        {
            TownExpansionState state = null;
            var key = Prefix + (factionId ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(factionId) && PlayerPrefs.HasKey(key))
                state = JsonUtility.FromJson<TownExpansionState>(PlayerPrefs.GetString(key));
            state ??= new TownExpansionState { factionId = factionId };
            state.unlockedRegionIds ??= new List<string>();
            if (!state.unlockedRegionIds.Contains(TownExpansionPrototypeCatalog.CoreRegionId))
                state.unlockedRegionIds.Add(TownExpansionPrototypeCatalog.CoreRegionId);
            if (definition != null)
            {
                state.mapTemplateId = definition.MapId;
                state.mapVersion = definition.MapVersion;
                foreach (var id in definition.InitialRegionIds())
                    if (!state.unlockedRegionIds.Contains(id)) state.unlockedRegionIds.Add(id);
            }
            return state;
        }

        public static bool TryPurchaseLocal(string factionId, string regionId,
            out TownExpansionState state, out string error)
        {
            state = Load(factionId);
            if (!TownExpansionPrototypeCatalog.Regions.TryGetValue(regionId ?? string.Empty, out var region) ||
                region.goldCost <= 0)
            {
                error = "Town region does not exist or cannot be purchased.";
                return false;
            }
            if (state.unlockedRegionIds.Contains(region.regionId))
            {
                error = "Town region is already unlocked.";
                return false;
            }
            foreach (var prerequisite in region.prerequisites)
            {
                if (state.unlockedRegionIds.Contains(prerequisite)) continue;
                error = $"Unlock region '{prerequisite}' first.";
                return false;
            }
            if (!Splice.Core.PlayerWallet.TrySpend(region.goldCost))
            {
                error = $"Not enough Gold. Need {region.goldCost}.";
                return false;
            }
            state.mapTemplateId = TownExpansionPrototypeCatalog.MapTemplateId;
            state.mapVersion = TownExpansionPrototypeCatalog.MapVersion;
            state.unlockedRegionIds.Add(region.regionId);
            state.revision++;
            Save(state);
            error = string.Empty;
            return true;
        }

        public static void Save(TownExpansionState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.factionId)) return;
            state.unlockedRegionIds ??= new List<string>();
            PlayerPrefs.SetString(Prefix + state.factionId, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        public static bool TryPurchaseLocal(string factionId, TownMapDefinitionSO definition,
            string regionId, out TownExpansionState state, out string error)
        {
            state = Load(factionId, definition);
            if (definition == null)
            {
                error = "Town map definition is missing.";
                return false;
            }
            if (!definition.CanPurchase(regionId, state.unlockedRegionIds, out error)) return false;
            var region = definition.GetRegion(regionId);
            if (!Splice.Core.PlayerWallet.TrySpend(region.purchaseGoldCost))
            {
                error = $"Not enough Gold. Need {region.purchaseGoldCost}.";
                return false;
            }
            state.unlockedRegionIds.Add(region.regionId);
            state.revision++;
            Save(state);
            return true;
        }

        public static void DeleteForTests(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return;
            PlayerPrefs.DeleteKey(Prefix + factionId);
        }
    }
}
