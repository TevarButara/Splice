using Splice.Data;
using UnityEngine;

namespace Splice.Combat
{
    // กระสุน "ภาพล้วน" (ไม่ใช่ NetworkObject). ทุก client spawn ตัวเองผ่าน TurretController.FireClientRpc —
    // ดาเมจคิดที่ server (TowerCharacter) เท่านั้น ตัวนี้แค่บิน+เล่น FX. ค่าปรับ (ความเร็ว/ความสูง/effect) มาจาก
    // TurretDefinitionSO ที่ turret ส่งเข้ามาตอน Launch → ammo prefab เหลือแค่ mesh + particle ในตัว ไม่ต้องตั้งเลข.
    // การบิน time-boxed: ถึงเป้าใน travelTime ที่ turret คำนวณ (server คำนวณเลขเดียวกันไปตั้งเวลาลงดาเมจ ภาพ impact จึงตรง).
    public class ProjectileVisual : MonoBehaviour
    {
        [Header("Particle ในตัว ammo (scene ref — SO อ้างไม่ได้, เว้นได้)")]
        [Tooltip("particle หัว/ปากกระบอก ติดกับกระสุนตลอดการบิน")]
        [SerializeField] private ParticleSystem headParticle;
        [Tooltip("particle ท้าย/trail ระหว่างบิน")]
        [SerializeField] private ParticleSystem trailParticle;

        private IProjectileConfig def;
        private Transform body;           // root ของ ammo (ขยับทั้งก้อน เผื่อ script อยู่บน child)
        private Transform target;         // null ได้ (เป้าหายก่อน) → ใช้ fallbackPoint
        private Vector3 fromPoint;
        private Vector3 fallbackPoint;
        private float travelTime;
        private float elapsed;
        private bool flying;

        private float MinHeight => def != null ? def.MinHeight : 0.5f;
        private float MaxHeight => def != null ? def.MaxHeight : 2f;
        private float MaxLifetime => def != null ? def.MaxLifetime : 6f;

        // spawn + launch กระสุน (ใช้ร่วมทั้งป้อมและมอน). from = จุดยิง, target = เกาะเป้า (null ได้), fallback = ตำแหน่งเป้าตอนยิง
        public static ProjectileVisual Spawn(IProjectileConfig config, Vector3 from, Transform target, Vector3 fallback, float travelTime)
        {
            if (config == null || config.ProjectilePrefab == null) return null;
            var go = Instantiate(config.ProjectilePrefab, from, Quaternion.identity);
            var proj = go.GetComponentInChildren<ProjectileVisual>();
            if (proj == null)
            {
                Debug.LogWarning($"[Projectile] ammo prefab '{config.ProjectilePrefab.name}' ไม่มี ProjectileVisual — กระสุนจะไม่วิ่ง");
                Destroy(go);
                return null;
            }
            proj.Launch(from, target, fallback, travelTime, config);
            return proj;
        }

        // flightTime คำนวณจากผู้ยิง (distance/avgSpeed) — บังคับให้ตรงกับเวลาดาเมจฝั่ง server. config = ค่าจาก SO
        public void Launch(Vector3 from, Transform targetTransform, Vector3 fallback, float flightTime, IProjectileConfig config)
        {
            def = config;
            body = transform.root;   // spawn แบบไม่มี parent → root = ammo ทั้งก้อน
            fromPoint = from;
            target = targetTransform;
            fallbackPoint = fallback;
            travelTime = Mathf.Max(0.02f, flightTime);
            elapsed = 0f;
            flying = true;
            body.position = from;

            if (def != null) OneShotEffect.Spawn(def.LaunchEffect, from, body.rotation);   // หายเองเมื่อเล่นจบ
            if (headParticle != null) headParticle.Play();
            if (trailParticle != null) trailParticle.Play();
        }

        private Vector3 TargetPoint => target != null ? target.position : fallbackPoint;

        private void Update()
        {
            if (!flying) return;

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / travelTime);
            var eased = SpeedEased(t);          // แปลงเวลา → สัดส่วนระยะ (ไล่ start→end speed, คงเวลารวม)

            var dest = TargetPoint;             // homing: เกาะตำแหน่งเป้าล่าสุด (แนวราบ)
            var pos = Vector3.Lerp(fromPoint, dest, eased);

            // ความสูง: อ้างอิงเหนือจุดยิงเสมอ (ไม่เอา y ของเป้า) → ไม่ดิ่งทะลุพื้น. โค้ง min→max→min
            var h = Mathf.Lerp(MinHeight, MaxHeight, Mathf.Sin(eased * Mathf.PI));
            pos.y = fromPoint.y + h;

            var dir = pos - body.position;
            body.position = pos;
            if (dir.sqrMagnitude > 0.0000001f) body.rotation = Quaternion.LookRotation(dir.normalized);

            if (t >= 1f || elapsed >= MaxLifetime) Impact();
        }

        // t เชิงเวลา → สัดส่วนระยะที่บินได้ โดยความเร็วไล่เชิงเส้น start→end (พื้นที่ใต้กราฟ normalize ให้ s(1)=1)
        private float SpeedEased(float t)
        {
            if (def == null) return t;
            var a = def.StartSpeed;
            var b = def.EndSpeed;
            var denom = (a + b) * 0.5f;
            if (denom < 0.0001f) return t;
            return (a * t + 0.5f * (b - a) * t * t) / denom;
        }

        private void Impact()
        {
            flying = false;
            if (def != null) OneShotEffect.Spawn(def.ImpactEffect, body.position, Quaternion.identity);   // หายเองเมื่อเล่นจบ
            if (trailParticle != null) trailParticle.Stop();
            Destroy(body.gameObject);   // ทำลายทั้งก้อน (เผื่อ script อยู่บน child)
        }
    }
}
