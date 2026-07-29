using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.FxStudio.Editor
{
    public enum SpliceFxValidationSeverity
    {
        Warning,
        Error
    }

    public sealed class SpliceFxValidationIssue
    {
        public SpliceFxValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }

        public SpliceFxValidationIssue(SpliceFxValidationSeverity severity,
            string code, string message, UnityEngine.Object context)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Context = context;
        }

        public override string ToString() =>
            $"[{Severity}] {Code}: {Message}";
    }

    public sealed class SpliceFxValidationResult
    {
        private readonly List<SpliceFxValidationIssue> issues = new();
        public IReadOnlyList<SpliceFxValidationIssue> Issues => issues;
        public int ErrorCount =>
            issues.Count(issue =>
                issue.Severity == SpliceFxValidationSeverity.Error);
        public int WarningCount =>
            issues.Count(issue =>
                issue.Severity == SpliceFxValidationSeverity.Warning);
        public bool IsValid => ErrorCount == 0;

        public void Error(string code, string message,
            UnityEngine.Object context = null) =>
            issues.Add(new SpliceFxValidationIssue(
                SpliceFxValidationSeverity.Error, code, message, context));

        public void Warning(string code, string message,
            UnityEngine.Object context = null) =>
            issues.Add(new SpliceFxValidationIssue(
                SpliceFxValidationSeverity.Warning, code, message, context));

        public string Summary() =>
            $"Splice FX Studio: {(IsValid ? "PASS" : "FAIL")} | " +
            $"Errors {ErrorCount}, Warnings {WarningCount}";
    }

    public static class SpliceFxValidator
    {
        [MenuItem("Splice/FX Studio/Validate Project", priority = 1720)]
        public static void ValidateFromMenu()
        {
            var result = ValidateProject();
            Log(result);
            EditorUtility.DisplayDialog("Splice FX Studio",
                result.Summary(), "OK");
        }

        public static SpliceFxValidationResult ValidateProject()
        {
            var result = new SpliceFxValidationResult();
            var presets = LoadAll<SpliceFxPresetDefinition>();
            var subEffects = LoadAll<SpliceFxSubEffectDefinition>();
            var sequences = LoadAll<SpliceFxBlendSequence>();
            var packages = LoadAll<SpliceFxSkillPackage>();
            ValidateUniqueIds(presets, item => item.presetId,
                "FX_PRESET_ID_DUPLICATE", result);
            ValidateUniqueIds(subEffects, item => item.subFxId,
                "FX_SUB_ID_DUPLICATE", result);
            ValidateUniqueIds(sequences, item => item.sequenceId,
                "FX_SEQUENCE_ID_DUPLICATE", result);
            ValidateUniqueIds(packages, item => item.packageId,
                "FX_PACKAGE_ID_DUPLICATE", result);
            foreach (var preset in presets) ValidatePreset(preset, result);
            foreach (var subFx in subEffects) ValidateSubFx(subFx, result);
            foreach (var sequence in sequences)
                ValidateSequence(sequence, result);
            foreach (var package in packages)
                ValidatePackage(package, result);
            return result;
        }

        public static void ValidatePreset(SpliceFxPresetDefinition preset,
            SpliceFxValidationResult result)
        {
            if (preset == null) return;
            if (string.IsNullOrWhiteSpace(preset.presetId))
                result.Error("FX_PRESET_ID_MISSING",
                    "Preset id is required.", preset);
            if (preset.templatePrefab == null)
            {
                result.Warning("FX_PRESET_TEMPLATE_MISSING",
                    $"Preset '{preset.presetId}' has no reusable template.",
                    preset);
                return;
            }
            ValidateVisualPrefab(preset.templatePrefab, preset.budget,
                preset, result);
        }

        public static void ValidateSubFx(
            SpliceFxSubEffectDefinition subFx,
            SpliceFxValidationResult result)
        {
            if (subFx == null) return;
            if (string.IsNullOrWhiteSpace(subFx.subFxId))
                result.Error("FX_SUB_ID_MISSING",
                    "SubFX id is required.", subFx);
            if (subFx.preset == null)
                result.Error("FX_SUB_PRESET_MISSING",
                    $"SubFX '{subFx.subFxId}' has no preset.", subFx);
            if (subFx.EffectiveTemplate == null)
                result.Error("FX_SUB_TEMPLATE_MISSING",
                    $"SubFX '{subFx.subFxId}' has no effective template.",
                    subFx);
            if (subFx.sourceTexture != null &&
                subFx.processedTexture == null &&
                subFx.alpha.mode != SpliceFxAlphaMode.SourceAlpha)
                result.Warning("FX_SUB_ALPHA_NOT_GENERATED",
                    $"SubFX '{subFx.subFxId}' uses alpha processing but has no generated texture.",
                    subFx);
            if (subFx.quality == SpliceFxQualityMask.None)
                result.Error("FX_SUB_QUALITY_EMPTY",
                    $"SubFX '{subFx.subFxId}' is disabled for every quality tier.",
                    subFx);
            if (subFx.preset != null &&
                subFx.lifetime > subFx.preset.budget.maxLifetimeSeconds)
                result.Error("FX_SUB_LIFETIME_BUDGET",
                    $"SubFX '{subFx.subFxId}' lifetime {subFx.lifetime:0.##}s exceeds preset budget {subFx.preset.budget.maxLifetimeSeconds:0.##}s.",
                    subFx);
            if (subFx.preset != null && subFx.EffectiveTexture != null)
            {
                var texture = subFx.EffectiveTexture;
                var maximum = Mathf.Max(texture.width, texture.height);
                if (maximum > subFx.preset.budget.maxTextureSize)
                    result.Error("FX_TEXTURE_SIZE_BUDGET",
                        $"SubFX '{subFx.subFxId}' texture is {texture.width}x{texture.height}; preset maximum is {subFx.preset.budget.maxTextureSize}.",
                        subFx);
                var estimatedKb = EstimateAstc6x6Kb(
                    texture.width, texture.height);
                if (subFx.preset.budget.maxEstimatedTextureMemoryKb > 0 &&
                    estimatedKb >
                    subFx.preset.budget.maxEstimatedTextureMemoryKb)
                    result.Error("FX_TEXTURE_MEMORY_BUDGET",
                        $"SubFX '{subFx.subFxId}' estimated ASTC 6x6 memory is {estimatedKb} KB; budget is {subFx.preset.budget.maxEstimatedTextureMemoryKb} KB.",
                        subFx);
            }
            var customNames = new HashSet<string>();
            foreach (var value in subFx.customValues)
            {
                if (value == null ||
                    string.IsNullOrWhiteSpace(value.propertyName))
                {
                    result.Error("FX_CUSTOM_PROPERTY_NAME_MISSING",
                        $"SubFX '{subFx.subFxId}' contains an unnamed custom property.",
                        subFx);
                    continue;
                }
                if (!customNames.Add(value.propertyName))
                    result.Error("FX_CUSTOM_PROPERTY_DUPLICATE",
                        $"SubFX '{subFx.subFxId}' contains duplicate custom property '{value.propertyName}'.",
                        subFx);
            }
        }

        public static void ValidateSequence(
            SpliceFxBlendSequence sequence,
            SpliceFxValidationResult result)
        {
            if (sequence == null) return;
            if (sequence.clips.Count == 0)
                result.Warning("FX_SEQUENCE_EMPTY",
                    $"Sequence '{sequence.sequenceId}' has no clips.",
                    sequence);
            for (var i = 0; i < sequence.clips.Count; i++)
            {
                var clip = sequence.clips[i];
                if (clip == null || clip.subFx == null)
                    result.Error("FX_SEQUENCE_CLIP_MISSING_SUB",
                        $"Sequence '{sequence.sequenceId}' clip {i + 1} has no SubFX.",
                        sequence);
                else if (clip.quality == SpliceFxQualityMask.None)
                    result.Error("FX_SEQUENCE_CLIP_QUALITY_EMPTY",
                        $"Sequence '{sequence.sequenceId}' clip {i + 1} is disabled for all tiers.",
                        sequence);
            }
        }

        public static void ValidatePackage(
            SpliceFxSkillPackage package,
            SpliceFxValidationResult result)
        {
            if (package == null) return;
            var keys = new HashSet<string>();
            foreach (var binding in package.stages)
            {
                if (binding == null) continue;
                var key = binding.stage == SpliceFxStage.Custom
                    ? $"{binding.stage}:{binding.customMarker}"
                    : binding.stage.ToString();
                if (!keys.Add(key))
                    result.Error("FX_PACKAGE_STAGE_DUPLICATE",
                        $"Package '{package.packageId}' contains duplicate stage '{key}'.",
                        package);
                if (binding.sequence == null)
                    result.Error("FX_PACKAGE_SEQUENCE_MISSING",
                        $"Package '{package.packageId}' stage '{key}' has no sequence.",
                        package);
                else if (binding.exportedPrefab == null)
                    result.Warning("FX_PACKAGE_NOT_EXPORTED",
                        $"Package '{package.packageId}' stage '{key}' needs export.",
                        package);
            }
        }

        public static void Log(SpliceFxValidationResult result)
        {
            foreach (var issue in result.Issues)
            {
                if (issue.Severity == SpliceFxValidationSeverity.Error)
                    Debug.LogError($"[Splice FX] {issue.Code}: {issue.Message}",
                        issue.Context);
                else
                    Debug.LogWarning(
                        $"[Splice FX] {issue.Code}: {issue.Message}",
                        issue.Context);
            }
            if (result.IsValid) Debug.Log(result.Summary());
            else Debug.LogError(result.Summary());
        }

        private static void ValidateVisualPrefab(GameObject prefab,
            SpliceFxMobileBudget budget,
            UnityEngine.Object context,
            SpliceFxValidationResult result)
        {
            var visualCount =
                prefab.GetComponentsInChildren<VisualEffect>(true).Length;
            var particles =
                prefab.GetComponentsInChildren<ParticleSystem>(true);
            var renderers =
                prefab.GetComponentsInChildren<Renderer>(true);
            if (visualCount == 0 && particles.Length == 0 &&
                renderers.Length == 0)
                result.Error("FX_PRESET_TEMPLATE_EMPTY",
                    $"Template '{prefab.name}' has no supported visual component.",
                    context);
            if (visualCount > budget.maxVisualEffectComponents)
                result.Error("FX_VFX_COMPONENT_BUDGET",
                    $"Template '{prefab.name}' uses {visualCount} VisualEffect components; budget is {budget.maxVisualEffectComponents}.",
                    context);
            if (renderers.Length > budget.maxRenderers)
                result.Error("FX_RENDERER_BUDGET",
                    $"Template '{prefab.name}' uses {renderers.Length} renderers; budget is {budget.maxRenderers}.",
                    context);
            var maxParticles = 0;
            foreach (var particle in particles)
                maxParticles += particle.main.maxParticles;
            if (maxParticles > budget.maxParticles)
                result.Error("FX_PARTICLE_BUDGET",
                    $"Template '{prefab.name}' particle capacity is {maxParticles}; budget is {budget.maxParticles}.",
                    context);
            foreach (var visual in
                     prefab.GetComponentsInChildren<VisualEffect>(true))
                if (visual.visualEffectAsset == null)
                    result.Error("FX_GRAPH_UNASSIGNED",
                        $"Template '{prefab.name}' has an unassigned VisualEffect component.",
                        context);
            foreach (var transform in
                     prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject) > 0)
                    result.Error("FX_TEMPLATE_MISSING_SCRIPT",
                        $"Template '{prefab.name}' contains a missing script.",
                        context);
        }

        private static void ValidateUniqueIds<T>(IEnumerable<T> assets,
            Func<T, string> selector, string code,
            SpliceFxValidationResult result) where T : UnityEngine.Object
        {
            foreach (var group in assets
                         .Where(asset => asset != null)
                         .GroupBy(selector)
                         .Where(group => !string.IsNullOrWhiteSpace(group.Key) &&
                                         group.Count() > 1))
                foreach (var asset in group)
                    result.Error(code,
                        $"Duplicate id '{group.Key}'.", asset);
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var result = new List<T>();
            foreach (var guid in
                     AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        private static int EstimateAstc6x6Kb(int width, int height)
        {
            var blocksX = Mathf.Max(1, Mathf.CeilToInt(width / 6f));
            var blocksY = Mathf.Max(1, Mathf.CeilToInt(height / 6f));
            return Mathf.CeilToInt(blocksX * blocksY * 16f / 1024f);
        }
    }
}
