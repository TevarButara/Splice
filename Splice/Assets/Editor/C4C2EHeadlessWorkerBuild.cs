#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Splice.EditorTools
{
    public static class C4C2EHeadlessWorkerBuild
    {
        public static readonly string DefaultOutputPath =
            Path.Combine(Path.GetTempPath(), "SpliceRaidWorkerC4C2E.app");

        [MenuItem("Splice/Backend/Build C4C2E Headless Worker")]
        public static void BuildDefault() => Build(DefaultOutputPath);

        public static string Build(string outputPath)
        {
            var fullPath = Path.GetFullPath(outputPath ?? string.Empty);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
            var relativePath = Path.GetRelativePath(temporaryRoot, fullPath);
            if (relativePath == "." || Path.IsPathRooted(relativePath) || relativePath == ".." ||
                relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("C4C2E worker proof builds must stay under the temporary directory.");

            var addressables = AddressableAssetSettingsDefaultObject.Settings ??
                               throw new InvalidOperationException("Addressables settings are unavailable.");
            var previousBuildOption = addressables.BuildAddressablesWithPlayerBuild;
            try
            {
                // Headless authoritative simulation uses immutable API payloads and no content catalog.
                // Keep the normal Player/Addressables security policy unchanged.
                addressables.BuildAddressablesWithPlayerBuild =
                    AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/=======SCENES/Bootstrap.unity" },
                    locationPathName = fullPath,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    // The C4C2E proof runs this Development Player with -batchmode -nographics.
                    // Production CI can switch to Server once that optional Unity module is installed.
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.Development | BuildOptions.StrictMode,
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("C4C2E headless worker build failed: " +
                                                   report.summary.result);
                UnityEngine.Debug.Log($"[C4C2E] Headless worker built: {fullPath} " +
                                      $"({report.summary.totalSize} bytes, " +
                                      $"{report.summary.totalTime.TotalSeconds:F1}s)");
                return fullPath;
            }
            finally
            {
                addressables.BuildAddressablesWithPlayerBuild = previousBuildOption;
            }
        }
    }
}
#endif
