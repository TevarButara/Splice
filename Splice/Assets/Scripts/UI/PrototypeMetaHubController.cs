using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>
    /// Prototype meta shell for BuildZone. Town editing remains the 3D background while Raid and
    /// Defense History are focused overlays. This deliberately uses only runtime UGUI primitives:
    /// it is deterministic, build-safe, and can later receive final art without changing the flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeMetaHubController : MonoBehaviour
    {
        private static readonly Color Backdrop = new(0.025f, 0.035f, 0.055f, .94f);
        private static readonly Color Header = new(0.045f, 0.06f, 0.09f, .96f);
        private static readonly Color Panel = new(0.075f, 0.095f, 0.13f, .97f);
        private static readonly Color PanelSoft = new(0.105f, 0.13f, 0.17f, .98f);
        private static readonly Color Lime = new(0.65f, 0.95f, 0.25f, 1f);
        private static readonly Color Coral = new(1f, 0.30f, 0.42f, 1f);
        private static readonly Color Cyan = new(0.20f, 0.76f, 1f, 1f);
        private static readonly Color Amber = new(1f, 0.72f, 0.22f, 1f);
        private static readonly Color White = new(0.96f, 0.98f, 1f, 1f);
        private static readonly Color Muted = new(0.64f, 0.70f, 0.78f, 1f);
        private const string OnboardingKey = "Splice.Prototype.Onboarding.v1";

        [SerializeField] private BaseBuildManager buildManager;

        private RaidTargetProvider targetProvider;
        private RaidHistoryController historyController;
        private CancellationTokenSource lifetimeCancellation;
        private GameObject contentBackdrop;
        private GameObject raidPanel;
        private GameObject historyPanel;
        private GameObject onboardingPanel;
        private Transform targetList;
        private Transform historyList;
        private TMP_Text sectionTitle;
        private TMP_Text statusText;
        private TMP_Text walletText;
        private Button townTab;
        private Button raidTab;
        private Button historyTab;
        private bool refreshingTargets;
        private bool refreshingHistory;

        public bool IsRaidPanelVisible => raidPanel != null && raidPanel.activeSelf;
        public bool IsHistoryPanelVisible => historyPanel != null && historyPanel.activeSelf;
        public bool IsOnboardingVisible => onboardingPanel != null && onboardingPanel.activeSelf;

        private void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            if (buildManager == null) buildManager = FindFirstObjectByType<BaseBuildManager>();
            EnsureActiveFaction();
            targetProvider = GetComponent<RaidTargetProvider>();
            if (targetProvider == null) targetProvider = gameObject.AddComponent<RaidTargetProvider>();
            if (buildManager != null) targetProvider.ConfigureRegistry(buildManager.Registry);
            historyController = GetComponent<RaidHistoryController>();
            if (historyController == null) historyController = gameObject.AddComponent<RaidHistoryController>();
            BuildUi();
            ShowTown();
        }

        private IEnumerator Start()
        {
            // BaseBuildManager and palette also initialize in Start. One frame makes the first-launch
            // faction assignment visible to them without depending on Script Execution Order.
            yield return null;
            foreach (var palette in FindObjectsByType<BaseBuildPalette>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                palette.Rebuild();
            _ = RefreshWalletAsync();
            if (!PlayerPrefs.HasKey(OnboardingKey) && onboardingPanel != null)
                onboardingPanel.SetActive(true);
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        public void ShowTown()
        {
            SetPanelState(false, false);
            if (sectionTitle != null) sectionTitle.text = "TOWN COMMAND";
            if (statusText != null)
                statusText.text = "Build defenses • Checkout the draft • Review & deploy an immutable snapshot";
            SetTabState(townTab, true, Lime);
            SetTabState(raidTab, false, Coral);
            SetTabState(historyTab, false, Cyan);
        }

        public void ShowRaid()
        {
            SetPanelState(true, false);
            if (sectionTitle != null) sectionTitle.text = "RAID TARGETS";
            SetTabState(townTab, false, Lime);
            SetTabState(raidTab, true, Coral);
            SetTabState(historyTab, false, Cyan);
            _ = RefreshTargetsAsync();
        }

        public void ShowHistory()
        {
            SetPanelState(false, true);
            if (sectionTitle != null) sectionTitle.text = "DEFENSE HISTORY";
            SetTabState(townTab, false, Lime);
            SetTabState(raidTab, false, Coral);
            SetTabState(historyTab, true, Cyan);
            _ = RefreshHistoryAsync();
        }

        public void CompleteOnboarding()
        {
            PlayerPrefs.SetInt(OnboardingKey, 1);
            PlayerPrefs.Save();
            if (onboardingPanel != null) onboardingPanel.SetActive(false);
        }

        public void ResetOnboardingForTests()
        {
            PlayerPrefs.DeleteKey(OnboardingKey);
            if (onboardingPanel != null) onboardingPanel.SetActive(true);
        }

        private void EnsureActiveFaction()
        {
            if (PlayerProfile.HasActiveFaction || buildManager?.Registry == null) return;
            foreach (var faction in buildManager.Registry.Factions)
            {
                if (faction == null || string.IsNullOrWhiteSpace(faction.factionId)) continue;
                PlayerProfile.UnlockFaction(faction.factionId);
                PlayerProfile.ActiveFactionId = faction.factionId;
                Debug.Log($"[PrototypeHub] First-launch faction selected: {faction.factionId}", this);
                break;
            }
        }

        private async Task RefreshWalletAsync()
        {
            try
            {
                var wallet = await SpliceServiceHub.Wallet.GetWalletAsync(lifetimeCancellation.Token);
                if (walletText != null)
                    walletText.text = $"GOLD  {PlayerWallet.MetaGold:N0}    GEMS  {wallet.warGemBalance:N0}";
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (walletText != null) walletText.text = $"GOLD  {PlayerWallet.MetaGold:N0}    GEMS  --";
                Debug.LogWarning("[PrototypeHub] Wallet unavailable: " + exception.Message, this);
            }
        }

        private async Task RefreshTargetsAsync()
        {
            if (refreshingTargets || targetProvider == null || lifetimeCancellation == null) return;
            refreshingTargets = true;
            SetStatus("SCANNING THE WORLD FOR RAIDABLE TOWNS…", Muted);
            ClearChildren(targetList);
            try
            {
                var targets = await targetProvider.GenerateTargetsAsync(lifetimeCancellation.Token);
                RenderTargets(targets);
                SetStatus(targets.Count == 0
                    ? "NO ELIGIBLE TARGETS • TRY AGAIN OR DEPLOY MORE LOCAL TEST TOWNS"
                    : $"{CountRaidable(targets)} RAIDABLE TARGETS • REWARDS AND RISK ARE SHOWN BEFORE COMMIT",
                    targets.Count == 0 ? Amber : Muted);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                SetStatus("TARGET SERVICE UNAVAILABLE • " + exception.Message.ToUpperInvariant(), Coral);
                RenderRetry(targetList, ShowRaid);
            }
            finally
            {
                refreshingTargets = false;
            }
        }

        private async Task RefreshHistoryAsync()
        {
            if (refreshingHistory || historyController == null || lifetimeCancellation == null) return;
            refreshingHistory = true;
            SetStatus("LOADING VERIFIED DEFENSE REPORTS…", Muted);
            ClearChildren(historyList);
            try
            {
                await historyController.RefreshAsync();
                RenderHistory(historyController.Items);
                if (!string.IsNullOrWhiteSpace(historyController.LastError))
                    SetStatus("HISTORY SERVICE UNAVAILABLE • " +
                              historyController.LastError.ToUpperInvariant(), Coral);
                else
                    SetStatus(historyController.Items.Count == 0
                        ? "NO DEFENSE REPORTS YET • DEPLOY A TOWN, THEN RUN AN INCOMING RAID"
                        : $"{historyController.Items.Count} VERIFIED REPORTS • REPLAY OR REVENGE WHEN ELIGIBLE",
                        historyController.Items.Count == 0 ? Amber : Muted);
            }
            catch (Exception exception)
            {
                SetStatus("HISTORY SERVICE UNAVAILABLE • " + exception.Message.ToUpperInvariant(), Coral);
                RenderRetry(historyList, ShowHistory);
            }
            finally
            {
                refreshingHistory = false;
            }
        }

        private void RenderTargets(IReadOnlyList<RaidTarget> targets)
        {
            if (targetList == null) return;
            var shown = Mathf.Min(3, targets?.Count ?? 0);
            for (var index = 0; index < shown; index++)
            {
                var target = targets[index];
                var card = CreatePanel($"Target {index + 1}", targetList, Panel, new Vector2(480f, 535f));
                var source = target.IsSnapshotBacked ? $"PLAYER SNAPSHOT  V{target.snapshotRevision}" : "WORLD BOT OUTPOST";
                CreateText(card, target.displayName.ToUpperInvariant(), 28f, White,
                    TextAlignmentOptions.TopLeft, new Vector2(34f, -35f), new Vector2(412f, 76f), FontStyles.Bold);
                CreateText(card, source, 17f, target.IsSnapshotBacked ? Cyan : Muted,
                    TextAlignmentOptions.TopLeft, new Vector2(34f, -114f), new Vector2(412f, 30f), FontStyles.Bold);
                CreateText(card,
                    $"POWER  <b>{target.basePowerRating:N0}</b>\n" +
                    $"DEFENSE  {target.towerCount} towers  •  {target.garrisonCount} garrison\n" +
                    $"CAPACITY  {target.usedCapacity}/{target.maxCapacity}\n\n" +
                    $"EXPECTED GOLD  <color=#{ColorUtility.ToHtmlStringRGB(Amber)}><b>{target.StoredGold:N0}</b></color>\n" +
                    "WAR GEM STAKE  <b>100</b>\nFULL VICTORY  <color=#A6F23F><b>+180</b></color>",
                    22f, White, TextAlignmentOptions.TopLeft, new Vector2(34f, -168f),
                    new Vector2(412f, 250f), FontStyles.Normal);
                var captured = index;
                var canRaid = target.CanRaid(PlayerProfile.AccountId, out var reason);
                var button = CreateButton(card, canRaid ? "REVIEW RAID CONTRACT" : "INSPECTION ONLY",
                    new Vector2(34f, 45f), new Vector2(412f, 76f), canRaid ? Coral : PanelSoft,
                    canRaid ? White : Muted, () => StartRaid(captured));
                button.interactable = canRaid;
                if (!canRaid)
                    CreateText(card, reason, 14f, Muted, TextAlignmentOptions.BottomLeft,
                        new Vector2(34f, 126f), new Vector2(412f, 34f), FontStyles.Normal, false);
            }
            if (shown == 0) RenderEmpty(targetList, "NO RAIDABLE TOWNS", "Refresh the target pool or switch to local services.");
        }

        private void StartRaid(int index)
        {
            var targets = targetProvider?.LastBuildResult?.targets;
            if (targets == null || index < 0 || index >= targets.Count)
            {
                SetStatus("TARGET CHANGED • REFRESH AND TRY AGAIN", Coral);
                return;
            }
            if (!RaidContext.TrySelectTarget(targets[index], PlayerProfile.ActiveFactionId,
                    PlayerProfile.AccountId, out var error))
            {
                SetStatus(error.ToUpperInvariant(), Coral);
                return;
            }
            PrototypeFlowRouter.LoadRaid();
        }

        private void RenderHistory(IReadOnlyList<RaidDefenseHistoryItemDto> items)
        {
            if (historyList == null) return;
            var shown = Mathf.Min(4, items?.Count ?? 0);
            for (var index = 0; index < shown; index++)
            {
                var item = items[index];
                var row = CreatePanel($"Defense Report {index + 1}", historyList, Panel,
                    new Vector2(1480f, 155f));
                var held = item.outcome != "FULL_VICTORY" && item.outcome != "EXTRACTED";
                CreateText(row, held ? "DEFENSE HELD" : "TOWN BREACHED", 25f,
                    held ? Lime : Coral, TextAlignmentOptions.TopLeft,
                    new Vector2(30f, -24f), new Vector2(275f, 38f), FontStyles.Bold);
                CreateText(row,
                    $"{ShortName(item.attackerDisplayName)}  •  {item.outcome}\n" +
                    $"{FormatUtc(item.completedUtc)}  •  Rings {item.breachedRings}  •  " +
                    $"War Gems {Signed(item.defenderWarGemDelta)}",
                    19f, White, TextAlignmentOptions.TopLeft,
                    new Vector2(320f, -25f), new Vector2(650f, 88f), FontStyles.Normal);
                var captured = index;
                var replay = CreateButton(row, item.replayAvailable ? "PLAY REPLAY" : "REPLAY UNAVAILABLE",
                    new Vector2(1010f, 39f), new Vector2(205f, 66f),
                    item.replayAvailable ? Cyan : PanelSoft, item.replayAvailable ? White : Muted,
                    () => PlayReplay(captured));
                replay.interactable = item.replayAvailable;
                var revenge = CreateButton(row, item.revengeAvailable ? "REVENGE" : item.revengeState,
                    new Vector2(1230f, 39f), new Vector2(220f, 66f),
                    item.revengeAvailable ? Coral : PanelSoft, item.revengeAvailable ? White : Muted,
                    () => _ = StartRevengeAsync(captured));
                revenge.interactable = item.revengeAvailable;
            }
            if (shown == 0) RenderEmpty(historyList, "YOUR TOWN HAS NOT BEEN RAIDED",
                "Deploy a snapshot, then use the incoming-defense route to generate a verified report.");
        }

        private void PlayReplay(int index)
        {
            if (historyController.PlayReplay(index)) return;
            SetStatus(historyController.LastError.ToUpperInvariant(), Coral);
        }

        private async Task StartRevengeAsync(int index)
        {
            SetStatus("PREPARING IMMUTABLE REVENGE TARGET…", Muted);
            var result = await historyController.PrepareRevengeAsync(index);
            if (result.success) return; // Controller owns the scene handoff.
            SetStatus((result.error ?? historyController.LastError).ToUpperInvariant(), Coral);
        }

        private void BuildUi()
        {
            var canvas = FindHubCanvas();
            if (canvas == null)
            {
                Debug.LogError("[PrototypeHub] BuildZone requires a Canvas.", this);
                return;
            }

            var root = CreateRect("Prototype Meta UI", null);
            Stretch(root);
            var overlayCanvas = root.gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 500;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var header = CreatePanel("Command Header", root, Header, Vector2.zero);
            SetStretchTop(header, 98f);
            header.GetComponent<Image>().raycastTarget = false;
            sectionTitle = CreateText(header, "TOWN COMMAND", 31f, White,
                TextAlignmentOptions.Left, new Vector2(34f, -20f), new Vector2(600f, 55f), FontStyles.Bold);
            statusText = CreateText(header, string.Empty, 16f, Muted,
                TextAlignmentOptions.Left, new Vector2(34f, -61f), new Vector2(1100f, 28f), FontStyles.Normal);
            walletText = CreateText(header, $"GOLD  {PlayerWallet.MetaGold:N0}    GEMS  ...", 22f, White,
                TextAlignmentOptions.Right, new Vector2(-34f, -31f), new Vector2(560f, 48f), FontStyles.Bold);
            SetTopRight(walletText.rectTransform, new Vector2(-34f, -26f), new Vector2(560f, 48f));

            contentBackdrop = CreatePanel("Meta Content Backdrop", root, Backdrop, Vector2.zero).gameObject;
            var contentRect = contentBackdrop.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(0f, 104f);
            contentRect.offsetMax = new Vector2(0f, -104f);

            raidPanel = CreateRect("Raid Target Screen", contentBackdrop.transform).gameObject;
            Stretch(raidPanel.GetComponent<RectTransform>());
            historyPanel = CreateRect("Defense History Screen", contentBackdrop.transform).gameObject;
            Stretch(historyPanel.GetComponent<RectTransform>());
            BuildRaidPanel(raidPanel.transform);
            BuildHistoryPanel(historyPanel.transform);

            var nav = CreatePanel("Primary Navigation", root, Header, Vector2.zero);
            SetStretchBottom(nav, 98f);
            townTab = CreateButton(nav, "TOWN", new Vector2(-390f, 12f), new Vector2(350f, 72f), PanelSoft, White, ShowTown);
            raidTab = CreateButton(nav, "RAID", new Vector2(0f, 12f), new Vector2(350f, 72f), PanelSoft, White, ShowRaid);
            historyTab = CreateButton(nav, "DEFENSE", new Vector2(390f, 12f), new Vector2(350f, 72f), PanelSoft, White, ShowHistory);

            BuildOnboarding(root);
        }

        private static Canvas FindHubCanvas()
        {
            Canvas fallback = null;
            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (!candidate.isRootCanvas || candidate.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (candidate.name == "UI") return candidate;
                fallback ??= candidate;
            }
            return fallback;
        }

        private void BuildRaidPanel(Transform root)
        {
            CreateText(root, "CHOOSE A TARGET", 40f, White, TextAlignmentOptions.TopLeft,
                new Vector2(70f, -54f), new Vector2(700f, 58f), FontStyles.Bold);
            CreateText(root, "Scout power, loot and risk before locking the raid contract.", 20f, Muted,
                TextAlignmentOptions.TopLeft, new Vector2(70f, -108f), new Vector2(920f, 36f), FontStyles.Normal);
            CreateButton(root, "REFRESH TARGETS", new Vector2(-70f, -50f), new Vector2(270f, 62f),
                PanelSoft, White, () => _ = RefreshTargetsAsync(), true);
            targetList = CreateRect("Target Cards", root);
            SetCentered((RectTransform)targetList, new Vector2(1520f, 560f), new Vector2(0f, -22f));
            var layout = targetList.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void BuildHistoryPanel(Transform root)
        {
            CreateText(root, "DEFENSE REPORTS", 40f, White, TextAlignmentOptions.TopLeft,
                new Vector2(70f, -54f), new Vector2(700f, 58f), FontStyles.Bold);
            CreateText(root, "Verified outcomes from attacks against your deployed town snapshot.", 20f, Muted,
                TextAlignmentOptions.TopLeft, new Vector2(70f, -108f), new Vector2(920f, 36f), FontStyles.Normal);
            CreateButton(root, "REFRESH REPORTS", new Vector2(-70f, -50f), new Vector2(270f, 62f),
                PanelSoft, White, () => _ = RefreshHistoryAsync(), true);
            historyList = CreateRect("History Rows", root);
            SetCentered((RectTransform)historyList, new Vector2(1480f, 690f), new Vector2(0f, -30f));
            var layout = historyList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void BuildOnboarding(Transform root)
        {
            onboardingPanel = CreatePanel("First Raid Briefing", root, new Color(0.015f, 0.02f, 0.035f, .98f),
                Vector2.zero).gameObject;
            Stretch(onboardingPanel.GetComponent<RectTransform>());
            var card = CreatePanel("Briefing Card", onboardingPanel.transform, Panel, new Vector2(1040f, 720f));
            SetCentered(card, new Vector2(1040f, 720f), Vector2.zero);
            CreateText(card, "YOUR TOWN. YOUR RAID PLAN.", 42f, White, TextAlignmentOptions.Center,
                new Vector2(70f, -55f), new Vector2(900f, 72f), FontStyles.Bold);
            CreateText(card,
                "<color=#A6F23F><b>1  BUILD & CHECKOUT</b></color>\n" +
                "Place towers and garrison, then commit the town draft.\n\n" +
                "<color=#31C2FF><b>2  DEPLOY A SNAPSHOT</b></color>\n" +
                "Review the immutable defense copy other players will raid.\n\n" +
                "<color=#FF4D6B><b>3  RAID FOR WAR GEMS</b></color>\n" +
                "Pick a target, inspect stake and payout, control your Hero, then extract or destroy the Core.\n\n" +
                "<color=#A3B2C6>Defense reports unlock Replay and Revenge when verified.</color>",
                25f, White, TextAlignmentOptions.TopLeft,
                new Vector2(95f, -165f), new Vector2(850f, 390f), FontStyles.Normal);
            CreateButton(card, "ENTER TOWN", new Vector2(270f, 48f), new Vector2(500f, 82f),
                Lime, new Color(0.05f, 0.07f, 0.1f, 1f), CompleteOnboarding);
        }

        private void SetPanelState(bool showRaid, bool showHistory)
        {
            if (contentBackdrop != null) contentBackdrop.SetActive(showRaid || showHistory);
            if (raidPanel != null) raidPanel.SetActive(showRaid);
            if (historyPanel != null) historyPanel.SetActive(showHistory);
        }

        private void SetStatus(string value, Color color)
        {
            if (statusText == null) return;
            statusText.text = value;
            statusText.color = color;
        }

        private static int CountRaidable(IReadOnlyList<RaidTarget> targets)
        {
            var count = 0;
            if (targets == null) return count;
            foreach (var target in targets)
                if (target != null && target.CanRaid(PlayerProfile.AccountId, out _)) count++;
            return count;
        }

        private static void SetTabState(Button button, bool selected, Color accent)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = selected ? accent : PanelSoft;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = selected ? new Color(.04f, .055f, .075f, 1f) : White;
        }

        private static void RenderEmpty(Transform parent, string title, string body)
        {
            var card = CreatePanel("Empty State", parent, Panel, new Vector2(900f, 300f));
            CreateText(card, title, 30f, White, TextAlignmentOptions.Center,
                new Vector2(40f, -65f), new Vector2(820f, 50f), FontStyles.Bold);
            CreateText(card, body, 20f, Muted, TextAlignmentOptions.Center,
                new Vector2(80f, -135f), new Vector2(740f, 90f), FontStyles.Normal);
        }

        private static void RenderRetry(Transform parent, Action retry)
        {
            if (parent == null) return;
            var card = CreatePanel("Retry State", parent, Panel, new Vector2(720f, 280f));
            CreateText(card, "COULD NOT LOAD", 28f, Coral, TextAlignmentOptions.Center,
                new Vector2(40f, -45f), new Vector2(640f, 55f), FontStyles.Bold);
            CreateButton(card, "TRY AGAIN", new Vector2(160f, 40f), new Vector2(400f, 72f),
                Coral, White, retry);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
        }

        private static string ShortName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN RAIDER";
            return value.Length <= 16 ? value.ToUpperInvariant() : value[..16].ToUpperInvariant();
        }

        private static string FormatUtc(string value) =>
            DateTime.TryParse(value, out var parsed)
                ? parsed.ToLocalTime().ToString("dd MMM • HH:mm")
                : "TIME UNKNOWN";

        private static string Signed(long value) => value >= 0 ? $"+{value}" : value.ToString();

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, .10f);
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string value, float size, Color color,
            TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions, FontStyles style,
            bool topAnchored = true)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (topAnchored)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
            }
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size,
            Color background, Color foreground, Action callback, bool topRight = false)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (topRight)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
            }
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = background;
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.92f, .96f, 1f, 1f);
            colors.pressedColor = new Color(.72f, .80f, .90f, 1f);
            colors.disabledColor = new Color(.35f, .38f, .44f, .65f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.onClick.AddListener(() => callback?.Invoke());
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, .13f);
            outline.effectDistance = new Vector2(1f, -1f);

            var text = CreateText(rect, label, 20f, foreground, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.zero, FontStyles.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetStretchTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;
        }

        private static void SetStretchBottom(RectTransform rect, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, height);
        }

        private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
