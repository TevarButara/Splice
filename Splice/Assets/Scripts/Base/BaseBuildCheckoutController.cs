using System;
using System.Threading;
using Splice.Backend;
using TMPro;
using Splice.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.Base
{
    // Checkout owns its runtime wiring as well as the confirmation state. This avoids a silent dead button
    // when a scene merge or prefab override drops persistent Button.onClick entries.
    public class BaseBuildCheckoutController : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [Tooltip("panel ยืนยัน (ปิดไว้ตอนเริ่ม)")]
        [SerializeField] private GameObject confirmPanel;
        [Tooltip("ข้อความยืนยัน เช่น 'จ่าย X ทอง?'")]
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        private GameObject modalBackdrop;
        private CancellationTokenSource lifetimeCancellation;
        private string checkoutIdempotencyKey;
        private bool checkoutInFlight;

        public bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            ResolveReferences();
            BuildAndStyleConfirmation();
            WireButtons();
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (modalBackdrop != null) modalBackdrop.SetActive(false);
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            if (openButton != null) openButton.onClick.RemoveListener(OpenConfirm);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelConfirm);
        }

        public void OpenConfirm()
        {
            if (buildManager == null)
            {
                Debug.LogError("[BaseBuildCheckout] BaseBuildManager reference is missing.");
                return;
            }

            if (confirmPanel == null)
            {
                // Functional fallback: a missing visual must not make Checkout silently do nothing.
                Confirm();
                return;
            }

            RefreshConfirmation();
            checkoutIdempotencyKey = Guid.NewGuid().ToString("N");
            if (modalBackdrop != null)
            {
                modalBackdrop.SetActive(true);
                modalBackdrop.transform.SetAsLastSibling();
            }
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
        }

        public void Confirm() => _ = ConfirmAsync();

        private async System.Threading.Tasks.Task ConfirmAsync()
        {
            if (buildManager == null || checkoutInFlight) return;
            checkoutInFlight = true;
            var success = false;
            var error = string.Empty;
            if (SpliceServiceHub.IsRemoteMeta)
            {
                if (string.IsNullOrWhiteSpace(checkoutIdempotencyKey))
                    checkoutIdempotencyKey = Guid.NewGuid().ToString("N");
                try
                {
                    var result = await buildManager.CheckoutRemoteAsync(
                        checkoutIdempotencyKey, lifetimeCancellation.Token);
                    success = result.success;
                    error = result.error;
                }
                catch (OperationCanceledException)
                {
                    checkoutInFlight = false;
                    return;
                }
                catch (Exception exception)
                {
                    success = false;
                    error = exception.Message;
                }
            }
            else success = buildManager.Checkout();

            if (!success)
            {
                if (confirmLabel != null)
                    confirmLabel.text = "<color=#FF6B78><b>CHECKOUT FAILED</b></color>\n<size=22>" +
                                        (string.IsNullOrWhiteSpace(error)
                                            ? "Not enough Gold or town data is incomplete."
                                            : error) + "</size>";
                checkoutInFlight = false;
                return;
            }

            checkoutIdempotencyKey = string.Empty;
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (modalBackdrop != null) modalBackdrop.SetActive(false);
            checkoutInFlight = false;
            Debug.Log("[BaseBuildCheckout] Checkout confirmed and authoritative town draft persisted.");
        }

        public void CancelConfirm()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (modalBackdrop != null) modalBackdrop.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (buildManager == null) buildManager = FindFirstObjectByType<BaseBuildManager>();
            if (openButton == null) openButton = GetComponent<Button>();
            if (confirmPanel != null && confirmButton == null)
            {
                var buttons = confirmPanel.GetComponentsInChildren<Button>(true);
                if (buttons.Length > 0) confirmButton = buttons[0];
            }
            if (confirmPanel != null && confirmLabel == null)
                confirmLabel = confirmPanel.GetComponentInChildren<TMP_Text>(true);
        }

        private void WireButtons()
        {
            WireGuaranteed(openButton, OpenConfirm);
            WireGuaranteed(confirmButton, Confirm);
            WireGuaranteed(cancelButton, CancelConfirm);
        }

        private static void WireGuaranteed(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            // RemoveListener only removes this matching runtime callback; serialized scene events stay intact.
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void RefreshConfirmation()
        {
            var net = buildManager.NetCost;
            var remote = SpliceServiceHub.IsRemoteMeta;
            var canAfford = remote || net <= buildManager.WalletGold;
            if (confirmButton != null) confirmButton.interactable = canAfford && !checkoutInFlight;
            SetButtonLabel(confirmButton, canAfford
                ? remote ? "SYNC SERVER DRAFT" : "CONFIRM CHECKOUT"
                : "NOT ENOUGH GOLD");

            if (confirmLabel == null) return;
            var transaction = remote
                ? "SERVER VALIDATES NOW • GOLD CHARGED ON DEPLOY"
                : net > 0
                ? $"COST  <color=#FFBC57>{net:N0} GOLD</color>"
                : net < 0
                    ? $"REFUND  <color=#61E6A7>{-net:N0} GOLD</color>"
                    : "<color=#61E6A7>NO GOLD COST</color>";
            confirmLabel.text =
                $"<b>COMMIT TOWN CHANGES?</b>\n<size=28>{transaction}</size>\n" +
                $"<size=18><color=#A9B8CC>Wallet {buildManager.WalletGold:N0} • Defense " +
                $"{buildManager.UsedCapacity}/{buildManager.DefenseCapacity}</color></size>";
        }

        private void BuildAndStyleConfirmation()
        {
            if (confirmPanel == null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                confirmPanel.transform.SetParent(canvas.transform, false);
                modalBackdrop = CreateBackdrop(canvas.transform);
                var modalCanvas = confirmPanel.GetComponent<Canvas>();
                if (modalCanvas == null) modalCanvas = confirmPanel.AddComponent<Canvas>();
                modalCanvas.overrideSorting = true;
                modalCanvas.sortingOrder = 300;
                if (confirmPanel.GetComponent<GraphicRaycaster>() == null)
                    confirmPanel.AddComponent<GraphicRaycaster>();
            }
            var panelRect = confirmPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(.5f, .5f);
                panelRect.anchorMax = new Vector2(.5f, .5f);
                panelRect.pivot = new Vector2(.5f, .5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(860f, 480f);
                panelRect.localScale = Vector3.one;
            }

            var panelImage = confirmPanel.GetComponent<Image>();
            if (panelImage != null && !SpliceUiSkinLibrary.ApplyPanel(panelImage))
            {
                panelImage.color = new Color32(17, 26, 43, 248);
                EnsureOutline(confirmPanel, new Color32(57, 215, 210, 180), new Vector2(3f, -3f));
            }
            CreateHeaderSkin(confirmPanel.transform);

            if (confirmLabel != null)
            {
                var rect = confirmLabel.rectTransform;
                rect.anchorMin = new Vector2(.5f, 1f);
                rect.anchorMax = new Vector2(.5f, 1f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -70f);
                rect.sizeDelta = new Vector2(710f, 230f);
                rect.localScale = Vector3.one;
                confirmLabel.fontSize = 34f;
                confirmLabel.enableAutoSizing = true;
                confirmLabel.fontSizeMin = 18f;
                confirmLabel.fontSizeMax = 34f;
                confirmLabel.alignment = TextAlignmentOptions.Center;
                confirmLabel.color = new Color32(244, 248, 255, 255);
                confirmLabel.raycastTarget = false;
            }

            if (confirmButton != null)
            {
                SetButtonRect(confirmButton, new Vector2(185f, 55f), new Vector2(330f, 76f));
                StyleButton(confirmButton, new Color32(97, 230, 167, 255), new Color32(17, 26, 43, 255));
            }

            if (cancelButton == null) cancelButton = CreateCancelButton(confirmPanel.transform);
            else
            {
                SetButtonRect(cancelButton, new Vector2(-185f, 55f), new Vector2(330f, 76f));
                StyleButton(cancelButton, new Color32(32, 50, 76, 255), new Color32(244, 248, 255, 255));
                SetButtonLabel(cancelButton, "CANCEL");
            }
        }

        private static GameObject CreateBackdrop(Transform parent)
        {
            var go = new GameObject("Checkout Modal Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color32(8, 15, 27, 205);
            image.raycastTarget = true;
            return go;
        }

        private static Button CreateCancelButton(Transform parent)
        {
            var go = new GameObject("CheckoutCancelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var button = go.GetComponent<Button>();
            SetButtonRect(button, new Vector2(-185f, 55f), new Vector2(330f, 76f));
            StyleButton(button, new Color32(32, 50, 76, 255), new Color32(244, 248, 255, 255));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "CANCEL";
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(244, 248, 255, 255);
            label.raycastTarget = false;
            return button;
        }

        private static void SetButtonRect(Button button, Vector2 position, Vector2 size)
        {
            if (button == null) return;
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void StyleButton(Button button, Color background, Color foreground)
        {
            var image = button.GetComponent<Image>();
            var generatedSkin = image != null && SpliceUiSkinLibrary.ApplyButton(image,
                SpliceUiSkinLibrary.ButtonTint(background));
            if (image != null && !generatedSkin) image.color = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.9f, 1f, 1f, 1f);
            colors.pressedColor = new Color(.72f, .84f, .9f, 1f);
            colors.disabledColor = new Color(.35f, .4f, .47f, .7f);
            button.colors = colors;
            if (!generatedSkin) EnsureOutline(button.gameObject, new Color32(57, 215, 210, 125), new Vector2(2f, -2f));
            if (generatedSkin) foreground = new Color32(244, 248, 255, 255);

            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.color = foreground;
                tmp.fontStyle = FontStyles.Bold;
                tmp.fontSize = 20f;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.color = foreground;
                legacy.fontStyle = FontStyle.Bold;
                legacy.fontSize = 20;
                legacy.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void SetButtonLabel(Button button, string value)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = value;
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = value;
        }

        private static void CreateHeaderSkin(Transform parent)
        {
            var existing = parent.Find("Checkout Header Skin");
            var go = existing != null
                ? existing.gameObject
                : new GameObject("Checkout Header Skin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            if (existing == null) rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -14f);
            rect.sizeDelta = new Vector2(720f, 135f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            if (!SpliceUiSkinLibrary.ApplyHeader(image)) image.color = new Color32(32, 50, 76, 255);
            rect.SetAsFirstSibling();
        }

        private static void EnsureOutline(GameObject target, Color color, Vector2 distance)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }
    }
}
