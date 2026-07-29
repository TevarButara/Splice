#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
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
            AssertCue(hero.skill3, "Skill3", hero.skill3.castVfx,
                hero.skill3.persistentVfx, hero.skill3.endVfx);
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
            Assert.That(guids, Has.Length.EqualTo(16));
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
