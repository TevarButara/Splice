#if UNITY_EDITOR
using System.Collections.Generic;
using Splice.Base;
using Splice.Data;
using Splice.Input;
using Splice.RaidWorker;
using Splice.UI;
using Splice.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.Editor.UI
{
    public static class SpliceSceneUiAuthoringEditor
    {
        public const string MenuPath = "Splice/UI/Bake All Scene UI";
        public const string BuildZonePath = "Assets/=======SCENES/BuildZone.unity";
        public const string RaidArenaPath = "Assets/=======SCENES/RaidArena.unity";
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        [MenuItem(MenuPath)]
        public static void BakeFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Scene UI] Exit Play Mode before baking UI.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BakeAllAndSave();
        }

        // Stable entry point for Unity MCP and tests.
        public static void BakeAllAndSave()
        {
            EditorSceneManager.SaveOpenScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var buildZone = EditorSceneManager.OpenScene(BuildZonePath, OpenSceneMode.Single);
                BakeBuildZone(buildZone);
                EditorSceneManager.MarkSceneDirty(buildZone);
                EditorSceneManager.SaveScene(buildZone);

                var raidArena = EditorSceneManager.OpenScene(RaidArenaPath, OpenSceneMode.Single);
                BakeRaidArena(raidArena);
                EditorSceneManager.MarkSceneDirty(raidArena);
                EditorSceneManager.SaveScene(raidArena);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            Debug.Log("[Scene UI] All runtime-owned UI migrated into scenes; responsive canvas contract saved.");
        }

        private static void BakeBuildZone(Scene scene)
        {
            ConfigureRootScreenCanvases(scene);

            var checkout = FindInScene<BaseBuildCheckoutController>(scene);
            if (checkout == null) throw new MissingReferenceException("BuildZone has no BaseBuildCheckoutController.");
            checkout.RebuildEditorUi();
            if (!checkout.HasEditorAuthoredUi)
                throw new MissingReferenceException("BuildZone checkout UI could not be serialized.");
            EditorUtility.SetDirty(checkout);

            var buildManager = FindInScene<BaseBuildManager>(scene);
            var cameraPan = FindInScene<CameraPanController>(scene);
            var basePoint = FindTransform(scene, "BasePoint");
            if (buildManager == null || cameraPan == null || basePoint == null)
                throw new MissingReferenceException(
                    "BuildZone requires BaseBuildManager, CameraPanController and an exact 'BasePoint'.");

            var baseDefinition = EnsureNaturalBaseDefinition(buildManager.Registry);
            EnsureEditorBasePreview(scene, basePoint, baseDefinition);

            var townBase = FindInScene<PlayerTownBaseController>(scene);
            if (townBase == null) townBase = buildManager.gameObject.AddComponent<PlayerTownBaseController>();
            townBase.ConfigureEditorReferences(buildManager.Registry, basePoint, cameraPan);
            EditorUtility.SetDirty(townBase);

            var meta = FindInScene<PrototypeMetaHubController>(scene);
            if (meta != null)
            {
                meta.EnsureEditorDynamicViews();
                if (!meta.HasEditorAuthoredUi)
                    throw new MissingReferenceException(
                        "BuildZone meta UI still contains missing editor-authored dynamic views.");
                meta.ConfigureTownBaseController(townBase);
                EditorUtility.SetDirty(meta);
            }
        }

        private static void BakeRaidArena(Scene scene)
        {
            ConfigureRootScreenCanvases(scene);

            var incoming = FindInScene<IncomingRaidScenarioController>(scene);
            if (incoming == null) throw new MissingReferenceException("RaidArena has no IncomingRaidScenarioController.");
            incoming.RebuildEditorStatusUi();
            if (!incoming.HasEditorAuthoredStatusUi)
                throw new MissingReferenceException("Incoming raid status UI could not be serialized.");
            EditorUtility.SetDirty(incoming);

            var replay = FindInScene<RaidCommandStreamPresentationController>(scene);
            if (replay == null) throw new MissingReferenceException(
                "RaidArena has no RaidCommandStreamPresentationController.");
            replay.RebuildEditorReplayUi();
            if (!replay.HasEditorAuthoredReplayUi)
                throw new MissingReferenceException("Replay HUD could not be serialized.");
            EditorUtility.SetDirty(replay);

            RaidResultUI resultUi = null;
            foreach (var candidate in FindAllInScene<RaidResultUI>(scene))
                if (candidate.CanAuthorEditorUi && candidate.enabled) { resultUi = candidate; break; }
            if (resultUi == null) throw new MissingReferenceException("RaidArena has no configured RaidResultUI.");
            resultUi.RebuildEditorReturnButton();
            if (!resultUi.HasEditorAuthoredReturnButton)
                throw new MissingReferenceException("Return-to-Town button could not be serialized.");
            EditorUtility.SetDirty(resultUi);
        }

        private static void ConfigureRootScreenCanvases(Scene scene)
        {
            foreach (var canvas in FindAllInScene<Canvas>(scene))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                SpliceSceneUiThemeController.ConfigurePrototypeCanvasScaler(canvas);
                EditorUtility.SetDirty(canvas.GetComponent<CanvasScaler>());
            }
        }

        private static BaseDefinitionSO EnsureNaturalBaseDefinition(FactionRegistrySO registry)
        {
            if (registry == null || registry.Factions.Count == 0)
                throw new MissingReferenceException("BuildZone FactionRegistry is missing or empty.");
            var faction = registry.Factions[0];
            if (faction == null) throw new MissingReferenceException("BuildZone first faction is null.");
            if (faction.townBase != null) return faction.townBase;

            const string assetPath = "Assets/Prefabs/Natural/Constructor/Natural_TownBase.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BaseDefinitionSO>(assetPath);
            if (definition == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Natural/Constructor/nat-base-lv1-7500.prefab");
                if (prefab == null) throw new MissingReferenceException(
                    "Natural level-1 base prefab was not found in the Constructor folder.");
                definition = ScriptableObject.CreateInstance<BaseDefinitionSO>();
                definition.baseId = "town-base";
                definition.displayName = "Natural Town Core";
                definition.levels.Add(new BaseLevelDefinition
                {
                    level = 1,
                    prefab = prefab,
                    maxHealth = 7500,
                    defenseCapacity = 100,
                    powerRating = 100,
                    upgradeGoldCost = 0,
                    upgradeDurationSeconds = 0f,
                });
                AssetDatabase.CreateAsset(definition, assetPath);
            }
            faction.townBase = definition;
            EditorUtility.SetDirty(faction);
            return definition;
        }

        private static void EnsureEditorBasePreview(Scene scene, Transform basePoint,
            BaseDefinitionSO definition)
        {
            var level = definition?.ResolveLevel(1);
            if (level?.prefab == null) return;
            GameObject preview = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == level.prefab.name) { preview = root; break; }
            if (preview == null)
            {
                for (var index = 0; index < basePoint.childCount; index++)
                    if (basePoint.GetChild(index).name == level.prefab.name)
                    {
                        preview = basePoint.GetChild(index).gameObject;
                        break;
                    }
            }
            if (preview == null)
                preview = PrefabUtility.InstantiatePrefab(level.prefab, scene) as GameObject;
            if (preview == null) return;
            preview.name = level.prefab.name;
            preview.transform.SetParent(basePoint, false);
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.identity;
            preview.SetActive(true);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var values = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
                values.AddRange(root.GetComponentsInChildren<T>(true));
            return values;
        }

        private static Transform FindTransform(Scene scene, string exactName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var value in root.GetComponentsInChildren<Transform>(true))
                    if (value.name == exactName) return value;
            }
            return null;
        }
    }

    public static class SpliceSceneUiAuthoringValidator
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/=======SCENES/Bootstrap.unity",
            SpliceSceneUiAuthoringEditor.BuildZonePath,
            SpliceSceneUiAuthoringEditor.RaidArenaPath,
            "Assets/=======SCENES/RaidAttackerPresentation.unity",
            "Assets/=======SCENES/RaidDefenderPresentation.unity",
            "Assets/=======SCENES/SampleScene.unity",
        };

        public static void Validate(ContentValidationReport report)
        {
            foreach (var path in ScenePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                var opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    ValidateCanvasContract(scene, report);
                    if (path == SpliceSceneUiAuthoringEditor.BuildZonePath)
                        ValidateBuildZone(scene, report);
                    else if (path == SpliceSceneUiAuthoringEditor.RaidArenaPath)
                        ValidateRaidArena(scene, report);
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateCanvasContract(Scene scene, ContentValidationReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null ||
                    scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                    scaler.referenceResolution != SpliceSceneUiAuthoringEditor.ReferenceResolution ||
                    Mathf.Abs(scaler.matchWidthOrHeight - .5f) > .001f)
                    report.Error("SCENE_UI_SCALER_INVALID",
                        $"{scene.name}/{canvas.name} must use ScaleWithScreenSize 1920x1080, Match 0.5.",
                        canvas);
            }
        }

        private static void ValidateBuildZone(Scene scene, ContentValidationReport report)
        {
            var checkout = Find<BaseBuildCheckoutController>(scene);
            if (checkout == null || !checkout.HasEditorAuthoredUi)
                report.Error("BUILDZONE_CHECKOUT_UI_RUNTIME",
                    "BuildZone checkout confirmation must be fully serialized.", checkout);
            var townBase = Find<PlayerTownBaseController>(scene);
            if (townBase == null || !townBase.HasRequiredReferences)
                report.Error("BUILDZONE_BASE_CONTRACT",
                    "BuildZone requires a configured PlayerTownBaseController and exact BasePoint.", townBase);
            var meta = Find<PrototypeMetaHubController>(scene);
            if (meta == null || !meta.HasEditorAuthoredUi)
                report.Error("BUILDZONE_META_UI_RUNTIME",
                    "BuildZone target cards, history rows and list states must be fully serialized.", meta);
        }

        private static void ValidateRaidArena(Scene scene, ContentValidationReport report)
        {
            var incoming = Find<IncomingRaidScenarioController>(scene);
            if (incoming == null || !incoming.HasEditorAuthoredStatusUi)
                report.Error("RAID_INCOMING_UI_RUNTIME",
                    "Incoming raid status must be fully serialized in RaidArena.", incoming);
            var replay = Find<RaidCommandStreamPresentationController>(scene);
            if (replay == null || !replay.HasEditorAuthoredReplayUi)
                report.Error("RAID_REPLAY_UI_RUNTIME",
                    "Authoritative replay HUD must be fully serialized in RaidArena.", replay);
            RaidResultUI result = null;
            foreach (var candidate in FindAll<RaidResultUI>(scene))
                if (candidate.enabled && candidate.CanAuthorEditorUi) { result = candidate; break; }
            if (result == null || !result.HasEditorAuthoredReturnButton)
                report.Error("RAID_RESULT_UI_RUNTIME",
                    "Raid result Return-to-Town button must be serialized in RaidArena.", result);
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static List<T> FindAll<T>(Scene scene) where T : Component
        {
            var values = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
                values.AddRange(root.GetComponentsInChildren<T>(true));
            return values;
        }
    }
}
#endif
