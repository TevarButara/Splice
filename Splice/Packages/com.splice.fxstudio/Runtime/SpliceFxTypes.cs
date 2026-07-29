using System;
using UnityEngine;

namespace Splice.FxStudio
{
    public enum SpliceFxPresetFamily
    {
        Ground,
        Burst,
        Trail,
        Projectile,
        Beam,
        Orbit,
        Aura,
        Shield,
        Summon,
        Environment
    }

    public enum SpliceFxElementStyle
    {
        Neutral,
        Fire,
        Water,
        Lightning,
        Nature,
        Holy,
        Dark,
        Arcane,
        Physical
    }

    [Flags]
    public enum SpliceFxMotionModifier
    {
        None = 0,
        Spin = 1 << 0,
        Pulse = 1 << 1,
        Expand = 1 << 2,
        Contract = 1 << 3,
        Follow = 1 << 4,
        Noise = 1 << 5,
        Home = 1 << 6
    }

    public enum SpliceFxAlphaMode
    {
        SourceAlpha,
        LuminanceToAlpha,
        ChromaKey,
        RedChannel,
        GreenChannel,
        BlueChannel,
        AlphaChannel
    }

    public enum SpliceFxPropertyType
    {
        Float,
        Int,
        Bool,
        Vector2,
        Vector3,
        Vector4,
        Color,
        Texture,
        Gradient,
        Curve
    }

    public enum SpliceFxStage
    {
        Cast,
        Launch,
        Travel,
        Impact,
        Persistent,
        End,
        Custom
    }

    public enum SpliceFxPlacement
    {
        WorldPoint,
        GroundSurface,
        HeroRoot,
        HeroEffectAnchor
    }

    public enum SpliceFxScaleMode
    {
        AuthoredWorld,
        HeroRelative,
        AbilityCastRange,
        AbilityEffectRadius
    }

    [Flags]
    public enum SpliceFxQualityMask
    {
        None = 0,
        Low = 1 << 0,
        Medium = 1 << 1,
        High = 1 << 2,
        All = Low | Medium | High
    }

    public enum SpliceFxQualityTier
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public sealed class SpliceFxAlphaSettings
    {
        public SpliceFxAlphaMode mode = SpliceFxAlphaMode.SourceAlpha;
        public Color chromaKey = Color.black;
        [Range(0f, 1f)] public float tolerance = 0.08f;
        [Range(0.001f, 1f)] public float softness = 0.18f;
        [Range(0f, 1f)] public float despill = 0.75f;
        [Range(0f, 1f)] public float threshold;
        [Range(0.001f, 1f)] public float feather = 0.1f;
        public bool invert;
        public bool multiplySourceAlpha = true;
        [Range(32, 2048)] public int maximumSize = 1024;
    }

    [Serializable]
    public sealed class SpliceFxPropertySchema
    {
        public string propertyName;
        public string displayName;
        public SpliceFxPropertyType propertyType = SpliceFxPropertyType.Float;
        public bool required;
        public float minimum;
        public float maximum = 1f;
        [TextArea] public string tooltip;
    }

    [Serializable]
    public sealed class SpliceFxPropertyValue
    {
        public string propertyName;
        public SpliceFxPropertyType propertyType = SpliceFxPropertyType.Float;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public Vector4 vectorValue;
        public Color colorValue = Color.white;
        public Texture textureValue;
        public Gradient gradientValue = new();
        public AnimationCurve curveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    [Serializable]
    public sealed class SpliceFxMobileBudget
    {
        [Min(1)] public int maxParticles = 256;
        [Min(1)] public int maxRenderers = 8;
        [Min(1)] public int maxVisualEffectComponents = 4;
        [Min(0.05f)] public float maxLifetimeSeconds = 8f;
        [Min(64)] public int maxTextureSize = 1024;
        [Min(0)] public int maxEstimatedTextureMemoryKb = 4096;
    }

    public static class SpliceFxQuality
    {
        public static SpliceFxQualityTier? OverrideTier { get; set; }

        public static SpliceFxQualityTier Current
        {
            get
            {
                if (OverrideTier.HasValue) return OverrideTier.Value;
                var count = Mathf.Max(1, QualitySettings.names.Length);
                if (count == 1) return SpliceFxQualityTier.Medium;
                var normalized = QualitySettings.GetQualityLevel() /
                                 (float)(count - 1);
                if (normalized < 0.34f) return SpliceFxQualityTier.Low;
                return normalized < 0.67f
                    ? SpliceFxQualityTier.Medium
                    : SpliceFxQualityTier.High;
            }
        }

        public static SpliceFxQualityMask MaskFor(SpliceFxQualityTier tier) =>
            tier switch
            {
                SpliceFxQualityTier.Low => SpliceFxQualityMask.Low,
                SpliceFxQualityTier.Medium => SpliceFxQualityMask.Medium,
                _ => SpliceFxQualityMask.High
            };
    }
}
