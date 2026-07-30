using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.FxStudio
{
    public sealed class SpliceFxAuxiliaryLayerMarker : MonoBehaviour
    {
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpliceFxQualityGate : MonoBehaviour
    {
        [SerializeField] private SpliceFxQualityMask quality =
            SpliceFxQualityMask.All;
        [SerializeField] private GameObject target;

        public void Configure(
            SpliceFxQualityMask mask, GameObject value)
        {
            quality = mask == SpliceFxQualityMask.None
                ? SpliceFxQualityMask.All
                : mask;
            target = value;
            Evaluate(SpliceFxQuality.Current);
        }

        public void Evaluate(SpliceFxQualityTier tier)
        {
            if (target == null) return;
            target.SetActive(
                (quality & SpliceFxQuality.MaskFor(tier)) != 0);
        }

        private void OnEnable() =>
            Evaluate(SpliceFxQuality.Current);

        private void Update() =>
            Evaluate(SpliceFxQuality.Current);
    }

    internal static class SpliceFxGradientTextureCache
    {
        private static readonly Dictionary<int, Texture2D> Cache = new();

        public static Texture2D Get(Gradient gradient)
        {
            gradient ??= new Gradient();
            var key = Fingerprint(gradient);
            if (Cache.TryGetValue(key, out var texture) &&
                texture != null)
                return texture;

            const int width = 128;
            texture = new Texture2D(
                width, 1, TextureFormat.RGBA32, false, true)
            {
                name = $"FX_Gradient_{key:X8}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[width];
            for (var i = 0; i < width; i++)
                pixels[i] = gradient.Evaluate(
                    i / (float)(width - 1));
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        internal static int Fingerprint(Gradient gradient)
        {
            if (gradient == null) return 0;
            unchecked
            {
                var hash = 17;
                foreach (var key in gradient.colorKeys)
                {
                    hash = hash * 31 ^ key.color.GetHashCode();
                    hash = hash * 31 ^ key.time.GetHashCode();
                }
                foreach (var key in gradient.alphaKeys)
                {
                    hash = hash * 31 ^ key.alpha.GetHashCode();
                    hash = hash * 31 ^ key.time.GetHashCode();
                }
                return hash;
            }
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpliceFxInstanceGroup : MonoBehaviour
    {
        [SerializeField] private SpliceFxSubEffectDefinition definition;
        [SerializeField] private bool useInlineLayout;
        [SerializeField] private SpliceFxInstanceLayout inlineLayout;
        [SerializeField] private List<Transform> instances = new();
        [SerializeField] private List<bool> layoutEnabled = new();
        private readonly List<Quaternion> baseRotations = new();
        private double startedAt;
        private bool externalTimeControl;
        private int evaluatedVisibleCount;

        public IReadOnlyList<Transform> Instances => instances;
        public bool ExternalTimeControl => externalTimeControl;

        public void ConfigureEditor(
            SpliceFxSubEffectDefinition value,
            List<Transform> instanceTransforms,
            List<bool> enabledStates)
        {
            definition = value;
            useInlineLayout = false;
            inlineLayout = null;
            instances = instanceTransforms ?? new List<Transform>();
            layoutEnabled = enabledStates ?? new List<bool>();
            CaptureRotations();
            startedAt = Now;
            EvaluatePreview(0f, SpliceFxQuality.Current);
        }

        public void ConfigureLayer(
            SpliceFxInstanceLayout layout,
            List<Transform> instanceTransforms,
            List<bool> enabledStates)
        {
            definition = null;
            useInlineLayout = true;
            inlineLayout = layout ?? new SpliceFxInstanceLayout();
            instances = instanceTransforms ?? new List<Transform>();
            layoutEnabled = enabledStates ?? new List<bool>();
            CaptureRotations();
            startedAt = Now;
            EvaluatePreview(0f, SpliceFxQuality.Current);
        }

        public void RestartInstances()
        {
            EnsureRotations();
            startedAt = Now;
            EvaluatePreview(0f, SpliceFxQuality.Current);
        }

        public void SetExternalTimeControl(bool enabled) =>
            externalTimeControl = enabled;

        public float GetLocalElapsed(
            Transform descendant, float elapsedSeconds)
        {
            if (descendant == null) return Mathf.Max(0f, elapsedSeconds);
            var layout = Layout;
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null ||
                    (descendant != instance &&
                     !descendant.IsChildOf(instance)))
                    continue;
                return Mathf.Max(0f, elapsedSeconds -
                    (layout?.DelayFor(i,
                        evaluatedVisibleCount > 0
                            ? evaluatedVisibleCount
                            : instances.Count) ?? 0f));
            }
            return Mathf.Max(0f, elapsedSeconds);
        }

        public void EvaluatePreview(float elapsedSeconds,
            SpliceFxQualityTier quality)
        {
            EnsureRotations();
            var layout = Layout;
            var visibleCount = layout != null
                ? layout.CountFor(quality)
                : instances.Count;
            evaluatedVisibleCount = visibleCount;
            var spinSpeed =
                layout?.selfSpinDegreesPerSecond ?? 0f;
            var spinAxis = layout != null &&
                           layout.selfSpinAxis.sqrMagnitude > 0.0001f
                ? layout.selfSpinAxis.normalized
                : Vector3.up;

            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null) continue;
                var authoredEnabled = i >= layoutEnabled.Count ||
                                      layoutEnabled[i];
                var delay = layout?.DelayFor(i, visibleCount) ?? 0f;
                var localElapsed =
                    Mathf.Max(0f, elapsedSeconds - delay);
                var started = elapsedSeconds + 0.0001f >= delay;
                var withinDuration = layout == null ||
                                     layout.activeDuration <= 0f ||
                                     localElapsed <
                                     layout.activeDuration;
                instance.gameObject.SetActive(
                    authoredEnabled && i < visibleCount &&
                    started && withinDuration);
                if (i >= baseRotations.Count) continue;
                var direction = layout?.alternateSelfSpin == true &&
                                (i & 1) == 1
                    ? -1f
                    : 1f;
                instance.localRotation = baseRotations[i] *
                                         Quaternion.AngleAxis(
                                             spinSpeed *
                                             localElapsed *
                                             direction,
                                             spinAxis);
            }
        }

        private void OnEnable()
        {
            CaptureRotations();
            startedAt = Now;
        }

        private void OnDisable()
        {
            for (var i = 0; i < instances.Count &&
                            i < baseRotations.Count; i++)
            {
                if (instances[i] != null)
                    instances[i].localRotation = baseRotations[i];
            }
        }

        private void Update()
        {
            if (definition == null || externalTimeControl) return;
            EvaluatePreview((float)(Now - startedAt),
                SpliceFxQuality.Current);
        }

        private void CaptureRotations()
        {
            baseRotations.Clear();
            foreach (var instance in instances)
                baseRotations.Add(instance != null
                    ? instance.localRotation
                    : Quaternion.identity);
        }

        private void EnsureRotations()
        {
            if (baseRotations.Count != instances.Count)
                CaptureRotations();
        }

        private SpliceFxInstanceLayout Layout =>
            useInlineLayout
                ? inlineLayout
                : definition?.instanceLayout;

        private static double Now =>
            Time.realtimeSinceStartupAsDouble;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpliceFxPropertyDriver : MonoBehaviour
    {
        [SerializeField] private SpliceFxSubEffectDefinition definition;
        private MaterialPropertyBlock propertyBlock;

        public SpliceFxSubEffectDefinition Definition => definition;

        public void Configure(SpliceFxSubEffectDefinition value)
        {
            definition = value;
            Apply();
        }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            if (definition == null) return;
            foreach (var visual in GetComponentsInChildren<VisualEffect>(true))
            {
                if (visual.GetComponentInParent<
                        SpliceFxAuxiliaryLayerMarker>() != null)
                    continue;
                ApplyVisualEffect(visual);
            }
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.GetComponentInParent<
                        SpliceFxAuxiliaryLayerMarker>() != null)
                    continue;
                ApplyParticleSystem(particle);
            }
            foreach (var trail in GetComponentsInChildren<TrailRenderer>(true))
            {
                if (trail.GetComponentInParent<
                        SpliceFxAuxiliaryLayerMarker>() != null)
                    continue;
                ApplyTrail(trail);
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<
                        SpliceFxAuxiliaryLayerMarker>() != null)
                    continue;
                ApplyRenderer(renderer);
            }
        }

        private void ApplyVisualEffect(VisualEffect visual)
        {
            if (visual == null || visual.visualEffectAsset == null) return;
            var preset = definition.preset;
            SetTexture(visual, Property(preset?.mainTextureProperty, "MainTexture"),
                definition.EffectiveTexture);
            SetVector4(visual, Property(preset?.mainColorProperty, "MainColor"),
                definition.mainColor * Mathf.Max(1f, definition.emission));
            var gradientId = Shader.PropertyToID("MainGradient");
            if (visual.HasGradient(gradientId))
                visual.SetGradient(gradientId, definition.mainGradient);
            SetFloat(visual, "GradientMode",
                (float)definition.gradientMode);
            SetFloat(visual, Property(preset?.emissionProperty, "Emission"),
                definition.emission);
            SetFloat(visual, Property(preset?.lifetimeProperty, "Lifetime"),
                definition.lifetime);
            SetFloat(visual, Property(preset?.spawnRateProperty, "SpawnRate"),
                definition.spawnRate);
            SetFloat(visual, Property(preset?.speedProperty, "Speed"),
                definition.speed);
            SetFloat(visual, Property(preset?.sizeProperty, "Size"),
                definition.size);
            SetFloat(visual, Property(preset?.radiusProperty, "Radius"),
                definition.radius);
            SetFloat(visual,
                Property(preset?.rotationSpeedProperty, "RotationSpeed"),
                definition.rotationSpeed);
            SetFloat(visual, Property(preset?.noiseProperty, "NoiseStrength"),
                definition.noiseStrength);

            foreach (var value in definition.customValues)
                ApplyCustom(visual, value);
        }

        private void ApplyParticleSystem(ParticleSystem particle)
        {
            if (particle == null) return;
            var main = particle.main;
            main.startColor = definition.gradientMode ==
                              SpliceFxGradientMode.Solid
                ? definition.mainColor
                : Color.white;
            main.startLifetime = Mathf.Max(0.01f, definition.lifetime);
            main.startSpeed = Mathf.Max(0f, definition.speed);
            main.startSize = Mathf.Max(0.001f, definition.size);

            var emission = particle.emission;
            emission.rateOverTime = Mathf.Max(0f, definition.spawnRate);

            var shape = particle.shape;
            if (shape.enabled)
                shape.radius = Mathf.Max(0f, definition.radius);

            var noise = particle.noise;
            if (noise.enabled)
                noise.strength = Mathf.Max(0f, definition.noiseStrength);

            var colorOverLifetime = particle.colorOverLifetime;
            colorOverLifetime.enabled =
                definition.gradientMode !=
                SpliceFxGradientMode.Solid;
            if (colorOverLifetime.enabled)
                colorOverLifetime.color =
                    new ParticleSystem.MinMaxGradient(
                        definition.mainGradient);
        }

        private void ApplyTrail(TrailRenderer trail)
        {
            trail.colorGradient =
                definition.gradientMode ==
                SpliceFxGradientMode.Solid
                    ? SolidGradient(definition.mainColor)
                    : definition.mainGradient;
        }

        private void ApplyRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            var texture = definition.EffectiveTexture;
            if (texture != null)
            {
                propertyBlock.SetTexture("_BaseMap", texture);
                propertyBlock.SetTexture("_MainTex", texture);
            }
            var supportsGradient = renderer.sharedMaterial != null &&
                                   renderer.sharedMaterial.HasProperty(
                                       "_GradientMap");
            var color = supportsGradient
                ? definition.mainColor
                : definition.mainColor *
                  Mathf.Max(1f, definition.emission);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color);
            if (supportsGradient)
            {
                propertyBlock.SetTexture("_GradientMap",
                    SpliceFxGradientTextureCache.Get(
                        definition.mainGradient));
                propertyBlock.SetFloat("_GradientMode",
                    (float)definition.gradientMode);
                propertyBlock.SetFloat("_GradientReverse",
                    definition.reverseGradient ? 1f : 0f);
                propertyBlock.SetFloat("_FxEmission",
                    Mathf.Max(0f, definition.emission));
                propertyBlock.SetFloat("_StrokeMode",
                    (float)definition.strokeMode);
                propertyBlock.SetColor("_StrokeColor",
                    definition.strokeColor);
                propertyBlock.SetFloat("_StrokeWidth",
                    Mathf.Max(0f, definition.strokeWidth));
                propertyBlock.SetFloat("_StrokeDashFrequency",
                    Mathf.Max(1f,
                        definition.strokeDashFrequency));
            }
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static Gradient SolidGradient(Color color)
        {
            var result = new Gradient();
            result.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a, 1f)
                });
            return result;
        }

        private static void ApplyCustom(VisualEffect visual,
            SpliceFxPropertyValue value)
        {
            if (visual == null || value == null ||
                string.IsNullOrWhiteSpace(value.propertyName))
                return;
            var id = Shader.PropertyToID(value.propertyName);
            switch (value.propertyType)
            {
                case SpliceFxPropertyType.Float:
                    if (visual.HasFloat(id)) visual.SetFloat(id, value.floatValue);
                    break;
                case SpliceFxPropertyType.Int:
                    if (visual.HasInt(id)) visual.SetInt(id, value.intValue);
                    break;
                case SpliceFxPropertyType.Bool:
                    if (visual.HasBool(id)) visual.SetBool(id, value.boolValue);
                    break;
                case SpliceFxPropertyType.Vector2:
                    if (visual.HasVector2(id))
                        visual.SetVector2(id,
                            new Vector2(value.vectorValue.x, value.vectorValue.y));
                    break;
                case SpliceFxPropertyType.Vector3:
                    if (visual.HasVector3(id))
                        visual.SetVector3(id,
                            new Vector3(value.vectorValue.x, value.vectorValue.y,
                                value.vectorValue.z));
                    break;
                case SpliceFxPropertyType.Vector4:
                case SpliceFxPropertyType.Color:
                    if (visual.HasVector4(id))
                        visual.SetVector4(id,
                            value.propertyType == SpliceFxPropertyType.Color
                                ? (Vector4)value.colorValue
                                : value.vectorValue);
                    break;
                case SpliceFxPropertyType.Texture:
                    if (visual.HasTexture(id) && value.textureValue != null)
                        visual.SetTexture(id, value.textureValue);
                    break;
                case SpliceFxPropertyType.Gradient:
                    if (visual.HasGradient(id) && value.gradientValue != null)
                        visual.SetGradient(id, value.gradientValue);
                    break;
                case SpliceFxPropertyType.Curve:
                    if (visual.HasAnimationCurve(id) && value.curveValue != null)
                        visual.SetAnimationCurve(id, value.curveValue);
                    break;
            }
        }

        private static void SetFloat(VisualEffect visual, string property,
            float value)
        {
            if (string.IsNullOrWhiteSpace(property)) return;
            var id = Shader.PropertyToID(property);
            if (visual.HasFloat(id)) visual.SetFloat(id, value);
        }

        private static void SetVector4(VisualEffect visual, string property,
            Vector4 value)
        {
            if (string.IsNullOrWhiteSpace(property)) return;
            var id = Shader.PropertyToID(property);
            if (visual.HasVector4(id)) visual.SetVector4(id, value);
        }

        private static void SetTexture(VisualEffect visual, string property,
            Texture value)
        {
            if (value == null || string.IsNullOrWhiteSpace(property)) return;
            var id = Shader.PropertyToID(property);
            if (visual.HasTexture(id)) visual.SetTexture(id, value);
        }

        private static string Property(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpliceFxMotionPlayer : MonoBehaviour
    {
        [SerializeField] private SpliceFxSubEffectDefinition definition;
        [SerializeField] private bool useInlineMotions;
        [SerializeField] private List<SpliceFxMotionLayer> inlineMotions =
            new();
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private double startedAt;
        private bool captured;
        private MaterialPropertyBlock propertyBlock;
        private bool externalTimeControl;

        public SpliceFxSubEffectDefinition Definition => definition;
        public bool ExternalTimeControl => externalTimeControl;

        public void Configure(SpliceFxSubEffectDefinition value)
        {
            definition = value;
            useInlineMotions = false;
            inlineMotions.Clear();
            CaptureCurrentAsBase();
            RestartMotion();
        }

        public void ConfigureInline(
            List<SpliceFxMotionLayer> motions)
        {
            definition = null;
            useInlineMotions = true;
            inlineMotions = motions ?? new List<SpliceFxMotionLayer>();
            CaptureCurrentAsBase();
            RestartMotion();
        }

        public void CaptureCurrentAsBase()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
            captured = true;
        }

        public void RestartMotion()
        {
            if (!captured) CaptureCurrentAsBase();
            startedAt = Now;
            Evaluate(0f);
        }

        public void EvaluatePreview(float elapsedSeconds)
        {
            if (!captured) CaptureCurrentAsBase();
            Evaluate(Mathf.Max(0f, elapsedSeconds));
        }

        public void SetExternalTimeControl(bool enabled) =>
            externalTimeControl = enabled;

        private void OnEnable()
        {
            CaptureCurrentAsBase();
            startedAt = Now;
        }

        private void OnDisable()
        {
            if (!captured) return;
            transform.SetLocalPositionAndRotation(
                basePosition, baseRotation);
            transform.localScale = baseScale;
            ApplyVisuals(1f, 1f, Vector2.zero);
        }

        private void Update()
        {
            if ((definition == null && !useInlineMotions) ||
                externalTimeControl) return;
            Evaluate((float)(Now - startedAt));
        }

        private void Evaluate(float elapsed)
        {
            if ((definition == null && !useInlineMotions) ||
                !captured) return;
            var position = basePosition;
            var rotation = baseRotation;
            var scaleMultiplier = 1f;
            var brightness = 1f;
            var opacity = 1f;
            var uvOffset = Vector2.zero;

            var motions = useInlineMotions
                ? inlineMotions
                : definition?.motions;
            if (motions != null && motions.Count > 0)
            {
                foreach (var motion in motions)
                {
                    if (motion?.enabled != true) continue;
                    ApplyMotion(motion, elapsed, ref position,
                        ref rotation, ref scaleMultiplier,
                        ref brightness, ref opacity, ref uvOffset);
                }
            }
            else if (definition != null)
            {
                ApplyLegacy(definition.motionModifiers, elapsed,
                    ref rotation, ref scaleMultiplier,
                    ref position, definition.rotationSpeed);
            }

            transform.SetLocalPositionAndRotation(position, rotation);
            transform.localScale =
                baseScale * Mathf.Max(0.001f, scaleMultiplier);
            ApplyVisuals(brightness, opacity, uvOffset);
        }

        private static void ApplyMotion(SpliceFxMotionLayer motion,
            float elapsed, ref Vector3 position, ref Quaternion rotation,
            ref float scale, ref float brightness, ref float opacity,
            ref Vector2 uvOffset)
        {
            var local = elapsed - Mathf.Max(0f, motion.delaySeconds);
            if (local < 0f) return;
            var duration = Mathf.Max(0.01f, motion.durationSeconds);
            var motionTime = motion.loop
                ? local
                : Mathf.Min(local, duration);
            var normalized = motion.loop
                ? Mathf.Repeat(local / duration, 1f)
                : Mathf.Clamp01(local / duration);
            var curve = motion.curve != null
                ? motion.curve.Evaluate(normalized)
                : normalized;
            var axis = motion.axis.sqrMagnitude > 0.0001f
                ? motion.axis.normalized
                : Vector3.up;
            var timeInDuration = motionTime / duration;
            var wave = Mathf.Sin(
                (timeInDuration * motion.speed + motion.phase) *
                Mathf.PI * 2f);
            var amount = Mathf.Max(0f, motion.amount);

            switch (motion.type)
            {
                case SpliceFxMotionType.Spin:
                    rotation *= Quaternion.AngleAxis(
                        motion.speed * timeInDuration, axis);
                    break;
                case SpliceFxMotionType.Pulse:
                    scale *= Mathf.Max(0.001f, 1f + wave * amount);
                    break;
                case SpliceFxMotionType.Expand:
                    scale *= 1f + curve * amount;
                    break;
                case SpliceFxMotionType.Contract:
                    scale *= Mathf.Max(0.001f, 1f - curve * amount);
                    break;
                case SpliceFxMotionType.Float:
                    position += axis * (wave * amount);
                    break;
                case SpliceFxMotionType.Orbit:
                {
                    var reference = Vector3.Cross(axis, Vector3.forward);
                    if (reference.sqrMagnitude < 0.0001f)
                        reference = Vector3.Cross(axis, Vector3.right);
                    position += Quaternion.AngleAxis(
                                    motion.speed * timeInDuration * 360f,
                                    axis) *
                                reference.normalized * amount;
                    break;
                }
                case SpliceFxMotionType.Flicker:
                    brightness *= Mathf.Lerp(
                        Mathf.Max(0f, 1f - amount), 1f,
                        wave * 0.5f + 0.5f);
                    break;
                case SpliceFxMotionType.FadeIn:
                    opacity *= Mathf.Lerp(1f - Mathf.Clamp01(amount),
                        1f, curve);
                    break;
                case SpliceFxMotionType.FadeOut:
                    opacity *= Mathf.Lerp(1f,
                        1f - Mathf.Clamp01(amount), curve);
                    break;
                case SpliceFxMotionType.UvScroll:
                    uvOffset += motion.uvSpeed *
                                (motion.speed * timeInDuration);
                    break;
                case SpliceFxMotionType.Shake:
                {
                    var sample = timeInDuration * Mathf.Max(
                        0.01f, Mathf.Abs(motion.speed));
                    position += new Vector3(
                        Mathf.PerlinNoise(sample, 0.17f) * 2f - 1f,
                        Mathf.PerlinNoise(0.31f, sample) * 2f - 1f,
                        Mathf.PerlinNoise(sample, 0.73f) * 2f - 1f) *
                                amount;
                    break;
                }
            }
        }

        private static void ApplyLegacy(
            SpliceFxMotionModifier modifiers, float elapsed,
            ref Quaternion rotation, ref float scale,
            ref Vector3 position, float spinSpeed)
        {
            if ((modifiers & SpliceFxMotionModifier.Spin) != 0)
                rotation *= Quaternion.AngleAxis(
                    elapsed * spinSpeed, Vector3.up);
            if ((modifiers & SpliceFxMotionModifier.Pulse) != 0)
                scale *= 1f + Mathf.Sin(elapsed * Mathf.PI * 3f) *
                         0.18f;
            if ((modifiers & SpliceFxMotionModifier.Expand) != 0)
                scale *= Mathf.Lerp(1f, 1.8f,
                    Mathf.Clamp01(elapsed));
            if ((modifiers & SpliceFxMotionModifier.Contract) != 0)
                scale *= Mathf.Lerp(1f, 0.2f,
                    Mathf.Clamp01(elapsed));
            if ((modifiers & SpliceFxMotionModifier.Noise) != 0)
                position += new Vector3(
                    Mathf.PerlinNoise(elapsed * 12f, 0f) - 0.5f,
                    0f,
                    Mathf.PerlinNoise(0f, elapsed * 12f) - 0.5f) *
                            0.12f;
        }

        private void ApplyVisuals(float brightness, float opacity,
            Vector2 uvOffset)
        {
            // Inline visual-layer motions do not own a SubFX definition.
            // Use white as a neutral multiplier so Fade/Flicker/UV Scroll
            // can animate Trail and Particle layers without replacing their
            // authored gradients or texture property blocks.
            var authoredColor = definition != null
                ? definition.mainColor
                : Color.white;
            var authoredEmission = definition != null
                ? Mathf.Max(1f, definition.emission)
                : 1f;
            var color = authoredColor *
                        (Mathf.Max(0f, brightness) *
                         authoredEmission);
            color.a = authoredColor.a *
                      Mathf.Clamp01(opacity);
            propertyBlock ??= new MaterialPropertyBlock();

            foreach (var renderer in
                     GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_EmissionColor", color);
                var textureTransform = new Vector4(
                    1f, 1f, uvOffset.x, uvOffset.y);
                propertyBlock.SetVector("_BaseMap_ST",
                    textureTransform);
                propertyBlock.SetVector("_MainTex_ST",
                    textureTransform);
                renderer.SetPropertyBlock(propertyBlock);
            }

            foreach (var particle in
                     GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.startColor = color;
            }

            foreach (var visual in
                     GetComponentsInChildren<VisualEffect>(true))
            {
                if (visual.visualEffectAsset == null) continue;
                var colorProperty = string.IsNullOrWhiteSpace(
                    definition?.preset?.mainColorProperty)
                    ? "MainColor"
                    : definition.preset.mainColorProperty;
                var colorId = Shader.PropertyToID(colorProperty);
                if (visual.HasVector4(colorId))
                    visual.SetVector4(colorId, color);
                var uvId = Shader.PropertyToID("UVOffset");
                if (visual.HasVector2(uvId))
                    visual.SetVector2(uvId, uvOffset);
            }
        }

        private static double Now =>
            Time.realtimeSinceStartupAsDouble;
    }

    [Serializable]
    public sealed class SpliceFxRuntimeLayer
    {
        public string label;
        public GameObject visual;
        public float startSeconds;
        public float durationSeconds = 1f;
        public SpliceFxQualityMask quality = SpliceFxQualityMask.All;
        public bool loop;
        [NonSerialized] public bool active;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpliceFxSequenceRuntime : MonoBehaviour
    {
        [SerializeField] private List<SpliceFxRuntimeLayer> layers = new();
        [SerializeField] private float durationSeconds = 1f;
        private double startedAt;
        private bool externalTimeControl;

        public IReadOnlyList<SpliceFxRuntimeLayer> Layers => layers;
        public float DurationSeconds => durationSeconds;
        public bool ExternalTimeControl => externalTimeControl;

        public void ConfigureEditor(List<SpliceFxRuntimeLayer> value,
            float duration)
        {
            layers = value ?? new List<SpliceFxRuntimeLayer>();
            durationSeconds = Mathf.Max(0.05f, duration);
            ResetLayers();
        }

        public void RestartSequence()
        {
            startedAt = Now;
            ResetLayers();
        }

        public void EvaluatePreview(float elapsedSeconds,
            SpliceFxQualityTier quality)
        {
            Evaluate(Mathf.Max(0f, elapsedSeconds), quality, true);
        }

        public void SetExternalTimeControl(bool enabled) =>
            externalTimeControl = enabled;

        private void OnEnable()
        {
            startedAt = Now;
            ResetLayers();
        }

        private void OnDisable() => ResetLayers();

        private void Update()
        {
            if (externalTimeControl) return;
            var elapsed = (float)(Now - startedAt);
            Evaluate(elapsed, SpliceFxQuality.Current, false);
        }

        private void Evaluate(float elapsed, SpliceFxQualityTier quality,
            bool forceExactTime)
        {
            var activeQuality = SpliceFxQuality.MaskFor(quality);
            foreach (var layer in layers)
            {
                if (layer?.visual == null) continue;
                var allowed = (layer.quality & activeQuality) != 0;
                var localDuration = Mathf.Max(0.01f, layer.durationSeconds);
                var shouldBeActive = allowed &&
                                     elapsed >= Mathf.Max(0f, layer.startSeconds) &&
                                     (layer.loop ||
                                      elapsed < layer.startSeconds + localDuration);
                if (!forceExactTime &&
                    shouldBeActive == layer.active &&
                    layer.visual.activeSelf == shouldBeActive)
                    continue;
                layer.active = shouldBeActive;
                layer.visual.SetActive(shouldBeActive);
                if (!shouldBeActive) continue;
                if (!forceExactTime)
                {
                    Restart(layer.visual);
                    continue;
                }

                var localTime = Mathf.Max(0f,
                    elapsed - Mathf.Max(0f, layer.startSeconds));
                if (layer.loop)
                    localTime %= localDuration;
                SimulateAt(layer.visual, localTime, quality);
            }
        }

        private void ResetLayers()
        {
            foreach (var layer in layers)
            {
                if (layer == null) continue;
                layer.active = false;
                if (layer.visual != null) layer.visual.SetActive(false);
            }
        }

        private static void Restart(GameObject root)
        {
            foreach (var group in
                     root.GetComponentsInChildren<SpliceFxInstanceGroup>(
                         true))
                group.RestartInstances();
            foreach (var motion in
                     root.GetComponentsInChildren<SpliceFxMotionPlayer>(
                         true))
                motion.RestartMotion();
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.Clear();
                trail.emitting = true;
            }
            foreach (var visual in root.GetComponentsInChildren<VisualEffect>(true))
            {
                visual.Reinit();
                visual.Play();
            }
        }

        private static void SimulateAt(GameObject root, float seconds,
            SpliceFxQualityTier quality)
        {
            var groups =
                root.GetComponentsInChildren<SpliceFxInstanceGroup>(true);
            foreach (var group in groups)
                group.EvaluatePreview(seconds, quality);
            foreach (var motion in
                     root.GetComponentsInChildren<SpliceFxMotionPlayer>(
                         true))
                motion.EvaluatePreview(InstanceLocalTime(
                    groups, motion.transform, seconds));
            foreach (var particle in
                     root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!particle.gameObject.activeInHierarchy) continue;
                var localTime = InstanceLocalTime(
                    groups, particle.transform, seconds);
                particle.Stop(true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(localTime, true, true, true);
                particle.Pause(true);
            }
            foreach (var trail in
                     root.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.Clear();
                trail.emitting = true;
            }
            foreach (var visual in
                     root.GetComponentsInChildren<VisualEffect>(true))
            {
                if (!visual.gameObject.activeInHierarchy) continue;
                var localTime = InstanceLocalTime(
                    groups, visual.transform, seconds);
                visual.Reinit();
                if (localTime > 0f)
                {
                    const float fixedStep = 1f / 30f;
                    var steps = Mathf.Clamp(
                        Mathf.CeilToInt(localTime / fixedStep),
                        1, 240);
                    visual.Simulate(localTime / steps, (uint)steps);
                }
                visual.pause = true;
            }
        }

        private static float InstanceLocalTime(
            IReadOnlyList<SpliceFxInstanceGroup> groups,
            Transform component, float time)
        {
            foreach (var group in groups)
                foreach (var instance in group.Instances)
                    if (instance != null &&
                        (component == instance ||
                         component.IsChildOf(instance)))
                        return group.GetLocalElapsed(component, time);
            return Mathf.Max(0f, time);
        }

        private static double Now => Time.realtimeSinceStartupAsDouble;
    }

    [DisallowMultipleComponent]
    public sealed class SpliceFxGeneratedMetadata : MonoBehaviour
    {
        [SerializeField] private string sourceId;
        [SerializeField] private int schemaVersion;
        [SerializeField] private string generatedUtc;

        public string SourceId => sourceId;
        public int SchemaVersion => schemaVersion;
        public string GeneratedUtc => generatedUtc;

        public void ConfigureEditor(string id, int version, string utc)
        {
            sourceId = id;
            schemaVersion = Mathf.Max(1, version);
            generatedUtc = utc;
        }
    }
}
