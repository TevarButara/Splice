using System;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Combat;
using Splice.Core;
using UnityEngine;

namespace Splice.Base
{
    // ตอนจบ raid: คิด loot จากทองคลังของเป้าหมาย ให้ผู้บุก (roadmap 5.4).
    // ⚠️ Production must settle defender loss and attacker credit on the authoritative server.
    public class RaidRewardController : MonoBehaviour
    {
        [SerializeField] private RaidManager raidManager;
        [Tooltip("Step 2 loot ledger — เว้นว่างได้เพื่อใช้ legacy Full Victory reward ระหว่าง migration")]
        [SerializeField] private RaidLootController lootController;
        [Range(0f, 1f)][SerializeField] private float lootPercent = 0.2f;

        private bool rewarded;
        private CancellationTokenSource lifetimeCancellation;

        private void OnEnable()
        {
            lifetimeCancellation = new CancellationTokenSource();
            if (lootController == null) lootController = FindFirstObjectByType<RaidLootController>();
            if (raidManager != null) raidManager.OnRaidEnded += OnRaidEnded;
        }

        private void OnDisable()
        {
            if (raidManager != null) raidManager.OnRaidEnded -= OnRaidEnded;
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        private void OnRaidEnded(RaidOutcome outcome) => _ = OnRaidEndedAsync(outcome);

        private async Task OnRaidEndedAsync(RaidOutcome outcome)
        {
            if (rewarded || raidManager == null || !raidManager.IsServer) return;
            rewarded = true;
            RaidContext.LastLootGained = 0;

            // We are the defender watching a simulated remote attacker. Never credit the local wallet with
            // the attacker's loot (and never mark our town target as locally looted).
            if (RaidSessionContext.Current?.isIncomingDefense == true) return;

            if (lootController != null)
            {
                if (RaidContext.HasTarget && RaidContext.Target.Looted) return;
                if (!lootController.TrySettle(outcome, out var settledLoot)) return;
                await CreditAndRememberAsync(settledLoot);
                if (RaidContext.HasTarget &&
                    (outcome == RaidOutcome.FullVictory ||
                     (outcome == RaidOutcome.Extracted && settledLoot > 0)))
                    RaidContext.Target.Looted = true;
                return;
            }

            if (!RaidContext.HasTarget || RaidContext.Target.Looted) return;
            if (outcome == RaidOutcome.FullVictory)
            {
                var loot = Mathf.FloorToInt(RaidContext.Target.StoredGold * lootPercent);
                await CreditAndRememberAsync(loot);
                RaidContext.Target.Looted = true;
            }
        }

        private async Task CreditAndRememberAsync(int loot)
        {
            if (lifetimeCancellation == null) return;
            try
            {
                // Settlement and reward listeners may run in either order. The local adapter merges a later
                // Gold value into the same report; the remote adapter will send only a server-authored result.
                var result = await SpliceServiceHub.RaidSettlement.CreditLootAsync(
                    Mathf.Max(0, loot), lifetimeCancellation.Token);
                if (!result.success)
                {
                    Debug.LogError("[Raid] loot settlement failed: " + result.error, this);
                    return;
                }
                Debug.Log($"[Raid] settlement loot {result.creditedGold}, ทองรวม {result.metaGoldBalance}");
            }
            catch (OperationCanceledException)
            {
                // Scene teardown owns cancellation.
            }
        }
    }
}
