using Splice.Combat;
using Splice.Core;
using UnityEngine;

namespace Splice.Base
{
    // ตอนจบ raid: คิด loot จากทองคลังของเป้าหมาย ให้ผู้บุก (roadmap 5.4).
    // greybox: บุกสำเร็จ (ทำลาย Fort) → ได้ lootPercent ของ storedGold เข้า PlayerWallet.
    // ⚠️ ตอน raid ฐานผู้เล่นจริง: ต้อง **หัก loot จากฐาน defender + คิด/validate ฝั่ง server** (architecture §5.10/§10)
    //    + shield/cooldown ต่อคู่เป้า (กันสแปมตีซ้ำ) — ทำตอนย้ายเป็น server-authoritative.
    public class RaidRewardController : MonoBehaviour
    {
        [SerializeField] private RaidManager raidManager;
        [Tooltip("สัดส่วน loot ที่ได้เมื่อบุกสำเร็จ (0-1) จากทองคลังเป้าหมาย")]
        [Range(0f, 1f)][SerializeField] private float lootPercent = 0.2f;

        private bool rewarded;

        private void OnEnable()
        {
            if (raidManager != null) raidManager.OnRaidEnded += OnRaidEnded;
        }

        private void OnDisable()
        {
            if (raidManager != null) raidManager.OnRaidEnded -= OnRaidEnded;
        }

        private void OnRaidEnded(RaidOutcome outcome)
        {
            if (rewarded) return;
            rewarded = true;

            RaidContext.LastLootGained = 0;
            if (!RaidContext.HasTarget || RaidContext.Target.Looted) return; // ตีซ้ำเป้าเดิม (replay) = ไม่ได้ loot อีก

            // ได้ loot เมื่อบุกสำเร็จ (Fort แตก). แพ้/หมดเวลา = ไม่ได้ (greybox)
            if (outcome == RaidOutcome.MonstersWin)
            {
                var loot = Mathf.FloorToInt(RaidContext.Target.StoredGold * lootPercent);
                if (loot > 0)
                {
                    PlayerWallet.Add(loot);
                    RaidContext.LastLootGained = loot;
                }
                RaidContext.Target.Looted = true;
                Debug.Log($"[Raid] บุกสำเร็จ — loot {loot}, ทองรวม {PlayerWallet.MetaGold}");
            }
        }
    }
}
