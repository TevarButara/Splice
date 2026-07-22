#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Splice.ContentUpdates;
using Splice.Editor.Validation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Splice.Editor.ContentUpdates
{
    [Serializable]
    public sealed class LiveContentUpdateProofReport
    {
        public int schemaVersion = 1;
        public string baselineContentVersion;
        public string updateContentVersion;
        public string baselineCatalogSha256;
        public string updateCatalogSha256;
        public bool addressablesContentUpdateBuild;
        public bool playerRebuildInvoked;
        public string validationAddress;
        public string result;
    }

    public static class LiveContentBuildPipeline
    {
        public const string BuildMenu = "Splice/Live Content/3. Build Local Remote Content";
        public const string ProofMenu = "Splice/Live Content/4. Run Content-Only Update Proof";
        private const string ProofRelativePath = "Backend/content/generated/live-content-update-proof.json";

        [MenuItem(BuildMenu, priority = 1802)]
        public static void BuildFromMenu()
        {
            BuildFullContent(LiveContentRuntime.EmbeddedContentVersion);
        }

        [MenuItem(ProofMenu, priority = 1803)]
        public static void RunContentOnlyUpdateProofFromMenu()
        {
            RunContentOnlyUpdateProof();
        }

        public static AddressablesPlayerBuildResult BuildFullContent(string contentVersion)
        {
            var settings = Prepare(contentVersion);
            AddressableAssetSettings.BuildPlayerContent(out var result);
            EnsureBuildSucceeded(result, "Addressables full content build");
            PublishLocalManifest(result, contentVersion);
            Debug.Log($"[LiveContent] Built remote content {contentVersion}: {ResolveCatalogPath(result, contentVersion)}");
            return result;
        }

        public static LiveContentUpdateProofReport RunContentOnlyUpdateProof()
        {
            var probe = AssetDatabase.LoadAssetAtPath<LiveContentProbeSO>(
                LiveContentAddressablesConfigurator.ProbeAssetPath);
            if (probe == null) throw new InvalidOperationException("Live-content probe asset is missing.");

            probe.contentVersion = LiveContentRuntime.EmbeddedContentVersion;
            probe.message = "Baseline content shipped with the app catalog.";
            EditorUtility.SetDirty(probe);
            AssetDatabase.SaveAssets();
            var baseline = BuildFullContent(LiveContentRuntime.EmbeddedContentVersion);
            var baselineHash = FileSha256(ResolveCatalogPath(baseline,
                LiveContentRuntime.EmbeddedContentVersion));
            var contentStatePath = baseline.ContentStateFilePath;
            if (string.IsNullOrWhiteSpace(contentStatePath) || !File.Exists(contentStatePath))
                contentStatePath = ContentUpdateScript.GetContentStateDataPath(false,
                    AddressableAssetSettingsDefaultObject.Settings);
            if (string.IsNullOrWhiteSpace(contentStatePath) || !File.Exists(contentStatePath))
                throw new InvalidOperationException("Addressables content-state file was not preserved.");

            const string updateVersion = "1.0.1";
            probe = AssetDatabase.LoadAssetAtPath<LiveContentProbeSO>(
                LiveContentAddressablesConfigurator.ProbeAssetPath);
            if (probe == null) throw new InvalidOperationException("Live-content probe was not reloaded after build.");
            probe.contentVersion = updateVersion;
            probe.message = "Content-only update loaded without rebuilding the Unity Player.";
            EditorUtility.SetDirty(probe);
            AssetDatabase.SaveAssets();

            var settings = Prepare(updateVersion);
            var update = ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
            EnsureBuildSucceeded(update, "Addressables content-only update build");
            var updateHash = FileSha256(ResolveCatalogPath(update, updateVersion));
            if (string.Equals(baselineHash, updateHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Content update did not produce a changed catalog hash.");
            if (!update.IsUpdateContentBuild)
                throw new InvalidOperationException("Addressables did not mark the result as a content update build.");

            PublishLocalManifest(update, updateVersion);
            var report = new LiveContentUpdateProofReport
            {
                baselineContentVersion = LiveContentRuntime.EmbeddedContentVersion,
                updateContentVersion = updateVersion,
                baselineCatalogSha256 = baselineHash,
                updateCatalogSha256 = updateHash,
                addressablesContentUpdateBuild = update.IsUpdateContentBuild,
                playerRebuildInvoked = false,
                validationAddress = LiveContentAddressablesConfigurator.ProbeAddress,
                result = "PASS",
            };
            WriteRepositoryFile(ProofRelativePath, JsonUtility.ToJson(report, true) + Environment.NewLine);
            Debug.Log($"<color=#63E6BE><b>[LiveContent] CONTENT-ONLY UPDATE PROOF PASS</b></color> " +
                      $"{baselineHash[..12]} -> {updateHash[..12]}; Unity Player build was not invoked.");
            return report;
        }

        private static AddressableAssetSettings Prepare(string contentVersion)
        {
            var settings = LiveContentAddressablesConfigurator.Configure();
            SpliceContentCatalogExporter.ExportProject();
            var validation = SpliceContentValidatorMenu.ValidateProject(false, false);
            if (!validation.IsValid) throw new InvalidOperationException(validation.DetailedSummary());
            settings.OverridePlayerVersion = contentVersion;
            settings.profileSettings.SetValue(settings.activeProfileId,
                LiveContentAddressablesConfigurator.ContentVersionVariable, contentVersion);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void PublishLocalManifest(AddressablesPlayerBuildResult result,
            string contentVersion)
        {
            var catalogPath = ResolveCatalogPath(result, contentVersion);
            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
                throw new InvalidOperationException("Addressables did not produce a remote catalog.");
            var catalogHash = FileSha256(catalogPath);
            var targetFolder = Path.GetFileName(Path.GetDirectoryName(catalogPath));
            var catalogFile = Path.GetFileName(catalogPath);
            var catalogUrl = $"http://127.0.0.1:8081/{targetFolder}/{catalogFile}";
            var manifest = new LiveContentManifest
            {
                contentVersion = contentVersion,
                minimumClientVersion = "0.0.0",
                serverRulesVersion = SpliceContentCatalogExporter.ServerContentVersion,
                catalogUrl = catalogUrl,
                catalogSha256 = catalogHash,
                rollbackCatalogUrl = string.Empty,
                validationAddress = LiveContentAddressablesConfigurator.ProbeAddress,
                packs = new[]
                {
                    new LiveContentPackManifest { label = "season/default", mandatory = true },
                    new LiveContentPackManifest { label = "map/default", mandatory = false },
                    new LiveContentPackManifest { label = "hero/default", mandatory = false },
                },
            };
            var serverData = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                "ServerData");
            Directory.CreateDirectory(serverData);
            File.WriteAllText(Path.Combine(serverData, "live-content-manifest.json"),
                JsonUtility.ToJson(manifest, true) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static void EnsureBuildSucceeded(AddressablesPlayerBuildResult result, string operation)
        {
            if (result == null || !string.IsNullOrWhiteSpace(result.Error))
                throw new InvalidOperationException($"{operation} failed: {result?.Error}");
        }

        private static string ResolveCatalogPath(AddressablesPlayerBuildResult result,
            string contentVersion)
        {
            if (result == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(result.RemoteCatalogJsonFilePath) &&
                File.Exists(result.RemoteCatalogJsonFilePath)) return result.RemoteCatalogJsonFilePath;
            if (!string.IsNullOrWhiteSpace(result.RemoteCatalogHashFilePath))
            {
                var binary = Path.ChangeExtension(result.RemoteCatalogHashFilePath, ".bin");
                if (File.Exists(binary)) return binary;
                var json = Path.ChangeExtension(result.RemoteCatalogHashFilePath, ".json");
                if (File.Exists(json)) return json;
            }
            var unityRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var target = EditorUserBuildSettings.activeBuildTarget.ToString();
            var fallback = Path.Combine(unityRoot, "ServerData", target,
                $"catalog_{contentVersion}.bin");
            if (File.Exists(fallback)) return fallback;
            var targetFolder = Path.Combine(unityRoot, "ServerData", target);
            if (Directory.Exists(targetFolder))
            {
                var latest = Directory.GetFiles(targetFolder, "catalog_*.bin")
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(latest)) return latest;
            }
            return string.Empty;
        }

        private static string FileSha256(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Addressables catalog path is missing.");
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static void WriteRepositoryFile(string relativePath, string content)
        {
            var unityRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                            throw new InvalidOperationException("Unity project root was not found.");
            var repositoryRoot = Directory.GetParent(unityRoot)?.FullName ??
                                 throw new InvalidOperationException("Repository root was not found.");
            var path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
    }
}
#endif
