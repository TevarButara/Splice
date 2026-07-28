using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Splice.Data
{
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
            return regions.Find(region => region != null &&
                string.Equals(region.regionId, regionId, StringComparison.Ordinal));
        }

        public List<string> InitialRegionIds() => regions
            .Where(region => region?.initiallyUnlocked == true &&
                             !string.IsNullOrWhiteSpace(region.regionId))
            .Select(region => region.regionId).ToList();

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
                if (unlockedRegionIds != null && unlockedRegionIds.Contains(prerequisite)) continue;
                error = $"Unlock region '{prerequisite}' first.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public bool ContainsUnlocked(Vector3 position, Vector3 mapOrigin, float footprint,
            IReadOnlyCollection<string> unlockedRegionIds)
        {
            foreach (var region in regions)
            {
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
            foreach (var region in regions)
            {
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
}
