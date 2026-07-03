using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "NewMonster", menuName = "Splice/Monster Definition")]
    public class MonsterDefinitionSO : ScriptableObject
    {
        public string monsterId;
        public string displayName;
        public int maxHealth;
        public int attackDamage;
        public float attackCooldown;
        public float attackRange = 1f;
        public float moveSpeed = 2f;
        [FormerlySerializedAs("manaCost")] public int goldCost;
        public Sprite icon;
        public GameObject prefab;
    }
}
