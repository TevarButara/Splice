#if UNITY_EDITOR
using System.Collections.Generic;
using Splice.Data;
using Splice.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.Editor.Validation
{
    public static class RowanVfxVerticalSliceValidator
    {
        private const string Root = "Assets/Prefabs/Natural/Heroes/1-Rowan";
        private const int MaxParticlesPerPrefab = 256;

        public static void Validate(ContentValidationReport report)
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(
                Root + "/Rowan_Definition.asset");
            if (hero == null)
            {
                report.Error("ROWAN_VFX_HERO_MISSING", "Rowan definition is missing.");
                return;
            }
            if (hero.normalAttackEffectPrefab == null)
                report.Error("ROWAN_VFX_NORMAL_MISSING",
                    "Rowan normal attack has no pooled VFX prefab.", hero);
            else ValidatePrefab(hero.normalAttackEffectPrefab, report);

            ValidateAbility(hero.blinkAbility, "Blink", report,
                "castVfx", "travelVfx", "impactVfx");
            ValidateAbility(hero.healAbility, "Heal", report,
                "castVfx", "persistentVfx", "endVfx");
            ValidateAbility(hero.skill1, "Skill 1", report,
                "launchVfx", "travelVfx", "impactVfx");
            ValidateAbility(hero.skill2, "Skill 2", report,
                "castVfx", "persistentVfx", "endVfx");
            ValidateAbility(hero.skill3, "Skill 3", report,
                "castVfx", "persistentVfx", "endVfx");

            foreach (var graphName in new[]
                     { "Rowan_GPU_Burst", "Rowan_GPU_Loop", "Rowan_GPU_Trail" })
            {
                var path = Root + "/VFX/Graphs/" + graphName + ".vfx";
                if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path) == null)
                    report.Error("ROWAN_VFX_GRAPH_MISSING",
                        "Missing Visual Effect Graph: " + path);
            }
        }

        private static void ValidateAbility(HeroAbilityDefinitionSO ability, string label,
            ContentValidationReport report, params string[] requiredCueFields)
        {
            if (ability == null)
            {
                report.Error("ROWAN_VFX_ABILITY_MISSING",
                    "Rowan " + label + " definition is missing.");
                return;
            }
            if (!ability.HasStagedVfx)
                report.Error("ROWAN_VFX_STAGES_MISSING",
                    "Rowan " + label + " has no staged VFX.", ability);
            var cues = new Dictionary<string, HeroAbilityVfxCue>
            {
                { "castVfx", ability.castVfx },
                { "launchVfx", ability.launchVfx },
                { "travelVfx", ability.travelVfx },
                { "impactVfx", ability.impactVfx },
                { "persistentVfx", ability.persistentVfx },
                { "endVfx", ability.endVfx }
            };
            foreach (var field in requiredCueFields)
            {
                if (!cues.TryGetValue(field, out var cue) || cue?.IsConfigured != true)
                {
                    report.Error("ROWAN_VFX_REQUIRED_STAGE_MISSING",
                        "Rowan " + label + " is missing required " + field + ".", ability);
                    continue;
                }
                if (field == "travelVfx" && cue.travelDurationSeconds <= 0f)
                    report.Error("ROWAN_VFX_TRAVEL_DURATION_INVALID",
                        "Rowan " + label + " travel duration must be positive.", ability);
                ValidatePrefab(cue.prefab, report);
            }
        }

        private static void ValidatePrefab(GameObject prefab,
            ContentValidationReport report)
        {
            if (prefab == null) return;
            var hasVisual = prefab.GetComponentInChildren<VisualEffect>(true) != null ||
                            prefab.GetComponentInChildren<ParticleSystem>(true) != null ||
                            prefab.GetComponentInChildren<TrailRenderer>(true) != null;
            if (!hasVisual)
                report.Error("ROWAN_VFX_PREFAB_EMPTY",
                    "VFX prefab has no supported visual component: " + prefab.name, prefab);
            var particleBudget = 0;
            foreach (var particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
                particleBudget += particle.main.maxParticles;
            if (particleBudget > MaxParticlesPerPrefab)
                report.Error("ROWAN_VFX_PARTICLE_BUDGET_EXCEEDED",
                    prefab.name + " particle budget is " + particleBudget +
                    " (max " + MaxParticlesPerPrefab + ").", prefab);
            foreach (var tf in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tf.gameObject) > 0)
                    report.Error("ROWAN_VFX_MISSING_SCRIPT",
                        prefab.name + " contains a missing script.", prefab);
            foreach (var visual in prefab.GetComponentsInChildren<VisualEffect>(true))
                if (visual.visualEffectAsset == null)
                    report.Error("ROWAN_VFX_GRAPH_NOT_ASSIGNED",
                        prefab.name + " contains an unassigned VisualEffect component.", prefab);
        }
    }
}
#endif
