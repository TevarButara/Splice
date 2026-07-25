#if UNITY_EDITOR
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using PinePie.SimpleJoystick;
using Splice.Characters;
using Splice.Combat;
using Splice.Data;
using Splice.Input;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.Tests.EditMode
{
    public sealed class HeroJoyControlEditModeTests
    {
        private const string RaidArenaPath = "Assets/=======SCENES/RaidArena.unity";
        private const string RowanPath =
            "Assets/Prefabs/Natural/Heroes/1-Rowan/Rowan_Definition.asset";
        private const string TestHeroPath =
            "Assets/Prefabs/Heroes/Hero_Test_Definition.asset";

        [Test]
        public void RaidArena_JoyCanvasHasJoystickAndAllActionButtons()
        {
            var existing = SceneManager.GetSceneByPath(RaidArenaPath);
            var openedForTest = !existing.isLoaded;
            var scene = openedForTest
                ? EditorSceneManager.OpenScene(RaidArenaPath, OpenSceneMode.Additive)
                : existing;
            try
            {
                var canvas = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(transform => transform.name == "CanvasJoyControl");
                var controller = canvas.GetComponent<HeroActionButtonController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(canvas.GetComponentInChildren<JoystickController>(true), Is.Not.Null);

                var buttonNames = canvas.GetComponentsInChildren<Button>(true)
                    .Select(button => button.name)
                    .ToArray();
                CollectionAssert.IsSubsetOf(
                    new[]
                    {
                        "bt-blink", "bt-heal", "bt-attack", "bt-skill1", "bt-skill2", "bt-skill3",
                        "bt-auto", "bt-target-mon", "bt-target-tower"
                    },
                    buttonNames);
                Assert.That(
                    canvas.GetComponentsInChildren<Transform>(true)
                        .Any(item => item.name == "Panel_Attack_Button"),
                    Is.True);
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void UniversalActions_AreSharedAndRowanHasThreeSkills()
        {
            var rowan = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(RowanPath);
            var testHero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(TestHeroPath);
            Assert.That(rowan, Is.Not.Null);
            Assert.That(testHero, Is.Not.Null);
            Assert.That(rowan.blinkAbility, Is.SameAs(testHero.blinkAbility));
            Assert.That(rowan.healAbility, Is.SameAs(testHero.healAbility));
            Assert.That(rowan.blinkAbility.effect, Is.EqualTo(HeroAbilityEffect.ForwardBlink));
            Assert.That(rowan.blinkAbility.animationState, Is.EqualTo("Sprint"));
            Assert.That(rowan.healAbility.effect, Is.EqualTo(HeroAbilityEffect.SelfHeal));
            Assert.That(rowan.healAbility.healing, Is.GreaterThan(0));
            Assert.That(rowan.GetAbility(HeroAbilitySlot.Skill1), Is.Not.Null);
            Assert.That(rowan.GetAbility(HeroAbilitySlot.Skill2), Is.Not.Null);
            Assert.That(rowan.GetAbility(HeroAbilitySlot.Skill3), Is.Not.Null);
            Assert.That(rowan.animSet, Is.Not.Null);
            Assert.That(rowan.animSet.idle, Is.EqualTo("Idle"));
            Assert.That(rowan.animSet.walk, Is.EqualTo("Walk"));
            Assert.That(rowan.animSet.attack1, Is.EqualTo("Attack"));
            Assert.That(rowan.animSet.attack2, Is.Not.Empty);
            Assert.That(rowan.animSet.death, Is.EqualTo("Death"));
            Assert.That(rowan.animSet.landing, Is.Not.Empty);
            Assert.That(rowan.maxMana, Is.GreaterThan(0f));
            Assert.That(rowan.manaGenerationPercentPerSecond, Is.GreaterThanOrEqualTo(0f));
            Assert.That(rowan.normalAttackImpactDelay, Is.GreaterThan(0f));
            Assert.That(rowan.blinkAbility.effectPlacement, Is.EqualTo(HeroAbilityEffectPlacement.HeroEffectAnchor));
            Assert.That(rowan.healAbility.effectPlacement, Is.EqualTo(HeroAbilityEffectPlacement.HeroEffectAnchor));
            Assert.That(rowan.blinkAbility.castType, Is.EqualTo(HeroAbilityCastType.SelfCast));
            Assert.That(rowan.healAbility.castType, Is.EqualTo(HeroAbilityCastType.SelfCast));
            Assert.That(rowan.skill1.castType, Is.EqualTo(HeroAbilityCastType.LockedTarget));
            Assert.That(rowan.skill2.castType, Is.EqualTo(HeroAbilityCastType.SelfCast));
            Assert.That(rowan.skill3.castType, Is.EqualTo(HeroAbilityCastType.DragArea));
            Assert.That(rowan.skill3.damageMode, Is.EqualTo(HeroAbilityDamageMode.DamageOverTime));
            Assert.That(rowan.normalAttackEffectPrefab, Is.Not.Null);
        }

        [Test]
        public void RowanAnimator_ContainsEveryButtonAnimationState()
        {
            var rowan = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(RowanPath);
            var animator = rowan.prefab.GetComponentInChildren<Animator>(true);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            var states = controller.layers
                .SelectMany(layer => layer.stateMachine.states)
                .Select(state => state.state.name)
                .ToArray();
            CollectionAssert.IsSubsetOf(
                new[] { "Idle", "Walk", "Attack", "Sprint", "Skill1", "Skill2", "Skill3" },
                states);
        }

        [Test]
        public void RowanAnimator_CanEnterLocomotionAndActionStates()
        {
            var rowan = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(RowanPath);
            var instance = Object.Instantiate(rowan.prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                animator.Update(0f);

                Assert.That(AnimatorUtil.SafeCrossFade(animator, rowan.animSet.walk, 0f), Is.True);
                animator.Update(0.01f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("Walk")));

                Assert.That(AnimatorUtil.SafeCrossFade(animator, rowan.animSet.attack1, 0f), Is.True);
                animator.Update(0.01f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("Attack")));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GroundSurfaceResolver_SnapsUnitsAndFxToGroundLayer()
        {
            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "RegressionGroundSurface";
            surface.layer = LayerMask.NameToLayer("Ground");
            surface.transform.position = new Vector3(10000f, 7f, 10000f);
            try
            {
                Physics.SyncTransforms();
                var desired = new Vector3(10000f, -20f, 10000f);
                Assert.That(
                    GroundSurfaceResolver.TrySnap(desired, null, out var snapped, 0.15f),
                    Is.True);
                Assert.That(snapped.y, Is.EqualTo(7.15f).Within(0.02f));
            }
            finally
            {
                Object.DestroyImmediate(surface);
            }
        }

        [Test]
        public void TargetAssist_KeepsHeroModeAndJoystickContract()
        {
            Assert.That(
                RaidHeroCharacter.TargetAssistControlMode,
                Is.EqualTo(HeroControlMode.Manual),
                "Target buttons must not switch the Hero to Auto or hide Manual controls.");
        }

        [Test]
        public void TargetAssist_UsesOnlyScreenVisibleExplicitCandidates()
        {
            var cameraObject = new GameObject("TargetVisibilityRegressionCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            try
            {
                Assert.That(
                    HeroActionButtonController.IsBoundsVisible(
                        camera,
                        new Bounds(new Vector3(0f, 0f, 10f), Vector3.one)),
                    Is.True);
                Assert.That(
                    HeroActionButtonController.IsBoundsVisible(
                        camera,
                        new Bounds(new Vector3(100f, 0f, 10f), Vector3.one)),
                    Is.False);
                Assert.That(
                    HeroActionButtonController.IsBoundsVisible(
                        camera,
                        new Bounds(new Vector3(0f, 0f, -10f), Vector3.one)),
                    Is.False);

                Assert.That(
                    typeof(TowerCharacter).GetProperty(
                        "Instances",
                        BindingFlags.Public | BindingFlags.Static),
                    Is.Not.Null,
                    "Remote clients need a presentation-safe tower list for screen target selection.");
                Assert.That(
                    typeof(RaidHeroCharacter).GetMethod(
                        "AcquirePreferredFocusTarget",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    Is.Null,
                    "The server must never replace a dead target with an off-screen nearest target.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void DotDamage_DistributesConfiguredTotalExactlyAcrossDuration()
        {
            var ability = ScriptableObject.CreateInstance<HeroAbilityDefinitionSO>();
            try
            {
                ability.damageMode = HeroAbilityDamageMode.DamageOverTime;
                ability.damage = 101;
                ability.damageDurationSeconds = 3f;
                ability.dotTickIntervalSeconds = 0.5f;

                var distributed = 0;
                for (var i = 0; i < ability.DamageTickCount; i++)
                    distributed += ability.DamageAtTick(i);

                Assert.That(ability.DamageTickCount, Is.EqualTo(6));
                Assert.That(distributed, Is.EqualTo(101));
                Assert.That(ability.DamageAtTick(-1), Is.Zero);
                Assert.That(ability.DamageAtTick(ability.DamageTickCount), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void RowanHeroPresentation_MovementAndAttackDriveAnimator()
        {
            var rowan = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(RowanPath);
            var instance = Object.Instantiate(rowan.prefab);
            var definitionCopy = Object.Instantiate(rowan);
            definitionCopy.normalAttackEffectPrefab = null;
            try
            {
                var hero = instance.GetComponent<RaidHeroCharacter>();
                SetPrivateField(hero, "definition", definitionCopy);
                InvokePrivate(hero, "InitializePresentation");

                instance.transform.position += Vector3.right;
                InvokePrivate(hero, "UpdatePresentationFromReplicatedTransform");
                Assert.That(
                    GetPrivateField<string>(hero, "currentPresentationState"),
                    Is.EqualTo("Walk"));

                SetPrivateField(hero, "lastPresentationMovementTime", float.NegativeInfinity);
                InvokePrivate(hero, "UpdatePresentationFromReplicatedTransform");
                Assert.That(
                    GetPrivateField<string>(hero, "currentPresentationState"),
                    Is.EqualTo("Idle"));

                InvokePrivate(hero, "PlayNormalAttackPresentation", false);
                Assert.That(
                    GetPrivateField<string>(hero, "currentPresentationState"),
                    Is.EqualTo("Attack"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(definitionCopy);
            }
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}");
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}");
            return (T)field.GetValue(target);
        }
    }
}
#endif
