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
            var historyRowType = Type.GetType("Splice.UI.PrototypeDefenseHistoryRowView, Assembly-CSharp");
            var listStateType = Type.GetType("Splice.UI.PrototypeListStateView, Assembly-CSharp");
            var checkoutType = Type.GetType("Splice.Base.BaseBuildCheckoutController, Assembly-CSharp");
            var townBaseType = Type.GetType("Splice.Base.PlayerTownBaseController, Assembly-CSharp");
            var placementType = Type.GetType("Splice.Placement.GroundPlacementProfile, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(deploymentType, Is.Not.Null);
            Assert.That(targetCardType, Is.Not.Null);
            Assert.That(historyRowType, Is.Not.Null);
            Assert.That(listStateType, Is.Not.Null);
            Assert.That(placementType, Is.Not.Null);
            Component controller = null;
            Component deploymentController = null;
            var rootCount = 0;
            var deploymentRootCount = 0;
            var targetCardCount = 0;
            var historyRowCount = 0;
            var listStateCount = 0;
            Component checkout = null;
            Component townBase = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Prototype Meta UI") rootCount++;
                if (controller == null)
                    controller = root.GetComponentInChildren(controllerType, true) as Component;
                if (deploymentController == null)
                    deploymentController = root.GetComponentInChildren(deploymentType, true) as Component;
                checkout ??= root.GetComponentInChildren(checkoutType, true) as Component;
                townBase ??= root.GetComponentInChildren(townBaseType, true) as Component;
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "Town Deployment UI") deploymentRootCount++;
                    if (rect.GetComponent(targetCardType) != null) targetCardCount++;
                    if (rect.GetComponent(historyRowType) != null) historyRowCount++;
                    if (rect.GetComponent(listStateType) != null) listStateCount++;
                }
            }

            Assert.That(scene.name, Is.EqualTo("BuildZone"));
            Assert.That(controller, Is.Not.Null);
            Assert.That((bool)controllerType.GetProperty("HasEditorAuthoredUi").GetValue(controller), Is.True);
            Assert.That(rootCount, Is.EqualTo(1),
                "Awake must bind the serialized shell and must not create another runtime Canvas.");
            Assert.That(targetCardCount, Is.EqualTo(3),
                "Runtime must reuse the three serialized target cards.");
            Assert.That(historyRowCount, Is.EqualTo(4),
                "Runtime must reuse four serialized defense-history rows.");
            Assert.That(listStateCount, Is.EqualTo(2),
                "Runtime must reuse serialized raid/history empty and retry states.");
            Assert.That(deploymentController, Is.Not.Null);
            Assert.That((bool)deploymentType.GetProperty("HasEditorAuthoredUi").GetValue(deploymentController), Is.True);
            Assert.That(deploymentRootCount, Is.EqualTo(1),
                "Awake must bind deployment cards and must not create a duplicate runtime hierarchy.");
            Assert.That(checkout, Is.Not.Null);
            Assert.That((bool)checkoutType.GetProperty("HasEditorAuthoredUi").GetValue(checkout), Is.True,
                "Pa_ConFirmCheckOut and all modal visuals must be serialized in BuildZone.");
            Assert.That(townBase, Is.Not.Null);
            Assert.That((bool)townBaseType.GetProperty("HasRequiredReferences").GetValue(townBase), Is.True);
            var basePoint = townBaseType.GetProperty("BasePoint").GetValue(townBase) as Transform;
            var spawnedBase = townBaseType.GetProperty("SpawnedBase").GetValue(townBase) as GameObject;
            Assert.That(basePoint, Is.Not.Null);
            Assert.That(basePoint.name, Is.EqualTo("BasePoint"));
            Assert.That(spawnedBase, Is.Not.Null);
            Assert.That(spawnedBase.transform.parent, Is.EqualTo(basePoint));
            Assert.That(spawnedBase.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(basePoint.position.y, Is.EqualTo(0f).Within(.01f),
                "Runtime must raycast the Ground layer instead of stopping on PanBounds.");
            var placement = spawnedBase.GetComponent(placementType);
            Assert.That(placement, Is.Not.Null);
            var renderers = spawnedBase.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            Assert.That(bounds.min.y, Is.EqualTo(basePoint.position.y).Within(.05f),
                "Natural Base must touch the terrain and must not sink or float.");
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
