#if UNITY_EDITOR
using NUnit.Framework;
using Splice.Base;
using Splice.Core;
using Splice.Input;
using Splice.Data;
using Splice.Placement;
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
                BaseBuildCheckoutController checkoutController = null;
                PlayerTownBaseController townBaseController = null;
                var uiRootCount = 0;
                var deploymentRootCount = 0;
                var targetCardCount = 0;
                var historyRowCount = 0;
                var listStateCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "Prototype Meta UI") uiRootCount++;
                    controller ??= root.GetComponentInChildren<PrototypeMetaHubController>(true);
                    deploymentController ??= root.GetComponentInChildren<TownSnapshotCommitController>(true);
                    checkoutController ??= root.GetComponentInChildren<BaseBuildCheckoutController>(true);
                    townBaseController ??= root.GetComponentInChildren<PlayerTownBaseController>(true);
                    foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (rect.name == "Town Deployment UI") deploymentRootCount++;
                        if (rect.GetComponent<PrototypeRaidTargetCardView>() != null) targetCardCount++;
                        if (rect.GetComponent<PrototypeDefenseHistoryRowView>() != null) historyRowCount++;
                        if (rect.GetComponent<PrototypeListStateView>() != null) listStateCount++;
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
                Assert.That(controller.EditorAuthoredHistoryRowCount, Is.EqualTo(4));
                Assert.That(historyRowCount, Is.EqualTo(4),
                    "All defense report rows must exist in the scene before Play Mode.");
                Assert.That(listStateCount, Is.EqualTo(2),
                    "Raid/history empty and retry states must exist before Play Mode.");
                Assert.That(deploymentController, Is.Not.Null);
                Assert.That(deploymentController.HasEditorAuthoredUi, Is.True,
                    "Deployment status and review cards must be serialized instead of built in Awake.");
                Assert.That(deploymentController.EditorUiRoot.scene, Is.EqualTo(scene));
                Assert.That(deploymentRootCount, Is.EqualTo(1));
                Assert.That(checkoutController, Is.Not.Null);
                Assert.That(checkoutController.HasEditorAuthoredUi, Is.True,
                    "Pa_ConFirmCheckOut and its backdrop/header/buttons must be serialized.");
                Assert.That(townBaseController, Is.Not.Null);
                Assert.That(townBaseController.HasRequiredReferences, Is.True);
                Assert.That(townBaseController.BasePoint.name, Is.EqualTo("BasePoint"));
                Assert.That(townBaseController.BasePoint.childCount, Is.GreaterThan(0),
                    "A level-1 base preview must be visible at BasePoint in the editor.");
                Assert.That(townBaseController.BasePoint.position.y, Is.EqualTo(0f).Within(.01f),
                    "BasePoint must be snapped to BuildZoneTerrain, never the PanBounds volume.");
                var preview = townBaseController.BasePoint.GetChild(0);
                var placement = preview.GetComponent<GroundPlacementProfile>();
                Assert.That(placement, Is.Not.Null);
                Assert.That(placement.IsComplete, Is.True);
                Assert.That(placement.GroundAnchor.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(placement.TryGetRendererBounds(out var baseBounds), Is.True);
                Assert.That(baseBounds.min.y, Is.EqualTo(townBaseController.BasePoint.position.y).Within(.05f),
                    "The canonical Natural Base renderer bottom must touch the authored ground.");
                var groundLayer = LayerMask.NameToLayer(GroundPlacementUtility.GroundLayerName);
                Assert.That(groundLayer, Is.GreaterThanOrEqualTo(8));
                Assert.That(GameObject.Find("BuildZoneTerrain")?.layer, Is.EqualTo(groundLayer));
                Assert.That(GameObject.Find("PanBounds")?.layer, Is.Not.EqualTo(groundLayer),
                    "PanBounds must never be accepted as a terrain hit.");
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EveryRootScreenCanvas_UsesOneCrossPlatformResponsiveContract()
        {
            var scenePaths = new[]
            {
                "Assets/=======SCENES/Bootstrap.unity",
                "Assets/=======SCENES/BuildZone.unity",
                "Assets/=======SCENES/RaidArena.unity",
                "Assets/=======SCENES/RaidAttackerPresentation.unity",
                "Assets/=======SCENES/RaidDefenderPresentation.unity",
                "Assets/=======SCENES/SampleScene.unity",
            };
            foreach (var scenePath in scenePaths)
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                var openedForTest = !scene.IsValid() || !scene.isLoaded;
                if (openedForTest) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    {
                        if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                        var scaler = canvas.GetComponent<CanvasScaler>();
                        Assert.That(scaler, Is.Not.Null, $"{scene.name}/{canvas.name}");
                        Assert.That(scaler.uiScaleMode,
                            Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize),
                            $"{scene.name}/{canvas.name}");
                        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)),
                            $"{scene.name}/{canvas.name}");
                        Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(.5f).Within(.001f),
                            $"{scene.name}/{canvas.name}");
                    }
                }
                finally
                {
                    if (openedForTest) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void RaidResultEditorBake_IsIdempotentAndNeverDriftsButtons()
        {
            const string scenePath = "Assets/=======SCENES/RaidArena.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                RaidResultUI result = null;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<RaidResultUI>(true))
                    if (candidate.enabled && candidate.CanAuthorEditorUi) result = candidate;
                Assert.That(result, Is.Not.Null);

                var serialized = new SerializedObject(result);
                var retry = serialized.FindProperty("playAgainButton").objectReferenceValue as Button;
                var returnToTown =
                    serialized.FindProperty("returnToTownButton").objectReferenceValue as Button;
                Assert.That(retry, Is.Not.Null);
                Assert.That(returnToTown, Is.Not.Null);
                result.RebuildEditorReturnButton();
                var retryAfterFirst = retry.GetComponent<RectTransform>().anchoredPosition;
                var returnAfterFirst = returnToTown.GetComponent<RectTransform>().anchoredPosition;
                result.RebuildEditorReturnButton();
                Assert.That(retry.GetComponent<RectTransform>().anchoredPosition,
                    Is.EqualTo(retryAfterFirst));
                Assert.That(returnToTown.GetComponent<RectTransform>().anchoredPosition,
                    Is.EqualTo(returnAfterFirst));
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BaseDefinition_ResolvesHighestUnlockedLevelWithoutSceneRules()
        {
            var definition = ScriptableObject.CreateInstance<BaseDefinitionSO>();
            try
            {
                var level1 = new BaseLevelDefinition { level = 1, defenseCapacity = 100 };
                var level3 = new BaseLevelDefinition { level = 3, defenseCapacity = 300 };
                definition.levels.Add(level3);
                definition.levels.Add(level1);
                Assert.That(definition.ResolveLevel(1), Is.SameAs(level1));
                Assert.That(definition.ResolveLevel(2), Is.SameAs(level1));
                Assert.That(definition.ResolveLevel(99), Is.SameAs(level3));
            }
            finally
            {
                Object.DestroyImmediate(definition);
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
