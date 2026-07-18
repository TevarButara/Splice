using UnityEngine;

namespace Splice.Data
{
    // config ร่วมของกระสุน cosmetic — ใช้ได้ทั้งป้อม (TurretDefinitionSO) และมอน (ProjectileDefinitionSO)
    // ProjectileVisual อ่านค่าผ่าน interface นี้ จึงไม่ผูกกับ SO ชนิดใดชนิดหนึ่ง (reuse ได้)
    public interface IProjectileConfig
    {
        GameObject ProjectilePrefab { get; }
        float StartSpeed { get; }
        float EndSpeed { get; }
        float AverageSpeed { get; }
        float MinFlightSeconds { get; }
        float MinHeight { get; }
        float MaxHeight { get; }
        float MaxLifetime { get; }
        GameObject LaunchEffect { get; }   // start effect (ตอนออกตัว)
        GameObject ImpactEffect { get; }   // ตอนปะทะ
    }
}
