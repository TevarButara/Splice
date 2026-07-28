#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Materials
{
    public enum UrpMaterialAuditStatus
    {
        Compatible,
        SafeToConvert,
        ManualReview
    }

    [Serializable]
    public sealed class UrpMaterialAudit
    {
        public string AssetPath;
        public string SourceShader;
        public string TargetShader;
        public string Reason;
        public UrpMaterialAuditStatus Status;

        public bool CanConvert => Status == UrpMaterialAuditStatus.SafeToConvert &&
                                  !string.IsNullOrEmpty(TargetShader);
    }

    public sealed class UrpMaterialConversionResult
    {
        public int ConvertedCount;
        public string BackupDirectory;
        public readonly List<string> Errors = new List<string>();
        public bool Succeeded => Errors.Count == 0;
    }

    public static class UrpMaterialDoctorCore
    {
        public const string ToolName = "URP Material Doctor";
        public const string BackupRootRelative = "Library/SpliceMaterialBackups";
        public const string UrpUniversalVfxShader = "Splice/VFX/URP Universal";
        public const string UrpAdditiveVfxShader = "Splice/VFX/URP Additive Intensify";
        public const string UrpSmokeVfxShader = "Splice/VFX/URP Procedural Smoke";

        private static readonly Dictionary<string, string> ShaderMappings =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Standard", "Universal Render Pipeline/Lit" },
                { "Standard (Specular setup)", "Universal Render Pipeline/Lit" },
                { "Mobile/Particles/Additive", "Universal Render Pipeline/Particles/Unlit" },
                { "Mobile/Particles/Alpha Blended", "Universal Render Pipeline/Particles/Unlit" },
                { "VFX/UniversalShader", UrpUniversalVfxShader },
                { "VFX/Additive Intensify", UrpAdditiveVfxShader },
                { "VFX/SmokeProcedular", UrpSmokeVfxShader }
            };

        public static bool IsAllowedSelectedFolder(string assetPath, out string reason)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                reason = "Select one folder in the Project window.";
                return false;
            }

            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                reason = "The selected folder must be below Assets/.";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                reason = "The selection is not a valid Project folder.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static string GetSelectedFolderPath()
        {
            if (Selection.objects == null || Selection.objects.Length != 1)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return AssetDatabase.IsValidFolder(path) ? path.Replace('\\', '/').TrimEnd('/') : string.Empty;
        }

        public static List<UrpMaterialAudit> AuditFolder(string folderPath)
        {
            if (!IsAllowedSelectedFolder(folderPath, out string reason))
                throw new ArgumentException(reason, nameof(folderPath));

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(AuditMaterialAtPath)
                .ToList();
        }

        public static UrpMaterialAudit AuditMaterialAtPath(string assetPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                return Manual(assetPath, "<missing material>", "Material asset could not be loaded.");
            }

            return AuditMaterial(material, assetPath);
        }

        public static UrpMaterialAudit AuditMaterial(Material material, string assetPath = "<memory>")
        {
            if (material == null)
                return Manual(assetPath, "<missing material>", "Material is null.");

            Shader shader = material.shader;
            if (shader == null)
                return Manual(assetPath, "<missing shader>", "Missing shader must be assigned manually.");

            string shaderName = shader.name;
            string pipelineTag = material.GetTag("RenderPipeline", false, string.Empty);
            bool explicitUrp = string.Equals(pipelineTag, "UniversalPipeline", StringComparison.Ordinal);
            bool shaderHasError = ShaderUtil.ShaderHasError(shader);

            if (explicitUrp && shader.isSupported && !shaderHasError)
            {
                return new UrpMaterialAudit
                {
                    AssetPath = assetPath,
                    SourceShader = shaderName,
                    TargetShader = shaderName,
                    Status = UrpMaterialAuditStatus.Compatible,
                    Reason = "Already uses a supported URP shader."
                };
            }

            if (ShaderMappings.TryGetValue(shaderName, out string targetShader))
            {
                Shader target = Shader.Find(targetShader);
                if (target == null)
                    return Manual(assetPath, shaderName, $"Required target shader is missing: {targetShader}");
                if (!target.isSupported || ShaderUtil.ShaderHasError(target))
                    return Manual(assetPath, shaderName, $"Target shader is not currently valid: {targetShader}");

                return new UrpMaterialAudit
                {
                    AssetPath = assetPath,
                    SourceShader = shaderName,
                    TargetShader = targetShader,
                    Status = UrpMaterialAuditStatus.SafeToConvert,
                    Reason = "Known property-preserving URP mapping."
                };
            }

            string problem = shaderHasError
                ? "Shader has compile errors and no approved conversion mapping."
                : !shader.isSupported
                    ? "Shader is unsupported and has no approved conversion mapping."
                    : "Custom/non-URP shader has no approved mapping; automatic replacement could damage its visuals.";
            return Manual(assetPath, shaderName, problem);
        }

        public static UrpMaterialConversionResult ConvertAudits(
            string selectedFolder,
            IReadOnlyList<UrpMaterialAudit> audits)
        {
            var result = new UrpMaterialConversionResult();
            if (!IsAllowedSelectedFolder(selectedFolder, out string reason))
            {
                result.Errors.Add(reason);
                return result;
            }

            string normalizedFolder = selectedFolder.Replace('\\', '/').TrimEnd('/') + "/";
            List<UrpMaterialAudit> candidates = audits?
                .Where(a => a != null && a.CanConvert)
                .ToList() ?? new List<UrpMaterialAudit>();

            foreach (UrpMaterialAudit audit in candidates)
            {
                string path = (audit.AssetPath ?? string.Empty).Replace('\\', '/');
                if (!path.StartsWith(normalizedFolder, StringComparison.Ordinal))
                    result.Errors.Add($"Out-of-scope asset was blocked: {path}");
                if (Shader.Find(audit.TargetShader) == null)
                    result.Errors.Add($"Target shader is missing: {audit.TargetShader}");
            }

            if (result.Errors.Count > 0 || candidates.Count == 0)
                return result;

            try
            {
                result.BackupDirectory = CreateBackup(candidates);
            }
            catch (Exception exception)
            {
                result.Errors.Add($"Backup failed; no materials were changed. {exception.Message}");
                return result;
            }

            bool editingStarted = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                editingStarted = true;
                foreach (UrpMaterialAudit audit in candidates)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(audit.AssetPath);
                    if (material == null)
                        throw new InvalidOperationException($"Could not load {audit.AssetPath}");

                    Shader target = Shader.Find(audit.TargetShader);
                    if (target == null)
                        throw new InvalidOperationException($"Could not find {audit.TargetShader}");

                    Undo.RecordObject(material, $"{ToolName}: Convert Material");
                    ConvertMaterialInPlace(material, target, audit.SourceShader);
                    EditorUtility.SetDirty(material);
                    result.ConvertedCount++;
                }

                AssetDatabase.StopAssetEditing();
                editingStarted = false;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                if (editingStarted)
                    AssetDatabase.StopAssetEditing();
                RestoreBackup(result.BackupDirectory, candidates.Select(c => c.AssetPath));
                result.ConvertedCount = 0;
                result.Errors.Add($"Conversion failed and was rolled back from backup. {exception.Message}");
            }

            return result;
        }

        public static void ConvertMaterialInPlace(Material material, Shader target, string sourceShaderName)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var snapshot = MaterialSnapshot.Capture(material);
            string[] keywords = material.shaderKeywords;
            int renderQueue = material.renderQueue;

            material.shader = target;
            material.shaderKeywords = keywords;

            if (sourceShaderName == "Standard" || sourceShaderName == "Standard (Specular setup)")
            {
                snapshot.ApplyTexture(material, "_MainTex", "_BaseMap");
                snapshot.ApplyColor(material, "_Color", "_BaseColor");
                snapshot.ApplyTexture(material, "_BumpMap", "_BumpMap");
                snapshot.ApplyFloat(material, "_BumpScale", "_BumpScale");
                snapshot.ApplyTexture(material, "_EmissionMap", "_EmissionMap");
                snapshot.ApplyColor(material, "_EmissionColor", "_EmissionColor");
                snapshot.ApplyFloat(material, "_Cutoff", "_Cutoff");
                snapshot.ApplyFloat(material, "_Metallic", "_Metallic");
                snapshot.ApplyFloat(material, "_Glossiness", "_Smoothness");
                if (sourceShaderName == "Standard (Specular setup)" && material.HasProperty("_WorkflowMode"))
                    material.SetFloat("_WorkflowMode", 0f);
            }
            else if (sourceShaderName == "Mobile/Particles/Additive" ||
                     sourceShaderName == "Mobile/Particles/Alpha Blended")
            {
                snapshot.ApplyTexture(material, "_MainTex", "_BaseMap");
                snapshot.ApplyColor(material, "_TintColor", "_BaseColor");
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_Blend"))
                    material.SetFloat("_Blend", sourceShaderName.EndsWith("Additive", StringComparison.Ordinal) ? 1f : 0f);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            }

            if (renderQueue >= 0)
                material.renderQueue = renderQueue;
        }

        private static string CreateBackup(IReadOnlyList<UrpMaterialAudit> candidates)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("Could not resolve project root.");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupRoot = Path.Combine(projectRoot, BackupRootRelative, stamp);
            Directory.CreateDirectory(backupRoot);

            var manifest = new BackupManifest
            {
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                Assets = candidates.Select(c => c.AssetPath).ToArray()
            };

            foreach (string assetPath in manifest.Assets)
            {
                string source = Path.Combine(projectRoot, assetPath);
                string destination = Path.Combine(backupRoot, assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? backupRoot);
                File.Copy(source, destination, true);
            }

            File.WriteAllText(
                Path.Combine(backupRoot, "manifest.json"),
                JsonUtility.ToJson(manifest, true));
            return backupRoot;
        }

        private static void RestoreBackup(string backupRoot, IEnumerable<string> assetPaths)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("Could not resolve project root.");
            foreach (string assetPath in assetPaths)
            {
                string source = Path.Combine(backupRoot, assetPath);
                string destination = Path.Combine(projectRoot, assetPath);
                if (File.Exists(source))
                    File.Copy(source, destination, true);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static UrpMaterialAudit Manual(string path, string sourceShader, string reason)
        {
            return new UrpMaterialAudit
            {
                AssetPath = path,
                SourceShader = sourceShader,
                TargetShader = string.Empty,
                Status = UrpMaterialAuditStatus.ManualReview,
                Reason = reason
            };
        }

        [Serializable]
        private sealed class BackupManifest
        {
            public string CreatedUtc;
            public string[] Assets;
        }

        private sealed class MaterialSnapshot
        {
            private readonly Dictionary<string, TextureValue> _textures = new Dictionary<string, TextureValue>();
            private readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();
            private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();

            public static MaterialSnapshot Capture(Material material)
            {
                var value = new MaterialSnapshot();
                CaptureTexture(material, value, "_MainTex");
                CaptureTexture(material, value, "_BaseMap");
                CaptureTexture(material, value, "_BumpMap");
                CaptureTexture(material, value, "_EmissionMap");
                CaptureColor(material, value, "_Color");
                CaptureColor(material, value, "_BaseColor");
                CaptureColor(material, value, "_TintColor");
                CaptureColor(material, value, "_EmissionColor");
                CaptureFloat(material, value, "_BumpScale");
                CaptureFloat(material, value, "_Cutoff");
                CaptureFloat(material, value, "_Metallic");
                CaptureFloat(material, value, "_Glossiness");
                return value;
            }

            public void ApplyTexture(Material material, string source, string destination)
            {
                if (!_textures.TryGetValue(source, out TextureValue value) || !material.HasProperty(destination))
                    return;
                material.SetTexture(destination, value.Texture);
                material.SetTextureScale(destination, value.Scale);
                material.SetTextureOffset(destination, value.Offset);
            }

            public void ApplyColor(Material material, string source, string destination)
            {
                if (_colors.TryGetValue(source, out Color value) && material.HasProperty(destination))
                    material.SetColor(destination, value);
            }

            public void ApplyFloat(Material material, string source, string destination)
            {
                if (_floats.TryGetValue(source, out float value) && material.HasProperty(destination))
                    material.SetFloat(destination, value);
            }

            private static void CaptureTexture(Material material, MaterialSnapshot snapshot, string property)
            {
                if (!material.HasProperty(property)) return;
                snapshot._textures[property] = new TextureValue
                {
                    Texture = material.GetTexture(property),
                    Scale = material.GetTextureScale(property),
                    Offset = material.GetTextureOffset(property)
                };
            }

            private static void CaptureColor(Material material, MaterialSnapshot snapshot, string property)
            {
                if (material.HasProperty(property))
                    snapshot._colors[property] = material.GetColor(property);
            }

            private static void CaptureFloat(Material material, MaterialSnapshot snapshot, string property)
            {
                if (material.HasProperty(property))
                    snapshot._floats[property] = material.GetFloat(property);
            }

            private struct TextureValue
            {
                public Texture Texture;
                public Vector2 Scale;
                public Vector2 Offset;
            }
        }
    }
}
#endif
