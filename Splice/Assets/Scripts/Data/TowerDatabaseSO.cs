using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    // Catalog of towers the Fort/Defender side can build (architecture 5.6). Mirrors CardDatabaseSO:
    // the server resolves a client-sent towerId to its definition here before spawning.
    [CreateAssetMenu(fileName = "TowerDatabase", menuName = "Splice/Tower Database")]
    public class TowerDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<TowerDefinitionSO> towers = new();

        private Dictionary<string, TowerDefinitionSO> lookup;

        public IReadOnlyList<TowerDefinitionSO> AllTowers => towers;

        public TowerDefinitionSO GetById(string towerId)
        {
            lookup ??= BuildLookup();
            return lookup.GetValueOrDefault(towerId);
        }

        private Dictionary<string, TowerDefinitionSO> BuildLookup()
        {
            var map = new Dictionary<string, TowerDefinitionSO>();
            foreach (var tower in towers)
            {
                if (tower != null) map[tower.towerId] = tower;
            }
            return map;
        }
    }
}
