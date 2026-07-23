using System.Collections.Generic;
using Splice.Scenes;
using Splice.UI;
using Splice.Validation;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Validation
{
    public static class RaidSceneArchitectureValidator
    {
        public const string BootstrapScenePath = "Assets/=======SCENES/Bootstrap.unity";

        public static void Validate(ContentValidationReport report)
        {
            var required = new[]
            {
                BootstrapScenePath,
                $"Assets/=======SCENES/{PrototypeFlowContract.HubScene}.unity",
                RaidSceneCompositionController.ArenaScenePath,
                RaidSceneCompositionController.AttackerScenePath,
                RaidSceneCompositionController.DefenderScenePath,
            };
            var enabled = new HashSet<string>();
            var buildScenes = EditorBuildSettings.scenes;
            for (var i = 0; i < buildScenes.Length; i++)
                if (buildScenes[i].enabled) enabled.Add(buildScenes[i].path);

            for (var i = 0; i < required.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(required[i]);
                if (asset == null)
                {
                    report.Error("RAID_SCENE_MISSING", $"Required raid scene is missing: {required[i]}");
                    continue;
                }
                if (!enabled.Contains(required[i]))
                    report.Error("RAID_SCENE_NOT_IN_BUILD", $"Required raid scene is not enabled in Build Settings: {required[i]}", asset);
            }

            var firstEnabled = string.Empty;
            for (var i = 0; i < buildScenes.Length; i++)
            {
                if (!buildScenes[i].enabled) continue;
                firstEnabled = buildScenes[i].path;
                break;
            }
            if (firstEnabled != BootstrapScenePath)
                report.Error("BOOTSTRAP_NOT_FIRST", "Bootstrap.unity must be the first enabled build scene.");
        }
    }
}
