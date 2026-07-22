#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using Splice.Base;
using Splice.Editor.Validation;
using Splice.RaidWorker;
using Splice.Scenes;
using Splice.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Splice.Tests.EditMode
{
    public sealed class RaidSceneArchitectureEditModeTests
    {
        [Test]
        public void RequiredRaidScenes_ArePresentEnabledAndBootstrapIsFirst()
        {
            var report = new ContentValidationReport();
            RaidSceneArchitectureValidator.Validate(report);
            Assert.That(report.ErrorCount, Is.Zero, report.DetailedSummary());
        }

        [Test]
        public void RaidArena_HasSingleAuthoritativeReplayAndNoCompetingAutoDemo()
        {
            var existing = SceneManager.GetSceneByPath(RaidSceneCompositionController.ArenaScenePath);
            var openedForTest = !existing.isLoaded;
            var scene = openedForTest
                ? EditorSceneManager.OpenScene(RaidSceneCompositionController.ArenaScenePath,
                    OpenSceneMode.Additive)
                : existing;
            try
            {
                var presentations = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RaidCommandStreamPresentationController>(true))
                    .ToArray();
                var incoming = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<IncomingRaidScenarioController>(true))
                    .Single();
                var serialized = new SerializedObject(incoming);

                Assert.That(presentations, Has.Length.EqualTo(1));
                Assert.That(presentations[0].gameObject.name, Is.EqualTo("[Authoritative Raid Replay]"));
                Assert.That(serialized.FindProperty("autoStartOnPlay").boolValue, Is.False,
                    "Legacy incoming demo must not auto-start beside command-stream replay.");
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
