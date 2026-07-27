#if UNITY_EDITOR
using NUnit.Framework;
using Splice.Base;
using Splice.Core;
using Splice.Input;
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
                TownSnapshotCommitController deploymentController = null;
                var uiRootCount = 0;
                var deploymentRootCount = 0;
                var targetCardCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "Prototype Meta UI") uiRootCount++;
                    controller ??= root.GetComponentInChildren<PrototypeMetaHubController>(true);
                    deploymentController ??= root.GetComponentInChildren<TownSnapshotCommitController>(true);
                    foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (rect.name == "Town Deployment UI") deploymentRootCount++;
                        if (rect.GetComponent<PrototypeRaidTargetCardView>() != null) targetCardCount++;
                    }
                }

                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasEditorAuthoredUi, Is.True,
                    "BuildZone must serialize the complete meta shell instead of creating it in Awake.");
                Assert.That(controller.EditorUiRoot, Is.Not.Null);
                Assert.That(controller.EditorUiRoot.scene, Is.EqualTo(scene));
                Assert.That(uiRootCount, Is.EqualTo(1));
                Assert.That(controller.EditorAuthoredTargetCardCount, Is.EqualTo(3));
                Assert.That(targetCardCount, Is.EqualTo(3),
                    "All three target cards must exist in the scene before Play Mode.");
                Assert.That(deploymentController, Is.Not.Null);
                Assert.That(deploymentController.HasEditorAuthoredUi, Is.True,
                    "Deployment status and review cards must be serialized instead of built in Awake.");
                Assert.That(deploymentController.EditorUiRoot.scene, Is.EqualTo(scene));
                Assert.That(deploymentRootCount, Is.EqualTo(1));
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CameraPan_DecorativeGraphicsDoNotBlockMapDrag_ButButtonsDo()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(canvas.transform, false);
            button.transform.SetParent(backdrop.transform, false);
            try
            {
                Assert.That(CameraPanController.IsUiInteractionBlockingWorldPan(backdrop), Is.False,
                    "A decorative full-screen Image must not disable BuildZone camera pan.");
                Assert.That(CameraPanController.IsUiInteractionBlockingWorldPan(button), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
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
