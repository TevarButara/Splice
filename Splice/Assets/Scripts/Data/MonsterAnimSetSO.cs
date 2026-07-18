using UnityEngine;

namespace Splice.Data
{
    // ชื่อ state/clip ใน Animator ต่อสถานะของมอน — "กองกลาง" แก้ที่เดียว ไม่ hardcode ในโค้ด.
    // ทำ 1 asset กลางแล้วลากใส่มอนทุกตัว (มอนพิเศษจะใช้ชุดชื่อของตัวเองก็ได้ ถ้า assign asset คนละตัว).
    // เว้นช่องไหนว่าง = โค้ด fallback ไปใช้ชื่อ default ของช่องนั้น.
    [CreateAssetMenu(fileName = "MonsterAnimSet", menuName = "Splice/Monster Anim Set")]
    public class MonsterAnimSetSO : ScriptableObject
    {
        public string idle = "Idle";
        public string walk = "Walk";
        public string attack = "Attack";
        public string injured = "Injured";
        public string death = "Death";
        public string victory = "Victory";
        public string lose = "Lose";
        public string landing = "Landing";   // เล่นตอน spawn
        public string dance = "Dance";        // เผื่อไว้
        public string spell = "Spell";        // supporter cast ตอนมานาเต็ม
    }
}
