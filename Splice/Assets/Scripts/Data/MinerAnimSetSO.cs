using UnityEngine;

namespace Splice.Data
{
    // ชื่อ state/clip ใน Animator ของ miner — "กองกลาง" แก้ที่เดียว ไม่ hardcode (เหมือน MonsterAnimSetSO).
    // เพิ่ม Farming = ท่าตอนเก็บเกี่ยว/ขุดบ่อทอง. Landing เล่นตอน spawn, Dance เผื่อไว้.
    // เว้นช่องว่าง = โค้ด fallback ไปใช้ชื่อ default. ทำ 1 asset กลางแล้วลากใส่ miner ทุกตัว.
    [CreateAssetMenu(fileName = "MinerAnimSet", menuName = "Splice/Miner Anim Set")]
    public class MinerAnimSetSO : ScriptableObject
    {
        public string idle = "Idle";
        public string walk = "Walk";
        public string farming = "Farming";   // ท่าตอนขุด/เก็บเกี่ยว
        public string death = "Death";
        public string landing = "Landing";   // เล่นตอน spawn
        public string dance = "Dance";        // เผื่อไว้
    }
}
