#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PinePie.SimpleJoystick;
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
                    new[] { "bt-blink", "bt-heal", "bt-attack", "bt-skill1", "bt-skill2", "bt-skill3" },
                    buttonNames);
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
                new[] { "Attack", "Sprint", "Skill1", "Skill2", "Skill3" },
                states);
        }
    }
}
#endif
