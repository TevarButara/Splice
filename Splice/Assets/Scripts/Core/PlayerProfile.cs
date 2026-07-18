using System.Collections.Generic;
using UnityEngine;

namespace Splice.Core
{
    // โปรไฟล์ผู้เล่น local (PlayerPrefs) — architecture §1.1/§5.10.
    // โมเดลใหม่ (v0.2): faction = "loadout สลับได้" — 1 บัญชีเป็นเจ้าของได้หลาย faction, แต่ละเผ่าที่ปลดล็อก
    // มีเมืองของตัวเอง 1 หลัง (โมเดล B). ActiveFactionId = เผ่า/เมืองที่กำลังใช้อยู่.
    // AccountId = id เครื่อง (placeholder จนกว่าจะมีระบบบัญชีจริง) ใช้เป็น ownerAccountId ของเมือง +
    // เช็ค attacker≠defender กัน self-farming (architecture §5.10 กติกาข้อ 2).
    // Phase 1 เก็บ local; ย้าย cloud profile ตอน Phase 2 ได้โดยคง API.
    public static class PlayerProfile
    {
        private const string AccountKey = "Splice.Profile.AccountId";
        private const string ActiveFactionKey = "Splice.Profile.ActiveFactionId";
        private const string OwnedFactionsKey = "Splice.Profile.OwnedFactions";

        // เพดานจำนวนเมือง/เผ่าที่ปลดล็อกได้ (gate — คุมด้วย progression/IAP ทีหลัง) กัน attention เฉลี่ยบางเกิน
        public const int MaxCitySlots = 3;

        [System.Serializable]
        private class FactionList { public List<string> ids = new(); }

        // id เครื่อง สร้างครั้งเดียวแล้วคงที่ — ใช้เป็นเจ้าของเมือง + เช็คห้ามบุกเมืองตัวเอง
        public static string AccountId
        {
            get
            {
                var id = PlayerPrefs.GetString(AccountKey, string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    id = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(AccountKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        // faction/เมืองที่กำลังใช้อยู่ (loadout ปัจจุบัน)
        public static string ActiveFactionId
        {
            get => PlayerPrefs.GetString(ActiveFactionKey, string.Empty);
            set { PlayerPrefs.SetString(ActiveFactionKey, value ?? string.Empty); PlayerPrefs.Save(); }
        }

        public static bool HasActiveFaction => !string.IsNullOrEmpty(ActiveFactionId);

        // base level ต่อเมือง/เผ่า — คุม DefenseCapacity (เพดานฝ่ายรับ ผูกกับ progression ไม่ใช่เงิน, architecture §5.10).
        // Phase 1 ยังไม่มีระบบ level-up จริง (default 1) — ตั้งค่าเทส/ปั้นทีหลังผ่าน SetBaseLevel
        private const string BaseLevelPrefix = "Splice.Base.Level.";

        public static int BaseLevel(string factionId) =>
            string.IsNullOrEmpty(factionId) ? 1 : Mathf.Max(1, PlayerPrefs.GetInt(BaseLevelPrefix + factionId, 1));

        public static void SetBaseLevel(string factionId, int level)
        {
            if (string.IsNullOrEmpty(factionId)) return;
            PlayerPrefs.SetInt(BaseLevelPrefix + factionId, Mathf.Max(1, level));
            PlayerPrefs.Save();
        }

        // faction ที่ผู้เล่นปลดล็อกแล้ว (แต่ละอัน = เมือง 1 หลัง)
        public static IReadOnlyList<string> OwnedFactionIds => LoadOwned().ids;
        public static bool Owns(string factionId) => LoadOwned().ids.Contains(factionId);
        public static bool CanUnlockMore => LoadOwned().ids.Count < MaxCitySlots;

        // ปลดล็อกเผ่า (= เปิดเมืองใหม่ 1 หลัง). คืน false ถ้ามีอยู่แล้ว หรือเต็มเพดาน slot
        public static bool UnlockFaction(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return false;
            var owned = LoadOwned();
            if (owned.ids.Contains(factionId)) return false;
            if (owned.ids.Count >= MaxCitySlots) return false;
            owned.ids.Add(factionId);
            SaveOwned(owned);
            return true;
        }

        private static FactionList LoadOwned()
        {
            if (!PlayerPrefs.HasKey(OwnedFactionsKey)) return new FactionList();
            return JsonUtility.FromJson<FactionList>(PlayerPrefs.GetString(OwnedFactionsKey)) ?? new FactionList();
        }

        private static void SaveOwned(FactionList list)
        {
            PlayerPrefs.SetString(OwnedFactionsKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }
    }
}
