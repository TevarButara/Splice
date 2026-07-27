#if UNITY_EDITOR
using NUnit.Framework;
using Splice.Base;
using Splice.Core;
using Splice.UI;
using Splice.RaidWorker;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Splice.Tests.EditMode
{
    public sealed class PrototypeFlowEditModeTests
    {
        [Test]
        public void EnabledScenesContract_RequiresCompleteTownRaidResultLoop()
        {
            var complete = new[]
            {
                "Bootstrap", "BuildZone", "RaidArena",
                "RaidAttackerPresentation", "RaidDefenderPresentation",
            };
            Assert.That(PrototypeFlowContract.ValidateEnabledSceneNames(complete, out var missing), Is.True);
            Assert.That(missing, Is.Empty);

            Assert.That(PrototypeFlowContract.ValidateEnabledSceneNames(
                new[] { "Bootstrap", "RaidArena" }, out missing), Is.False);
            Assert.That(missing, Does.Contain("BuildZone"));
        }

        [TestCase(true, false, false, true)]
        [TestCase(false, false, false, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, false, true, false)]
        public void RaidContract_AutoOpensOnlyForProductTargetRoute(bool hasTarget,
            bool incomingDefense, bool pendingReplay, bool expected)
        {
            Assert.That(PrototypeFlowContract.ShouldAutoOpenRaidContract(
                hasTarget, incomingDefense, pendingReplay), Is.EqualTo(expected));
        }

        [Test]
        public void HubAndRaidSceneNames_MatchTargetSelectionAndHistoryRoute()
        {
            Assert.That(PrototypeFlowContract.HubScene, Is.EqualTo("BuildZone"));
            Assert.That(PrototypeFlowContract.RaidScene, Is.EqualTo("RaidArena"));
            Assert.That(PrototypeFlowContract.RequiredSceneNames, Has.Length.EqualTo(5));
        }

        [TestCase(true, true, false, false, false, true)]
        [TestCase(true, true, true, false, false, false)]
        [TestCase(true, true, false, true, false, false)]
        [TestCase(true, true, false, false, true, false)]
        [TestCase(false, true, false, false, false, false)]
        public void DevelopmentReplay_NeverRacesARealRaidRoute(bool autoStart, bool developmentAllowed,
            bool hasTarget, bool hasSession, bool hasReplay, bool expected)
        {
            Assert.That(RaidCommandStreamPresentationController.ShouldAutoStartDevelopmentDemo(
                autoStart, developmentAllowed, hasTarget, hasSession, hasReplay), Is.EqualTo(expected));
        }

        [Test]
        public void RaidUiScaler_UsesResponsivePrototypeReference()
        {
            var root = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                SpliceSceneUiThemeController.ConfigurePrototypeCanvasScaler(canvas);
                var scaler = root.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(.5f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildZone_MetaShellIsEditorAuthoredAndHasNoDuplicateRoot()
        {
            const string scenePath = "Assets/=======SCENES/BuildZone.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                PrototypeMetaHubController controller = null;
                var uiRootCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "Prototype Meta UI") uiRootCount++;
                    controller ??= root.GetComponentInChildren<PrototypeMetaHubController>(true);
                }

                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasEditorAuthoredUi, Is.True,
                    "BuildZone must serialize the complete meta shell instead of creating it in Awake.");
                Assert.That(controller.EditorUiRoot, Is.Not.Null);
                Assert.That(controller.EditorUiRoot.scene, Is.EqualTo(scene));
                Assert.That(uiRootCount, Is.EqualTo(1));
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RaidContract_RetriesOnlyTheKnownLocalServerStartupRace()
        {
            Assert.That(LocalRaidStakeController.IsTransientReadinessError(
                "Raid snapshot server is not ready."), Is.True);
            Assert.That(LocalRaidStakeController.IsTransientReadinessError(
                "Selected immutable snapshot is unavailable before stake debit."), Is.False);
            Assert.That(LocalRaidStakeController.IsTransientReadinessError(null), Is.False);
        }

        [Test]
        public void LocalPveHost_UsesEphemeralLoopbackPort_ToAvoidEditorPortCollisions()
        {
            var root = new GameObject("LocalTransport", typeof(UnityTransport));
            try
            {
                var transport = root.GetComponent<UnityTransport>();
                transport.ConnectionData.Port = 7777;
                transport.ConnectionData.Address = "10.0.0.1";
                transport.ConnectionData.ServerListenAddress = "0.0.0.0";

                GameBootstrap.ConfigureLocalPveTransport(transport);

                Assert.That(transport.ConnectionData.Port, Is.EqualTo(GameBootstrap.LocalPveEphemeralPort));
                Assert.That(transport.ConnectionData.Address, Is.EqualTo("127.0.0.1"));
                Assert.That(transport.ConnectionData.ServerListenAddress, Is.EqualTo("127.0.0.1"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, false, "CONFIRM RAID")]
        [TestCase(true, false, "PREPARING...")]
        [TestCase(false, true, "STARTING RAID...")]
        public void RaidContract_ConfirmButtonAlwaysShowsCurrentAction(bool preparing,
            bool confirming, string expected)
        {
            Assert.That(LocalRaidStakeController.ConfirmButtonText(preparing, confirming),
                Is.EqualTo(expected));
        }

        [Test]
        public void PrototypeBuild_AllowsHttpOnlyForDevelopmentLocalContent()
        {
            Assert.That(PlayerSettings.insecureHttpOption,
                Is.EqualTo(InsecureHttpOption.DevelopmentOnly),
                "Local Addressables may use HTTP in Development builds; production must remain HTTPS-only.");
        }
    }
}
#endif
