using UnityEngine;

namespace Splice.Data
{
    // ชื่อ state/clip ใน Animator ของป้อมปืน — "กองกลาง" แก้ที่เดียว ไม่ hardcode ในโค้ด (คู่ขนานกับ MonsterAnimSetSO).
    // ตอนนี้โค้ดสั่งเล่นเฉพาะ "fire" (ตอนยิง) — ส่วน idle/ทรานสิชันกลับ ให้จัดการใน Animator Controller เอง.
    // เว้นช่องว่าง = โค้ด fallback ไปใช้ชื่อ default. ทำ 1 asset กลางแล้วลากใส่ turret ทุกตัว.
    [CreateAssetMenu(fileName = "TurretAnimSet", menuName = "Splice/Turret Anim Set")]
    public class TurretAnimSetSO : ScriptableObject
    {
        [Tooltip("state ตอนยิง (โค้ด CrossFade อันนี้ทุกครั้งที่ยิง)")]
        public string fire = "fire";
    }
}
