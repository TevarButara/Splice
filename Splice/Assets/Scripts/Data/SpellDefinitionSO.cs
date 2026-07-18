using UnityEngine;

namespace Splice.Data
{
    public enum SpellEffect
    {
        Heal,    // ฟื้น HP
        Shield,  // สร้างโล่ absorb (มีเวลา)
        Buff     // เสริมพลัง attack/moveSpeed/attackSpeed ชั่วคราว
    }

    public enum SpellTargeting
    {
        SingleAlly, // เป้าเดียว (Heal = เลือดน้อยสุด / อื่นๆ = ใกล้สุด)
        AreaAllies  // ทุกตัวฝ่ายเดียวกันในรัศมี
    }

    // สเปลของ supporter (architecture 5.8+). data-driven: 1 spell/supporter, reuse ได้.
    // มานาเต็ม (100) → cast 1 ครั้ง → มานารีเซ็ต → ชาร์จใหม่จากการโจมตี (manaPerAttack ต่อครั้ง)
    [CreateAssetMenu(fileName = "NewSpell", menuName = "Splice/Spell Definition")]
    public class SpellDefinitionSO : ScriptableObject
    {
        public string displayName;

        [Header("ชนิด")]
        public SpellEffect effect = SpellEffect.Heal;
        public SpellTargeting targeting = SpellTargeting.AreaAllies;
        [Tooltip("รัศมีหาเป้าฝ่ายเดียวกัน (world units)")]
        public float radius = 4f;
        [Tooltip("โชว์เส้นวง area ของสเปล (สีม่วง) ใน Scene view ตลอดเวลา — เฉพาะ targeting = AreaAllies. " +
                 "ไม่ติ๊ก = โชว์เฉพาะตอนเลือกตัวมอน. เป็น Gizmo (Game view ต้องเปิดปุ่ม Gizmos)")]
        public bool showAreaGizmo = false;

        [Header("มานา")]
        [Tooltip("มานาที่ได้ต่อการโจมตี 1 ครั้ง (มานาเต็ม = 100 → cast). เช่น 25 = ตี 4 ครั้งเต็ม")]
        public float manaPerAttack = 25f;

        [Header("Heal (effect = Heal)")]
        public int healAmount = 20;

        [Header("Shield/โล่ (effect = Shield)")]
        [Tooltip("ปริมาณ absorb ที่บวกให้ (refresh+เอาแรงสุด ไม่บวกซ้อน)")]
        public int shieldAmount = 30;
        [Tooltip("โล่อยู่ได้กี่วินาที")]
        public float shieldDuration = 5f;

        [Header("Buff/เสริมพลัง (effect = Buff — refresh + เอาแรงสุด)")]
        [Tooltip("คูณพลังโจมตี (1 = ไม่เปลี่ยน, 1.5 = +50%)")]
        public float attackMultiplier = 1f;
        [Tooltip("คูณความเร็วเดิน (1.5 = เดินเร็วขึ้น 50%)")]
        public float moveSpeedMultiplier = 1f;
        [Tooltip("คูณความเร็วโจมตี = ลด cooldown (1.5 = ตีถี่ขึ้น 50%)")]
        public float attackSpeedMultiplier = 1f;
        [Tooltip("ระยะเวลา buff (วินาที) แล้วค่อยกลับปกติ")]
        public float buffDuration = 5f;
    }
}
