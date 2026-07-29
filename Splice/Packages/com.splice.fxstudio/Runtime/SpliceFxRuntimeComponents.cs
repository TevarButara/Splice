using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.FxStudio
{
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
                ApplyVisualEffect(visual);
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
                ApplyParticleSystem(particle);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                ApplyRenderer(renderer);
        }

        private void ApplyVisualEffect(VisualEffect visual)
        {
            if (visual == null || visual.visualEffectAsset == null) return;
            var preset = definition.preset;
            SetTexture(visual, Property(preset?.mainTextureProperty, "MainTexture"),
                definition.EffectiveTexture);
            SetVector4(visual, Property(preset?.mainColorProperty, "MainColor"),
                definition.mainColor * Mathf.Max(1f, definition.emission));
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
            main.startColor = definition.mainColor;
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
            var color = definition.mainColor *
                        Mathf.Max(1f, definition.emission);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color);
            renderer.SetPropertyBlock(propertyBlock);
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
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private double startedAt;
        private bool captured;
        private MaterialPropertyBlock propertyBlock;

        public SpliceFxSubEffectDefinition Definition => definition;

        public void Configure(SpliceFxSubEffectDefinition value)
        {
            definition = value;
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
            if (definition == null) return;
            Evaluate((float)(Now - startedAt));
        }

        private void Evaluate(float elapsed)
        {
            if (definition == null || !captured) return;
            var position = basePosition;
            var rotation = baseRotation;
            var scaleMultiplier = 1f;
            var brightness = 1f;
            var opacity = 1f;
            var uvOffset = Vector2.zero;

            if (definition.motions != null &&
                definition.motions.Count > 0)
            {
                foreach (var motion in definition.motions)
                {
                    if (motion?.enabled != true) continue;
                    ApplyMotion(motion, elapsed, ref position,
                        ref rotation, ref scaleMultiplier,
                        ref brightness, ref opacity, ref uvOffset);
                }
            }
            else
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
            var wave = Mathf.Sin(
                (motionTime * motion.speed + motion.phase) *
                Mathf.PI * 2f);
            var amount = Mathf.Max(0f, motion.amount);

            switch (motion.type)
            {
                case SpliceFxMotionType.Spin:
                    rotation *= Quaternion.AngleAxis(
                        motion.speed * motionTime, axis);
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
                                    motion.speed * motionTime * 360f,
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
                                (motion.speed * motionTime);
                    break;
                case SpliceFxMotionType.Shake:
                {
                    var sample = motionTime * Mathf.Max(
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
            if (definition == null) return;
            var color = definition.mainColor *
                        (Mathf.Max(0f, brightness) *
                         Mathf.Max(1f, definition.emission));
            color.a = definition.mainColor.a *
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
                    definition.preset?.mainColorProperty)
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

        public IReadOnlyList<SpliceFxRuntimeLayer> Layers => layers;
        public float DurationSeconds => durationSeconds;

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

        private void OnEnable()
        {
            startedAt = Now;
            ResetLayers();
        }

        private void OnDisable() => ResetLayers();

        private void Update()
        {
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
                SimulateAt(layer.visual, localTime);
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

        private static void SimulateAt(GameObject root, float seconds)
        {
            foreach (var motion in
                     root.GetComponentsInChildren<SpliceFxMotionPlayer>(
                         true))
                motion.EvaluatePreview(seconds);
            foreach (var particle in
                     root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(seconds, true, true, true);
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
                visual.Reinit();
                if (seconds > 0f)
                    visual.Simulate(seconds, 1);
                visual.pause = true;
            }
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
