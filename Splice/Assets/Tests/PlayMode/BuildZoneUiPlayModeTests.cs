using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class BuildZoneUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildZone_DoesNotCreateDuplicateMetaUiAtRuntime()
        {
            var load = SceneManager.LoadSceneAsync("BuildZone", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var controllerType = Type.GetType("Splice.UI.PrototypeMetaHubController, Assembly-CSharp");
            var deploymentType = Type.GetType("Splice.UI.TownSnapshotCommitController, Assembly-CSharp");
            var targetCardType = Type.GetType("Splice.UI.PrototypeRaidTargetCardView, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(deploymentType, Is.Not.Null);
            Assert.That(targetCardType, Is.Not.Null);
            Component controller = null;
            Component deploymentController = null;
            var rootCount = 0;
            var deploymentRootCount = 0;
            var targetCardCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Prototype Meta UI") rootCount++;
                if (controller == null)
                    controller = root.GetComponentInChildren(controllerType, true) as Component;
                if (deploymentController == null)
                    deploymentController = root.GetComponentInChildren(deploymentType, true) as Component;
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "Town Deployment UI") deploymentRootCount++;
                    if (rect.GetComponent(targetCardType) != null) targetCardCount++;
                }
            }

            Assert.That(scene.name, Is.EqualTo("BuildZone"));
            Assert.That(controller, Is.Not.Null);
            Assert.That((bool)controllerType.GetProperty("HasEditorAuthoredUi").GetValue(controller), Is.True);
            Assert.That(rootCount, Is.EqualTo(1),
                "Awake must bind the serialized shell and must not create another runtime Canvas.");
            Assert.That(targetCardCount, Is.EqualTo(3),
                "Runtime must reuse the three serialized target cards.");
            Assert.That(deploymentController, Is.Not.Null);
            Assert.That((bool)deploymentType.GetProperty("HasEditorAuthoredUi").GetValue(deploymentController), Is.True);
            Assert.That(deploymentRootCount, Is.EqualTo(1),
                "Awake must bind deployment cards and must not create a duplicate runtime hierarchy.");
        }

        [UnityTest]
        public IEnumerator BuildZone_CameraPanMovesAndKeepsTheAuthoredZoomRange()
        {
            var load = SceneManager.LoadSceneAsync("BuildZone", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            var controllerType = Type.GetType("Splice.Input.CameraPanController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            var controller = UnityEngine.Object.FindFirstObjectByType(controllerType) as Component;
            Assert.That(controller, Is.Not.Null);
            var camera = controller.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null);

            var before = controller.transform.position;
            controllerType.GetMethod("PanBy", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(controller, new object[] { new Vector2(100f, 0f) });
            Assert.That(controller.transform.position, Is.Not.EqualTo(before),
                "BuildZone camera must respond to the same drag-pan path used by Raid controls.");

            var maxOrthoSize = (float)controllerType
                .GetField("maxOrthoSize", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(controller);
            Assert.That(maxOrthoSize, Is.GreaterThanOrEqualTo(camera.orthographicSize),
                "The first scroll must not snap an authored size-133.5 camera down to the old size-20 limit.");
        }
    }
}
