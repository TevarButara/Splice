using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Splice.Data
{
    public enum MapGameMode
    {
        Town,
        AsyncRaid,
        World,
        Forest,
        PvP,
    }

    [CreateAssetMenu(menuName = "Splice/Maps/Map Definition", fileName = "MapDefinition")]
    public class MapDefinitionSO : ScriptableObject
    {
        [SerializeField] private string mapId = "map-default";
        [Min(1), SerializeField] private int mapVersion = 1;
        [SerializeField] private MapGameMode gameMode;
        [SerializeField] private string sceneName;
        [SerializeField] private Vector3 cameraFocus;
        [Min(1f), SerializeField] private float cameraRadius = 40f;

        public string MapId => mapId;
        public int MapVersion => Mathf.Max(1, mapVersion);
        public MapGameMode GameMode => gameMode;
        public string SceneName => sceneName;
        public Vector3 CameraFocus => cameraFocus;
        public float CameraRadius => Mathf.Max(1f, cameraRadius);
    }

    [Serializable]
    public sealed class TownRegionDefinition
    {
        public string regionId = "core";
        public string displayName = "Town Core";
        public Vector2 localCenter;
        public Vector2 size = new(24f, 24f);
        public bool initiallyUnlocked = true;
        [Min(0)] public int purchaseGoldCost;
        [Min(0)] public int additionalDefenseCapacity;
        public List<string> prerequisiteRegionIds = new();

        public bool Contains(Vector3 worldPosition, Vector3 mapOrigin, float footprint)
        {
            var halfPiece = Mathf.Max(0f, footprint) * 0.5f;
            var half = new Vector2(Mathf.Max(0f, size.x) * 0.5f, Mathf.Max(0f, size.y) * 0.5f);
            var local = worldPosition - mapOrigin;
            return Mathf.Abs(local.x - localCenter.x) + halfPiece <= half.x + 0.001f &&
                   Mathf.Abs(local.z - localCenter.y) + halfPiece <= half.y + 0.001f;
        }
    }

    [CreateAssetMenu(menuName = "Splice/Maps/Town Map Definition", fileName = "TownMapDefinition")]
    public sealed class TownMapDefinitionSO : MapDefinitionSO
    {
        [SerializeField] private List<TownRegionDefinition> regions = new();

        public IReadOnlyList<TownRegionDefinition> Regions => regions;

        public TownRegionDefinition GetRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId)) return null;
            for (var i = 0; i < regions.Count; i++)
                if (regions[i] != null &&
                    string.Equals(regions[i].regionId, regionId, StringComparison.Ordinal))
                    return regions[i];
            return null;
        }

        public List<string> InitialRegionIds()
        {
            var result = new List<string>();
            for (var i = 0; i < regions.Count; i++)
                if (regions[i]?.initiallyUnlocked == true &&
                    !string.IsNullOrWhiteSpace(regions[i].regionId))
                    result.Add(regions[i].regionId);
            return result;
        }

        public bool CanPurchase(string regionId, IReadOnlyCollection<string> unlockedRegionIds,
            out string error)
        {
            var region = GetRegion(regionId);
            if (region == null)
            {
                error = "Town region does not exist in this map version.";
                return false;
            }

            if (unlockedRegionIds != null && unlockedRegionIds.Contains(region.regionId))
            {
                error = "Town region is already unlocked.";
                return false;
            }

            foreach (var prerequisite in region.prerequisiteRegionIds)
            {
                if (unlockedRegionIds == null || !unlockedRegionIds.Contains(prerequisite))
                {
                    error = $"Unlock region '{prerequisite}' first.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool ContainsUnlocked(Vector3 position, Vector3 mapOrigin, float footprint,
            IReadOnlyCollection<string> unlockedRegionIds)
        {
            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region == null || string.IsNullOrWhiteSpace(region.regionId)) continue;
                var unlocked = region.initiallyUnlocked ||
                               unlockedRegionIds?.Contains(region.regionId) == true;
                if (unlocked && region.Contains(position, mapOrigin, footprint)) return true;
            }
            return false;
        }

        public Bounds CalculateUnlockedBounds(Vector3 mapOrigin,
            IReadOnlyCollection<string> unlockedRegionIds)
        {
            var hasBounds = false;
            var bounds = new Bounds(mapOrigin, Vector3.zero);
            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region == null || (!region.initiallyUnlocked &&
                    unlockedRegionIds?.Contains(region.regionId) != true)) continue;
                var center = mapOrigin + new Vector3(region.localCenter.x, 0f, region.localCenter.y);
                var regionBounds = new Bounds(center,
                    new Vector3(Mathf.Max(0.01f, region.size.x), 1f, Mathf.Max(0.01f, region.size.y)));
                if (!hasBounds)
                {
                    bounds = regionBounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(regionBounds);
            }
            return bounds;
        }
    }

    public enum WorldNodeKind
    {
        PlayerTown,
        RaidTarget,
        Forest,
        PvP,
        Locked,
    }

    [Serializable]
    public sealed class WorldMapNodeDefinition
    {
        public string nodeId;
        public string displayName;
        public WorldNodeKind kind;
        public Vector2 mapPosition;
        public string destinationScene;
        public string contentId;
        [Min(0)] public int requiredPlayerLevel;
        public List<string> prerequisiteNodeIds = new();
    }

    [CreateAssetMenu(menuName = "Splice/Maps/World Map Definition", fileName = "WorldMapDefinition")]
    public sealed class WorldMapDefinitionSO : MapDefinitionSO
    {
        [SerializeField] private List<WorldMapNodeDefinition> nodes = new();
        public IReadOnlyList<WorldMapNodeDefinition> Nodes => nodes;
    }

    [CreateAssetMenu(menuName = "Splice/Maps/Forest Zone Definition", fileName = "ForestZoneDefinition")]
    public sealed class ForestZoneDefinitionSO : MapDefinitionSO
    {
        [SerializeField] private string zoneId = "forest-01";
        [Min(1), SerializeField] private int encounterDurationSeconds = 60;
        [Min(1), SerializeField] private int monsterCount = 6;
        [Min(0), SerializeField] private int fragmentDropMin = 1;
        [Min(0), SerializeField] private int fragmentDropMax = 3;
        [Min(1), SerializeField] private int fragmentsPerDiamond = 100;
        [Min(0), SerializeField] private int weeklyDiamondCap = 3;

        public string ZoneId => zoneId;
        public int EncounterDurationSeconds => Mathf.Max(1, encounterDurationSeconds);
        public int MonsterCount => Mathf.Max(1, monsterCount);
        public int FragmentDropMin => Mathf.Max(0, fragmentDropMin);
        public int FragmentDropMax => Mathf.Max(FragmentDropMin, fragmentDropMax);
        public int FragmentsPerDiamond => Mathf.Max(1, fragmentsPerDiamond);
        public int WeeklyDiamondCap => Mathf.Max(0, weeklyDiamondCap);
    }
}
