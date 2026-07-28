#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Splice.Base;
using Splice.RaidWorker;
using Splice.UI;
using Splice.Validation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.Editor.UI
{
    /// <summary>
    /// Designer-safe UI maintenance commands. Validate never writes, Repair only restores
    /// serialized references to existing objects, and Rebuild is the only command allowed
    /// to replace designer-authored hierarchy/layout.
    /// </summary>
    public static class SpliceSceneUiMaintenanceEditor
    {
        public const string ValidateMenuPath = "Splice/UI/Validate Scene UI";
        public const string RepairMenuPath = "Splice/UI/Repair Missing UI References (Safe)";
        public const string RebuildMenuPath = "Splice/UI/Rebuild UI From Defaults...";
        public const string RebuildWarningMessage =
            "คำสั่งนี้จะลบ/สร้าง Generated Scene UI ใหม่ใน BuildZone และ RaidArena\n\n" +
            "ตำแหน่ง ขนาด Anchor สี Sprite ฟอนต์ และการจัดวางที่แก้เองใน UI เหล่านี้อาจสูญหาย " +
            "และ Scene จะถูกบันทึกทันที\n\n" +
            "ใช้ Validate หรือ Repair Missing UI References หากต้องการเก็บดีไซน์ปัจจุบัน\n\n" +
            "ยืนยันล้างและสร้าง UI ใหม่จากค่าเริ่มต้นหรือไม่?";

        [MenuItem(ValidateMenuPath, priority = 1000)]
        public static void ValidateFromMenu()
        {
            if (!CanRun("validate")) return;
            var report = new ContentValidationReport();
            SpliceSceneUiAuthoringValidator.Validate(report);
            LogReport(report, "Scene UI Validation");
            EditorUtility.DisplayDialog("Splice Scene UI Validator",
                report.IsValid
                    ? $"PASS\n\nScene UI contract is complete.\nWarnings: {report.WarningCount}\n\nNo Scene or UI was modified."
                    : $"FAIL\n\nErrors: {report.ErrorCount}\nWarnings: {report.WarningCount}\n\nSee Console for exact objects.\nNo Scene or UI was modified.",
                "OK");
        }

        [MenuItem(RepairMenuPath, priority = 1010)]
        public static void RepairFromMenu()
        {
            if (!CanRun("repair")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var repaired = RepairAllAndSave();
            var report = new ContentValidationReport();
            SpliceSceneUiAuthoringValidator.Validate(report);
            LogReport(report, "Scene UI Repair");
            EditorUtility.DisplayDialog("Repair Missing UI References",
                $"Repaired references: {repaired}\nValidation: {(report.IsValid ? "PASS" : "FAIL")}\n\n" +
                "Existing RectTransform, style, Sprite, font and hierarchy were not changed." +
                (report.IsValid ? string.Empty : "\n\nSome required objects are genuinely missing. Use Rebuild UI From Defaults only if you accept losing generated UI customization."),
                "OK");
        }

        [MenuItem(RebuildMenuPath, priority = 1090)]
        public static void RebuildFromMenu()
        {
            if (!CanRun("rebuild")) return;
            if (!EditorUtility.DisplayDialog("Rebuild UI From Defaults — DESTRUCTIVE",
                    RebuildWarningMessage, "DELETE & REBUILD", "CANCEL")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RebuildAllAndSaveConfirmed();
            EditorUtility.DisplayDialog("Rebuild UI From Defaults",
                "Generated UI was rebuilt and both scenes were saved. Run Validate Scene UI and review layouts before committing.",
                "OK");
        }

        // Stable safe entry point for MCP, legacy Bake callers and automated tests.
        public static int RepairAllAndSave()
        {
            EditorSceneManager.SaveOpenScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var repaired = 0;
            try
            {
                repaired += RepairSceneAndSave(SpliceSceneUiAuthoringEditor.BuildZonePath, true);
                repaired += RepairSceneAndSave(SpliceSceneUiAuthoringEditor.RaidArenaPath, false);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            Debug.Log($"[Scene UI] Safe reference repair complete; {repaired} reference(s) restored. Designer layout was not changed.");
            return repaired;
        }

        // Never expose this method directly as a menu without RebuildFromMenu's confirmation.
        internal static void RebuildAllAndSaveConfirmed()
        {
            EditorSceneManager.SaveOpenScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var buildZone = EditorSceneManager.OpenScene(
                    SpliceSceneUiAuthoringEditor.BuildZonePath, OpenSceneMode.Single);
                RebuildBuildZoneDefaults(buildZone);
                EditorSceneManager.MarkSceneDirty(buildZone);
                EditorSceneManager.SaveScene(buildZone);

                var raidArena = EditorSceneManager.OpenScene(
                    SpliceSceneUiAuthoringEditor.RaidArenaPath, OpenSceneMode.Single);
                RebuildRaidArenaDefaults(raidArena);
                EditorSceneManager.MarkSceneDirty(raidArena);
                EditorSceneManager.SaveScene(raidArena);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            Debug.LogWarning("[Scene UI] Generated UI rebuilt from defaults after explicit designer confirmation.");
        }

        // Test hook: assigns references only. It must not create, destroy, reparent or restyle objects.
        public static int RepairSceneReferences(Scene scene, bool buildZone)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            return buildZone ? RepairBuildZoneReferences(scene) : RepairRaidArenaReferences(scene);
        }

        private static int RepairSceneAndSave(string path, bool buildZone)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var repaired = RepairSceneReferences(scene, buildZone);
            if (repaired <= 0) return 0;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return repaired;
        }

        private static int RepairBuildZoneReferences(Scene scene)
        {
            var repaired = 0;
            var checkout = Find<BaseBuildCheckoutController>(scene);
            if (checkout != null) repaired += RepairCheckout(checkout, scene);
            var meta = Find<PrototypeMetaHubController>(scene);
            if (meta != null) repaired += RepairMetaHub(meta, scene);
            var deployment = Find<TownSnapshotCommitController>(scene);
            if (deployment != null) repaired += RepairDeployment(deployment, scene);
            return repaired;
        }

        private static int RepairRaidArenaReferences(Scene scene)
        {
            var repaired = 0;
            var incoming = Find<IncomingRaidScenarioController>(scene);
            if (incoming != null)
            {
                var serialized = new SerializedObject(incoming);
                var banner = Reference<GameObject>(serialized, "statusBanner") ?? FindObject(scene, "IncomingRaidStatus");
                repaired += Assign(serialized, "statusBanner", banner);
                repaired += Assign(serialized, "statusLabel", FindNamed<TMP_Text>(banner, "Status"));
                repaired += Apply(serialized, incoming);
            }

            var replay = Find<RaidCommandStreamPresentationController>(scene);
            if (replay != null)
            {
                var serialized = new SerializedObject(replay);
                var root = Reference<GameObject>(serialized, "overlayRoot") ?? FindObject(scene, "Authoritative Replay HUD");
                repaired += Assign(serialized, "overlayRoot", root);
                repaired += Assign(serialized, "overlayCanvas", root != null ? root.GetComponent<Canvas>() : null);
                repaired += Assign(serialized, "titleLabel", FindNamed<TMP_Text>(root, "AUTHORITY_TITLE"));
                repaired += Assign(serialized, "statusLabel", FindNamed<TMP_Text>(root, "AUTHORITY_STATUS"));
                repaired += Assign(serialized, "progressFill", FindNamed<Image>(root, "Progress Fill"));
                repaired += Apply(serialized, replay);
            }

            RaidResultUI result = null;
            foreach (var candidate in FindAll<RaidResultUI>(scene))
                if (candidate.enabled && candidate.CanAuthorEditorUi) { result = candidate; break; }
            if (result != null)
            {
                var serialized = new SerializedObject(result);
                repaired += Assign(serialized, "returnToTownButton",
                    FindNamed<Button>(FindObject(scene, "ReturnToTownButton"), "ReturnToTownButton"));
                repaired += Apply(serialized, result);
            }
            return repaired;
        }

        private static int RepairCheckout(BaseBuildCheckoutController checkout, Scene scene)
        {
            var serialized = new SerializedObject(checkout);
            var panel = Reference<GameObject>(serialized, "confirmPanel") ?? FindObject(scene, "Pa_ConFirmCheckOut");
            var canvas = checkout.GetComponentInParent<Canvas>();
            var repaired = 0;
            repaired += Assign(serialized, "confirmPanel", panel);
            repaired += Assign(serialized, "confirmLabel", FindNamed<TMP_Text>(panel, "Text (TMP)"));
            repaired += Assign(serialized, "openButton", checkout.GetComponent<Button>());
            repaired += Assign(serialized, "confirmButton", FindNamed<Button>(panel, "OK"));
            repaired += Assign(serialized, "cancelButton", FindNamed<Button>(panel, "CheckoutCancelButton"));
            repaired += Assign(serialized, "modalBackdrop",
                FindDirectChild(canvas != null ? canvas.transform : null, "Checkout Modal Backdrop"));
            repaired += Assign(serialized, "headerSkin", FindDirectChild(panel != null ? panel.transform : null, "Checkout Header Skin"));
            repaired += Apply(serialized, checkout);
            return repaired;
        }

        private static int RepairMetaHub(PrototypeMetaHubController meta, Scene scene)
        {
            var serialized = new SerializedObject(meta);
            var root = Reference<GameObject>(serialized, "editorUiRoot") ?? FindObject(scene, "Prototype Meta UI");
            var header = FindNamedObject(root, "Command Header");
            var nav = FindNamedObject(root, "Primary Navigation");
            var raidPanel = FindNamedObject(root, "Raid Target Screen");
            var historyPanel = FindNamedObject(root, "Defense History Screen");
            var targetList = FindNamedObject(root, "Target Cards");
            var historyList = FindNamedObject(root, "History Rows");
            var headerTexts = DirectComponents<TMP_Text>(header);
            var repaired = 0;
            repaired += Assign(serialized, "editorUiRoot", root);
            repaired += Assign(serialized, "contentBackdrop", FindNamedObject(root, "Meta Content Backdrop"));
            repaired += Assign(serialized, "raidPanel", raidPanel);
            repaired += Assign(serialized, "historyPanel", historyPanel);
            repaired += Assign(serialized, "onboardingPanel", FindNamedObject(root, "First Raid Briefing"));
            repaired += Assign(serialized, "targetList", targetList != null ? targetList.transform : null);
            repaired += AssignArray(serialized, "targetCards", ComponentsSorted<PrototypeRaidTargetCardView>(targetList));
            repaired += Assign(serialized, "raidListState", FindNamed<PrototypeListStateView>(raidPanel, "Raid List State"));
            repaired += Assign(serialized, "historyList", historyList != null ? historyList.transform : null);
            repaired += AssignArray(serialized, "historyRows", ComponentsSorted<PrototypeDefenseHistoryRowView>(historyList));
            repaired += Assign(serialized, "historyListState", FindNamed<PrototypeListStateView>(historyPanel, "History List State"));
            repaired += Assign(serialized, "sectionTitle", headerTexts.Count > 0 ? headerTexts[0] : null);
            repaired += Assign(serialized, "statusText", headerTexts.Count > 1 ? headerTexts[1] : null);
            repaired += Assign(serialized, "walletText", headerTexts.Count > 2 ? headerTexts[2] : null);
            repaired += Assign(serialized, "townTab", FindNamed<Button>(nav, "TOWN"));
            repaired += Assign(serialized, "raidTab", FindNamed<Button>(nav, "RAID"));
            repaired += Assign(serialized, "historyTab", FindNamed<Button>(nav, "DEFENSE"));
            repaired += Assign(serialized, "refreshTargetsButton", FindNamed<Button>(raidPanel, "REFRESH TARGETS"));
            repaired += Assign(serialized, "refreshHistoryButton", FindNamed<Button>(historyPanel, "REFRESH REPORTS"));
            repaired += Assign(serialized, "onboardingContinueButton", FindNamed<Button>(root, "ENTER TOWN"));
            repaired += Apply(serialized, meta);
            return repaired;
        }

        private static int RepairDeployment(TownSnapshotCommitController deployment, Scene scene)
        {
            var serialized = new SerializedObject(deployment);
            var root = Reference<GameObject>(serialized, "editorUiRoot") ?? FindObject(scene, "Town Deployment UI");
            var repaired = 0;
            repaired += Assign(serialized, "editorUiRoot", root);
            repaired += Assign(serialized, "modalRoot", FindNamedObject(root, "Deployment Modal Backdrop"));
            repaired += Assign(serialized, "statusPill", FindNamed<TMP_Text>(root, "DRAFT Text"));
            repaired += Assign(serialized, "statusHeadline", FindNamed<TMP_Text>(root, "NO TOWN SNAPSHOT Text"));
            repaired += Assign(serialized, "statusBody", FindNamed<TMP_Text>(root, "Place defenses, Checkout, then deploy. Text"));
            repaired += Assign(serialized, "modalSubtitle", FindNamed<TMP_Text>(root, "FIRST DEPLOYMENT Text"));
            var statTexts = FindNamedAll<TMP_Text>(root, "— Text");
            repaired += Assign(serialized, "towerValue", statTexts.Count > 0 ? statTexts[0] : null);
            repaired += Assign(serialized, "garrisonValue", statTexts.Count > 1 ? statTexts[1] : null);
            repaired += Assign(serialized, "powerValue", statTexts.Count > 2 ? statTexts[2] : null);
            repaired += Assign(serialized, "validationText", FindNamed<TMP_Text>(root, "Validation Text"));
            repaired += Assign(serialized, "reviewButton", FindNamed<Button>(root, "Review Deployment"));
            repaired += Assign(serialized, "closeButton", FindNamed<Button>(root, "Close Review"));
            repaired += Assign(serialized, "cancelButton", FindNamed<Button>(root, "Cancel Deployment"));
            var deploy = FindNamed<Button>(root, "Deploy Snapshot");
            repaired += Assign(serialized, "deployButton", deploy);
            repaired += Assign(serialized, "deployButtonLabel", deploy != null ? deploy.GetComponentInChildren<TMP_Text>(true) : null);
            repaired += Apply(serialized, deployment);
            return repaired;
        }

        private static void RebuildBuildZoneDefaults(Scene scene)
        {
            ConfigureCanvasContract(scene);
            var checkout = Find<BaseBuildCheckoutController>(scene);
            if (checkout == null) throw new MissingReferenceException("BuildZone has no BaseBuildCheckoutController.");
            checkout.RebuildEditorUi();

            var meta = Find<PrototypeMetaHubController>(scene);
            if (meta == null) throw new MissingReferenceException("BuildZone has no PrototypeMetaHubController.");
            meta.RebuildEditorUi();

            var deployment = Find<TownSnapshotCommitController>(scene);
            if (deployment == null) throw new MissingReferenceException("BuildZone has no TownSnapshotCommitController.");
            deployment.RebuildEditorUi();

            EditorUtility.SetDirty(checkout);
            EditorUtility.SetDirty(meta);
            EditorUtility.SetDirty(deployment);
        }

        private static void RebuildRaidArenaDefaults(Scene scene)
        {
            ConfigureCanvasContract(scene);
            var incoming = Find<IncomingRaidScenarioController>(scene);
            if (incoming == null) throw new MissingReferenceException("RaidArena has no IncomingRaidScenarioController.");
            DestroyReferencedRoot(incoming, "statusBanner", "statusLabel");
            incoming.RebuildEditorStatusUi();

            var replay = Find<RaidCommandStreamPresentationController>(scene);
            if (replay == null) throw new MissingReferenceException("RaidArena has no RaidCommandStreamPresentationController.");
            DestroyReferencedRoot(replay, "overlayRoot", "overlayCanvas", "titleLabel", "statusLabel", "progressFill");
            replay.RebuildEditorReplayUi();

            RaidResultUI result = null;
            foreach (var candidate in FindAll<RaidResultUI>(scene))
                if (candidate.enabled && candidate.CanAuthorEditorUi) { result = candidate; break; }
            if (result == null) throw new MissingReferenceException("RaidArena has no configured RaidResultUI.");
            var resultSerialized = new SerializedObject(result);
            var returnProperty = resultSerialized.FindProperty("returnToTownButton");
            var returnButton = returnProperty != null ? returnProperty.objectReferenceValue as Button : null;
            if (returnButton != null) UnityEngine.Object.DestroyImmediate(returnButton.gameObject);
            if (returnProperty != null) returnProperty.objectReferenceValue = null;
            resultSerialized.ApplyModifiedPropertiesWithoutUndo();
            result.RebuildEditorReturnButton();

            EditorUtility.SetDirty(incoming);
            EditorUtility.SetDirty(replay);
            EditorUtility.SetDirty(result);
        }

        private static void DestroyReferencedRoot(Component controller, string rootProperty, params string[] otherProperties)
        {
            var serialized = new SerializedObject(controller);
            var root = serialized.FindProperty(rootProperty);
            var rootObject = root != null ? root.objectReferenceValue as GameObject : null;
            if (rootObject != null) UnityEngine.Object.DestroyImmediate(rootObject);
            if (root != null) root.objectReferenceValue = null;
            foreach (var propertyName in otherProperties)
            {
                var property = serialized.FindProperty(propertyName);
                if (property != null) property.objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCanvasContract(Scene scene)
        {
            foreach (var canvas in FindAll<Canvas>(scene))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                SpliceSceneUiThemeController.ConfigurePrototypeCanvasScaler(canvas);
                EditorUtility.SetDirty(canvas.GetComponent<CanvasScaler>());
            }
        }

        private static int Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue != null || value == null) return 0;
            property.objectReferenceValue = value;
            return 1;
        }

        private static int AssignArray<T>(SerializedObject serialized, string propertyName, IList<T> values)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray || values == null || values.Count == 0) return 0;
            var changed = 0;
            if (property.arraySize != values.Count)
            {
                property.arraySize = values.Count;
                changed++;
            }
            for (var index = 0; index < values.Count; index++)
            {
                var item = property.GetArrayElementAtIndex(index);
                if (item.objectReferenceValue != null || values[index] == null) continue;
                item.objectReferenceValue = values[index];
                changed++;
            }
            return changed;
        }

        private static int Apply(SerializedObject serialized, UnityEngine.Object owner)
        {
            if (!serialized.hasModifiedProperties) return 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
            return 0;
        }

        private static T Reference<T>(SerializedObject serialized, string propertyName) where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static GameObject FindObject(Scene scene, string exactName)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(transform.name, exactName, StringComparison.OrdinalIgnoreCase))
                    return transform.gameObject;
            return null;
        }

        private static GameObject FindNamedObject(GameObject root, string exactName)
        {
            if (root == null) return null;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(transform.name, exactName, StringComparison.OrdinalIgnoreCase))
                    return transform.gameObject;
            return null;
        }

        private static T FindNamed<T>(GameObject root, string exactName) where T : Component
        {
            var gameObject = FindNamedObject(root, exactName);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        private static T FindDirectChild<T>(Transform parent, string exactName) where T : UnityEngine.Object
        {
            if (parent == null) return null;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (!string.Equals(child.name, exactName, StringComparison.OrdinalIgnoreCase)) continue;
                if (typeof(T) == typeof(GameObject)) return child.gameObject as T;
                return child.GetComponent(typeof(T)) as T;
            }
            return null;
        }

        private static GameObject FindDirectChild(Transform parent, string exactName) =>
            FindDirectChild<GameObject>(parent, exactName);

        private static List<T> DirectComponents<T>(GameObject root) where T : Component
        {
            var values = new List<T>();
            if (root == null) return values;
            for (var index = 0; index < root.transform.childCount; index++)
            {
                var value = root.transform.GetChild(index).GetComponent<T>();
                if (value != null) values.Add(value);
            }
            return values;
        }

        private static List<T> ComponentsSorted<T>(GameObject root) where T : Component
        {
            var values = new List<T>();
            if (root == null) return values;
            values.AddRange(root.GetComponentsInChildren<T>(true));
            values.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return values;
        }

        private static List<T> FindNamedAll<T>(GameObject root, string exactName) where T : Component
        {
            var values = new List<T>();
            if (root == null) return values;
            foreach (var value in root.GetComponentsInChildren<T>(true))
                if (string.Equals(value.name, exactName, StringComparison.OrdinalIgnoreCase)) values.Add(value);
            values.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            return values;
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
            foreach (var root in scene.GetRootGameObjects()) values.AddRange(root.GetComponentsInChildren<T>(true));
            return values;
        }

        private static void LogReport(ContentValidationReport report, string prefix)
        {
            foreach (var issue in report.Issues)
            {
                var message = $"[{prefix}] {issue.Code}: {issue.Message}";
                if (issue.Severity == ContentValidationSeverity.Error) Debug.LogError(message, issue.Context);
                else Debug.LogWarning(message, issue.Context);
            }
            if (report.IsValid) Debug.Log($"<color=#63E6BE><b>[{prefix}] PASS</b></color>");
        }

        private static bool CanRun(string operation)
        {
            if (!EditorApplication.isPlaying) return true;
            Debug.LogError($"[Scene UI] Exit Play Mode before {operation}.");
            return false;
        }
    }
}
#endif