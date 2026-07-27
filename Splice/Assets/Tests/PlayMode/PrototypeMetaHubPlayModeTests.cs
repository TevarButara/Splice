using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Splice.Tests.PlayMode
{
    public sealed class PrototypeMetaHubPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildZone_CreatesCompleteMetaNavigationAndOnboarding()
        {
            var operation = SceneManager.LoadSceneAsync("BuildZone", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone) yield return null;
            yield return null;

            var type = Type.GetType("Splice.UI.PrototypeMetaHubController, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var controller = UnityEngine.Object.FindFirstObjectByType(type) as Component;
            Assert.That(controller, Is.Not.Null,
                "BuildZone must own the prototype meta shell in the player build.");
            var metaUi = GameObject.Find("Prototype Meta UI");
            Assert.That(metaUi, Is.Not.Null);
            Assert.That(metaUi.transform.parent, Is.Null);
            var metaCanvas = metaUi.GetComponent<Canvas>();
            Assert.That(metaCanvas, Is.Not.Null);
            Assert.That(metaCanvas.isRootCanvas, Is.True,
                "The meta shell must own a scaled root canvas, never a tower world-space health canvas.");
            Assert.That(metaCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            var scaler = metaUi.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(GameObject.Find("Primary Navigation"), Is.Not.Null);
            Assert.That(GameObject.Find("Command Header"), Is.Not.Null);

            type.GetMethod("ResetOnboardingForTests", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(controller, null);
            Assert.That(Property<bool>(type, controller, "IsOnboardingVisible"), Is.True);
            type.GetMethod("CompleteOnboarding", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(controller, null);
            Assert.That(Property<bool>(type, controller, "IsOnboardingVisible"), Is.False);
            PlayerPrefs.DeleteKey("Splice.Prototype.Onboarding.v1");
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator HubTabs_HideTownOverlayAndExposeRaidAndHistoryScreens()
        {
            var operation = SceneManager.LoadSceneAsync("BuildZone", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;

            var type = Type.GetType("Splice.UI.PrototypeMetaHubController, Assembly-CSharp");
            var controller = UnityEngine.Object.FindFirstObjectByType(type) as Component;
            Assert.That(controller, Is.Not.Null);
            type.GetMethod("CompleteOnboarding")?.Invoke(controller, null);

            type.GetMethod("ShowRaid")?.Invoke(controller, null);
            Assert.That(Property<bool>(type, controller, "IsRaidPanelVisible"), Is.True);
            Assert.That(Property<bool>(type, controller, "IsHistoryPanelVisible"), Is.False);

            type.GetMethod("ShowHistory")?.Invoke(controller, null);
            Assert.That(Property<bool>(type, controller, "IsRaidPanelVisible"), Is.False);
            Assert.That(Property<bool>(type, controller, "IsHistoryPanelVisible"), Is.True);

            type.GetMethod("ShowTown")?.Invoke(controller, null);
            Assert.That(Property<bool>(type, controller, "IsRaidPanelVisible"), Is.False);
            Assert.That(Property<bool>(type, controller, "IsHistoryPanelVisible"), Is.False);
            PlayerPrefs.DeleteKey("Splice.Prototype.Onboarding.v1");
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator RaidResult_AlwaysOffersReturnToTownBesideRetry()
        {
            var operation = SceneManager.LoadSceneAsync("RaidArena", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone) yield return null;
            yield return null;

            var type = Type.GetType("Splice.UI.RaidResultUI, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            Component resultUi = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType(type,
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate is Behaviour behaviour && behaviour.enabled)
                {
                    resultUi = candidate as Component;
                    break;
                }
            }
            Assert.That(resultUi, Is.Not.Null);
            var hasAuthored = (bool)type.GetProperty("HasEditorAuthoredReturnButton",
                BindingFlags.Instance | BindingFlags.Public)?.GetValue(resultUi);
            var returnButton = type.GetProperty("ReturnToTownButton",
                    BindingFlags.Instance | BindingFlags.Public)?.GetValue(resultUi) as Button;
            Assert.That(hasAuthored, Is.True,
                "Raid result navigation must be authored in RaidArena, never cloned at runtime.");
            Assert.That(returnButton, Is.Not.Null,
                "Every completed raid needs a visible route back into the meta loop.");
            Assert.That(returnButton.name, Is.EqualTo("ReturnToTownButton"));
        }

        private static T Property<T>(Type type, Component instance, string name) =>
            (T)type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
    }
}
