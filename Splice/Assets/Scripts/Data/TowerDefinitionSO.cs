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

        [Tooltip("ทองที่ใช้วางป้อมนี้ (ฝั่ง Fort/Defender) — ใช้เป็นฐานคำนวณค่าซ่อม/คืนเงินตอนทำลายด้วย")]
        public int goldCost;

        [Header("Upgrade chain (ต่อ tier)")]
        [Tooltip("ป้อม gen ถัดไปที่อัพเกรดไปเป็น — เว้นว่าง = level สูงสุดแล้ว")]
        public TowerDefinitionSO nextTier;
        [Tooltip("ทองที่ใช้อัพเกรดจาก tier นี้ → nextTier (ราคาตายตัวต่อ level)")]
        public int upgradeCost;

        public GameObject prefab;
    }
}
