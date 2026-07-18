using UnityEngine;

namespace Splice.Data
{
    // กระสุนของมอน (และ reuse กับอย่างอื่นได้) — ตัวยิงไกลใส่ SO นี้ใน MonsterDefinitionSO, ตัวตีประชิดเว้นว่าง.
    // ดาเมจคิด/ลงที่ server (ตอนกระสุนถึงเป้า), ตัวกระสุนเป็น visual ล้วน (ProjectileVisual). ใช้ interface ร่วมกับป้อม.
    [CreateAssetMenu(fileName = "NewProjectile", menuName = "Splice/Projectile Definition")]
    public class ProjectileDefinitionSO : ScriptableObject, IProjectileConfig
    {
        [Tooltip("prefab กระสุน (obj) — ต้องมี ProjectileVisual (trail/head particle อยู่บน prefab นี้)")]
        public GameObject projectilePrefab;
        [Tooltip("ความเร็วต้น (unit/วินาที)")]
        public float startSpeed = 12f;
        [Tooltip("ความเร็วปลาย (unit/วินาที) — น้อยกว่าต้น = หน่วง, มากกว่า = เร่ง")]
        public float endSpeed = 14f;
        [Tooltip("เวลาบินขั้นต่ำ (วินาที) — กันเร็วจนถึงเป้าในเฟรมเดียว. ใช้ทั้งภาพ+เวลาลงดาเมจ")]
        public float minFlightSeconds = 0.08f;
        [Tooltip("สูงต่ำสุดเหนือจุดยิง (ตอนออก/ถึงเป้า). >0 = ไม่ทะลุพื้น")]
        public float minHeight = 0.5f;
        [Tooltip("สูงสุดเหนือจุดยิง (ยอดโค้งกลางทาง). = minHeight → บินราบ / มากกว่า → โค้งแบบธนู")]
        public float maxHeight = 2f;
        [Tooltip("อายุสูงสุดกันค้าง (วินาที)")]
        public float maxLifetime = 6f;

        [Header("FX (prefab, เว้นว่างได้)")]
        [Tooltip("start effect — เกิดตอนออกตัวที่ปากกระบอก")]
        public GameObject launchEffect;
        [Tooltip("impact effect — เกิดตอนปะทะเป้า")]
        public GameObject impactEffect;

        public GameObject ProjectilePrefab => projectilePrefab;
        public float StartSpeed => startSpeed;
        public float EndSpeed => endSpeed;
        public float AverageSpeed => Mathf.Max(0.01f, (startSpeed + endSpeed) * 0.5f);
        public float MinFlightSeconds => minFlightSeconds;
        public float MinHeight => minHeight;
        public float MaxHeight => maxHeight;
        public float MaxLifetime => maxLifetime;
        public GameObject LaunchEffect => launchEffect;
        public GameObject ImpactEffect => impactEffect;
    }
}
