using System;
using System.Collections.Generic;
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

    public enum SpliceFxInstanceLayoutMode
    {
        Single,
        Radial,
        Arc,
        Line,
        Grid,
        RandomRing,
        Manual
    }

    public enum SpliceFxInstanceFacing
    {
        KeepAuthored,
        FaceOutward,
        FaceInward,
        TangentClockwise,
        TangentCounterClockwise
    }

    public enum SpliceFxInstanceMotionScope
    {
        WholeFormation,
        EachInstance
    }

    [Serializable]
    public sealed class SpliceFxManualInstance
    {
        public string label = "Instance";
        public bool enabled = true;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [Serializable]
    public sealed class SpliceFxInstanceLayout
    {
        public SpliceFxInstanceLayoutMode mode =
            SpliceFxInstanceLayoutMode.Single;
        [Range(1, 64)] public int highCount = 1;
        [Range(0, 64)] public int mediumCount = 1;
        [Range(0, 64)] public int lowCount = 1;
        public Vector3 centerOffset;
        public Vector3 baseEulerAngles;
        public Vector3 baseScale = Vector3.one;

        [Header("Plane / Facing")]
        public Vector3 planeAxis = Vector3.up;
        public Vector3 startDirection = Vector3.forward;
        public SpliceFxInstanceFacing facing =
            SpliceFxInstanceFacing.KeepAuthored;

        [Header("Radial / Arc / Random Ring")]
        [Min(0f)] public float radius = 1f;
        [Min(0f)] public float innerRadius = 0.5f;
        [Range(1f, 360f)] public float arcDegrees = 360f;
        public float startAngleDegrees;

        [Header("Line / Grid")]
        public Vector3 lineDirection = Vector3.right;
        [Min(0.01f)] public float spacing = 1f;
        [Range(1, 16)] public int gridColumns = 3;
        public Vector2 gridSpacing = Vector2.one;

        [Header("Per Instance Variation")]
        public Vector3 eulerStep;
        public float uniformScaleStep;
        [Min(0f)] public float angleJitter;
        [Min(0f)] public float radiusJitter;
        [Min(0f)] public float rotationJitter;
        [Min(0f)] public float scaleJitter;
        public int randomSeed = 1337;

        [Header("Individual Animation")]
        public SpliceFxInstanceMotionScope motionScope =
            SpliceFxInstanceMotionScope.WholeFormation;
        public Vector3 selfSpinAxis = Vector3.up;
        public float selfSpinDegreesPerSecond;
        public bool alternateSelfSpin;
        [Min(0f)] public float activationDelayStep;
        [Min(0f)] public float activeDuration;
        public bool reverseActivationOrder;

        [Header("Manual Instances")]
        public List<SpliceFxManualInstance> manualInstances = new();

        public int MaximumCount => mode ==
                                   SpliceFxInstanceLayoutMode.Manual
            ? manualInstances?.Count ?? 0
            : Mathf.Clamp(highCount, 1, 64);

        public int CountFor(SpliceFxQualityTier tier)
        {
            var maximum = Mathf.Max(0, MaximumCount);
            return tier switch
            {
                SpliceFxQualityTier.Low =>
                    Mathf.Clamp(lowCount, 0, maximum),
                SpliceFxQualityTier.Medium =>
                    Mathf.Clamp(mediumCount, 0, maximum),
                _ => maximum
            };
        }

        public static SpliceFxInstanceLayout RadialFive() =>
            new()
            {
                mode = SpliceFxInstanceLayoutMode.Radial,
                highCount = 5,
                mediumCount = 4,
                lowCount = 3,
                radius = 1.5f,
                facing = SpliceFxInstanceFacing.FaceOutward,
                eulerStep = Vector3.zero
            };

        public float DelayFor(int index, int total)
        {
            var safeTotal = Mathf.Max(1, total);
            var order = reverseActivationOrder
                ? safeTotal - 1 - Mathf.Clamp(index, 0, safeTotal - 1)
                : Mathf.Clamp(index, 0, safeTotal - 1);
            return Mathf.Max(0f, activationDelayStep) * order;
        }
    }

    public readonly struct SpliceFxInstancePose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly bool Enabled;

        public SpliceFxInstancePose(Vector3 position,
            Quaternion rotation, Vector3 scale, bool enabled = true)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Enabled = enabled;
        }
    }

    public static class SpliceFxInstanceLayoutSolver
    {
        public static SpliceFxInstanceLayout ToManual(
            SpliceFxInstanceLayout source)
        {
            var poses = Build(source);
            var result = new SpliceFxInstanceLayout
            {
                mode = SpliceFxInstanceLayoutMode.Manual,
                highCount = Mathf.Max(1, poses.Count),
                mediumCount = Mathf.Clamp(
                    source?.mediumCount ?? poses.Count, 0, poses.Count),
                lowCount = Mathf.Clamp(
                    source?.lowCount ?? poses.Count, 0, poses.Count),
                planeAxis = source?.planeAxis ?? Vector3.up,
                startDirection = source?.startDirection ?? Vector3.forward,
                selfSpinAxis = source?.selfSpinAxis ?? Vector3.up,
                motionScope = source?.motionScope ??
                              SpliceFxInstanceMotionScope.WholeFormation,
                selfSpinDegreesPerSecond =
                    source?.selfSpinDegreesPerSecond ?? 0f,
                alternateSelfSpin =
                    source?.alternateSelfSpin ?? false,
                activationDelayStep =
                    source?.activationDelayStep ?? 0f,
                activeDuration = source?.activeDuration ?? 0f,
                reverseActivationOrder =
                    source?.reverseActivationOrder ?? false,
                manualInstances = new List<SpliceFxManualInstance>(
                    poses.Count)
            };
            for (var i = 0; i < poses.Count; i++)
            {
                var pose = poses[i];
                result.manualInstances.Add(new SpliceFxManualInstance
                {
                    label = $"Instance {i + 1}",
                    enabled = pose.Enabled,
                    localPosition = pose.Position,
                    localEulerAngles = pose.Rotation.eulerAngles,
                    localScale = pose.Scale
                });
            }
            return result;
        }

        public static List<SpliceFxInstancePose> Build(
            SpliceFxInstanceLayout layout)
        {
            var result = new List<SpliceFxInstancePose>();
            if (layout == null)
            {
                result.Add(new SpliceFxInstancePose(
                    Vector3.zero, Quaternion.identity, Vector3.one));
                return result;
            }
            if (layout.mode == SpliceFxInstanceLayoutMode.Manual)
            {
                if (layout.manualInstances == null) return result;
                foreach (var item in layout.manualInstances)
                {
                    if (item == null) continue;
                    result.Add(new SpliceFxInstancePose(
                        item.localPosition,
                        Quaternion.Euler(item.localEulerAngles),
                        SanitizeScale(item.localScale), item.enabled));
                }
                return result;
            }

            var count = layout.mode == SpliceFxInstanceLayoutMode.Single
                ? 1
                : Mathf.Clamp(layout.highCount, 1, 64);
            var random = new System.Random(layout.randomSeed);
            var axis = SafeDirection(layout.planeAxis, Vector3.up);
            var start = Vector3.ProjectOnPlane(
                layout.startDirection, axis);
            start = SafeDirection(start,
                Vector3.Cross(axis, Vector3.right));
            var side = SafeDirection(Vector3.Cross(axis, start),
                Vector3.right);

            for (var i = 0; i < count; i++)
            {
                var position = layout.centerOffset;
                var radial = start;
                switch (layout.mode)
                {
                    case SpliceFxInstanceLayoutMode.Radial:
                    case SpliceFxInstanceLayoutMode.Arc:
                    {
                        var fullCircle =
                            layout.mode ==
                            SpliceFxInstanceLayoutMode.Radial ||
                            layout.arcDegrees >= 359.99f;
                        var denominator = fullCircle
                            ? count
                            : Mathf.Max(1, count - 1);
                        var angle = layout.startAngleDegrees +
                                    layout.arcDegrees * i / denominator +
                                    Signed(random) *
                                    layout.angleJitter;
                        radial = Quaternion.AngleAxis(angle, axis) * start;
                        var radius = Mathf.Max(0f,
                            layout.radius +
                            Signed(random) * layout.radiusJitter);
                        position += radial * radius;
                        break;
                    }
                    case SpliceFxInstanceLayoutMode.Line:
                    {
                        var direction = SafeDirection(
                            layout.lineDirection, Vector3.right);
                        position += direction *
                                    ((i - (count - 1) * 0.5f) *
                                     Mathf.Max(0.01f, layout.spacing));
                        radial = direction;
                        break;
                    }
                    case SpliceFxInstanceLayoutMode.Grid:
                    {
                        var columns = Mathf.Clamp(
                            layout.gridColumns, 1, count);
                        var rows = Mathf.CeilToInt(count /
                                                   (float)columns);
                        var column = i % columns;
                        var row = i / columns;
                        var x = (column - (columns - 1) * 0.5f) *
                                layout.gridSpacing.x;
                        var y = (row - (rows - 1) * 0.5f) *
                                layout.gridSpacing.y;
                        position += side * x + start * y;
                        radial = SafeDirection(
                            position - layout.centerOffset, start);
                        break;
                    }
                    case SpliceFxInstanceLayoutMode.RandomRing:
                    {
                        var angle = layout.startAngleDegrees +
                                    (float)random.NextDouble() *
                                    layout.arcDegrees;
                        radial = Quaternion.AngleAxis(angle, axis) * start;
                        var inner = Mathf.Min(
                            layout.innerRadius, layout.radius);
                        var outer = Mathf.Max(
                            layout.innerRadius, layout.radius);
                        var radius = Mathf.Lerp(inner, outer,
                            Mathf.Sqrt((float)random.NextDouble()));
                        position += radial * radius;
                        break;
                    }
                }

                var facing = FacingRotation(
                    layout.facing, radial, axis);
                var jitterEuler = Vector3.one *
                                  (Signed(random) *
                                   layout.rotationJitter);
                var authored = Quaternion.Euler(
                    layout.baseEulerAngles +
                    layout.eulerStep * i + jitterEuler);
                var scaleValue = 1f +
                                 layout.uniformScaleStep * i +
                                 Signed(random) * layout.scaleJitter;
                result.Add(new SpliceFxInstancePose(
                    position,
                    facing * authored,
                    SanitizeScale(layout.baseScale *
                                  Mathf.Max(0.001f, scaleValue))));
            }
            return result;
        }

        private static Quaternion FacingRotation(
            SpliceFxInstanceFacing facing, Vector3 radial,
            Vector3 up)
        {
            if (facing == SpliceFxInstanceFacing.KeepAuthored)
                return Quaternion.identity;
            var direction = SafeDirection(radial, Vector3.forward);
            direction = facing switch
            {
                SpliceFxInstanceFacing.FaceInward => -direction,
                SpliceFxInstanceFacing.TangentClockwise =>
                    Quaternion.AngleAxis(90f, up) * direction,
                SpliceFxInstanceFacing.TangentCounterClockwise =>
                    Quaternion.AngleAxis(-90f, up) * direction,
                _ => direction
            };
            return Quaternion.LookRotation(direction, up);
        }

        private static Vector3 SafeDirection(
            Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 0.0001f
                ? value.normalized
                : fallback.normalized;

        private static float Signed(System.Random random) =>
            (float)random.NextDouble() * 2f - 1f;

        private static Vector3 SanitizeScale(Vector3 value) =>
            new(Mathf.Max(0.001f, Mathf.Abs(value.x)),
                Mathf.Max(0.001f, Mathf.Abs(value.y)),
                Mathf.Max(0.001f, Mathf.Abs(value.z)));
    }

    [Serializable]
    public sealed class SpliceFxMotionLayer
    {
        public string label = "Motion";
        public bool enabled = true;
        public SpliceFxMotionType type;
        [Tooltip("Spin: degrees completed in Duration. Cyclic motions: cycles completed in Duration. Kept as 'speed' for serialized asset compatibility.")]
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
                    result.speed = 360f;
                    result.amount = 1f;
                    result.durationSeconds = 4f;
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
