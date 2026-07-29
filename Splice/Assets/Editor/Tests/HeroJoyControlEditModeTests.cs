#if UNITY_EDITOR
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using PinePie.SimpleJoystick;
using Splice.Characters;
using Splice.Combat;
using Splice.Data;
using Splice.Input;
using Splice.Placement;
using TMPro;
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
        private const string RowanSourcePrefabPath =
            "Assets/Prefabs/Natural/Heroes/1-Rowan/nat-Rowan11000.prefab";
        private const string RowanPlaceablePrefabPath =
            "Assets/Prefabs/Natural/Heroes/1-Rowan/nat-Rowan11000_Placeable.prefab";
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
                var buttons = canvas.GetComponentsInChildren<Button>(true);
                var reborn = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                    .Single(button => button.name == "bt-reborn");
                Assert.That(
                    reborn.GetComponentsInChildren<TMP_Text>(true)
                        .Any(label => label.name == "reborn_count"),
                    Is.True);
                foreach (var abilityButtonName in new[]
                         {
                             "bt-blink", "bt-heal", "bt-skill1", "bt-skill2", "bt-skill3"
                         })
                {
                    var abilityButton = buttons.Single(button => button.name == abilityButtonName);
                    var overlay = abilityButton.GetComponentsInChildren<Image>(true)
                        .Single(image => image.name == "cool-overlay");
                    Assert.That(overlay.type, Is.EqualTo(Image.Type.Filled), abilityButtonName);
                    Assert.That(
                        overlay.fillMethod,
                        Is.EqualTo(Image.FillMethod.Radial360),
                        abilityButtonName);
                    Assert.That(
                        abilityButton.GetComponentsInChildren<TMP_Text>(true)
                            .Any(label => label.name == "cooldown"),
                        Is.True,
                        abilityButtonName);
                }
                Assert.That(
                    canvas.GetComponentsInChildren<Transform>(true)
                        .Any(item => item.name == "Panel_Attack_Button"),
                    Is.True);
                controller.EnsureBinding();
                Assert.That(
                    controller.HasCompleteBinding,
                    Is.True,
                    "The action controller must bind the bottom-HUD reborn button across Canvas roots.");
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AbilityCooldownFill_CountsDownFromFullToEmpty()
        {
            Assert.That(HeroActionButtonController.CalculateCooldownFill(8f, 8f), Is.EqualTo(1f));
            Assert.That(HeroActionButtonController.CalculateCooldownFill(4f, 8f), Is.EqualTo(0.5f));
            Assert.That(HeroActionButtonController.CalculateCooldownFill(0f, 8f), Is.EqualTo(0f));
            Assert.That(HeroActionButtonController.CalculateCooldownFill(1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void HeroAbilityAvailability_RequiresEnoughMana()
        {
            Assert.That(RaidHeroCharacter.HasSufficientMana(20f, 20f), Is.True);
            Assert.That(RaidHeroCharacter.HasSufficientMana(19.9f, 20f), Is.False);
            Assert.That(RaidHeroCharacter.HasSufficientMana(0f, 0f), Is.True);
        }

        [Test]
        public void HeroAutoCombat_UsesPlanarRangeAndSensibleHealThreshold()
        {
            Assert.That(
                RaidHeroCharacter.HorizontalSqrDistance(
                    new Vector3(0f, 100f, 0f),
                    new Vector3(1f, -100f, 0f)),
                Is.EqualTo(1f),
                "Tower/Hero pivot height must not prevent an in-range normal attack.");
            Assert.That(RaidHeroCharacter.ShouldAutoHeal(900, 1000, 500), Is.False);
            Assert.That(RaidHeroCharacter.ShouldAutoHeal(700, 1000, 500), Is.True);
            Assert.That(RaidHeroCharacter.ShouldAutoHeal(500, 1000, 100), Is.True);
            Assert.That(RaidHeroCharacter.ShouldAutoHeal(1000, 1000, 500), Is.False);
        }

        [Test]
        public void HeroAutoCombat_HasSkillAndHealDecisionPaths()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(
                typeof(RaidHeroCharacter).GetMethod("TryUseAutoHeal", flags),
                Is.Not.Null);
            Assert.That(
                typeof(RaidHeroCharacter).GetMethod("TryUseAutoSkill", flags),
                Is.Not.Null);
            Assert.That(
                typeof(RaidHeroCharacter).GetMethod("ResolveAutoTarget", flags),
                Is.Not.Null);
        }

        [Test]
        public void ManualReborn_IsExposedAsServerValidatedHeroIntent()
        {
            Assert.That(
                typeof(RaidHeroCharacter).GetProperty(
                    "CanReborn",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(RaidHeroCharacter).GetMethod(
                    "RequestRebornServerRpc",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                typeof(HeroActionButtonController).GetMethod(
                    "Reborn",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
        }

        [Test]
        public void RebornBinding_ReplacesHeroResolvedBeforeOwnerSpawn()
        {
            var controllerObject = new GameObject("RebornBindingRegressionController");
            var staleObject = new GameObject("StaleSyntheticHero");
            var preferredObject = new GameObject("PreferredOwnerHero");
            var controller = controllerObject.AddComponent<HeroActionButtonController>();
            var stale = staleObject.AddComponent<RaidHeroCharacter>();
            var preferred = preferredObject.AddComponent<RaidHeroCharacter>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
            var heroField = typeof(HeroActionButtonController).GetField("hero", flags);
            var instanceField = typeof(RaidHeroCharacter).GetField("<Instance>k__BackingField", staticFlags);
            var resolve = typeof(HeroActionButtonController).GetMethod("ResolveHero", flags);
            var originalInstance = RaidHeroCharacter.Instance;
            try
            {
                Assert.That(heroField, Is.Not.Null);
                Assert.That(instanceField, Is.Not.Null);
                Assert.That(resolve, Is.Not.Null);
                heroField.SetValue(controller, stale);
                instanceField.SetValue(null, preferred);

                resolve.Invoke(controller, null);

                Assert.That(
                    heroField.GetValue(controller),
                    Is.SameAs(preferred),
                    "A UI controller created before the owner Hero spawns must not stay bound to a stale Hero.");
            }
            finally
            {
                instanceField?.SetValue(null, originalInstance);
                Object.DestroyImmediate(preferredObject);
                Object.DestroyImmediate(staleObject);
                Object.DestroyImmediate(controllerObject);
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
            Assert.That(rowan.skill3.castType, Is.EqualTo(HeroAbilityCastType.LockedTarget));
            Assert.That(rowan.skill3.damageMode, Is.EqualTo(HeroAbilityDamageMode.Instant));
            Assert.That(rowan.skill3.execution, Is.TypeOf<MultiDashHeroAbilityExecutionSO>());
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
        public void RowanPlaceable_PreservesAnimationBindingsAndSourceFootprint()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RowanSourcePrefabPath);
            var placeable = AssetDatabase.LoadAssetAtPath<GameObject>(RowanPlaceablePrefabPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(placeable, Is.Not.Null);

            var sourceAnimator = source.GetComponentInChildren<Animator>(true);
            var placeableAnimator = placeable.GetComponentInChildren<Animator>(true);
            Assert.That(sourceAnimator, Is.Not.Null);
            Assert.That(placeableAnimator, Is.Not.Null);
            Assert.That(placeableAnimator.transform, Is.SameAs(placeable.transform),
                "The Animator root must not gain a VisualRoot prefix.");

            foreach (var clip in sourceAnimator.runtimeAnimatorController.animationClips)
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (string.IsNullOrEmpty(binding.path)) continue;
                Assert.That(placeable.transform.Find(binding.path), Is.Not.Null,
                    $"{clip.name} cannot resolve animated path '{binding.path}' in Placeable.");
            }

            var profile = placeable.GetComponent<GroundPlacementProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.IsComplete, Is.True);
            Assert.That(profile.VisualRoot, Is.SameAs(placeable.transform),
                "Animated gameplay prefabs must preserve their original hierarchy.");

            var sourceBounds = RendererBounds(source);
            var placeableBounds = RendererBounds(placeable);
            var sourceFootprint = Mathf.Max(sourceBounds.size.x, sourceBounds.size.z);
            var placeableFootprint = Mathf.Max(placeableBounds.size.x, placeableBounds.size.z);
            Assert.That(placeableFootprint, Is.EqualTo(sourceFootprint).Within(.05f),
                "Grounding must not make the Hero larger while attached skill VFX keep source scale.");
            Assert.That(placeableBounds.min.y, Is.EqualTo(profile.GroundAnchor.position.y)
                .Within(.02f));

            var serializedHero = new SerializedObject(
                placeable.GetComponent<RaidHeroCharacter>());
            Assert.That(serializedHero.FindProperty("animator").objectReferenceValue,
                Is.SameAs(placeableAnimator));
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
        public void TargetAssist_ReleasesCharacterAfterItLeavesCamera()
        {
            var cameraObject = new GameObject("OffscreenUnlockRegressionCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            var targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "OffscreenUnlockRegressionTarget";
            var target = targetObject.AddComponent<MonsterCharacter>();
            try
            {
                targetObject.transform.position = new Vector3(0f, 0f, 10f);
                Assert.That(
                    HeroActionButtonController.IsCharacterVisible(camera, target),
                    Is.True,
                    "A locked target may remain locked while visible.");

                targetObject.transform.position = new Vector3(100f, 0f, 10f);
                Assert.That(
                    HeroActionButtonController.IsCharacterVisible(camera, target),
                    Is.False,
                    "Walking or panning away must make the target eligible for immediate unlock.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
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

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, $"{root.name} has no renderers.");
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
#endif
