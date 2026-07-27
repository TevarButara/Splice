#if UNITY_EDITOR
using Splice.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.EditorTools
{
    public static class BuildZoneMetaUiEditorBuilder
    {
        private const string BuildZoneScenePath = "Assets/=======SCENES/BuildZone.unity";

        [MenuItem("Splice/UI/Rebuild BuildZone Meta UI")]
        public static void RebuildFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[BuildZone UI] Exit Play Mode before rebuilding the editor-authored UI.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RebuildAndSave();
            if (FindController() is { } controller)
                Selection.activeGameObject = controller.EditorUiRoot;
        }

        // Stable entry point for Unity MCP and batch-mode validation.
        public static void RebuildAndSave()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != BuildZoneScenePath)
                scene = EditorSceneManager.OpenScene(BuildZoneScenePath, OpenSceneMode.Single);

            var controller = FindController();
            if (controller == null)
                throw new MissingReferenceException(
                    "BuildZone requires exactly one PrototypeMetaHubController before its UI can be baked.");

            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Rebuild BuildZone Meta UI");
            controller.RebuildEditorUi();
            if (!controller.HasEditorAuthoredUi)
                throw new MissingReferenceException("BuildZone meta UI builder did not serialize every required reference.");

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildZone UI] Editor-authored Prototype Meta UI rebuilt and saved.");
        }

        private static PrototypeMetaHubController FindController()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<PrototypeMetaHubController>(true);
                if (controller != null) return controller;
            }
            return null;
        }
    }
}
#endif
