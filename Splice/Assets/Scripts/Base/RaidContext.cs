namespace Splice.Base
{
    // เป้าหมาย raid หนึ่งอัน = ผังเมือง (BaseLayout มี ownerAccountId/factionId/storedGold) + ข้อมูลโชว์
    [System.Serializable]
    public class RaidTarget
    {
        public string displayName;
        public int baseLevel = 1;
        public BaseLayout layout;
        public bool Looted;   // ปล้นไปแล้ว (กัน replay ตีซ้ำเป้าเดิมเพื่อ farm loot — stand-in ของ cooldown rule 3)

        public int StoredGold => layout != null ? layout.storedGold : 0;
    }

    // ตัวส่งต่อ "เป้าหมายที่กำลังบุก" ข้ามซีน (จอเลือกเป้า → ซีน raid) — static คงอยู่ข้ามการโหลดซีน (architecture §5.10).
    // ต่อไป (server) จะแทนด้วยการส่ง snapshot ผู้เล่นจริงมาที่นี่.
    public static class RaidContext
    {
        public static RaidTarget Target;          // เป้าหมายรอบนี้
        public static string AttackerFactionId;   // เผ่าที่ผู้เล่นใช้บุก (loadout)
        public static int LastLootGained;         // loot รอบล่าสุด (จอผลอ่าน)

        public static bool HasTarget => Target != null && Target.layout != null;

        public static void Clear() { Target = null; LastLootGained = 0; }
    }
}
