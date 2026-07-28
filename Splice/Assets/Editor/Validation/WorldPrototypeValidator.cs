#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Splice.Base;
using Splice.Data;
using Splice.UI;
using Splice.Validation;
using Splice.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Editor.Validation
{
    public static class WorldPrototypeValidator
    {
        private const string TownMapPath = "Assets/Maps/TownMap_Default.asset";
        private const string WorldMapPath = "Assets/Maps/WorldMap_Default.asset";
        private const string ForestPath = "Assets/Maps/ForestZone_01.asset";
        private const string BuildZonePath = "Assets/=======SCENES/BuildZone.unity";
        private const string WorldScenePath = "Assets/=======SCENES/WorldMap.unity";
        private const string ForestScenePath = "Assets/=======SCENES/ForestZone.unity";

        public static void Validate(ContentValidationReport report)
        {
            var town = AssetDatabase.LoadAssetAtPath<TownMapDefinitionSO>(TownMapPath);
            var world = AssetDatabase.LoadAssetAtPath<WorldMapDefinitionSO>(WorldMapPath);
            var forest = AssetDatabase.LoadAssetAtPath<ForestZoneDefinitionSO>(ForestPath);
            if (town == null) report.Error("TOWN_MAP_DEFINITION_MISSING", TownMapPath + " is missing.");
            else ValidateTownDefinition(town, report);
            if (world == null) report.Error("WORLD_MAP_DEFINITION_MISSING", WorldMapPath + " is missing.");
            else ValidateMapIdentity(world, PrototypeFlowContract.WorldMapScene, MapGameMode.World, report);
            if (forest == null) report.Error("FOREST_DEFINITION_MISSING", ForestPath + " is missing.");
            else
            {
                ValidateMapIdentity(forest, PrototypeFlowContract.ForestScene, MapGameMode.Forest, report);
                if (forest.FragmentDropMax < forest.FragmentDropMin)
                    report.Error("FOREST_DROP_RANGE_INVALID", "Forest fragment drop range is invalid.", forest);
                if (forest.FragmentsPerDiamond <= 0)
                    report.Error("FOREST_CONVERSION_INVALID", "Forest fragments-per-Diamond must be positive.", forest);
            }

            ValidateScene(BuildZonePath, report, scene =>
            {
                var manager = FindInScene<BaseBuildManager>(scene);
                var purchase = FindInScene<TownRegionPurchaseController>(scene);
                if (manager == null) report.Error("TOWN_BUILD_MANAGER_MISSING", "BuildZone has no BaseBuildManager.");
                else if (manager.TownMap == null)
                    report.Error("TOWN_MAP_NOT_ASSIGNED", "BuildZone BaseBuildManager has no TownMapDefinition.", manager);
                if (purchase == null || !purchase.HasEditorAuthoredUi)
                    report.Error("TOWN_EXPANSION_UI_INCOMPLETE",
                        "BuildZone Town Expansion UI must be serialized and editable before Play Mode.", purchase);
            });
            ValidateScene(WorldScenePath, report, scene =>
            {
                ValidateCameraAndLight(scene, "WORLD", report);
                var controller = FindInScene<WorldMapController>(scene);
                if (controller == null || !controller.HasEditorAuthoredUi)
                    report.Error("WORLD_MAP_UI_INCOMPLETE",
                        "WorldMap requires its editor-authored node UI.", controller);
            });
            ValidateScene(ForestScenePath, report, scene =>
            {
                ValidateCameraAndLight(scene, "FOREST", report);
                var controller = FindInScene<ForestEncounterController>(scene);
                if (controller == null || !controller.HasEditorAuthoredUi)
                    report.Error("FOREST_SCENE_CONTRACT_INCOMPLETE",
                        "ForestZone requires Hero, monsters and editor-authored HUD.", controller);
                var monsters = 0;
                foreach (var root in scene.GetRootGameObjects())
                    monsters += root.GetComponentsInChildren<ForestMonsterTarget>(true).Length;
                if (monsters < 1)
                    report.Error("FOREST_MONSTERS_MISSING", "ForestZone has no authored monster targets.");
            });
        }

        private static void ValidateTownDefinition(TownMapDefinitionSO town,
            ContentValidationReport report)
        {
            ValidateMapIdentity(town, "BuildZone", MapGameMode.Town, report);
            if (!string.Equals(town.MapId, TownExpansionPrototypeCatalog.MapTemplateId,
                    StringComparison.Ordinal) ||
                town.MapVersion != TownExpansionPrototypeCatalog.MapVersion)
                report.Error("TOWN_MAP_SERVER_CONTRACT_MISMATCH",
                    "Town map asset does not match the backend map identity/version.", town);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in town.Regions)
            {
                if (region == null || string.IsNullOrWhiteSpace(region.regionId))
                    report.Error("TOWN_REGION_ID_MISSING", "Town map contains a region without an ID.", town);
                else if (!ids.Add(region.regionId))
                    report.Error("TOWN_REGION_ID_DUPLICATE",
                        $"Town region '{region.regionId}' is duplicated.", town);
                if (region != null && (region.size.x <= 0f || region.size.y <= 0f))
                    report.Error("TOWN_REGION_SIZE_INVALID",
                        $"Town region '{region.regionId}' has invalid size.", town);
            }
            if (!ids.Contains(TownExpansionPrototypeCatalog.CoreRegionId))
                report.Error("TOWN_CORE_REGION_MISSING", "Town map has no core region.", town);
        }

        private static void ValidateMapIdentity(MapDefinitionSO definition, string sceneName,
            MapGameMode mode, ContentValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(definition.MapId) || definition.MapVersion < 1)
                report.Error("MAP_IDENTITY_INVALID", $"{definition.name} has invalid map identity.", definition);
            if (definition.GameMode != mode || !string.Equals(definition.SceneName, sceneName,
                    StringComparison.Ordinal))
                report.Error("MAP_SCENE_CONTRACT_MISMATCH",
                    $"{definition.name} does not target {mode}/{sceneName}.", definition);
        }

        private static void ValidateScene(string path, ContentValidationReport report,
            Action<Scene> validate)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (asset == null)
            {
                report.Error("WORLD_SCENE_MISSING", $"Required prototype scene is missing: {path}");
                return;
            }
            var enabled = Array.Exists(EditorBuildSettings.scenes,
                scene => scene.enabled && scene.path == path);
            if (!enabled) report.Error("WORLD_SCENE_NOT_IN_BUILD",
                $"Required prototype scene is not enabled in Build Settings: {path}", asset);
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { validate(scene); }
            finally
            {
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateCameraAndLight(Scene scene, string prefix,
            ContentValidationReport report)
        {
            var camera = FindInScene<Camera>(scene);
            var light = FindInScene<Light>(scene);
            if (camera == null) report.Error(prefix + "_CAMERA_MISSING", scene.name + " has no Camera.");
            if (light == null) report.Error(prefix + "_LIGHT_MISSING", scene.name + " has no main Light.");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
