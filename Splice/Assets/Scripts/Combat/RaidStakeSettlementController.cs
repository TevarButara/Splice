using System;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Base;
using Splice.Core;
using UnityEngine;

namespace Splice.Combat
{
    // Settles the local War Gem escrow exactly once from the server-side RaidManager outcome.
    public class RaidStakeSettlementController : MonoBehaviour
    {
        [SerializeField] private RaidManager raidManager;
        [SerializeField] private BreachRingController breachRingController;

        public bool LastSettlementSucceeded { get; private set; }
        private CancellationTokenSource lifetimeCancellation;
        private bool settling;

        private void OnEnable()
        {
            lifetimeCancellation = new CancellationTokenSource();
            if (raidManager == null) raidManager = FindFirstObjectByType<RaidManager>();
            if (breachRingController == null) breachRingController = FindFirstObjectByType<BreachRingController>();
            if (raidManager != null) raidManager.OnRaidEnded += HandleRaidEnded;
        }

        private void OnDisable()
        {
            if (raidManager != null) raidManager.OnRaidEnded -= HandleRaidEnded;
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        private void HandleRaidEnded(RaidOutcome outcome) => _ = HandleRaidEndedAsync(outcome);

        private async Task HandleRaidEndedAsync(RaidOutcome outcome)
        {
            if (settling || RaidSessionContext.Current?.isIncomingDefense == true) return;
            if (raidManager == null || !raidManager.IsServer || lifetimeCancellation == null) return;
            settling = true;
            try
            {
                var wallet = await SpliceServiceHub.Wallet.GetWalletAsync(lifetimeCancellation.Token);
                if (!wallet.hasPendingRaid) return;
                var breachedRings = breachRingController != null ? breachRingController.BreachedRingCount : 0;
                var result = await SpliceServiceHub.RaidSettlement.SettleActiveRaidAsync(
                    outcome, breachedRings, lifetimeCancellation.Token);
                LastSettlementSucceeded = result.success;
                if (result.success && result.report?.success == false)
                    Debug.LogWarning("[RaidReport] settlement completed but report is pending: " +
                                     result.report.error, this);
                if (!result.success)
                    Debug.LogError("[WarGem] " + result.error, this);
            }
            catch (OperationCanceledException)
            {
                // Scene teardown owns cancellation.
            }
            finally
            {
                settling = false;
            }
        }
    }
}
