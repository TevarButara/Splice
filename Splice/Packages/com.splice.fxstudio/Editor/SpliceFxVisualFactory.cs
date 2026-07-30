using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Splice.FxStudio.Editor
{
    internal static class SpliceFxVisualFactory
    {
        public static GameObject Build(
            SpliceFxSubEffectDefinition subFx)
        {
            if (subFx == null)
                throw new ArgumentNullException(nameof(subFx));
            if (subFx.EffectiveTemplate == null)
                throw new InvalidOperationException(
                    $"SubFX '{subFx.name}' has no effective template.");
            if (subFx.instanceLayout?.MaximumCount > 64)
                throw new InvalidOperationException(
                    $"SubFX '{subFx.name}' requests " +
                    $"{subFx.instanceLayout.MaximumCount} instances; " +
                    "the supported maximum is 64.");

            var root = new GameObject(
                $"SubFX_{SpliceFxPresetDefinition.SanitizeId(subFx.subFxId)}");
            var transforms = new List<Transform>();
            var enabledStates = new List<bool>();
            var poses = SpliceFxInstanceLayoutSolver.Build(
                subFx.instanceLayout);
            if (poses.Count == 0)
                poses.Add(new SpliceFxInstancePose(
                    Vector3.zero, Quaternion.identity, Vector3.one));

            for (var i = 0; i < poses.Count; i++)
            {
                var clone = PrefabUtility.InstantiatePrefab(
                    subFx.EffectiveTemplate) as GameObject;
                if (clone == null)
                    clone = Object.Instantiate(
                        subFx.EffectiveTemplate);
                var authoredPosition = clone.transform.localPosition;
                var authoredRotation = clone.transform.localRotation;
                var authoredScale = clone.transform.localScale;
                clone.name = $"Instance_{i + 1:00}";
                clone.transform.SetParent(root.transform, false);
                var pose = poses[i];
                clone.transform.localPosition =
                    authoredPosition + pose.Position;
                clone.transform.localRotation =
                    pose.Rotation * authoredRotation;
                clone.transform.localScale = Vector3.Scale(
                    authoredScale, pose.Scale);
                clone.SetActive(pose.Enabled);
                transforms.Add(clone.transform);
                enabledStates.Add(pose.Enabled);
                if (subFx.instanceLayout?.motionScope ==
                    SpliceFxInstanceMotionScope.EachInstance)
                {
                    var itemMotion =
                        clone.GetComponent<SpliceFxMotionPlayer>() ??
                        clone.AddComponent<SpliceFxMotionPlayer>();
                    itemMotion.Configure(subFx);
                }
            }

            BuildVisualLayers(root.transform, subFx);
            var driver = root.AddComponent<SpliceFxPropertyDriver>();
            driver.Configure(subFx);
            if (subFx.instanceLayout?.motionScope !=
                SpliceFxInstanceMotionScope.EachInstance)
            {
                var motion = root.AddComponent<SpliceFxMotionPlayer>();
                motion.Configure(subFx);
            }
            var group = root.AddComponent<SpliceFxInstanceGroup>();
            group.ConfigureEditor(subFx, transforms, enabledStates);
            root.SetActive(true);
            return root;
        }

        private static void BuildVisualLayers(
            Transform parent, SpliceFxSubEffectDefinition subFx)
        {
            if (subFx.visualLayers == null ||
                subFx.visualLayers.Count == 0)
                return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                SpliceFxStarterLibrary.Root +
                "/Materials/M_FXStudio_Additive.mat");
            for (var layerIndex = 0;
                 layerIndex < subFx.visualLayers.Count;
                 layerIndex++)
            {
                var layer = subFx.visualLayers[layerIndex];
                if (layer?.enabled != true) continue;
                var layout =
                    layer.instanceLayout ??
                    new SpliceFxInstanceLayout();
                if (layout.MaximumCount > 64)
                    throw new InvalidOperationException(
                        $"Visual layer '{layer.label}' requests " +
                        $"{layout.MaximumCount} instances; maximum is 64.");

                var layerRoot = new GameObject(
                    $"Layer_{layerIndex + 1:00}_{Safe(layer.label)}");
                layerRoot.transform.SetParent(parent, false);
                layerRoot.transform.localPosition =
                    layer.localPosition;
                layerRoot.transform.localRotation =
                    Quaternion.Euler(layer.localEulerAngles);
                layerRoot.transform.localScale =
                    SanitizeScale(layer.localScale);
                layerRoot.AddComponent<
                    SpliceFxAuxiliaryLayerMarker>();
                var visualRoot = new GameObject("Visuals");
                visualRoot.transform.SetParent(
                    layerRoot.transform, false);
                var gate =
                    layerRoot.AddComponent<SpliceFxQualityGate>();
                gate.Configure(layer.quality, visualRoot);
                if (layer.motions != null &&
                    layer.motions.Count > 0)
                {
                    var motion =
                        visualRoot.AddComponent<
                            SpliceFxMotionPlayer>();
                    motion.ConfigureInline(layer.motions);
                }

                var transforms = new List<Transform>();
                var enabledStates = new List<bool>();
                var poses = SpliceFxInstanceLayoutSolver.Build(layout);
                if (poses.Count == 0)
                    poses.Add(new SpliceFxInstancePose(
                        Vector3.zero, Quaternion.identity,
                        Vector3.one));
                for (var i = 0; i < poses.Count; i++)
                {
                    var instance = new GameObject(
                        $"{layer.type}_{i + 1:00}");
                    instance.transform.SetParent(
                        visualRoot.transform, false);
                    var pose = poses[i];
                    instance.transform.localPosition = pose.Position;
                    instance.transform.localRotation = pose.Rotation;
                    instance.transform.localScale = pose.Scale;
                    if (layer.type ==
                        SpliceFxVisualLayerType.Trail)
                        ConfigureTrail(instance, layer, material);
                    else
                        ConfigureParticle(instance, layer, material);
                    instance.SetActive(pose.Enabled);
                    transforms.Add(instance.transform);
                    enabledStates.Add(pose.Enabled);
                }
                var group =
                    visualRoot.AddComponent<SpliceFxInstanceGroup>();
                group.ConfigureLayer(
                    layout, transforms, enabledStates);
            }
        }

        private static void ConfigureTrail(
            GameObject instance,
            SpliceFxVisualLayerDefinition layer,
            Material material)
        {
            var trail = instance.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = Mathf.Max(0.01f, layer.trailTime);
            trail.minVertexDistance =
                Mathf.Max(0.001f,
                    layer.trailMinVertexDistance);
            trail.widthMultiplier = 1f;
            trail.widthCurve = AnimationCurve.Linear(
                0f, Mathf.Max(0.001f, layer.trailStartWidth),
                1f, Mathf.Max(0f, layer.trailEndWidth));
            trail.colorGradient =
                MultiplyGradient(layer.color, layer.emission);
            trail.textureMode = layer.trailTextureMode;
            trail.alignment = layer.trailAlignment;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = LightProbeUsage.Off;
            trail.reflectionProbeUsage =
                ReflectionProbeUsage.Off;
            trail.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            trail.emitting = true;
            ApplyTexture(trail, layer.texture);
        }

        private static void ConfigureParticle(
            GameObject instance,
            SpliceFxVisualLayerDefinition layer,
            Material material)
        {
            var particle = instance.AddComponent<ParticleSystem>();
            var main = particle.main;
            main.playOnAwake = true;
            main.loop = layer.particleLoop;
            main.duration = Mathf.Max(
                0.05f, layer.particleLifetime);
            main.maxParticles = Mathf.Clamp(
                layer.particleMaxCount, 1, 2048);
            main.startLifetime =
                Mathf.Max(0.01f, layer.particleLifetime);
            main.startSpeed =
                Mathf.Max(0f, layer.particleSpeed);
            main.startSize =
                Mathf.Max(0.001f, layer.particleSize);
            main.startColor = Color.white;
            main.simulationSpace = layer.particleWorldSpace
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;

            var emission = particle.emission;
            if (layer.particleEmission ==
                SpliceFxParticleEmissionMode.Burst)
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(
                        0f, (short)Mathf.Clamp(
                            layer.particleBurstCount, 1, 512))
                });
            }
            else
            {
                emission.rateOverTime =
                    Mathf.Max(0f, layer.particleRate);
            }

            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = layer.particleShape switch
            {
                SpliceFxParticleShape.Circle =>
                    ParticleSystemShapeType.Circle,
                SpliceFxParticleShape.Cone =>
                    ParticleSystemShapeType.Cone,
                SpliceFxParticleShape.Box =>
                    ParticleSystemShapeType.Box,
                _ => ParticleSystemShapeType.Sphere
            };
            shape.radius =
                Mathf.Max(0f, layer.particleShapeRadius);

            var color = particle.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(
                MultiplyGradient(layer.color, layer.emission));

            var force = particle.forceOverLifetime;
            force.enabled =
                layer.particleGravity.sqrMagnitude > 0.0001f;
            if (force.enabled)
            {
                force.space = layer.particleWorldSpace
                    ? ParticleSystemSimulationSpace.World
                    : ParticleSystemSimulationSpace.Local;
                force.x = layer.particleGravity.x;
                force.y = layer.particleGravity.y;
                force.z = layer.particleGravity.z;
            }

            var renderer =
                instance.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode =
                ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            ApplyTexture(renderer, layer.texture);
            particle.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void ApplyTexture(
            Renderer renderer, Texture texture)
        {
            if (renderer == null || texture == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", texture);
            block.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(block);
        }

        private static Gradient MultiplyGradient(
            Gradient source, float multiplier)
        {
            source ??= new Gradient();
            var colors = source.colorKeys;
            for (var i = 0; i < colors.Length; i++)
                colors[i].color *= Mathf.Max(0f, multiplier);
            var result = new Gradient();
            result.SetKeys(colors, source.alphaKeys);
            return result;
        }

        private static Vector3 SanitizeScale(Vector3 value) =>
            new(Mathf.Max(0.001f, Mathf.Abs(value.x)),
                Mathf.Max(0.001f, Mathf.Abs(value.y)),
                Mathf.Max(0.001f, Mathf.Abs(value.z)));

        private static string Safe(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? "Visual"
                : SpliceFxPresetDefinition.SanitizeId(value);
    }
}
