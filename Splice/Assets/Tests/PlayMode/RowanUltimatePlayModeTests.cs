using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class RowanUltimatePlayModeTests
    {
        [UnityTest]
        public IEnumerator MultiDash_RuntimeTypeSplitsEveryDamagePointExactly()
        {
            var executionType = Type.GetType(
                "Splice.Combat.MultiDashHeroAbilityExecutionSO, Assembly-CSharp");
            Assert.That(executionType, Is.Not.Null);
            var splitDamage = executionType.GetMethod(
                "SplitDamage", BindingFlags.Public | BindingFlags.Static);
            Assert.That(splitDamage, Is.Not.Null);

            var execution = ScriptableObject.CreateInstance(executionType);
            try
            {
                var strikeCount = executionType.GetField("strikeCount");
                Assert.That(strikeCount, Is.Not.Null);
                strikeCount.SetValue(execution, 7);

                var total = 0;
                for (var strike = 0; strike < 7; strike++)
                {
                    total += (int)splitDamage.Invoke(
                        null, new object[] { 703, 7, strike });
                    yield return null;
                }

                Assert.That(total, Is.EqualTo(703),
                    "Seven runtime strikes must neither lose nor create damage.");
                Assert.That((int)splitDamage.Invoke(
                    null, new object[] { 703, 7, 0 }), Is.EqualTo(101));
                Assert.That((int)splitDamage.Invoke(
                    null, new object[] { 703, 7, 3 }), Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.Destroy(execution);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UltimateGroundVfx_MultipliesWorldScaleWithHeroSize()
        {
            var profileType = Type.GetType(
                "Splice.Placement.GroundPlacementProfile, Assembly-CSharp");
            var runtimeScaleType = Type.GetType(
                "Splice.Combat.VfxRuntimeScale, Assembly-CSharp");
            var abilityType = Type.GetType(
                "Splice.Data.HeroAbilityDefinitionSO, Assembly-CSharp");
            var cueType = Type.GetType(
                "Splice.Data.HeroAbilityVfxCue, Assembly-CSharp");
            var placementType = Type.GetType(
                "Splice.Data.HeroAbilityEffectPlacement, Assembly-CSharp");
            var stageType = Type.GetType(
                "Splice.Data.HeroAbilityVfxStage, Assembly-CSharp");
            var playerType = Type.GetType(
                "Splice.Combat.HeroAbilityVfxSequencePlayer, Assembly-CSharp");
            var poolType = Type.GetType(
                "Splice.Combat.VfxPoolService, Assembly-CSharp");
            Assert.That(profileType, Is.Not.Null);
            Assert.That(runtimeScaleType, Is.Not.Null);
            Assert.That(abilityType, Is.Not.Null);
            Assert.That(cueType, Is.Not.Null);
            Assert.That(placementType, Is.Not.Null);
            Assert.That(stageType, Is.Not.Null);
            Assert.That(playerType, Is.Not.Null);
            Assert.That(poolType, Is.Not.Null);

            var hero = new GameObject("Scaled Hero");
            var ground = new GameObject("GroundAnchor");
            var cameraFocus = new GameObject("CameraFocus");
            var effectAnchor = new GameObject("EffectAnchor");
            ground.transform.SetParent(hero.transform, false);
            cameraFocus.transform.SetParent(hero.transform, false);
            effectAnchor.transform.SetParent(hero.transform, false);
            var profile = hero.AddComponent(profileType);
            profileType.GetMethod("ConfigureEditorReferences").Invoke(
                profile,
                new object[]
                {
                    hero.transform, ground.transform, cameraFocus.transform,
                    effectAnchor.transform, null
                });
            profileType.GetMethod(
                    "ConfigureEditorScaleReference",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null)
                .Invoke(profile, null);

            var vfxPrefab = new GameObject("Scale Aware Ultimate Cast");
            vfxPrefab.AddComponent(runtimeScaleType);
            var ability = ScriptableObject.CreateInstance(abilityType);
            abilityType.GetField("castRange").SetValue(ability, 5f);
            var cue = Activator.CreateInstance(cueType);
            cueType.GetField("enabled").SetValue(cue, true);
            cueType.GetField("prefab").SetValue(cue, vfxPrefab);
            cueType.GetField("lifetimeSeconds").SetValue(cue, 1f);
            cueType.GetField("placement").SetValue(
                cue, Enum.Parse(placementType, "GroundSurface"));
            abilityType.GetField("castVfx").SetValue(ability, cue);

            try
            {
                hero.transform.localScale = Vector3.one * 2f;
                playerType.GetMethod(
                        "PlayExecutionStage",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[]
                    {
                        ability,
                        Enum.Parse(stageType, "Cast"),
                        hero.transform,
                        effectAnchor.transform,
                        (object)Vector3.zero,
                        Vector3.zero,
                        1f
                    });
                yield return null;

                GameObject pooled = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<GameObject>(
                             FindObjectsSortMode.None))
                    if (candidate.name ==
                        "Scale Aware Ultimate Cast [Pooled]")
                    {
                        pooled = candidate;
                        break;
                    }

                Assert.That(pooled, Is.Not.Null);
                Assert.That(pooled.transform.lossyScale.x,
                    Is.EqualTo(10f).Within(.01f),
                    "5-unit authored radius at 2x Hero size must render at 10x world scale.");
            }
            finally
            {
                poolType.GetMethod(
                        "ReleaseAllForTests",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, null);
                UnityEngine.Object.Destroy(ability);
                UnityEngine.Object.Destroy(vfxPrefab);
                UnityEngine.Object.Destroy(hero);
            }
            yield return null;
        }
    }
}
