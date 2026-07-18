using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Data
{
    public enum MonsterMovement
    {
        Ground,   // เดินติดพื้นเท่านั้น (y อิงพื้นตามจุด waypoint/Fort)
        Flying    // ลอยเหนือพื้นตาม flightHeight
    }

    public enum MonsterRole
    {
        Warrior,   // เดิน+ตี (พฤติกรรมมาตรฐาน)
        Supporter  // เดิน+ตี + ชาร์จมานาแล้ว cast spell ช่วยพวกฝ่ายเดียวกัน
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

        [Header("Role")]
        [Tooltip("Warrior = เดิน+ตี / Supporter = เดิน+ตี + ชาร์จมานาแล้ว cast spell ช่วยพวก")]
        public MonsterRole role = MonsterRole.Warrior;
        [Tooltip("spell ของ supporter (เว้นว่างถ้าเป็น Warrior) — cast เมื่อมานาเต็ม")]
        public SpellDefinitionSO spell;

        [Header("Ranged (ยิงไกล)")]
        [Tooltip("กระสุนของมอน (obj+FX) — 'ตัวยิงไกล' ใส่, 'ตัวตีประชิด' เว้นว่าง = ตีแบบ melee. ดาเมจลงตอนกระสุนถึงเป้า (เหมือนป้อม)")]
        public ProjectileDefinitionSO projectile;
        [Tooltip("ขนาด footprint (world units) ที่มอนนี้กินบน grid ตอนวางเป็น garrison — grid ปรับขนาดช่องตามค่านี้ (dynamic)")]
        public float footprint = 1f;

        [Tooltip("กิน DefenseCapacity เท่าไหร่ ตอนวางเป็น garrison — เพดานฝ่ายรับผูกกับ base level ไม่ใช่เงิน (architecture §5.10)")]
        public int defenseCapacityCost = 1;
        [Tooltip("ความเร็วตอนบาดเจ็บ (HP ต่ำกว่า injuredHealthFraction) — 0 = ใช้ moveSpeed ปกติ")]
        public float injuredMoveSpeed;

        [Header("Behaviour tuning (ย้ายมาจาก MonsterCharacter — แก้ที่เดียว)")]
        [Tooltip("ระยะ (XZ) ที่ถือว่า 'ถึง' waypoint แล้วเลื่อนไปจุดถัดไป. ⚠️ ต้อง 'น้อยกว่า' Spline Sample Spacing ของ LanePath ไม่งั้นจะข้ามจุดรัวแล้วตัดโค้ง")]
        public float waypointArriveRadius = 0.15f;
        [Tooltip("เดินเยื้องจากเส้นกลางเลนได้ ±เท่านี้ (สุ่มประจำตัวตอนเกิด) — เดินเป็น 'แถบ' ไม่ทับเส้นเดียวกันหมด. 0 = เดินกลางเลนเป๊ะ")]
        public float laneOffsetRange = 0.8f;
        [Tooltip("สุ่มความเร็วเดินต่อตัว ±สัดส่วนนี้ (0.1 = ±10%) — ไม่เดินพร้อมเพรียงเหมือนหุ่นยนต์. 0 = เร็วเท่ากันหมด")]
        public float speedVariancePercent = 0.1f;
        [Tooltip("ระยะที่ถือว่า 'ถึงป้อมแล้ว' → หยุดรุกเข้า core (แยกจาก attackRange — ตั้งเล็กๆ)")]
        public float fortHoldDistance = 1.5f;
        [Tooltip("แวะตีป้อมข้างทางกี่วินาที แล้วเดินต่อ — มอนจะแวะตี 'เฉพาะป้อมที่ยิงมัน' (aggro) ไม่ใช่ทุกป้อมในระยะ. ครบเวลาแล้วเดินต่อ (ช่วงนั้นไม่ aggro ป้อมซ้ำ). 0 = ไม่แวะตีป้อมข้างทางเลย (มุ่งตรง Fort)")]
        public float roadsideEngageSeconds = 4f;
        [Tooltip("HP ต่ำกว่าสัดส่วนนี้ (0-1) → เดินท่า Injured")]
        public float injuredHealthFraction = 0.3f;
        [Tooltip("ความเร็วหันหน้าเข้าหาเป้า (องศา/วินาที)")]
        public float turnSpeedDegPerSec = 720f;

        [Header("Separation (กันมอนกองทับกันตรง Fort)")]
        [Tooltip("รัศมี (XZ) ที่เริ่มดันออกจากมอนชนิดเดียวกัน — ~2x รัศมีตัวมอน. 0 = ปิด")]
        public float separationRadius = 1f;
        [Tooltip("แรงดันแยก เทียบกับความเร็วเดินหน้า (<1 = เดินหน้าชนะแต่ยังกระจาย)")]
        public float separationStrength = 0.6f;
        [Tooltip("ปักหลักที่ Fort: ระยะขยับ/เฟรมต่ำกว่านี้ = หยุดนิ่ง (กันเบียด/สั่น/กระพริบ)")]
        public float settleDeadzone = 0.02f;

        [Header("Animation timing (ตั้งให้ตรงความยาว clip)")]
        [Tooltip("เวลาหยุดหันหน้า+ตี 1 ครั้ง (วินาที) — พอดีความยาว Attack clip. ดาเมจลงตอนจบ swing")]
        public float attackDurationSeconds = 0.6f;
        [Tooltip("เวลาโชว์ท่า Death ก่อนหายไป (วินาที)")]
        public float deathAnimSeconds = 1.5f;
        [Tooltip("เวลาเล่นท่า Landing ตอน spawn (วินาที) ก่อนเริ่มเดิน. 0 = ไม่มี landing เดินทันที")]
        public float landingSeconds = 0f;
        [Tooltip("เวลาค้างท่า Spell ตอน supporter cast (วินาที) — พอดีความยาว Spell clip. 0 = ไม่ค้าง (ข้ามท่า cast)")]
        public float spellCastSeconds = 0.8f;

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
