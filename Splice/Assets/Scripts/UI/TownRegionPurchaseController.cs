using System;
using System.Collections.Generic;
using System.Threading;
using Splice.Backend;
using Splice.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Editor-authored BuildZone panel. It asks the service boundary to buy a region; the client never sends cost.
    public sealed class TownRegionPurchaseController : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private List<TownRegionPurchaseButtonView> regionButtons = new();
        private CancellationTokenSource lifetime;
        private TownExpansionView lastExpansion;
        private bool busy;

        public bool HasEditorAuthoredUi =>
            buildManager != null && panel != null && openButton != null && closeButton != null &&
            statusText != null && regionButtons.Count > 0 && regionButtons.TrueForAll(v => v != null && v.IsComplete);

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
            if (!HasEditorAuthoredUi)
            {
                Debug.LogError("[TownExpansionUI] Editor-authored references are incomplete.", this);
                enabled = false;
                return;
            }
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            foreach (var view in regionButtons)
            {
                var captured = view.RegionId;
                view.Button.onClick.AddListener(() => Purchase(captured));
            }
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }

        public void Open()
        {
            panel.SetActive(true);
            _ = RefreshAsync();
        }

        public void Close() => panel.SetActive(false);
        public void Purchase(string regionId) => _ = PurchaseAsync(regionId);

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            if (busy) return;
            busy = true;
            SetButtons(false);
            try
            {
                var expansion = await SpliceServiceHub.TownExpansion.GetAsync(
                    buildManager.EditingFactionId, lifetime.Token);
                Render(expansion, string.Empty);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                statusText.text = exception.Message;
            }
            finally
            {
                busy = false;
                if (lastExpansion != null) Render(lastExpansion, statusText.text);
                else SetButtons(true);
            }
        }

        private async System.Threading.Tasks.Task PurchaseAsync(string regionId)
        {
            if (busy) return;
            busy = true;
            SetButtons(false);
            try
            {
                var result = await SpliceServiceHub.TownExpansion.PurchaseAsync(
                    buildManager.EditingFactionId, regionId, Guid.NewGuid().ToString("N"), lifetime.Token);
                if (!result.success || result.expansion == null)
                {
                    statusText.text = string.IsNullOrWhiteSpace(result.error)
                        ? "REGION PURCHASE FAILED" : result.error;
                    return;
                }
                var state = result.expansion.ToState();
                buildManager.ApplyExpansionState(state);
                Render(result.expansion, "REGION UNLOCKED • CHECKOUT A NEW DRAFT TO DEPLOY");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                statusText.text = exception.Message;
            }
            finally
            {
                busy = false;
                if (lastExpansion != null) Render(lastExpansion, statusText.text);
                else SetButtons(true);
            }
        }

        private void Render(TownExpansionView expansion, string message)
        {
            if (expansion == null) return;
            lastExpansion = expansion;
            var unlocked = new HashSet<string>(expansion.unlockedRegionIds ?? new List<string>());
            var offers = new Dictionary<string, TownRegionOfferDto>(StringComparer.Ordinal);
            foreach (var offer in expansion.availableRegions ?? new List<TownRegionOfferDto>())
                offers[offer.regionId] = offer;
            foreach (var view in regionButtons)
            {
                if (unlocked.Contains(view.RegionId))
                {
                    view.Label.text = view.RegionId.ToUpperInvariant() + "\nUNLOCKED";
                    view.Button.interactable = false;
                }
                else if (offers.TryGetValue(view.RegionId, out var offer))
                {
                    var prerequisitesMet = offer.prerequisiteRegionIds == null ||
                        offer.prerequisiteRegionIds.TrueForAll(unlocked.Contains);
                    view.Label.text = $"{offer.displayName.ToUpperInvariant()}\n{offer.goldCost:N0} GOLD";
                    view.Button.interactable = prerequisitesMet;
                }
                else view.Button.interactable = false;
            }
            statusText.text = string.IsNullOrWhiteSpace(message)
                ? $"MAP {expansion.mapTemplateId}@{expansion.mapVersion} • REV {expansion.revision}"
                : message;
        }

        private void SetButtons(bool value)
        {
            foreach (var view in regionButtons)
                if (view != null && view.Button != null) view.Button.interactable = value;
        }
    }
}
