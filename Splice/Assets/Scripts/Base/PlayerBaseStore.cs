using UnityEngine;

namespace Splice.Base
{
    // Save/load ฐานผู้เล่น + ทัพบุก (architecture 5.10). Phase 1: local JSON ใน PlayerPrefs
    // (pattern เดียวกับ LairManager) — เปลี่ยน backend เป็น cloud save ภายหลังได้โดยคง API เดิม
    // ผู้เรียกทุกฝั่งห้ามแตะ PlayerPrefs ตรงๆ ให้ผ่าน store นี้ที่เดียว
    //
    // โมเดล B (v0.2): 1 เมือง/faction → เก็บ "แยก key ต่อ factionId" (ผังของเผ่า human ไม่ทับเผ่า demon).
    public static class PlayerBaseStore
    {
        private const string LayoutPrefix = "Splice.Base.Layout.";
        private const string ArmiesPrefix = "Splice.Base.Armies.";

        private static string LayoutKey(string factionId) => LayoutPrefix + factionId;
        private static string ArmiesKey(string factionId) => ArmiesPrefix + factionId;

        public static bool HasLayout(string factionId) =>
            !string.IsNullOrEmpty(factionId) && PlayerPrefs.HasKey(LayoutKey(factionId));

        // ผังเมืองของ faction นี้ — null = ยังไม่เคยจัด (ให้ Build Mode สร้างชุดแรก)
        public static BaseLayout LoadLayout(string factionId)
        {
            if (string.IsNullOrEmpty(factionId) || !PlayerPrefs.HasKey(LayoutKey(factionId))) return null;
            return JsonUtility.FromJson<BaseLayout>(PlayerPrefs.GetString(LayoutKey(factionId)));
        }

        // เก็บด้วย key = layout.factionId (แต่ละเมืองรู้ faction ตัวเอง) — ต้อง stamp factionId ก่อนเรียก
        public static void SaveLayout(BaseLayout layout)
        {
            if (layout == null || string.IsNullOrEmpty(layout.factionId))
            {
                Debug.LogWarning("[PlayerBaseStore] SaveLayout: layout.factionId ว่าง — ข้าม (ต้อง stamp faction ก่อน save)");
                return;
            }
            PlayerPrefs.SetString(LayoutKey(layout.factionId), JsonUtility.ToJson(layout));
            PlayerPrefs.Save();
        }

        // ทัพบุกของ faction นี้ — ไม่เคยบันทึก = collection ว่าง (ไม่คืน null ให้ UI เช็คง่าย)
        public static ArmyPresetCollection LoadArmies(string factionId)
        {
            if (string.IsNullOrEmpty(factionId) || !PlayerPrefs.HasKey(ArmiesKey(factionId)))
                return new ArmyPresetCollection();
            return JsonUtility.FromJson<ArmyPresetCollection>(PlayerPrefs.GetString(ArmiesKey(factionId)));
        }

        public static void SaveArmies(string factionId, ArmyPresetCollection armies)
        {
            if (string.IsNullOrEmpty(factionId) || armies == null) return;
            PlayerPrefs.SetString(ArmiesKey(factionId), JsonUtility.ToJson(armies));
            PlayerPrefs.Save();
        }

        // ล้าง save ของ faction นี้ (debug/เทสเท่านั้น)
        public static void DeleteFaction(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return;
            PlayerPrefs.DeleteKey(LayoutKey(factionId));
            PlayerPrefs.DeleteKey(ArmiesKey(factionId));
            PlayerPrefs.Save();
        }
    }
}
