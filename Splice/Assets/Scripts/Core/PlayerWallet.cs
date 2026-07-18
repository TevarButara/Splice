using UnityEngine;

namespace Splice.Core
{
    // Meta currency ถาวร (คงอยู่ข้ามแมตช์) สำหรับ "สร้าง/อัพเกรด/ขยายเมือง" (architecture §5.10, ขั้น 5.5).
    // ⚠️ คนละตัวกับ "ทองในแมตช์" (GoldController จาก miner ที่รีเซ็ตทุก raid) — อย่าปนกัน.
    // Phase 1: local PlayerPrefs (แก้ง่าย — โอเคเฉพาะ greybox/PvE offline).
    // ก่อนเปิดเศรษฐกิจจริง/PvP: ย้ายเป็น server-authoritative (DB) — client ส่งแค่ intent, server คิด/หัก/validate เอง
    // (เหมือน IAP/currency §7, anti-cheat §10). idle income (ขั้น 5.5) จะเติมเข้ากระเป๋านี้.
    public static class PlayerWallet
    {
        private const string GoldKey = "Splice.Wallet.MetaGold";
        private const int DefaultGold = 500; // ทองตั้งต้น greybox

        public static int MetaGold
        {
            get => PlayerPrefs.GetInt(GoldKey, DefaultGold);
            private set { PlayerPrefs.SetInt(GoldKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (MetaGold < amount) return false;
            MetaGold -= amount;
            return true;
        }

        public static void Add(int amount)
        {
            if (amount > 0) MetaGold += amount;
        }
    }
}
