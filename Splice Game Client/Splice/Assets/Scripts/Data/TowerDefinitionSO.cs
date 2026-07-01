using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "NewTower", menuName = "Splice/Tower Definition")]
    public class TowerDefinitionSO : ScriptableObject
    {
        public string towerId;
        public string displayName;
        public int maxHealth;
        public int attackDamage;
        public float attackRange;
        public float attackCooldown;
        public GameObject prefab;
    }
}
