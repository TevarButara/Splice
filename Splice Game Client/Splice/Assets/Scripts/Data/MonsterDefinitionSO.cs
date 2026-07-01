using UnityEngine;

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
        public int manaCost;
        public Sprite icon;
        public GameObject prefab;
    }
}
