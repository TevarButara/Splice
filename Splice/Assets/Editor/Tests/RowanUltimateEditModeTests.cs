#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Splice.Characters;
using Splice.Combat;
using Splice.Data;
using UnityEditor;
using UnityEngine;

namespace Splice.Tests.EditMode
{
    public sealed class RowanUltimateEditModeTests
    {
        private const string Root =
            "Assets/Prefabs/Natural/Heroes/1-Rowan";

        [Test]
        public void MultiDashDamageSplit_PreservesExactTotal()
        {
            for (var total = 0; total <= 1000; total++)
            {
                var sum = 0;
                for (var strike = 0; strike < 7; strike++)
                    sum += MultiDashHeroAbilityExecutionSO.SplitDamage(
                        total, 7, strike);
                Assert.That(sum, Is.EqualTo(total), "total=" + total);
            }
        }

        [Test]
        public void RowanUltimate_UsesSevenServerAuthoredStrikes()
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(
                Root + "/Rowan_Definition.asset");
            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.skill3, Is.Not.Null);
            var execution =
                hero.skill3.execution as MultiDashHeroAbilityExecutionSO;
            Assert.That(execution, Is.Not.Null);
            Assert.That(execution.strikeCount, Is.EqualTo(7));
            Assert.That(execution.dashSpeed, Is.GreaterThan(0f));
            Assert.That(execution.returnSpeed, Is.GreaterThan(0f));
            Assert.That(execution.impactVfxLifetimeSeconds,
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(execution.randomizeMultipleTargets, Is.True);

            var sum = 0;
            for (var i = 0; i < execution.strikeCount; i++)
                sum += execution.DamageAtStrike(hero.skill3.damage, i);
            Assert.That(sum, Is.EqualTo(hero.skill3.damage));
        }

        [Test]
        public void RowanUltimate_CastRangeClearsNaturalTowerThreatEnvelope()
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(
                Root + "/Rowan_Definition.asset");
            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.skill3, Is.Not.Null);

            var maxTowerRange = 0f;
            var towerGuids = AssetDatabase.FindAssets(
                "t:TowerDefinitionSO",
                new[] { "Assets/Prefabs/Natural/Tower" });
            Assert.That(towerGuids, Is.Not.Empty);
            for (var i = 0; i < towerGuids.Length; i++)
            {
                var tower = AssetDatabase.LoadAssetAtPath<TowerDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(towerGuids[i]));
                if (tower != null)
                    maxTowerRange = Mathf.Max(maxTowerRange, tower.attackRange);
            }

            Assert.That(hero.skill3.castRange, Is.GreaterThan(maxTowerRange),
                "Rowan Ultimate must become usable before Rowan has to walk through a tower's full threat envelope.");
        }

        [Test]
        public void MultiDashExecution_EmitsSevenImpactsAndExactDamageThenEnds()
        {
            var heroObject = new GameObject("Rowan Ultimate Test Hero");
            var targetObject = new GameObject("Rowan Ultimate Test Target");
            var target = targetObject.AddComponent<RowanUltimateTargetProbe>();
            var ability = ScriptableObject.CreateInstance<HeroAbilityDefinitionSO>();
            var execution =
                ScriptableObject.CreateInstance<MultiDashHeroAbilityExecutionSO>();
            try
            {
                ability.abilityId = "rowan_ultimate_sequence_test";
                ability.damage = 703;
                ability.castRange = 6f;
                execution.strikeCount = 7;
                execution.targetOvershootDistance = 0f;
                execution.impactHoldSeconds = 0f;

                var impacts = 0;
                var totalDamage = 0;
                var completedHits = -1;
                var sawEnd = false;
                var context = new HeroAbilityExecutionContext
                {
                    HeroTransform = heroObject.transform,
                    Ability = ability,
                    CastOrigin = Vector3.zero,
                    PreferredTarget = target,
                    ResolveTargets = () => new List<CharacterBase> { target },
                    IsValidTarget = candidate => candidate == target,
                    CanContinue = () => true,
                    ResolveGroundedDestination = point => point,
                    Face = _ => { },
                    ApplyDamage = (_, amount) => totalDamage += amount,
                    Present = (stage, _, _, _) =>
                    {
                        if (stage == HeroAbilityVfxStage.Impact) impacts++;
                        if (stage == HeroAbilityVfxStage.End) sawEnd = true;
                    },
                    Completed = hits => completedHits = hits
                };

                var run = typeof(MultiDashHeroAbilityExecutionSO).GetMethod(
                    "Run", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(run, Is.Not.Null);
                Drain((IEnumerator)run.Invoke(execution, new object[] { context }));

                Assert.That(impacts, Is.EqualTo(7));
                Assert.That(completedHits, Is.EqualTo(7));
                Assert.That(totalDamage, Is.EqualTo(703));
                Assert.That(sawEnd, Is.True);
                Assert.That(heroObject.transform.position, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(execution);
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(heroObject);
            }
        }

        [Test]
        public void VfxQualityController_ActivatesOnlyRequestedTier()
        {
            var root = new GameObject("VFX Quality Test");
            var low = new GameObject("Low");
            var medium = new GameObject("Medium");
            var high = new GameObject("High");
            low.transform.SetParent(root.transform);
            medium.transform.SetParent(root.transform);
            high.transform.SetParent(root.transform);
            try
            {
                var controller = root.AddComponent<VfxQualityTierController>();
                controller.Configure(low, medium, high);
                controller.Apply(VfxQualityTier.Low);
                Assert.That(low.activeSelf, Is.True);
                Assert.That(medium.activeSelf, Is.False);
                Assert.That(high.activeSelf, Is.False);
                controller.Apply(VfxQualityTier.High);
                Assert.That(low.activeSelf, Is.False);
                Assert.That(medium.activeSelf, Is.False);
                Assert.That(high.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                VfxQualityTierController.OverrideTier = null;
            }
        }

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
                if (routine.Current is IEnumerator nested)
                    Drain(nested);
        }
    }

    public sealed class RowanUltimateTargetProbe : CharacterBase
    {
    }
}
#endif
