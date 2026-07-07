using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Data
{
    public enum MonsterMovement
    {
        Ground,   // เดินติดพื้นเท่านั้น (y อิงพื้นตามจุด waypoint/Fort)
        Flying    // ลอยเหนือพื้นตาม flightHeight
    }

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
        [Tooltip("ความเร็วตอนบาดเจ็บ (HP ต่ำกว่า injuredHealthFraction ในตัวมอน) — 0 = ใช้ moveSpeed ปกติ")]
        public float injuredMoveSpeed;

        [Header("Movement type")]
        [Tooltip("Ground = เดินติดพื้น / Flying = บินเหนือพื้น")]
        public MonsterMovement movement = MonsterMovement.Ground;
        [Tooltip("ความสูงเหนือพื้นตอนบิน (เฉพาะ Flying — Ground ไม่ใช้ค่านี้). ⚠️ ตั้ง attackRange ≥ flightHeight ถ้าอยากให้ตัวบินตี Fort บนพื้นได้")]
        public float flightHeight = 2f;

        [Tooltip("เวลา 'เกิด' (วินาที) หลังกดสั่งสร้างจนโผล่ในเลน — ต่อตัว. คิวในเลนเดียวกันสร้างทีละตัวตามค่านี้")]
        public float buildTimeSeconds = 2f;
        [FormerlySerializedAs("manaCost")] public int goldCost;
        public Sprite icon;
        public GameObject prefab;
    }
}
