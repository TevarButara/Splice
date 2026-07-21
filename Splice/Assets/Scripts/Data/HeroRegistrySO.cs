using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "HeroRegistry", menuName = "Splice/Hero Registry")]
    public class HeroRegistrySO : ScriptableObject
    {
        [SerializeField] private List<HeroDefinitionSO> heroes = new();
        private Dictionary<string, HeroDefinitionSO> byId;

        public IReadOnlyList<HeroDefinitionSO> Heroes => heroes;

        public HeroDefinitionSO Resolve(string heroId)
        {
            EnsureBuilt();
            return string.IsNullOrEmpty(heroId) ? null : byId.GetValueOrDefault(heroId);
        }

        private void OnEnable() => byId = null;
        private void OnValidate() => byId = null;

        private void EnsureBuilt()
        {
            if (byId != null) return;
            byId = new Dictionary<string, HeroDefinitionSO>();
            foreach (var hero in heroes)
            {
                if (hero == null || string.IsNullOrWhiteSpace(hero.heroId)) continue;
                if (byId.ContainsKey(hero.heroId))
                {
                    Debug.LogWarning($"[HeroRegistry] heroId ซ้ำ: '{hero.heroId}'", this);
                    continue;
                }

                byId.Add(hero.heroId, hero);
            }
        }
    }
}
