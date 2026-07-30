using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Splice.FxStudio.Editor
{
    public static class SpliceFxAlphaProcessor
    {
        public const string DefaultOutputRoot =
            "Assets/SpliceFXStudio/Generated/Textures";

        public static Texture2D GenerateTextureAsset(
            SpliceFxSubEffectDefinition subFx,
            string outputRoot = DefaultOutputRoot,
            bool overwrite = false)
        {
            if (subFx == null)
                throw new ArgumentNullException(nameof(subFx));
            if (subFx.SourceTextureForProcessing == null)
                throw new InvalidOperationException(
                    "Assign a source Texture2D or Sprite before generating alpha.");
            if (string.IsNullOrWhiteSpace(outputRoot) ||
                !outputRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Generated texture output must be inside Assets/.");

            EnsureAssetFolder(outputRoot);
            var safeId = SpliceFxPresetDefinition.SanitizeId(subFx.subFxId);
            var path = $"{outputRoot}/{safeId}_processed.png";
            if (!overwrite && File.Exists(ToAbsolutePath(path)))
                path = AssetDatabase.GenerateUniqueAssetPath(path);

            var settings = subFx.alpha ?? new SpliceFxAlphaSettings();
            var source = ReadScaled(
                subFx.SourceTextureForProcessing,
                subFx.SourcePixelRect,
                Mathf.Clamp(settings.maximumSize, 32, 2048));
            try
            {
                var pixels = source.GetPixels32();
                ProcessPixels(pixels, settings);
                source.SetPixels32(pixels);
                source.Apply(false, false);
                File.WriteAllBytes(ToAbsolutePath(path), source.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(path, settings.maximumSize);
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            subFx.processedTexture = result;
            EditorUtility.SetDirty(subFx);
            AssetDatabase.SaveAssets();
            return result;
        }

        public static void ProcessPixels(Color32[] pixels,
            SpliceFxAlphaSettings settings)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            settings ??= new SpliceFxAlphaSettings();
            var key = new Vector3(
                settings.chromaKey.r,
                settings.chromaKey.g,
                settings.chromaKey.b);
            var feather = Mathf.Max(0.001f, settings.feather);
            var softness = Mathf.Max(0.001f, settings.softness);

            for (var i = 0; i < pixels.Length; i++)
            {
                var source = (Color)pixels[i];
                var sourceAlpha = source.a;
                float alpha;
                switch (settings.mode)
                {
                    case SpliceFxAlphaMode.LuminanceToAlpha:
                    {
                        var luminance = source.r * 0.2126f +
                                        source.g * 0.7152f +
                                        source.b * 0.0722f;
                        alpha = SmoothStep(settings.threshold,
                            settings.threshold + feather, luminance);
                        break;
                    }
                    case SpliceFxAlphaMode.ChromaKey:
                    {
                        var rgb = new Vector3(source.r, source.g, source.b);
                        var distance = Vector3.Distance(rgb, key) /
                                       Mathf.Sqrt(3f);
                        alpha = SmoothStep(settings.tolerance,
                            settings.tolerance + softness, distance);
                        if (settings.despill > 0f)
                        {
                            var luminance = source.r * 0.2126f +
                                            source.g * 0.7152f +
                                            source.b * 0.0722f;
                            var neutral = new Color(luminance, luminance,
                                luminance, source.a);
                            source = Color.Lerp(source, neutral,
                                (1f - alpha) * settings.despill);
                        }
                        break;
                    }
                    case SpliceFxAlphaMode.RedChannel:
                        alpha = source.r;
                        break;
                    case SpliceFxAlphaMode.GreenChannel:
                        alpha = source.g;
                        break;
                    case SpliceFxAlphaMode.BlueChannel:
                        alpha = source.b;
                        break;
                    case SpliceFxAlphaMode.AlphaChannel:
                    case SpliceFxAlphaMode.SourceAlpha:
                    default:
                        alpha = sourceAlpha;
                        break;
                }

                if (settings.multiplySourceAlpha &&
                    settings.mode != SpliceFxAlphaMode.SourceAlpha &&
                    settings.mode != SpliceFxAlphaMode.AlphaChannel)
                    alpha *= sourceAlpha;
                if (settings.invert) alpha = 1f - alpha;
                source.a = Mathf.Clamp01(alpha);
                pixels[i] = source;
            }
        }

        private static Texture2D ReadScaled(
            Texture source, Rect sourcePixels, int maximumSize)
        {
            if (sourcePixels.width <= 0f ||
                sourcePixels.height <= 0f)
                sourcePixels = new Rect(
                    0f, 0f, source.width, source.height);
            var scale = Mathf.Min(1f,
                maximumSize / Mathf.Max(
                    sourcePixels.width, sourcePixels.height));
            var width = Mathf.Max(
                1, Mathf.RoundToInt(sourcePixels.width * scale));
            var height = Mathf.Max(
                1, Mathf.RoundToInt(sourcePixels.height * scale));
            var temporary = RenderTexture.GetTemporary(width, height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                var textureScale = new Vector2(
                    sourcePixels.width /
                    Mathf.Max(1f, source.width),
                    sourcePixels.height /
                    Mathf.Max(1f, source.height));
                var textureOffset = new Vector2(
                    sourcePixels.x /
                    Mathf.Max(1f, source.width),
                    sourcePixels.y /
                    Mathf.Max(1f, source.height));
                Graphics.Blit(source, temporary,
                    textureScale, textureOffset);
                RenderTexture.active = temporary;
                var result = new Texture2D(width, height,
                    TextureFormat.RGBA32, false, false);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void ConfigureImporter(string path, int maximumSize)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Mathf.Clamp(maximumSize, 32, 2048);
            importer.textureCompression = TextureImporterCompression.Compressed;
            ConfigureMobile(importer, "Android");
            ConfigureMobile(importer, "iPhone");
            importer.SaveAndReimport();
        }

        private static void ConfigureMobile(TextureImporter importer,
            string platform)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = importer.maxTextureSize;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 75;
            importer.SetPlatformTextureSettings(settings);
        }

        private static float SmoothStep(float minimum, float maximum,
            float value)
        {
            var t = Mathf.Clamp01((value - minimum) /
                                  Mathf.Max(0.0001f, maximum - minimum));
            return t * t * (3f - 2f * t);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException(
                    "Unable to resolve Unity project root.");
            return Path.Combine(projectRoot, assetPath);
        }

        internal static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
