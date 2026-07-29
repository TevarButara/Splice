#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Splice.Combat;
using Splice.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.Tests.EditMode
{
    public sealed class RowanVfxVerticalSliceEditModeTests
    {
        private const string Root = "Assets/Prefabs/Natural/Heroes/1-Rowan";

        [Test]
        public void RowanAbilities_HaveRequiredStagedVfx()
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(
                Root + "/Rowan_Definition.asset");
            Assert.That(hero, Is.Not.Null);
            AssertCue(hero.blinkAbility, "Blink", hero.blinkAbility.castVfx,
                hero.blinkAbility.travelVfx, hero.blinkAbility.impactVfx);
            AssertCue(hero.healAbility, "Heal", hero.healAbility.castVfx,
                hero.healAbility.persistentVfx, hero.healAbility.endVfx);
            AssertCue(hero.skill1, "Skill1", hero.skill1.launchVfx,
                hero.skill1.travelVfx, hero.skill1.impactVfx);
            AssertCue(hero.skill2, "Skill2", hero.skill2.castVfx,
                hero.skill2.persistentVfx, hero.skill2.endVfx);
            AssertCue(hero.skill3, "Skill3 Ultimate",
                hero.skill3.castVfx,
                hero.skill3.launchVfx,
                hero.skill3.travelVfx,
                hero.skill3.impactVfx,
                hero.skill3.endVfx);
            Assert.That(
                hero.skill3.execution,
                Is.TypeOf<MultiDashHeroAbilityExecutionSO>());
        }

        [Test]
        public void RowanGeneratedVfx_HaveGraphsAndStayWithinParticleBudget()
        {
            foreach (var graphName in new[]
                     { "Rowan_GPU_Burst", "Rowan_GPU_Loop", "Rowan_GPU_Trail" })
                Assert.That(AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                    Root + "/VFX/Graphs/" + graphName + ".vfx"), Is.Not.Null);

            var guids = AssetDatabase.FindAssets("t:Prefab",
                new[] { Root + "/VFX/Prefabs" });
            Assert.That(guids, Has.Length.GreaterThanOrEqualTo(16));
            var seen = new HashSet<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(seen.Add(prefab.name), Is.True, "Duplicate VFX prefab name.");
                Assert.That(prefab.GetComponentInChildren<VisualEffect>(true), Is.Not.Null,
                    path + " must include a Visual Effect Graph accent.");
                var particleBudget = 0;
                foreach (var particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
                    particleBudget += particle.main.maxParticles;
                Assert.That(particleBudget, Is.LessThanOrEqualTo(256), path);
                foreach (var tf in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tf.gameObject),
                        Is.Zero, path + " contains a missing script.");
            }
        }

        [Test]
        public void RowanUltimate_HasThreeQualityVariantsAndUserImpactGraph()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                    Root + "/VFX/Graphs/Rowan_Ultimate_v1.vfx"),
                Is.Not.Null);
            foreach (var prefabName in new[]
                     {
                         "Rowan_Ultimate_Cast_Ring",
                         "Rowan_Ultimate_Launch",
                         "Rowan_Ultimate_Travel_Trail",
                         "Rowan_Ultimate_Impact_Cross",
                         "Rowan_Ultimate_End_Return"
                     })
            {
                var path = Root + "/VFX/Prefabs/Ultimate/" +
                           prefabName + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var quality = prefab.GetComponent<VfxQualityTierController>();
                Assert.That(quality, Is.Not.Null, path);
                Assert.That(quality.Low, Is.Not.Null, path);
                Assert.That(quality.Medium, Is.Not.Null, path);
                Assert.That(quality.High, Is.Not.Null, path);
                Assert.That(
                    prefab.GetComponentsInChildren<UltimateVfxMotion>(true),
                    Has.Length.EqualTo(3),
                    path + " must animate every quality variant.");
            }
        }

        [Test]
        public void RowanUltimate_CastRingUsesVisibleColoredLinesWithoutWhiteFlash()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root +
                "/VFX/Prefabs/Ultimate/Rowan_Ultimate_Cast_Ring.prefab");
            Assert.That(prefab, Is.Not.Null);
            var outer = prefab.GetComponentsInChildren<LineRenderer>(true)
                .FirstOrDefault(line => line.name == "Outer Magic Circle");
            Assert.That(outer, Is.Not.Null);
            Assert.That(outer.sharedMaterial, Is.Not.Null);
            Assert.That(outer.sharedMaterial.GetFloat("_UseSoftParticles"),
                Is.Zero,
                "Ground rings must not disappear into the terrain depth.");
            Assert.That(outer.sharedMaterial.mainTexture,
                Is.Null.Or.SameAs(Texture2D.whiteTexture),
                "Line VFX need the shader's continuous white default, not a sparse flare texture.");
            Assert.That(
                prefab.GetComponentsInChildren<LineRenderer>(true)
                    .Any(line => line.name == "Wildblade Pentagram"),
                Is.True);

            var sparks = prefab.GetComponentsInChildren<ParticleSystem>(true)
                .FirstOrDefault(particle => particle.name == "Orbit Sparks");
            Assert.That(sparks, Is.Not.Null);
            var firstColor =
                sparks.colorOverLifetime.color.gradient.colorKeys[0].color;
            Assert.That(firstColor,
                Is.Not.EqualTo(Color.white),
                "The first particle frame must be hot orange, not a white flash.");
        }

        private static void AssertCue(HeroAbilityDefinitionSO ability, string label,
            params HeroAbilityVfxCue[] cues)
        {
            Assert.That(ability, Is.Not.Null, label + " ability is missing.");
            Assert.That(ability.HasStagedVfx, Is.True, label);
            foreach (var cue in cues)
            {
                Assert.That(cue, Is.Not.Null, label);
                Assert.That(cue.IsConfigured, Is.True, label);
                Assert.That(cue.lifetimeSeconds >= 0f, Is.True, label);
                if (cue == ability.travelVfx)
                    Assert.That(cue.travelDurationSeconds, Is.GreaterThan(0f), label);
            }
        }
    }
}
#endif
