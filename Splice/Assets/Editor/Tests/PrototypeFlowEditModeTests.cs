#if UNITY_EDITOR
using NUnit.Framework;
using Splice.Base;
using Splice.UI;
using Splice.RaidWorker;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

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
        public void RaidContract_RetriesOnlyTheKnownLocalServerStartupRace()
        {
            Assert.That(LocalRaidStakeController.IsTransientReadinessError(
                "Raid snapshot server is not ready."), Is.True);
            Assert.That(LocalRaidStakeController.IsTransientReadinessError(
                "Selected immutable snapshot is unavailable before stake debit."), Is.False);
            Assert.That(LocalRaidStakeController.IsTransientReadinessError(null), Is.False);
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
