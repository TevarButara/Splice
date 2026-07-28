#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using Splice.Base;
using Splice.Data;
using Splice.Editor.Validation;
using Splice.UI;
using Splice.Validation;
using Splice.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Tests.EditMode
{
    public sealed class WorldForestPrototypeEditModeTests
    {
        [TearDown]
        public void TearDown() => ForestHuntProgressStore.DeleteForTests();

        [Test]
        public void ForestSettlement_ConvertsFragmentsAndEnforcesWeeklyCap()
        {
            var now = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);

            var first = ForestHuntProgressStore.Settle(250, 100, 3, now);
            Assert.That(first.convertedDiamonds, Is.EqualTo(2));
            Assert.That(first.progress.fragments, Is.EqualTo(50));
            Assert.That(first.progress.diamondsEarnedThisWeek, Is.EqualTo(2));

            var second = ForestHuntProgressStore.Settle(300, 100, 3, now);
            Assert.That(second.convertedDiamonds, Is.EqualTo(1));
            Assert.That(second.weeklyCapReached, Is.True);
            Assert.That(second.progress.fragments, Is.EqualTo(250),
                "Fragments must remain banked after the weekly Diamond cap is reached.");
        }

        [Test]
        public void ForestSettlement_ResetsWeeklyCounterWithoutDeletingFragments()
        {
            ForestHuntProgressStore.Settle(250, 100, 3,
                new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc));

            var nextWeek = ForestHuntProgressStore.Load(
                new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(nextWeek.diamondsEarnedThisWeek, Is.Zero);
            Assert.That(nextWeek.fragments, Is.EqualTo(50));
            Assert.That(nextWeek.diamonds, Is.EqualTo(2));
        }

        [Test]
        public void WorldAndForestScenes_AreEditorAuthoredAndShareSceneOwnership()
        {
            ValidateScene("Assets/=======SCENES/WorldMap.unity", scene =>
            {
                Assert.That(Find<Camera>(scene), Is.Not.Null);
                Assert.That(Find<Light>(scene), Is.Not.Null);
                var controller = Find<WorldMapController>(scene);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasEditorAuthoredUi, Is.True);
                Assert.That(controller.Definition, Is.Not.Null);
                Assert.That(controller.Definition.Nodes.Count, Is.EqualTo(3));
            });
            ValidateScene("Assets/=======SCENES/ForestZone.unity", scene =>
            {
                Assert.That(Find<Camera>(scene), Is.Not.Null);
                Assert.That(Find<Light>(scene), Is.Not.Null);
                var controller = Find<ForestEncounterController>(scene);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasEditorAuthoredUi, Is.True);
                var targets = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ForestMonsterTarget>(true))
                    .ToArray();
                Assert.That(targets, Has.Length.EqualTo(6));
                Assert.That(targets.All(target => target.gameObject.scene == scene), Is.True,
                    "Generated monster children must inherit their parent's Scene; moving a non-root GameObject is invalid.");
            });
        }

        [Test]
        public void BuildZone_HasTownMapAndEditorAuthoredExpansionPanel()
        {
            ValidateScene("Assets/=======SCENES/BuildZone.unity", scene =>
            {
                var manager = Find<BaseBuildManager>(scene);
                var panel = Find<TownRegionPurchaseController>(scene);
                Assert.That(manager, Is.Not.Null);
                Assert.That(manager.TownMap, Is.Not.Null);
                Assert.That(manager.TownMap.MapId, Is.EqualTo(TownExpansionPrototypeCatalog.MapTemplateId));
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.HasEditorAuthoredUi, Is.True);
            });
        }

        [Test]
        public void ContentValidator_CoversWorldContractsWithoutWorldErrors()
        {
            var report = new ContentValidationReport();
            WorldPrototypeValidator.Validate(report);

            Assert.That(report.Issues.Where(issue =>
                issue.Severity == ContentValidationSeverity.Error), Is.Empty,
                report.DetailedSummary());
        }

        private static void ValidateScene(string path, Action<Scene> validation)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { validation(scene); }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T Find<T>(Scene scene) where T : Component
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
