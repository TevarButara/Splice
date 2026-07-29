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

    public enum SpliceFxMotionType
    {
        Spin,
        Pulse,
        Expand,
        Contract,
        Float,
        Orbit,
        Flicker,
        FadeIn,
        FadeOut,
        UvScroll,
        Shake
    }

    [Serializable]
    public sealed class SpliceFxMotionLayer
    {
        public string label = "Motion";
        public bool enabled = true;
        public SpliceFxMotionType type;
        [Tooltip("Spin uses degrees/second. Oscillating motions use cycles/second.")]
        public float speed = 1f;
        [Min(0f)] public float amount = 0.2f;
        [Min(0f)] public float delaySeconds;
        [Min(0.01f)] public float durationSeconds = 1f;
        [Range(0f, 1f)] public float phase;
        public bool loop = true;
        public Vector3 axis = Vector3.up;
        public Vector2 uvSpeed = new(0.2f, 0f);
        public AnimationCurve curve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public static SpliceFxMotionLayer Create(
            SpliceFxMotionType motionType)
        {
            var result = new SpliceFxMotionLayer
            {
                type = motionType,
                label = motionType.ToString()
            };
            switch (motionType)
            {
                case SpliceFxMotionType.Spin:
                    result.speed = 90f;
                    result.amount = 1f;
                    break;
                case SpliceFxMotionType.Pulse:
                    result.speed = 1.5f;
                    result.amount = 0.18f;
                    break;
                case SpliceFxMotionType.Expand:
                case SpliceFxMotionType.Contract:
                    result.speed = 1f;
                    result.amount = 0.8f;
                    result.loop = false;
                    break;
                case SpliceFxMotionType.Float:
                    result.speed = 1f;
                    result.amount = 0.35f;
                    break;
                case SpliceFxMotionType.Orbit:
                    result.speed = 0.75f;
                    result.amount = 1f;
                    break;
                case SpliceFxMotionType.Flicker:
                    result.speed = 8f;
                    result.amount = 0.35f;
                    break;
                case SpliceFxMotionType.FadeIn:
                case SpliceFxMotionType.FadeOut:
                    result.speed = 1f;
                    result.amount = 1f;
                    result.loop = false;
                    break;
                case SpliceFxMotionType.UvScroll:
                    result.speed = 1f;
                    result.amount = 1f;
                    result.uvSpeed = new Vector2(0.25f, 0f);
                    break;
                case SpliceFxMotionType.Shake:
                    result.speed = 14f;
                    result.amount = 0.08f;
                    break;
            }
            return result;
        }
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
