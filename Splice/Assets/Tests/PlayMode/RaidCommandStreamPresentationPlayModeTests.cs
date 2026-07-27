using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Splice.Tests.PlayMode
{
    public sealed class RaidCommandStreamPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator LocalAuthoritativeReplayBuildsActorsAndReachesComplete()
        {
            var root = new GameObject("Command Stream Presentation Test");
            root.SetActive(false);
            var type = Type.GetType(
                "Splice.RaidWorker.RaidCommandStreamPresentationController, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var controller = root.AddComponent(type);
            var canvasRoot = new GameObject("Authoritative Replay HUD Test",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(root.transform, false);
            var textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.That(textType, Is.Not.Null);
            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(canvasRoot.transform, false);
            var titleText = title.AddComponent(textType);
            var status = new GameObject("Status", typeof(RectTransform));
            status.transform.SetParent(canvasRoot.transform, false);
            var statusText = status.AddComponent(textType);
            var progress = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            progress.transform.SetParent(canvasRoot.transform, false);
            type.GetField("overlayRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, canvasRoot);
            type.GetField("overlayCanvas", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, canvasRoot.GetComponent<Canvas>());
            type.GetField("titleLabel", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, titleText);
            type.GetField("statusLabel", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, statusText);
            type.GetField("progressFill", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, progress.GetComponent<Image>());
            root.SetActive(true);
            type.GetMethod("ConfigureForTests")?.Invoke(controller, new object[] { 200f });
            type.GetMethod("BeginLocalDemo")?.Invoke(controller, null);

            Assert.That(Property<string>(type, controller, "LastError"), Is.Empty);
            Assert.That(Property<int>(type, controller, "SpawnedActorCount"), Is.GreaterThanOrEqualTo(3));
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!Property<bool>(type, controller, "IsComplete") &&
                   Time.realtimeSinceStartup < deadline) yield return null;

            Assert.That(Property<bool>(type, controller, "IsComplete"), Is.True,
                Property<string>(type, controller, "LastError"));
            Assert.That(Property<bool>(type, controller, "IsPlaying"), Is.False);
            Assert.That(Property<string>(type, controller, "LastCommandType"), Is.EqualTo("COMPLETE"));
            Assert.That(Property<int>(type, controller, "VisibleActorCount"), Is.GreaterThanOrEqualTo(3),
                "Presentation-owned actor roots must survive optional network prefab lifecycle teardown.");
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalAuthoritativeReplayClosesLegacyRolePicker()
        {
            var rolePanel = new GameObject("Legacy Role Picker");
            rolePanel.SetActive(false);
            var sideRoot = new GameObject("Side Selection Test");
            var sideType = Type.GetType("Splice.UI.SideSelectionController, Assembly-CSharp");
            Assert.That(sideType, Is.Not.Null);
            var sideController = sideRoot.AddComponent(sideType);
            sideType.GetField("selectionPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(sideController, rolePanel);
            yield return null;
            Assert.That(rolePanel.activeSelf, Is.True, "The regression fixture must reproduce the open picker.");

            sideType.GetMethod("CloseSelectionPanelForReplay")?.Invoke(sideController, null);

            Assert.That(rolePanel.activeSelf, Is.False,
                "Authoritative defender replay must not be obscured by the legacy role picker.");
            UnityEngine.Object.Destroy(sideRoot);
            UnityEngine.Object.Destroy(rolePanel);
            yield return null;
        }

        private static T Property<T>(Type type, Component instance, string name) =>
            (T)type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
    }
}
