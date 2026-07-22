using System;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Base;
using Splice.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Pre-raid target offer for the local Step 5B prototype. The side picker remains a dev shell; choosing
    // Invader now opens this explicit risk/reward confirmation before enabling attacker controls.
    public class LocalRaidStakeController : MonoBehaviour
    {
        [SerializeField] private SideSelectionController sideSelectionController;
        [SerializeField] private GameObject sideChoiceRoot;
        [SerializeField] private GameObject offerPanel;
        [SerializeField] private TMP_Text offerText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text confirmButtonLabel;
        [SerializeField] private Text cancelButtonLabel;
        [SerializeField] private RaidSceneAdapter raidSceneAdapter;

        [Header("Prototype Target Offer")]
        [SerializeField] private string targetName = "Blackstone Keep";
        [SerializeField] private string difficultyBand = "FAIR";
        [Min(0)] [SerializeField] private int entryStake = 100;
        [Min(0)] [SerializeField] private int fullVictoryPayout = 180;
        [Min(0)] [SerializeField] private int outerExtractionPayout = 60;
        [Min(0)] [SerializeField] private int innerExtractionPayout = 90;
        [Min(0)] [SerializeField] private int coreExtractionPayout = 120;

        private bool confirming;
        private bool preparing;
        private string feedback = string.Empty;
        private string startupNotice = string.Empty;
        private WalletView wallet = new();
        private RaidQuoteDto activeQuote;
        private string confirmIdempotencyKey;
        private CancellationTokenSource lifetimeCancellation;

        public int WalletBalance => wallet?.warGemBalance ?? 0;
        public bool HasPendingStake => wallet?.hasPendingRaid == true;

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            if (sideSelectionController == null) sideSelectionController = FindFirstObjectByType<SideSelectionController>();
            if (raidSceneAdapter == null) raidSceneAdapter = FindFirstObjectByType<RaidSceneAdapter>();
            if (offerPanel != null) offerPanel.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmRaid);
            if (cancelButton != null) cancelButton.onClick.AddListener(CancelOffer);
            if (confirmButtonLabel != null) confirmButtonLabel.text = "CONFIRM RAID";
            if (cancelButtonLabel != null) cancelButtonLabel.text = "CANCEL";
        }

        private void Start() => _ = InitializeAsync();

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            if (confirmButton != null) confirmButton.onClick.RemoveListener(ConfirmRaid);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelOffer);
        }

        public void OpenOffer() => _ = OpenOfferAsync();

        private async Task InitializeAsync()
        {
            try
            {
                var recovered = await SpliceServiceHub.Wallet.ResolveAbandonedRaidAsync(lifetimeCancellation.Token);
                if (recovered.success && recovered.transaction?.offer != null)
                    startupNotice = $"PREVIOUS RAID INTERRUPTED — STAKE {recovered.transaction.offer.entryStake} LOST";
                wallet = recovered.wallet ?? await SpliceServiceHub.Wallet.GetWalletAsync(lifetimeCancellation.Token);
                RefreshOffer();
            }
            catch (OperationCanceledException)
            {
                // Scene teardown owns cancellation.
            }
        }

        private async Task OpenOfferAsync()
        {
            if (preparing || confirming) return;
            preparing = true;
            confirming = false;
            feedback = string.Empty;
            activeQuote = null;
            confirmIdempotencyKey = string.Empty;
            if (raidSceneAdapter == null) raidSceneAdapter = FindFirstObjectByType<RaidSceneAdapter>();
            if (sideChoiceRoot != null) sideChoiceRoot.SetActive(false);
            if (offerPanel != null) offerPanel.SetActive(true);
            RefreshOffer();

            RaidPreparationResult preparation = null;
            if (raidSceneAdapter != null)
            {
                try
                {
                    preparation = await raidSceneAdapter.PrepareRaidAsync(lifetimeCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            if (preparation?.success != true)
            {
                feedback = string.IsNullOrWhiteSpace(preparation?.error)
                    ? "RAID SCENE IS NOT READY"
                    : preparation.error;
            }
            else
            {
                var target = preparation.target;
                try
                {
                    if (SpliceServiceHub.IsRemoteMeta)
                    {
                        var factionId = string.IsNullOrWhiteSpace(PlayerProfile.ActiveFactionId)
                            ? target.factionId
                            : PlayerProfile.ActiveFactionId;
                        var loadoutRequest = SpliceServiceHub.SelectedAttackerLoadout(factionId);
                        if (loadoutRequest == null)
                            throw new InvalidOperationException(
                                "SELECT AND SAVE AN ATTACKER ARMY BEFORE RAIDING");
                        var saved = await SpliceServiceHub.RaidContracts.SaveAttackerLoadoutAsync(
                            RaidSessionContext.Current.attackerLoadoutId, loadoutRequest,
                            Guid.NewGuid().ToString("N"), lifetimeCancellation.Token);
                        if (saved?.success != true)
                            throw new InvalidOperationException(saved?.error ?? "ATTACKER LOADOUT REJECTED");
                    }
                    activeQuote = await SpliceServiceHub.RaidContracts.CreateQuoteAsync(new CreateRaidQuoteRequest
                    {
                        targetId = target.targetId,
                        targetName = target.displayName,
                        difficultyBand = RaidSceneAdapter.DifficultyBandFor(target),
                        attackerLoadoutId = RaidSessionContext.Current?.attackerLoadoutId,
                    }, Guid.NewGuid().ToString("N"), lifetimeCancellation.Token);
                    confirmIdempotencyKey = Guid.NewGuid().ToString("N");
                    ApplyQuote(activeQuote);
                    wallet = await SpliceServiceHub.Wallet.GetWalletAsync(lifetimeCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    feedback = exception.Message;
                    raidSceneAdapter.CancelPreparedRaid();
                }
            }
            preparing = false;
            RefreshOffer();
        }

        public void CancelOffer()
        {
            if (confirming) return;
            if (raidSceneAdapter != null) raidSceneAdapter.CancelPreparedRaid();
            activeQuote = null;
            confirmIdempotencyKey = string.Empty;
            if (offerPanel != null) offerPanel.SetActive(false);
            if (sideChoiceRoot != null) sideChoiceRoot.SetActive(true);
        }

        public void ConfirmRaid() => _ = ConfirmRaidAsync();

        private async Task ConfirmRaidAsync()
        {
            if (confirming || preparing) return;
            confirming = true;

            BackendOperationResult readiness = null;
            if (raidSceneAdapter != null)
            {
                try
                {
                    readiness = await raidSceneAdapter.CanStartPreparedRaidAsync(lifetimeCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            if (readiness?.success != true)
            {
                confirming = false;
                feedback = string.IsNullOrWhiteSpace(readiness?.error)
                    ? "RAID SCENE IS NOT READY"
                    : readiness.error;
                RefreshOffer();
                return;
            }

            if (activeQuote == null || string.IsNullOrWhiteSpace(confirmIdempotencyKey))
            {
                confirming = false;
                feedback = "RAID QUOTE IS NOT READY";
                RefreshOffer();
                return;
            }

            RaidStartDto start;
            try
            {
                start = await SpliceServiceHub.RaidContracts.ConfirmAsync(activeQuote.quoteId,
                    confirmIdempotencyKey, lifetimeCancellation.Token);
                wallet = start.wallet ?? wallet;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                confirming = false;
                feedback = exception.Message;
                RefreshOffer();
                return;
            }

            if (!start.success)
            {
                confirming = false;
                feedback = start.error;
                RefreshOffer();
                return;
            }

            RaidAllocationDto allocation;
            try
            {
                allocation = await SpliceServiceHub.RaidContracts.AllocateAsync(start.raidId,
                    Guid.NewGuid().ToString("N"), lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                allocation = new RaidAllocationDto { success = false, error = exception.Message };
            }
            if (allocation?.success != true || !RaidSessionContext.BindAllocation(allocation))
            {
                var allocationRefund = await SpliceServiceHub.Wallet.CancelRaidBeforeStartAsync(
                    start.raidId, "ALLOCATION_FAILED", Guid.NewGuid().ToString("N"),
                    lifetimeCancellation.Token);
                wallet = allocationRefund.wallet ?? wallet;
                confirming = false;
                feedback = "RAID SERVER ALLOCATION FAILED — STAKE REFUNDED. " + allocation?.error;
                RefreshOffer();
                return;
            }

            var startup = await raidSceneAdapter.StartPreparedRaidAsync(
                start.raidId, lifetimeCancellation.Token);
            if (!startup.success)
            {
                var refund = await SpliceServiceHub.Wallet.CancelRaidBeforeStartAsync(
                    start.raidId, "CLIENT_START_FAILED", Guid.NewGuid().ToString("N"),
                    lifetimeCancellation.Token);
                wallet = refund.wallet ?? wallet;
                confirming = false;
                feedback = "RAID COULD NOT START — STAKE REFUNDED. " + startup.error;
                RefreshOffer();
                return;
            }

            feedback = string.Empty;
            startupNotice = string.Empty;
            if (offerPanel != null) offerPanel.SetActive(false);
            if (sideSelectionController != null) sideSelectionController.ConfirmMonsterRaid();
        }

        private void ApplyQuote(RaidQuoteDto quote)
        {
            if (quote == null) return;
            targetName = quote.targetName;
            difficultyBand = quote.difficultyBand;
            entryStake = quote.entryStake;
            fullVictoryPayout = quote.fullVictoryPayout;
            outerExtractionPayout = quote.outerExtractionPayout;
            innerExtractionPayout = quote.innerExtractionPayout;
            coreExtractionPayout = quote.coreExtractionPayout;
        }

        private void RefreshOffer()
        {
            var balance = WalletBalance;
            if (confirmButton != null) confirmButton.interactable = !preparing && !confirming &&
                activeQuote != null && balance >= entryStake && !HasPendingStake &&
                raidSceneAdapter != null && raidSceneAdapter.HasPreparedRaid && string.IsNullOrEmpty(feedback);
            if (offerText == null) return;

            var fullNet = fullVictoryPayout - entryStake;
            var outerNet = outerExtractionPayout - entryStake;
            var innerNet = innerExtractionPayout - entryStake;
            var coreNet = coreExtractionPayout - entryStake;
            var message =
                $"<size=140%><b>{targetName.ToUpperInvariant()}</b></size>  " +
                $"<color=#FFBC57>[{difficultyBand.ToUpperInvariant()}]</color>\n" +
                $"WAR GEM BALANCE  <b>{balance:N0}</b>\n\n" +
                $"<color=#FFBC57><b>ENTRY STAKE  -{entryStake:N0}</b></color>\n" +
                $"FULL VICTORY  <color=#61E6A7>+{fullVictoryPayout:N0}  (NET {Signed(fullNet)})</color>\n\n" +
                $"<b>EXTRACTION OPTIONS</b>\n" +
                $"OUTER  +{outerExtractionPayout:N0}  <color=#A9B8CC>NET {Signed(outerNet)}</color>\n" +
                $"INNER  +{innerExtractionPayout:N0}  <color=#A9B8CC>NET {Signed(innerNet)}</color>\n" +
                $"CORE   +{coreExtractionPayout:N0}  <color=#61E6A7>NET {Signed(coreNet)}</color>\n\n" +
                $"DEFEAT  <color=#FF6B78>STAKE {entryStake:N0} LOST</color>";

            if (!string.IsNullOrEmpty(startupNotice)) message += $"\n\n<color=#FFB347>{startupNotice}</color>";
            if (!string.IsNullOrEmpty(feedback)) message += $"\n\n<color=#FF6B6B>{feedback.ToUpperInvariant()}</color>";
            offerText.text = message;
        }

        private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
    }
}
