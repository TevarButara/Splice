using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Prototype B / Step 6A presentation layer. The hierarchy is generated at runtime so the full card and
    // modal remain a single reusable unit. All dimensions use Canvas coordinates and safe anchors.
    public sealed class TownSnapshotCommitController : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(17, 26, 43, 255);
        private static readonly Color Panel = new Color32(23, 36, 58, 255);
        private static readonly Color PanelSoft = new Color32(32, 50, 76, 255);
        private static readonly Color Cyan = new Color32(57, 215, 210, 255);
        private static readonly Color Mint = new Color32(97, 230, 167, 255);
        private static readonly Color Amber = new Color32(255, 188, 87, 255);
        private static readonly Color Coral = new Color32(255, 107, 120, 255);
        private static readonly Color White = new Color32(244, 248, 255, 255);
        private static readonly Color Muted = new Color32(169, 184, 204, 255);

        [SerializeField] private BaseBuildManager buildManager;

        private GameObject modalRoot;
        private TMP_Text statusPill;
        private TMP_Text statusHeadline;
        private TMP_Text statusBody;
        private TMP_Text modalSubtitle;
        private TMP_Text towerValue;
        private TMP_Text garrisonValue;
        private TMP_Text powerValue;
        private TMP_Text validationText;
        private Button deployButton;
        private TMP_Text deployButtonLabel;
        private CancellationTokenSource lifetimeCancellation;
        private bool refreshInFlight;
        private bool deploying;
        private string deployIdempotencyKey;

        private sealed class DeployableCheck
        {
            public BaseLayout layout;
            public TownSnapshotValidationReport report;
            public bool valid;
        }

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            if (buildManager == null) buildManager = FindFirstObjectByType<BaseBuildManager>();
            SpliceUiSkinLibrary.EnsureLoaded();
            BuildUi();
            RefreshStatus();
        }

        private void OnEnable() => InvokeRepeating(nameof(RefreshStatus), 0.35f, 0.35f);

        private void OnDisable() => CancelInvoke(nameof(RefreshStatus));

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
        }

        public void OpenReview()
        {
            if (modalRoot == null) return;
            modalRoot.SetActive(true);
            deployIdempotencyKey = Guid.NewGuid().ToString("N");
            _ = RenderReviewAsync(false);
        }

        public void CloseReview()
        {
            if (modalRoot != null) modalRoot.SetActive(false);
            deployIdempotencyKey = string.Empty;
        }

        public void DeploySnapshot() => _ = DeploySnapshotAsync();

        private async Task DeploySnapshotAsync()
        {
            if (deploying) return;
            deploying = true;
            var check = await TryGetDeployableAsync();
            if (!check.valid)
            {
                RenderValidation(check.report, false);
                deploying = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(deployIdempotencyKey))
                deployIdempotencyKey = Guid.NewGuid().ToString("N");
            var result = await SpliceServiceHub.TownSnapshots.DeployAsync(new DeployTownRequest
            {
                checkedOutLayout = check.layout,
                usedCapacity = buildManager.UsedCapacity,
                maxCapacity = buildManager.DefenseCapacity,
            }, deployIdempotencyKey, lifetimeCancellation.Token);
            if (!result.success || result.snapshot == null)
            {
                check.report.errors.Insert(0, string.IsNullOrWhiteSpace(result.error)
                    ? "Snapshot deployment failed."
                    : result.error);
                RenderValidation(check.report, false);
                deploying = false;
                return;
            }

            var snapshot = result.snapshot;
            modalSubtitle.text = $"DEPLOYED • REVISION {snapshot.revision} • IMMUTABLE ID {ShortId(snapshot.snapshotId)}";
            powerValue.text = snapshot.basePowerRating.ToString("N0");
            validationText.text =
                $"<color=#{ColorUtility.ToHtmlStringRGB(Mint)}><b>SNAPSHOT COMMITTED</b></color>\n\n" +
                $"Town revision {snapshot.revision} is now eligible for the local target pool. " +
                "Editing the draft will not change this deployed copy. Commit a new revision when you are ready.";
            deployButton.interactable = false;
            deployButtonLabel.text = "DEPLOYED";
            deployIdempotencyKey = string.Empty;
            deploying = false;
            RefreshStatus();
        }

        private async Task<DeployableCheck> TryGetDeployableAsync()
        {
            var result = new DeployableCheck
            {
                report = new TownSnapshotValidationReport(),
            };
            if (buildManager == null)
            {
                result.report.errors.Add("Build manager is not available in this scene.");
                return result;
            }

            var draft = await SpliceServiceHub.TownSnapshots.GetCheckedOutDraftAsync(
                buildManager.EditingFactionId, lifetimeCancellation.Token);
            result.layout = draft?.checkedOutLayout;
            result.report = TownSnapshotValidator.Validate(result.layout,
                buildManager.UsedCapacity, buildManager.DefenseCapacity);
            if (buildManager.HasUnsavedChanges || buildManager.NetCost != 0)
                result.report.errors.Insert(0, "Draft changed: press Checkout before deployment review.");
            result.valid = result.report.IsValid;
            return result;
        }

        private async Task RenderReviewAsync(bool preserveMessage)
        {
            DeployableCheck check;
            TownDefenseSnapshot latest;
            try
            {
                check = await TryGetDeployableAsync();
                latest = buildManager != null
                    ? await SpliceServiceHub.TownSnapshots.GetLatestAsync(
                        buildManager.EditingFactionId, lifetimeCancellation.Token)
                    : null;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (modalSubtitle == null) return;
            modalSubtitle.text = latest == null
                ? "FIRST DEPLOYMENT • REVIEW THE CHECKED-OUT DRAFT"
                : $"NEXT REVISION {latest.revision + 1} • CURRENTLY DEPLOYED V{latest.revision}";
            towerValue.text = (check.layout?.towers?.Count ?? 0).ToString();
            garrisonValue.text = (check.layout?.garrison?.Count ?? 0).ToString();
            powerValue.text = check.layout == null || buildManager == null
                ? "—"
                : TownSnapshotValidator.CalculateBasePower(
                    check.layout, buildManager.UsedCapacity,
                    Splice.Core.PlayerProfile.BaseLevel(check.layout.factionId)).ToString("N0");
            deployButton.interactable = check.valid && !deploying;
            deployButtonLabel.text = check.valid ? "DEPLOY SNAPSHOT" : "FIX DRAFT FIRST";
            if (!preserveMessage) RenderValidation(check.report, check.valid);
        }

        private void RenderValidation(TownSnapshotValidationReport report, bool valid)
        {
            var sb = new StringBuilder();
            if (valid)
            {
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Mint)}><b>READY TO DEPLOY</b></color>\n");
                sb.Append("The checked-out layout passed the Step 6A snapshot rules.\n");
            }
            else
            {
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Coral)}><b>DEPLOYMENT BLOCKED</b></color>\n");
                foreach (var error in report.errors) sb.Append($"• {error}\n");
            }

            if (report.warnings.Count > 0)
            {
                sb.Append($"\n<color=#{ColorUtility.ToHtmlStringRGB(Amber)}><b>TACTICAL NOTES</b></color>\n");
                foreach (var warning in report.warnings) sb.Append($"• {warning}\n");
            }
            validationText.text = sb.ToString().TrimEnd();
        }

        private void RefreshStatus() => _ = RefreshStatusAsync();

        private async Task RefreshStatusAsync()
        {
            if (refreshInFlight || statusPill == null) return;
            refreshInFlight = true;
            if (buildManager == null)
            {
                SetStatus("OFFLINE", Coral, "DEPLOYMENT UNAVAILABLE", "Build manager was not found.");
                refreshInFlight = false;
                return;
            }

            var faction = buildManager.EditingFactionId;
            TownDefenseSnapshot latest;
            TownDraftView draft;
            try
            {
                latest = await SpliceServiceHub.TownSnapshots.GetLatestAsync(
                    faction, lifetimeCancellation.Token);
                draft = await SpliceServiceHub.TownSnapshots.GetCheckedOutDraftAsync(
                    faction, lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                refreshInFlight = false;
                return;
            }
            catch (Exception exception)
            {
                SetStatus("OFFLINE", Coral, "DEPLOYMENT UNAVAILABLE", exception.Message);
                refreshInFlight = false;
                return;
            }
            if (buildManager.HasUnsavedChanges || buildManager.NetCost != 0)
            {
                var cost = buildManager.NetCost == 0 ? "layout changed" : $"checkout {buildManager.NetCost:N0} Gold";
                SetStatus("DRAFT", Amber, "UNSAVED DEFENSE", $"{cost} • Cap {buildManager.UsedCapacity}/{buildManager.DefenseCapacity}");
            }
            else if (latest != null)
            {
                SetStatus($"DEPLOYED V{latest.revision}", Mint, $"POWER {latest.basePowerRating:N0}",
                    $"Immutable snapshot • Cap {latest.usedCapacity}/{latest.maxCapacity}");
            }
            else if (draft?.exists == true)
            {
                SetStatus("READY", Cyan, "CHECKED-OUT TOWN", "Review validation and deploy the first snapshot.");
            }
            else
            {
                SetStatus("DRAFT", Amber, "NO TOWN SNAPSHOT", "Place defenses, Checkout, then deploy.");
            }
            refreshInFlight = false;
        }

        private void SetStatus(string pill, Color color, string headline, string body)
        {
            statusPill.text = pill;
            statusPill.color = color;
            statusHeadline.text = headline;
            statusBody.text = body;
        }

        private void BuildUi()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TownSnapshotUI] Controller must be placed under a Canvas.");
                return;
            }

            var layer = NewRect("Town Deployment UI", canvas.transform);
            Stretch(layer);
            layer.SetAsLastSibling();

            var card = PanelObject("Deployment Status Card", layer, Panel, new Vector2(600f, 280f));
            Anchor(card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -125f));
            if (!SpliceUiSkinLibrary.ApplyPanel(card.GetComponent<Image>()))
                AddOutline(card.gameObject, Cyan, new Vector2(2f, -2f));

            var accent = PanelObject("Accent", card, Cyan, new Vector2(7f, 280f));
            Anchor(accent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);

            Text("TOWN DEPLOYMENT", card, new Vector2(48f, -34f), new Vector2(330f, 30f), 20f, White, FontStyles.Bold);
            statusPill = Text("DRAFT", card, new Vector2(382f, -34f), new Vector2(165f, 30f), 16f, Amber,
                FontStyles.Bold, TextAlignmentOptions.Right);
            statusHeadline = Text("NO TOWN SNAPSHOT", card, new Vector2(48f, -80f), new Vector2(500f, 40f), 27f,
                White, FontStyles.Bold);
            statusBody = Text("Place defenses, Checkout, then deploy.", card, new Vector2(48f, -125f),
                new Vector2(500f, 46f), 17f, Muted, FontStyles.Normal);
            var review = Button("Review Deployment", card, new Vector2(48f, -192f), new Vector2(500f, 58f), Cyan,
                "REVIEW & DEPLOY", Ink);
            review.onClick.AddListener(OpenReview);

            modalRoot = PanelObject("Deployment Modal Backdrop", layer, new Color(0.025f, 0.04f, 0.075f, 0.84f), Vector2.zero).gameObject;
            Stretch(modalRoot.GetComponent<RectTransform>());
            var modalCard = PanelObject("Deployment Review Card", modalRoot.transform, Ink, new Vector2(900f, 680f));
            Anchor(modalCard, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
            if (!SpliceUiSkinLibrary.ApplyPanel(modalCard.GetComponent<Image>()))
                AddOutline(modalCard.gameObject, Cyan, new Vector2(2f, -2f));

            var headerAccent = PanelObject("Deployment Header Skin", modalCard, PanelSoft, new Vector2(790f, 132f));
            Anchor(headerAccent, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -12f));
            SpliceUiSkinLibrary.ApplyHeader(headerAccent.GetComponent<Image>());
            headerAccent.SetAsFirstSibling();
            Text("READY TO DEPLOY TOWN", modalCard, new Vector2(115f, -39f), new Vector2(670f, 44f), 32f, White,
                FontStyles.Bold, TextAlignmentOptions.Center);
            modalSubtitle = Text("FIRST DEPLOYMENT", modalCard, new Vector2(43f, -84f), new Vector2(690f, 28f), 15f,
                Cyan, FontStyles.Bold);
            var close = Button("Close Review", modalCard, new Vector2(806f, -36f), new Vector2(52f, 52f), PanelSoft, "×", White, 30f);
            close.onClick.AddListener(CloseReview);

            towerValue = StatCard(modalCard, new Vector2(42f, -133f), "TOWERS");
            garrisonValue = StatCard(modalCard, new Vector2(314f, -133f), "GARRISON");
            powerValue = StatCard(modalCard, new Vector2(586f, -133f), "BASE POWER");

            var validationPanel = PanelObject("Validation Panel", modalCard, Panel, new Vector2(816f, 318f));
            Anchor(validationPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -265f));
            validationText = Text("Validation", validationPanel, new Vector2(26f, -23f), new Vector2(764f, 272f), 18f,
                White, FontStyles.Normal);
            validationText.enableWordWrapping = true;
            validationText.overflowMode = TextOverflowModes.Ellipsis;

            var cancel = Button("Cancel Deployment", modalCard, new Vector2(42f, -612f), new Vector2(250f, 48f),
                PanelSoft, "BACK TO BUILD", White);
            cancel.onClick.AddListener(CloseReview);
            deployButton = Button("Deploy Snapshot", modalCard, new Vector2(514f, -612f), new Vector2(344f, 48f),
                Mint, "DEPLOY SNAPSHOT", Ink);
            deployButtonLabel = deployButton.GetComponentInChildren<TMP_Text>();
            deployButton.onClick.AddListener(DeploySnapshot);
            modalRoot.SetActive(false);
        }

        private static TMP_Text StatCard(Transform parent, Vector2 position, string label)
        {
            var card = PanelObject(label + " Stat", parent, PanelSoft, new Vector2(230f, 104f));
            Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position);
            Text(label, card, new Vector2(18f, -14f), new Vector2(194f, 22f), 14f, Muted, FontStyles.Bold);
            return Text("—", card, new Vector2(18f, -43f), new Vector2(194f, 43f), 28f, White, FontStyles.Bold);
        }

        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, Color color,
            string label, Color labelColor, float fontSize = 16f)
        {
            var rect = PanelObject(name, parent, color, size);
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var generatedSkin = size.y > 0f && size.x / size.y >= 2.4f &&
                                SpliceUiSkinLibrary.ApplyButton(rect.GetComponent<Image>(),
                                    SpliceUiSkinLibrary.ButtonTint(color));
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.88f, .96f, 1f, 1f);
            colors.pressedColor = new Color(.72f, .84f, .9f, 1f);
            colors.disabledColor = new Color(.35f, .4f, .47f, .7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (generatedSkin) labelColor = White;
            var text = Text(label, rect, Vector2.zero, size, fontSize, labelColor, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static TMP_Text Text(string value, Transform parent, Vector2 position, Vector2 size, float fontSize,
            Color color, FontStyles style, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var rect = NewRect(value + " Text", parent);
            rect.sizeDelta = size;
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform PanelObject(string name, Transform parent, Color color, Vector2 size)
        {
            var rect = NewRect(name, parent);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, .55f);
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }

        private static string ShortId(string id) => string.IsNullOrEmpty(id) ? "—" : id[..Mathf.Min(8, id.Length)].ToUpperInvariant();
    }
}
