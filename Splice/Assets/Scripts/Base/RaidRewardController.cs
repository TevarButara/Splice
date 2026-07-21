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
        [Tooltip("Step 2 loot ledger — เว้นว่างได้เพื่อใช้ legacy Full Victory reward ระหว่าง migration")]
        [SerializeField] private RaidLootController lootController;
        [Tooltip("สัดส่วน loot ที่ได้เมื่อบุกสำเร็จ (0-1) จากทองคลังเป้าหมาย")]
        [Range(0f, 1f)][SerializeField] private float lootPercent = 0.2f;

        private bool rewarded;

        private void OnEnable()
        {
            if (lootController == null) lootController = FindFirstObjectByType<RaidLootController>();
            if (raidManager != null) raidManager.OnRaidEnded += OnRaidEnded;
        }

        private void OnDisable()
        {
            if (raidManager != null) raidManager.OnRaidEnded -= OnRaidEnded;
        }

        private void OnRaidEnded(RaidOutcome outcome)
        {
            if (rewarded || raidManager == null || !raidManager.IsServer) return;
            rewarded = true;

            RaidContext.LastLootGained = 0;

            // Step 2 path: the server-side ledger settles exactly once. Full Victory banks every remaining
            // bucket; Extraction banks Secured only; Defeat banks zero.
            if (lootController != null)
            {
                if (RaidContext.HasTarget && RaidContext.Target.Looted) return;
                if (!lootController.TrySettle(outcome, out var settledLoot)) return;
                CreditAndRemember(settledLoot);

                if (RaidContext.HasTarget &&
                    (outcome == RaidOutcome.FullVictory ||
                     (outcome == RaidOutcome.Extracted && settledLoot > 0)))
                    RaidContext.Target.Looted = true;
                return;
            }

            // Migration fallback when the scene has not added RaidLootController yet.
            if (!RaidContext.HasTarget || RaidContext.Target.Looted) return;
            if (outcome == RaidOutcome.FullVictory)
            {
                var loot = Mathf.FloorToInt(RaidContext.Target.StoredGold * lootPercent);
                CreditAndRemember(loot);
                RaidContext.Target.Looted = true;
            }
        }

        private static void CreditAndRemember(int loot)
        {
            if (loot > 0) PlayerWallet.Add(loot);
            RaidContext.LastLootGained = Mathf.Max(0, loot);
            Debug.Log($"[Raid] settlement loot {loot}, ทองรวม {PlayerWallet.MetaGold}");
        }
    }
}
