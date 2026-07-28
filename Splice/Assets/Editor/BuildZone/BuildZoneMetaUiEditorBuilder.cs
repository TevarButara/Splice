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

        public static void RebuildFromMenu() =>
            Splice.Editor.UI.SpliceSceneUiMaintenanceEditor.RebuildFromMenu();

        // Legacy batch entry is intentionally blocked so destructive rebuilds cannot bypass confirmation.
        [System.Obsolete("Direct destructive rebuild is disabled. Use SpliceSceneUiMaintenanceEditor.RebuildFromMenu so the designer must confirm.")]
        public static void RebuildAndSave() => throw new System.InvalidOperationException(
            "Direct UI rebuild is disabled. Use Splice > UI > Rebuild UI From Defaults... and confirm the warning.");

        private static PrototypeMetaHubController FindController()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<PrototypeMetaHubController>(true);
                if (controller != null) return controller;
            }
            return null;
        }

        private static TownSnapshotCommitController FindDeploymentController()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<TownSnapshotCommitController>(true);
                if (controller != null) return controller;
            }
            return null;
        }
    }
}
#endif
