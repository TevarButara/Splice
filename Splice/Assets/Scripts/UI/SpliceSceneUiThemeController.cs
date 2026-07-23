using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.UI
{
    // Complete presentation replacement for the two prototype scenes. Gameplay components, Button references
    // and Canvas ownership remain intact while the old flat visuals are replaced by generated fantasy skins.
    public sealed class SpliceSceneUiThemeController : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(17, 26, 43, 245);
        private static readonly Color Panel = new Color32(23, 36, 58, 235);
        private static readonly Color PanelSoft = new Color32(32, 50, 76, 245);
        private static readonly Color Cyan = new Color32(57, 215, 210, 255);
        private static readonly Color Mint = new Color32(97, 230, 167, 255);
        private static readonly Color Amber = new Color32(255, 188, 87, 255);
        private static readonly Color Coral = new Color32(255, 107, 120, 255);
        private static readonly Color White = new Color32(244, 248, 255, 255);
        private static readonly Color Muted = new Color32(169, 184, 204, 255);
        private GameObject selectionBackdrop;
        private Transform roleSelectionPanel;
        private Transform stakeOfferPanel;

        private IEnumerator Start()
        {
            // Wait one frame so feature controllers can create their runtime controls first.
            yield return null;
            ApplyTheme();
            var wait = new WaitForSecondsRealtime(.1f);
            while (isActiveAndEnabled)
            {
                SyncSelectionBackdrop();
                yield return wait;
            }
        }

        [ContextMenu("Apply Splice UI Theme")]
        public void ApplyTheme()
        {
            SpliceUiSkinLibrary.EnsureLoaded();
            StyleCommonPanels();
            StyleCommonButtons();

            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "BuildZone") LayoutBuildZone();
            else if (sceneName == "Bootstrap" || sceneName == "RaidArena") LayoutBootstrap();
        }

        private void StyleCommonPanels()
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image == null || IsTownDeploymentUi(image.transform)) continue;
                var n = image.gameObject.name.ToLowerInvariant();
                if (n.Contains("wargemofferpanel") || n.Contains("confirmcheckout") || n == "panel")
                {
                    if (!SpliceUiSkinLibrary.ApplyPanel(image))
                    {
                        image.color = Panel;
                        EnsureOutline(image.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, .38f), new Vector2(2f, -2f));
                    }
                }
            }
        }

        private void StyleCommonButtons()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button == null || IsTownDeploymentUi(button.transform)) continue;
                var label = ReadButtonLabel(button).Trim();
                var key = (button.gameObject.name + " " + label).ToLowerInvariant();
                var background = PanelSoft;
                var foreground = White;

                if (ContainsAny(key, "cancel", "discard", "dispose", "sell", "clear", "deselect"))
                {
                    background = new Color32(66, 43, 60, 255);
                    foreground = new Color32(255, 208, 214, 255);
                }
                else if (ContainsAny(key, "checkout", "confirm raid", "confirm checkout", "ok", "deploy snapshot"))
                {
                    background = Mint;
                    foreground = new Color32(17, 26, 43, 255);
                }
                else if (ContainsAny(key, "invader", "raid a town"))
                {
                    background = Coral;
                    foreground = White;
                }
                else if (ContainsAny(key, "defender", "defend town", "view", "hero mode"))
                {
                    background = Cyan;
                    foreground = new Color32(17, 26, 43, 255);
                }

                StyleButton(button, background, foreground);
            }
        }

        private void LayoutBuildZone()
        {
            var top = FindDeep(transform, "TOP");
            if (top == null) return;

            foreach (var image in top.GetComponentsInChildren<Image>(true))
            {
                var key = image.gameObject.name.ToLowerInvariant();
                if (key.Contains("ecodisplay")) image.color = new Color32(17, 26, 43, 218);
            }

            var rail = EnsurePanel("Build Action Rail", top, Panel, new Vector2(610f, 460f));
            AnchorTopRight(rail, new Vector2(-30f, -430f));
            rail.SetAsFirstSibling();
            var railHeader = EnsureHeaderSkin("Build Actions Header", rail, new Vector2(550f, 95f), new Vector2(0f, -16f));
            railHeader.SetAsFirstSibling();
            EnsureLabel("Rail Header", rail, "BUILD ACTIONS", new Vector2(30f, -38f), new Vector2(550f, 42f),
                23f, White, TextAlignmentOptions.Center);

            LayoutAction(top, "BtCheckout", "CHECKOUT DRAFT", 515f, Mint, new Color32(17, 26, 43, 255));
            LayoutAction(top, "BtClearCart", "DISCARD DRAFT", 608f, PanelSoft, White);
            LayoutActionContains(top, "sellall", "SELL ALL", 701f, new Color32(66, 43, 60, 255), new Color32(255, 208, 214, 255));
            LayoutAction(top, "BtDeselect", "DESELECT", 794f, PanelSoft, White);

            var view = FindDeep(top, "BtViewSwap");
            if (view != null)
            {
                var rect = view.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.5f, 1f);
                rect.anchorMax = new Vector2(.5f, 1f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -115f);
                rect.sizeDelta = new Vector2(320f, 62f);
                rect.localScale = Vector3.one;
                var button = view.GetComponent<Button>();
                SetButtonLabel(button, "SWITCH VIEW");
                StyleButton(button, Cyan, new Color32(17, 26, 43, 255));
            }

            LayoutTopStat(top, "Cost", new Vector2(38f, -20f), new Vector2(470f, 60f), TextAlignmentOptions.Left);
            LayoutTopStat(top, "Wallet", new Vector2(-38f, -20f), new Vector2(470f, 60f), TextAlignmentOptions.Right, true);
            LayoutTopStat(top, "CapacityDisplay", new Vector2(0f, -20f), new Vector2(620f, 60f), TextAlignmentOptions.Center);
        }

        private void LayoutBootstrap()
        {
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                if (canvas != null && canvas.isRootCanvas)
                    ConfigurePrototypeCanvasScaler(canvas);

            var chooseCanvas = FindDeep(transform, "CanvasChooseSide");
            if (chooseCanvas != null)
            {
                var rolePanel = FindDirectChild(chooseCanvas, "Panel");
                var offerPanel = FindDirectChild(chooseCanvas, "WarGemOfferPanel");
                roleSelectionPanel = rolePanel;
                stakeOfferPanel = offerPanel;
                var canvas = chooseCanvas.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 200;
                }
                selectionBackdrop = EnsureFullscreenPanel("Raid Selection Backdrop", chooseCanvas,
                    new Color32(8, 15, 27, 205)).gameObject;
                selectionBackdrop.transform.SetAsFirstSibling();
                if (rolePanel != null) LayoutRolePanel(rolePanel);
                if (offerPanel != null) LayoutOfferPanel(offerPanel);
                SyncSelectionBackdrop();
            }

            var topCanvas = FindDeep(transform, "CanvasTOP");
            if (topCanvas != null) LayoutRaidTopBar(topCanvas);
        }

        public static void ConfigurePrototypeCanvasScaler(Canvas canvas)
        {
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
        }

        private void SyncSelectionBackdrop()
        {
            if (selectionBackdrop == null) return;
            var shouldShow = roleSelectionPanel != null && roleSelectionPanel.gameObject.activeSelf ||
                             stakeOfferPanel != null && stakeOfferPanel.gameObject.activeSelf;
            if (selectionBackdrop.activeSelf != shouldShow) selectionBackdrop.SetActive(shouldShow);
        }

        private static void LayoutRolePanel(Transform panel)
        {
            var rect = panel.GetComponent<RectTransform>();
            SetCentered(rect, new Vector2(1200f, 780f), new Vector2(0f, 20f));
            var image = panel.GetComponent<Image>();
            if (image != null && !SpliceUiSkinLibrary.ApplyPanel(image)) image.color = Ink;

            var header = EnsureHeaderSkin("Role Header Skin", panel, new Vector2(1040f, 175f), new Vector2(0f, -12f));
            header.SetAsFirstSibling();

            var title = panel.GetComponentInChildren<TMP_Text>(true);
            if (title != null)
            {
                title.text = "CHOOSE RAID ROLE";
                SetTopRect(title.rectTransform, new Vector2(0f, -64f), new Vector2(1030f, 86f));
                title.fontSize = 52f;
                title.fontStyle = FontStyles.Bold;
                title.color = White;
                title.alignment = TextAlignmentOptions.Center;
                title.raycastTarget = false;
            }

            EnsureLabel("Role Subtitle", panel, "ATTACK FOR LOOT  •  OR TEST YOUR DEFENSE", new Vector2(100f, -178f),
                new Vector2(1000f, 40f), 23f, Muted, TextAlignmentOptions.Center);
            var buttons = panel.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
            {
                SetCentered(buttons[0].GetComponent<RectTransform>(), new Vector2(780f, 112f), new Vector2(0f, 20f));
                SetButtonLabel(buttons[0], "DEFEND TOWN");
                StyleButton(buttons[0], Cyan, new Color32(17, 26, 43, 255));
            }
            if (buttons.Length > 1)
            {
                SetCentered(buttons[1].GetComponent<RectTransform>(), new Vector2(780f, 112f), new Vector2(0f, -135f));
                SetButtonLabel(buttons[1], "RAID A TOWN");
                StyleButton(buttons[1], Coral, White);
            }
            EnsureLabel("Role Hint", panel, "Defender is a local test route • Invader uses War Gem stake", new Vector2(100f, -680f),
                new Vector2(1000f, 36f), 20f, Muted, TextAlignmentOptions.Center);
        }

        private static void LayoutOfferPanel(Transform panel)
        {
            var rect = panel.GetComponent<RectTransform>();
            SetCentered(rect, new Vector2(1080f, 760f), new Vector2(0f, 10f));
            var image = panel.GetComponent<Image>();
            if (image != null && !SpliceUiSkinLibrary.ApplyPanel(image)) image.color = Ink;

            var header = EnsureHeaderSkin("Contract Header Skin", panel, new Vector2(940f, 145f), new Vector2(0f, -12f));
            header.SetAsFirstSibling();
            EnsureLabel("Contract Title", panel, "RAID CONTRACT", new Vector2(70f, -42f), new Vector2(940f, 54f),
                34f, White, TextAlignmentOptions.Center);

            var body = panel.GetComponentInChildren<TMP_Text>(true);
            if (body != null)
            {
                SetTopRect(body.rectTransform, new Vector2(0f, -125f), new Vector2(940f, 475f));
                body.fontSize = 28f;
                body.fontStyle = FontStyles.Normal;
                body.color = White;
                body.alignment = TextAlignmentOptions.TopLeft;
                body.enableWordWrapping = true;
                body.overflowMode = TextOverflowModes.Ellipsis;
                body.margin = new Vector4(20f, 14f, 20f, 14f);
                body.raycastTarget = false;
            }

            var buttons = panel.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
            {
                SetBottomRect(buttons[0].GetComponent<RectTransform>(), new Vector2(-205f, 48f), new Vector2(370f, 72f));
                StyleButton(buttons[0], Mint, new Color32(17, 26, 43, 255));
            }
            if (buttons.Length > 1)
            {
                SetBottomRect(buttons[1].GetComponent<RectTransform>(), new Vector2(205f, 48f), new Vector2(370f, 72f));
                StyleButton(buttons[1], PanelSoft, White);
            }
        }

        private static void LayoutRaidTopBar(Transform canvas)
        {
            var panel = FindDirectChild(canvas, "Panel");
            if (panel == null) return;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 94f);
            rect.localScale = Vector3.one;
            var image = panel.GetComponent<Image>();
            if (image != null && !SpliceUiSkinLibrary.ApplyHeader(image)) image.color = new Color32(17, 26, 43, 228);

            var tmps = panel.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in tmps)
            {
                if (text.GetComponentInParent<Button>() != null) continue;
                var textRect = text.rectTransform;
                textRect.anchorMin = new Vector2(.5f, 1f);
                textRect.anchorMax = new Vector2(.5f, 1f);
                textRect.pivot = new Vector2(.5f, 1f);
                textRect.anchoredPosition = new Vector2(0f, -20f);
                textRect.sizeDelta = new Vector2(1200f, 54f);
                textRect.localScale = Vector3.one;
                text.fontSize = 24f;
                text.color = White;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }

            LayoutHudButton(panel, "HeroModeButton", "HERO MODE", new Vector2(-36f, -118f));
            LayoutHudButton(panel, "HeroInteractButton", "INTERACT  [E]", new Vector2(-36f, -204f));
            EnsurePanel("Top Accent", panel, Cyan, new Vector2(2960f, 5f), false).anchoredPosition = new Vector2(0f, -89f);
        }

        private static void LayoutHudButton(Transform root, string name, string label, Vector2 position)
        {
            var target = FindDeep(root, name);
            if (target == null) return;
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(220f, 68f);
            rect.localScale = Vector3.one;
            var button = target.GetComponent<Button>();
            SetButtonLabel(button, label);
            StyleButton(button, PanelSoft, White);
            StretchButtonLabel(button);
        }

        private static void LayoutAction(Transform root, string name, string label, float y, Color bg, Color fg)
        {
            var target = FindDeep(root, name);
            if (target == null) return;
            LayoutActionObject(target, label, y, bg, fg);
        }

        private static void LayoutActionContains(Transform root, string partialName, string label, float y, Color bg, Color fg)
        {
            Transform target = null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.name.ToLowerInvariant().Contains(partialName)) continue;
                target = button.transform;
                break;
            }
            if (target != null) LayoutActionObject(target, label, y, bg, fg);
        }

        private static void LayoutActionObject(Transform target, string label, float y, Color bg, Color fg)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-60f, -y);
            rect.sizeDelta = new Vector2(550f, 78f);
            rect.localScale = Vector3.one;
            var button = target.GetComponent<Button>();
            SetButtonLabel(button, label);
            StyleButton(button, bg, fg);
        }

        private static void LayoutTopStat(Transform root, string name, Vector2 position, Vector2 size,
            TextAlignmentOptions alignment, bool right = false)
        {
            var target = FindDeep(root, name);
            if (target == null) return;
            var text = target.GetComponent<TMP_Text>() ?? target.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(right ? 1f : alignment == TextAlignmentOptions.Center ? .5f : 0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(right ? 1f : alignment == TextAlignmentOptions.Center ? .5f : 0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            text.fontSize = 27f;
            text.fontStyle = FontStyles.Bold;
            text.color = alignment == TextAlignmentOptions.Center ? Mint : White;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static void StyleButton(Button button, Color background, Color foreground)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            var generatedSkin = image != null && SpliceUiSkinLibrary.ApplyButton(image,
                SpliceUiSkinLibrary.ButtonTint(background));
            if (image != null && !generatedSkin) image.color = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.9f, 1f, 1f, 1f);
            colors.pressedColor = new Color(.72f, .84f, .9f, 1f);
            colors.selectedColor = new Color(.9f, 1f, 1f, 1f);
            colors.disabledColor = new Color(.35f, .4f, .47f, .65f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            var outline = button.GetComponent<Outline>();
            if (generatedSkin && outline != null) outline.enabled = false;
            else EnsureOutline(button.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, .38f), new Vector2(2f, -2f));

            if (generatedSkin) foreground = White;

            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.color = foreground;
                tmp.fontStyle = FontStyles.Bold;
                tmp.fontSize = 22f;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 11f;
                tmp.fontSizeMax = 22f;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
            }
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.color = foreground;
                legacy.fontStyle = FontStyle.Bold;
                legacy.fontSize = 20;
                legacy.resizeTextForBestFit = true;
                legacy.resizeTextMinSize = 11;
                legacy.resizeTextMaxSize = 20;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.raycastTarget = false;
            }
        }

        private static string ReadButtonLabel(Button button)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) return tmp.text ?? string.Empty;
            var legacy = button.GetComponentInChildren<Text>(true);
            return legacy != null ? legacy.text ?? string.Empty : string.Empty;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = value;
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = value;
        }

        private static void StretchButtonLabel(Button button)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.rectTransform.anchorMin = Vector2.zero;
                tmp.rectTransform.anchorMax = Vector2.one;
                tmp.rectTransform.offsetMin = Vector2.zero;
                tmp.rectTransform.offsetMax = Vector2.zero;
                tmp.rectTransform.localScale = Vector3.one;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.rectTransform.anchorMin = Vector2.zero;
                legacy.rectTransform.anchorMax = Vector2.one;
                legacy.rectTransform.offsetMin = Vector2.zero;
                legacy.rectTransform.offsetMax = Vector2.zero;
                legacy.rectTransform.localScale = Vector3.one;
                legacy.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static RectTransform EnsurePanel(string name, Transform parent, Color color, Vector2 size, bool topRight = true)
        {
            var existing = FindDirectChild(parent, name);
            RectTransform rect;
            Image image;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                image = go.GetComponent<Image>();
                image.raycastTarget = false;
            }
            else
            {
                rect = existing.GetComponent<RectTransform>();
                image = existing.GetComponent<Image>();
            }
            rect.sizeDelta = size;
            if (image != null)
            {
                if (size.x > 300f && size.y > 120f)
                {
                    if (!SpliceUiSkinLibrary.ApplyPanel(image)) image.color = color;
                }
                else image.color = color;
            }
            if (!topRight)
            {
                rect.anchorMin = new Vector2(.5f, 1f);
                rect.anchorMax = new Vector2(.5f, 1f);
                rect.pivot = new Vector2(.5f, 1f);
            }
            return rect;
        }

        private static RectTransform EnsureHeaderSkin(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var existing = FindDirectChild(parent, name);
            RectTransform rect;
            Image image;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                image = go.GetComponent<Image>();
                image.raycastTarget = false;
            }
            else
            {
                rect = existing.GetComponent<RectTransform>();
                image = existing.GetComponent<Image>();
            }
            SetTopRect(rect, position, size);
            if (!SpliceUiSkinLibrary.ApplyHeader(image)) image.color = PanelSoft;
            return rect;
        }

        private static RectTransform EnsureFullscreenPanel(string name, Transform parent, Color color)
        {
            var existing = FindDirectChild(parent, name);
            RectTransform rect;
            Image image;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            else
            {
                rect = existing.GetComponent<RectTransform>();
                image = existing.GetComponent<Image>();
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text EnsureLabel(string name, Transform parent, string value, Vector2 position,
            Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var existing = FindDirectChild(parent, name);
            TextMeshProUGUI text;
            RectTransform rect;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                text = go.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                rect = existing.GetComponent<RectTransform>();
                text = existing.GetComponent<TextMeshProUGUI>();
            }
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
        }

        private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetTopRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetBottomRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void EnsureOutline(GameObject target, Color color, Vector2 distance)
        {
            if (target == null || target.GetComponent<Graphic>() == null) return;
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static Transform FindDeep(Transform root, string exactName)
        {
            if (root == null) return null;
            if (root.name == exactName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), exactName);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDirectChild(Transform root, string exactName)
        {
            if (root == null) return null;
            for (var i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == exactName) return root.GetChild(i);
            return null;
        }

        private static bool IsTownDeploymentUi(Transform target)
        {
            while (target != null)
            {
                if (target.name == "Town Deployment UI" || target.name == "SnapshotDeploymentUI") return true;
                target = target.parent;
            }
            return false;
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (var term in terms) if (value.Contains(term)) return true;
            return false;
        }
    }
}
