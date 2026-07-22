using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Central runtime skin library. Source textures stay reusable and are cropped from their transparent
    // canvas before Sprite creation, so generated artwork fills UI rects without stretching its corners.
    public static class SpliceUiSkinLibrary
    {
        private static Sprite panel;
        private static Sprite button;
        private static Sprite header;
        private static bool attemptedLoad;

        public static bool IsReady
        {
            get
            {
                EnsureLoaded();
                return panel != null && button != null && header != null;
            }
        }

        public static void EnsureLoaded()
        {
            if (attemptedLoad) return;
            attemptedLoad = true;
            panel = CreateCroppedSprite("SpliceUI/SplicePanelFrame", .19f, .19f);
            button = CreateCroppedSprite("SpliceUI/SpliceButtonFrame", .18f, .32f);
            header = CreateCroppedSprite("SpliceUI/SpliceHeaderFrame", .18f, .30f);
        }

        public static bool ApplyPanel(Image image, Color? tint = null)
        {
            EnsureLoaded();
            return Apply(image, panel, tint ?? Color.white);
        }

        public static bool ApplyButton(Image image, Color? tint = null)
        {
            EnsureLoaded();
            return Apply(image, button, tint ?? Color.white);
        }

        public static bool ApplyHeader(Image image, Color? tint = null)
        {
            EnsureLoaded();
            return Apply(image, header, tint ?? Color.white);
        }

        public static Color ButtonTint(Color semanticColor)
        {
            if (semanticColor.r > semanticColor.g * 1.22f)
                return new Color(1f, .78f, .88f, 1f);
            if (semanticColor.g > semanticColor.r * 1.18f)
                return new Color(.82f, 1f, .94f, 1f);
            if (semanticColor.b > semanticColor.r * 1.2f)
                return new Color(.88f, .94f, 1f, 1f);
            return Color.white;
        }

        private static bool Apply(Image image, Sprite sprite, Color tint)
        {
            if (image == null || sprite == null) return false;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.fillCenter = true;
            image.color = tint;
            return true;
        }

        private static Sprite CreateCroppedSprite(string resourcePath, float horizontalBorderRatio, float verticalBorderRatio)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"[SpliceUI] Missing generated skin at Resources/{resourcePath}.png");
                return null;
            }

            var rect = new Rect(0f, 0f, texture.width, texture.height);
            try
            {
                var pixels = texture.GetPixels32();
                var minX = texture.width;
                var minY = texture.height;
                var maxX = -1;
                var maxY = -1;
                for (var y = 0; y < texture.height; y++)
                {
                    var row = y * texture.width;
                    for (var x = 0; x < texture.width; x++)
                    {
                        if (pixels[row + x].a <= 8) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX >= minX && maxY >= minY)
                {
                    minX = Mathf.Max(0, minX - 2);
                    minY = Mathf.Max(0, minY - 2);
                    maxX = Mathf.Min(texture.width - 1, maxX + 2);
                    maxY = Mathf.Min(texture.height - 1, maxY + 2);
                    rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                }
            }
            catch (UnityException exception)
            {
                Debug.LogWarning($"[SpliceUI] {texture.name} is not readable; using the full canvas. {exception.Message}");
            }

            var horizontal = Mathf.Floor(rect.width * horizontalBorderRatio);
            var vertical = Mathf.Floor(rect.height * verticalBorderRatio);
            horizontal = Mathf.Min(horizontal, rect.width * .49f);
            vertical = Mathf.Min(vertical, rect.height * .49f);
            return Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(horizontal, vertical, horizontal, vertical));
        }
    }
}
