using UnityEngine;

namespace Splice.Data
{
    // The five per-stat things a tower can be upgraded in (architecture 5.6).
    public enum TowerStat
    {
        Attack,
        Health,
        Armor,
        Range,
        Targets
    }

    // Upgrade curve for one stat: how much it gains per level, how many levels, and an increasing cost.
    [System.Serializable]
    public struct StatUpgrade
    {
        [Tooltip("จำนวนที่เพิ่มต่อ 1 ระดับ")]
        public float amountPerLevel;
        [Tooltip("อัพได้สูงสุดกี่ระดับ (0 = อัพสเตตัสนี้ไม่ได้)")]
        public int maxLevel;
        [Tooltip("ราคาอัพระดับแรก")]
        public int baseCost;
        [Tooltip("ตัวคูณราคาต่อระดับ (เช่น 1.5 = แพงขึ้น 50% ทุกระดับ)")]
        public float costGrowthPerLevel;

        // Cost to go from `currentLevel` -> `currentLevel+1`: baseCost × growth^currentLevel.
        public int CostForLevel(int currentLevel)
        {
            var growth = costGrowthPerLevel <= 0f ? 1f : costGrowthPerLevel;
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * Mathf.Pow(growth, currentLevel)));
        }
    }

    [CreateAssetMenu(fileName = "NewTower", menuName = "Splice/Tower Definition")]
    public class TowerDefinitionSO : ScriptableObject
    {
        public string towerId;
        public string displayName;
        public int maxHealth;
        public int attackDamage;
        public float attackRange;
        public float attackCooldown;
        [Tooltip("เกราะฐาน — ลดดาเมจแบบ % : ดาเมจจริง = dmg × 100/(100+armor)")]
        public int armor;
        [Tooltip("จำนวนเป้าที่ยิงพร้อมกันฐาน (1 = ยิงทีละตัว)")]
        public int maxTargets = 1;
        [Tooltip("เวลาสร้าง (วินาที) หลังกดวาง — ป้อมโผล่ทันทีแต่ 'ยังยิงไม่ได้' จนสร้างเสร็จ (คุม balance ไม่ให้วางง่ายเกิน). 0 = ใช้งานได้ทันที")]
        public float buildTimeSeconds = 3f;

        [Tooltip("ทองที่ใช้วางป้อมนี้ (ฝั่ง Fort/Defender) — ใช้เป็นฐานคำนวณค่าซ่อม/คืนเงินตอนทำลายด้วย")]
        public int goldCost;

        [Header("Per-stat upgrades (อัพแยกทีละสเตตัส)")]
        public StatUpgrade attackUpgrade;
        public StatUpgrade healthUpgrade;
        public StatUpgrade armorUpgrade;
        public StatUpgrade rangeUpgrade;
        public StatUpgrade targetsUpgrade;

        [Header("Upgrade chain (tier evolution — คนละระบบกับ per-stat)")]
        [Tooltip("ป้อม gen ถัดไปที่อัพเกรดไปเป็น — เว้นว่าง = ไม่มี tier ถัดไป")]
        public TowerDefinitionSO nextTier;
        [Tooltip("ทองที่ใช้อัพเกรดจาก tier นี้ → nextTier (ราคาตายตัวต่อ level)")]
        public int upgradeCost;

        public GameObject prefab;

        public StatUpgrade UpgradeFor(TowerStat stat)
        {
            return stat switch
            {
                TowerStat.Attack => attackUpgrade,
                TowerStat.Health => healthUpgrade,
                TowerStat.Armor => armorUpgrade,
                TowerStat.Range => rangeUpgrade,
                TowerStat.Targets => targetsUpgrade,
                _ => default
            };
        }
    }
}
