using UnityEngine;

namespace Splice.Data
{
    public enum TurretFireMode
    {
        Projectile, // ยิงกระสุนวิ่ง (มีเวลาเดินทาง, ดาเมจลงตอนถึงเป้า)
        Direct      // ยิงตรงทันที (beam วาบ, ดาเมจลงเลย, ต้องเล็งก่อน)
    }

    // แกน (local ของ Turret Pivot) — Spin = แกนที่หมุนรอบ, Forward = แกนปากกระบอก (ต้องตั้งฉากกัน)
    public enum TurretSpinAxis { X, Y, Z }

    // ค่าปรับทั้งหมดของป้อมปืน (turret + กระสุน) อยู่ที่เดียว — แก้ไฟล์นี้ไฟล์เดียวมีผลทุกป้อมที่ใช้ SO นี้.
    // ส่วน "ชิ้นส่วนใน prefab" (turretPivot/muzzle/beam/animator/particle) ยังลากใน TurretController/ProjectileVisual
    // เพราะ SO อ้าง Transform เฉพาะ prefab ไม่ได้.
    [CreateAssetMenu(fileName = "NewTurret", menuName = "Splice/Turret Definition")]
    public class TurretDefinitionSO : ScriptableObject, IProjectileConfig
    {
        [Header("โหมดยิง")]
        public TurretFireMode fireMode = TurretFireMode.Projectile;

        [Header("เล็ง")]
        [Tooltip("แกน (local ของ Turret Pivot) ที่ 'หมุนรอบ' เพื่อเล็ง — ป้อมตั้งพื้นทั่วไป = Y")]
        public TurretSpinAxis spinAxis = TurretSpinAxis.Y;
        [Tooltip("แกน (local ของ Turret Pivot) ที่เป็น 'ปากกระบอก/ทิศเล็ง' — ต้องตั้งฉากกับ Spin Axis")]
        public TurretSpinAxis forwardAxis = TurretSpinAxis.Z;
        [Tooltip("กลับทิศหมุน ถ้าหมุนหนีเป้า")]
        public bool invertSpin = false;
        [Tooltip("ความเร็วหมุนเล็ง (องศา/วินาที)")]
        public float turnSpeedDegPerSec = 360f;
        [Tooltip("โหมด Direct ต้องเล็งเข้าหาเป้าภายในองศานี้ก่อนยิง (Projectile ยิงได้เลย)")]
        public float aimToleranceDeg = 8f;
        [Tooltip("ไม่มีเป้า → หมุนกลับท่าพัก")]
        public bool returnToRestWhenIdle = true;


        [Header("Projectile (โหมด Projectile)")]
        [Tooltip("prefab กระสุน (ต้องมี ProjectileVisual)")]
        public GameObject projectilePrefab;
        [Tooltip("ความเร็วต้น (unit/วินาที)")]
        public float startSpeed = 12f;
        [Tooltip("ความเร็วปลาย (unit/วินาที) — น้อยกว่าต้น = หน่วง, มากกว่า = เร่ง")]
        public float endSpeed = 14f;
        [Tooltip("เวลาบินขั้นต่ำ (วินาที) — กันกระสุนเร็วจนถึงเป้าในเฟรมเดียว. ใช้ทั้งภาพ+เวลาลงดาเมจ")]
        public float minFlightSeconds = 0.08f;
        [Tooltip("สูงต่ำสุดเหนือจุดยิง (ตอนออก/ถึงเป้า). >0 = กระสุนอยู่สูงกว่า turret เสมอ ไม่ทะลุพื้น")]
        public float minHeight = 0.5f;
        [Tooltip("สูงสุดเหนือจุดยิง (ยอดโค้งกลางทาง). = minHeight → บินราบ / มากกว่า → โค้งแบบธนู")]
        public float maxHeight = 2f;
        [Tooltip("อายุสูงสุดกันค้าง (วินาที)")]
        public float maxLifetime = 6f;

        [Header("Projectile FX (prefab, เว้นว่างได้)")]
        [Tooltip("effect ตอนออกตัว (เกิดที่ปากกระบอก)")]
        public GameObject launchEffect;
        [Tooltip("effect ตอนปะทะเป้า (เกิดที่จุดกระทบ)")]
        public GameObject impactEffect;

        [Header("Direct (โหมด Direct)")]
        [Tooltip("เวลาโชว์เส้น beam วาบ (วินาที)")]
        public float beamFlashSeconds = 0.05f;
        [Tooltip("effect จุดกระทบโหมด Direct (prefab)")]
        public GameObject directImpactEffect;

        // ใช้คำนวณ travelTime ให้ตรงกันทั้ง server (ตั้งเวลาดาเมจ) และ client (ภาพ)
        public float AverageSpeed => Mathf.Max(0.01f, (startSpeed + endSpeed) * 0.5f);

        // IProjectileConfig — ให้ ProjectileVisual อ่านค่าร่วมกับ ProjectileDefinitionSO ได้ (ไม่ต้องย้ายค่า)
        public GameObject ProjectilePrefab => projectilePrefab;
        public float StartSpeed => startSpeed;
        public float EndSpeed => endSpeed;
        public float MinFlightSeconds => minFlightSeconds;
        public float MinHeight => minHeight;
        public float MaxHeight => maxHeight;
        public float MaxLifetime => maxLifetime;
        public GameObject LaunchEffect => launchEffect;
        public GameObject ImpactEffect => impactEffect;
    }
}
