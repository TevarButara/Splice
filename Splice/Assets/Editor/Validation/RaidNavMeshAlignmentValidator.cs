#if UNITY_EDITOR
using Splice.Base;
using Splice.Characters;
using Splice.Network;
using Splice.Scenes;
using Splice.Validation;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Editor.Validation
{
    public static class RaidNavMeshAlignmentValidator
    {
        private const float PositionTolerance = 0.05f;
        private const float RotationTolerance = 0.1f;

        public static void Validate(ContentValidationReport report)
        {
            var existing = SceneManager.GetSceneByPath(
                RaidSceneCompositionController.ArenaScenePath);
            var openedForValidation = !existing.isLoaded;
            var scene = openedForValidation
                ? EditorSceneManager.OpenScene(
                    RaidSceneCompositionController.ArenaScenePath,
                    OpenSceneMode.Additive)
                : existing;
            try
            {
                var surface = FindInScene<NavMeshSurface>(scene);
                var core = FindInScene<FortCore>(scene);
                var spawner = FindInScene<RaidHeroSpawner>(scene);
                if (surface == null)
                {
                    report.Error("RAID_NAVMESH_SURFACE_MISSING",
                        "RaidArena is missing its NavMeshSurface.");
                    return;
                }
                if (surface.navMeshData == null)
                {
                    report.Error("RAID_NAVMESH_DATA_MISSING",
                        "RaidArena NavMeshSurface has no baked data.", surface);
                    return;
                }

                var positionDelta = Vector3.Distance(
                    surface.transform.position, surface.navMeshData.position);
                var rotationDelta = Quaternion.Angle(
                    surface.transform.rotation, surface.navMeshData.rotation);
                if (positionDelta > PositionTolerance ||
                    rotationDelta > RotationTolerance)
                    report.Error("RAID_NAVMESH_TRANSFORM_MISMATCH",
                        "RaidArena NavMeshSurface moved after baking. " +
                        "Restore its transform to the NavMeshData bake pose or rebake. " +
                        "Position delta: " + positionDelta.ToString("F2") +
                        ", rotation delta: " + rotationDelta.ToString("F2") + ".",
                        surface);

                var renderer = surface.GetComponent<Renderer>();
                if (renderer != null && renderer.enabled)
                    report.Error("RAID_NAVMESH_PROXY_VISIBLE",
                        "The RaidArena navigation proxy Renderer must be disabled.",
                        renderer);
                var collider = surface.GetComponent<Collider>();
                if (collider != null && collider.enabled)
                    report.Error("RAID_NAVMESH_PROXY_COLLIDER_ENABLED",
                        "The RaidArena navigation proxy collider must stay disabled.",
                        collider);

                surface.RemoveData();
                surface.AddData();
                var contract = RaidSceneContract.Validate(core, spawner, surface);
                if (!contract.valid)
                    report.Error("RAID_NAVMESH_CONTRACT_INVALID",
                        "RaidArena navigation contract failed: " +
                        contract.ErrorSummary, surface);
            }
            finally
            {
                if (openedForValidation)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
