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
        private readonly MaterialPropertyBlock propertyBlock = new();

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

        private void OnEnable()
        {
            startedAt = Now;
            ResetLayers();
        }

        private void OnDisable() => ResetLayers();

        private void Update()
        {
            var elapsed = (float)(Now - startedAt);
            var activeQuality = SpliceFxQuality.MaskFor(SpliceFxQuality.Current);
            foreach (var layer in layers)
            {
                if (layer?.visual == null) continue;
                var allowed = (layer.quality & activeQuality) != 0;
                var localDuration = Mathf.Max(0.01f, layer.durationSeconds);
                var shouldBeActive = allowed &&
                                     elapsed >= Mathf.Max(0f, layer.startSeconds) &&
                                     (layer.loop ||
                                      elapsed < layer.startSeconds + localDuration);
                if (shouldBeActive == layer.active &&
                    layer.visual.activeSelf == shouldBeActive)
                    continue;
                layer.active = shouldBeActive;
                layer.visual.SetActive(shouldBeActive);
                if (shouldBeActive) Restart(layer.visual);
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
