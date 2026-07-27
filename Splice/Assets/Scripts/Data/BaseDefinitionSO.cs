using System;
using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    [Serializable]
    public sealed class BaseLevelDefinition
    {
        [Min(1)] public int level = 1;
        public GameObject prefab;
        [Min(1)] public int maxHealth = 1000;
        [Min(1)] public int defenseCapacity = 100;
        [Min(0)] public int powerRating = 100;
        [Min(0)] public int upgradeGoldCost;
        [Min(0f)] public float upgradeDurationSeconds;
    }

    /// <summary>
    /// Versioned town-core content for one faction. The profile stores only the level; this asset resolves
    /// the matching prefab and authoritative tuning without putting faction-specific rules in a scene.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBaseDefinition", menuName = "Splice/Town Base Definition")]
    public sealed class BaseDefinitionSO : ScriptableObject
    {
        [Tooltip("Stable id inside the owning faction, e.g. town-base.")]
        public string baseId = "town-base";
        public string displayName = "Town Core";
        public List<BaseLevelDefinition> levels = new();

        public BaseLevelDefinition ResolveLevel(int requestedLevel)
        {
            BaseLevelDefinition best = null;
            BaseLevelDefinition lowest = null;
            var requested = Mathf.Max(1, requestedLevel);
            foreach (var definition in levels)
            {
                if (definition == null) continue;
                if (lowest == null || definition.level < lowest.level) lowest = definition;
                if (definition.level > requested) continue;
                if (best == null || definition.level > best.level) best = definition;
            }
            return best ?? lowest;
        }
    }
}
