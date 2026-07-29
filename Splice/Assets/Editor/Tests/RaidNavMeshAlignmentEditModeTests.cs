#if UNITY_EDITOR
using NUnit.Framework;
using Splice.Base;
using Splice.Characters;
using Splice.Network;
using Splice.Scenes;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Tests.EditMode
{
    public sealed class RaidNavMeshAlignmentEditModeTests
    {
        [Test]
        public void RaidArena_BakedNavMeshAlignsWithSpawnAndCore()
        {
            var existing = SceneManager.GetSceneByPath(
                RaidSceneCompositionController.ArenaScenePath);
            var openedForTest = !existing.isLoaded;
            var scene = openedForTest
                ? EditorSceneManager.OpenScene(
                    RaidSceneCompositionController.ArenaScenePath,
                    OpenSceneMode.Additive)
                : existing;
            try
            {
                var surface = FindInScene<NavMeshSurface>(scene);
                var core = FindInScene<FortCore>(scene);
                var spawner = FindInScene<RaidHeroSpawner>(scene);
                Assert.That(surface, Is.Not.Null);
                Assert.That(surface.navMeshData, Is.Not.Null);
                Assert.That(Vector3.Distance(surface.transform.position,
                    surface.navMeshData.position), Is.LessThan(0.05f),
                    "The navigation proxy moved after its NavMesh was baked.");
                Assert.That(Quaternion.Angle(surface.transform.rotation,
                    surface.navMeshData.rotation), Is.LessThan(0.1f));

                var renderer = surface.GetComponent<Renderer>();
                var collider = surface.GetComponent<Collider>();
                Assert.That(renderer == null || !renderer.enabled, Is.True,
                    "The navigation-only proxy must not render in RaidArena.");
                Assert.That(collider == null || !collider.enabled, Is.True,
                    "The navigation-only proxy must not collide with units.");

                surface.RemoveData();
                surface.AddData();
                var contract = RaidSceneContract.Validate(core, spawner, surface);
                Assert.That(contract.valid, Is.True, contract.ErrorSummary);
                Assert.That(contract.completePathFound, Is.True,
                    contract.ErrorSummary);
            }
            finally
            {
                if (openedForTest)
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
