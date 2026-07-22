using System.Collections.Generic;
using Splice.Data;
using Splice.Editor.ContentUpdates;
using Splice.Validation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Splice.Editor.Validation
{
    public static class SpliceContentValidatorMenu
    {
        public const string MenuPath = "Splice/Validation/Validate Registered Content";

        [MenuItem(MenuPath, priority = 2000)]
        public static void ValidateAllFromMenu()
        {
            var report = ValidateProject(true);
            EditorUtility.DisplayDialog("Splice Content Validator", report.Summary(), "OK");
        }

        public static ContentValidationReport ValidateProject(bool logResults = false,
            bool includeGeneratedCatalog = true)
        {
            // Registries are the playable-content roots. The core follows every referenced dependency
            // (cards, characters, towers, abilities and projectiles), while unfinished unregistered drafts
            // remain free to exist without blocking a build.
            var report = SpliceContentValidationCore.Validate(
                LoadAll<FactionRegistrySO>(), LoadAll<HeroRegistrySO>());
            RaidSceneArchitectureValidator.Validate(report);
            BackendBoundaryValidator.Validate(report);
            LiveContentAddressablesConfigurator.Validate(report);
            if (includeGeneratedCatalog) SpliceContentCatalogExporter.ValidateGenerated(report);
            if (logResults) Log(report);
            return report;
        }

        public static void Log(ContentValidationReport report)
        {
            foreach (var issue in report.Issues)
            {
                var text = $"[Splice Content] {issue.Code}: {issue.Message}";
                if (issue.Severity == ContentValidationSeverity.Error) Debug.LogError(text, issue.Context);
                else Debug.LogWarning(text, issue.Context);
            }
            if (report.IsValid) Debug.Log($"<color=#63E6BE><b>{report.Summary()}</b></color>");
            else Debug.LogError(report.Summary());
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }
            return result;
        }
    }

    public sealed class SpliceContentBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport buildReport)
        {
            var report = SpliceContentValidatorMenu.ValidateProject(true);
            if (!report.IsValid)
                throw new BuildFailedException($"Splice content validation failed with {report.ErrorCount} error(s). Fix them before building.");
        }
    }
}
