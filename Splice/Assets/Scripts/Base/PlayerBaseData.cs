using System.Collections.Generic;
using UnityEngine;

namespace Splice.Base
{
    // Serializable snapshot ของ "ฐานผู้เล่น" (architecture 5.10) — เก็บเป็น JSON ต่อผู้เล่น
    // (ไม่ใช่ ScriptableObject เพราะเป็น save data ไม่ใช่ content)
    // id ทุกตัวเป็น composite id ของ FactionRegistry ("factionId/localId") → snapshot อยู่รอด
    // ข้ามการเพิ่ม/ลบ content และไม่ชนกันข้ามเผ่า

    // ป้อม 1 ตัวในผังฐาน — ตำแหน่งเป็น world position ที่ผ่าน grid snap ตอนวางแล้ว
    // (เก็บ world pos ตรงๆ แทน cell index เพื่อไม่ผูก save data กับค่า cellSize/gridOrigin ของแมป)
    [System.Serializable]
    public class PlacedTowerData
    {
        public string towerId;   // composite id (factionId/towerId)
        public Vector3 position;

        // per-stat upgrade levels (architecture 5.6) — apply ซ้ำตอน spawn จาก snapshot
        public int attackLevel;
        public int healthLevel;
        public int armorLevel;
        public int rangeLevel;
        public int targetsLevel;
    }

    // มอนสเตอร์เฝ้าฐาน (garrison) 1 ตัว — spawn แบบ hold-position ตอนถูก raid (ระบบ garrison = ขั้น 5.3)
    [System.Serializable]
    public class GarrisonMonsterData
    {
        public string cardId;    // composite id (factionId/cardId)
        public Vector3 position;
    }

    // ผังฐานทั้งชุดของผู้เล่น 1 คน — สิ่งที่ระบบใช้ตั้งรับแทนเจ้าของตอนถูก raid (async, ไม่ต้องออนไลน์)
    // หมายเหตุ: FortCore ไม่อยู่ใน layout — Fort เป็นของ scene/แมปฐาน (ตายตัวต่อแมป)
    [System.Serializable]
    public class BaseLayout
    {
        public int version = 1;  // เผื่อ migrate โครง save ในอนาคต
        [Tooltip("เจ้าของเมือง (PlayerProfile.AccountId) — server เช็ค attacker≠defender กัน self-farming (architecture §5.10)")]
        public string ownerAccountId;
        [Tooltip("faction ของเมืองนี้ (1 เมือง/faction — โมเดล B). ใช้เป็น key เก็บ/โหลดใน PlayerBaseStore")]
        public string factionId;
        public List<PlacedTowerData> towers = new();
        public List<GarrisonMonsterData> garrison = new();
        public List<string> minerCardIds = new();  // miner ที่ฐานนี้มี (composite card id ต่อตัว)
        public int storedGold;                     // ทองในคลังฐาน = เป้าโดนปล้น (loot % คิดในขั้น 5.4)
    }

    // ---------- ทัพบุก (invader) ----------

    // การ์ด 1 ชนิดในทัพ + จำนวน
    [System.Serializable]
    public class ArmyEntry
    {
        public string cardId;    // composite id
        public int count = 1;
    }

    // ทัพบุก 1 ชุดที่จัดไว้ล่วงหน้า — แก้ไข/บันทึกได้หลายชุด เลือกก่อนกด raid
    [System.Serializable]
    public class ArmyPreset
    {
        public string presetName = "Army";
        public List<ArmyEntry> entries = new();
    }

    // รวมทุก preset ของผู้เล่น + ชุดที่เลือกอยู่ (JsonUtility serialize list ตรงๆ ไม่ได้ ต้องมี wrapper)
    [System.Serializable]
    public class ArmyPresetCollection
    {
        public List<ArmyPreset> presets = new();
        public int selectedIndex;

        public ArmyPreset Selected =>
            presets.Count == 0 ? null : presets[Mathf.Clamp(selectedIndex, 0, presets.Count - 1)];
    }
}
