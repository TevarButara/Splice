using System.Collections.Generic;
using UnityEngine;

namespace Splice.FxStudio
{
    [CreateAssetMenu(fileName = "FxPreset",
        menuName = "Splice/FX Studio/Preset")]
    public sealed class SpliceFxPresetDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string presetId = "new_preset";
        public string displayName = "New Preset";
        public SpliceFxPresetFamily family;
        public SpliceFxElementStyle defaultElement;
        public Sprite icon;
        [TextArea] public string description;

        [Header("Reusable Template")]
        [Tooltip("A tested prefab containing VisualEffect, ParticleSystem, TrailRenderer or MeshRenderer components. The Studio never edits its graph.")]
        public GameObject templatePrefab;
        public SpliceFxMotionModifier supportedModifiers =
            SpliceFxMotionModifier.Spin |
            SpliceFxMotionModifier.Pulse |
            SpliceFxMotionModifier.Expand |
            SpliceFxMotionModifier.Contract;

        [Header("Stable Exposed Contract")]
        public string mainTextureProperty = "MainTexture";
        public string mainColorProperty = "MainColor";
        public string emissionProperty = "Emission";
        public string lifetimeProperty = "Lifetime";
        public string spawnRateProperty = "SpawnRate";
        public string speedProperty = "Speed";
        public string sizeProperty = "Size";
        public string radiusProperty = "Radius";
        public string rotationSpeedProperty = "RotationSpeed";
        public string noiseProperty = "NoiseStrength";
        public List<SpliceFxPropertySchema> customProperties = new();

        [Header("Mobile Budget")]
        public SpliceFxMobileBudget budget = new();
        [Min(1)] public int schemaVersion = 1;

        public bool HasTemplate => templatePrefab != null;

        private void OnValidate()
        {
            presetId = SanitizeId(presetId);
            schemaVersion = Mathf.Max(1, schemaVersion);
        }

        public static string SanitizeId(string value)
        {
            value = string.IsNullOrWhiteSpace(value)
                ? "unnamed"
                : value.Trim().ToLowerInvariant();
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            return new string(chars);
        }
    }

    [CreateAssetMenu(fileName = "SpliceFxPresetRegistry",
        menuName = "Splice/FX Studio/Preset Registry")]
    public sealed class SpliceFxPresetRegistry : ScriptableObject
    {
        public List<SpliceFxPresetDefinition> presets = new();

        public SpliceFxPresetDefinition Find(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId)) return null;
            foreach (var preset in presets)
                if (preset != null && preset.presetId == presetId)
                    return preset;
            return null;
        }
    }
}
