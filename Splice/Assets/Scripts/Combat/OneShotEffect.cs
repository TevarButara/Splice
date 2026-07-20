using UnityEngine;

namespace Splice.Combat
{
    // spawn effect แบบเล่นครั้งเดียวแล้วหายเอง (impact / launch / muzzle flash ฯลฯ)
    // คำนวณอายุจาก ParticleSystem + AudioSource ที่อยู่ใน prefab จริง → ไม่ต้องตั้งเลขเอง และไม่รั่วสะสม
    public static class OneShotEffect
    {
        public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float fallbackLifetime = 3f)
        {
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, position, rotation);
            Object.Destroy(go, Mathf.Max(0.05f, LifetimeOf(go, fallbackLifetime)));
        }

        // เกาะติดตัวละคร (เป็นลูกของ parent) — effect ขยับตามตัว. parent หายก่อน = effect หายตามอัตโนมัติ
        public static void SpawnAttached(GameObject prefab, Transform parent, float fallbackLifetime = 3f)
        {
            if (prefab == null || parent == null) return;
            var go = Object.Instantiate(prefab, parent.position, parent.rotation, parent);
            Object.Destroy(go, Mathf.Max(0.05f, LifetimeOf(go, fallbackLifetime)));
        }

        // อายุ = ตัวที่จบช้าสุดใน prefab. particle ที่ตั้ง loop ไว้จะจบเองไม่ได้ → ข้ามไปใช้ fallback แทน
        private static float LifetimeOf(GameObject go, float fallback)
        {
            var longest = 0f;

            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop) continue;
                var speed = main.simulationSpeed > 0.0001f ? main.simulationSpeed : 1f;
                var life = (main.duration + MaxOf(main.startLifetime) + MaxOf(main.startDelay)) / speed;
                if (life > longest) longest = life;
            }

            foreach (var a in go.GetComponentsInChildren<AudioSource>(true))
            {
                if (a.clip == null || a.loop) continue;
                if (a.clip.length > longest) longest = a.clip.length;
            }

            return longest > 0f ? longest : fallback;
        }

        // MinMaxCurve อ่านค่าสูงสุดตามโหมดที่ตั้งไว้ (constant / two constants / curve)
        private static float MaxOf(ParticleSystem.MinMaxCurve c) => c.mode switch
        {
            ParticleSystemCurveMode.Constant => c.constant,
            ParticleSystemCurveMode.TwoConstants => c.constantMax,
            _ => c.curveMultiplier
        };
    }
}
