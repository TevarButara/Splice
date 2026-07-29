using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Splice.FxStudio.Editor
{
    public static class SpliceFxExporter
    {
        public const string DefaultOutputRoot =
            "Assets/SpliceFXStudio/Generated/Prefabs";

        public static GameObject ExportSubFx(
            SpliceFxSubEffectDefinition subFx,
            string outputRoot = DefaultOutputRoot)
        {
            if (subFx == null)
                throw new ArgumentNullException(nameof(subFx));
            if (subFx.EffectiveTemplate == null)
                throw new InvalidOperationException(
                    $"SubFX '{subFx.name}' has no preset template.");
            ValidateGeneratedRoot(outputRoot);
            SpliceFxAlphaProcessor.EnsureAssetFolder(outputRoot);

            var root = PrefabUtility.InstantiatePrefab(
                subFx.EffectiveTemplate) as GameObject;
            if (root == null)
                root = UnityEngine.Object.Instantiate(
                    subFx.EffectiveTemplate);
            root.name = $"SubFX_{Safe(subFx.subFxId)}";
            root.SetActive(true);
            try
            {
                var driver = root.GetComponent<SpliceFxPropertyDriver>() ??
                             root.AddComponent<SpliceFxPropertyDriver>();
                driver.Configure(subFx);
                var metadata =
                    root.GetComponent<SpliceFxGeneratedMetadata>() ??
                    root.AddComponent<SpliceFxGeneratedMetadata>();
                metadata.ConfigureEditor(subFx.subFxId, subFx.schemaVersion,
                    DateTime.UtcNow.ToString("O"));
                var path = $"{outputRoot}/{root.name}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static GameObject ExportBlend(
            SpliceFxBlendSequence sequence,
            string outputRoot = DefaultOutputRoot)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));
            ValidateGeneratedRoot(outputRoot);
            var layerRoot = outputRoot + "/Layers";
            var sequenceRoot = outputRoot + "/Sequences";
            SpliceFxAlphaProcessor.EnsureAssetFolder(layerRoot);
            SpliceFxAlphaProcessor.EnsureAssetFolder(sequenceRoot);

            var root = new GameObject(
                $"FX_{Safe(sequence.sequenceId)}");
            try
            {
                var runtime = root.AddComponent<SpliceFxSequenceRuntime>();
                var metadata = root.AddComponent<SpliceFxGeneratedMetadata>();
                metadata.ConfigureEditor(sequence.sequenceId,
                    sequence.schemaVersion, DateTime.UtcNow.ToString("O"));
                var layers = new List<SpliceFxRuntimeLayer>();
                for (var i = 0; i < sequence.clips.Count; i++)
                {
                    var clip = sequence.clips[i];
                    if (clip?.subFx == null) continue;
                    var layerPrefab = ExportSubFx(clip.subFx, layerRoot);
                    var visual = PrefabUtility.InstantiatePrefab(
                        layerPrefab, root.transform) as GameObject;
                    if (visual == null)
                    {
                        visual = UnityEngine.Object.Instantiate(
                            layerPrefab, root.transform);
                        visual.name = layerPrefab.name;
                    }
                    visual.transform.localPosition = clip.localPosition;
                    visual.transform.localRotation =
                        Quaternion.Euler(clip.localEulerAngles);
                    visual.transform.localScale =
                        SanitizeScale(clip.localScale);
                    visual.SetActive(false);
                    layers.Add(new SpliceFxRuntimeLayer
                    {
                        label = string.IsNullOrWhiteSpace(clip.label)
                            ? $"Layer {i + 1}"
                            : clip.label,
                        visual = visual,
                        startSeconds = Mathf.Max(0f, clip.startSeconds),
                        durationSeconds =
                            Mathf.Max(0.01f, clip.durationSeconds),
                        quality = clip.quality == SpliceFxQualityMask.None
                            ? SpliceFxQualityMask.All
                            : clip.quality,
                        loop = clip.loop
                    });
                }
                runtime.ConfigureEditor(layers, sequence.DurationSeconds);
                var path =
                    $"{sequenceRoot}/{root.name}.prefab";
                var exported =
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                SpliceFxAddressables.Register(
                    exported,
                    $"splice-fx/sequence/{Safe(sequence.sequenceId)}");
                return exported;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void ExportSkillPackage(
            SpliceFxSkillPackage package,
            string outputRoot = DefaultOutputRoot)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));
            Undo.RecordObject(package, "Export Skill FX Package");
            foreach (var binding in package.stages)
            {
                if (binding?.sequence == null) continue;
                binding.exportedPrefab =
                    ExportBlend(binding.sequence, outputRoot);
            }
            EditorUtility.SetDirty(package);
            AssetDatabase.SaveAssets();
            SpliceFxAddressables.Register(
                package,
                $"splice-fx/package/{Safe(package.packageId)}");
        }

        private static void ValidateGeneratedRoot(string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot) ||
                !outputRoot.StartsWith("Assets/", StringComparison.Ordinal) ||
                outputRoot.IndexOf("/Generated",
                    StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException(
                    "FX Studio exports are restricted to an Assets/.../Generated folder.");
        }

        private static Vector3 SanitizeScale(Vector3 scale) =>
            new(
                Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.001f, Mathf.Abs(scale.z)));

        private static string Safe(string value) =>
            SpliceFxPresetDefinition.SanitizeId(value);
    }
}
