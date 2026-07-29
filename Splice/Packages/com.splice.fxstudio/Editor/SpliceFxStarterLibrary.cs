using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Splice.FxStudio.Editor
{
    public static class SpliceFxStarterLibrary
    {
        public const string Root = "Assets/SpliceFXStudio";
        public const string RegistryPath =
            Root + "/Presets/SpliceFxPresetRegistry.asset";

        private sealed class Starter
        {
            public string id;
            public string name;
            public string description;
            public SpliceFxPresetFamily family;
            public int maxParticles;
            public int maxRenderers;
        }

        private static readonly Starter[] Starters =
        {
            new()
            {
                id = "ground_ring",
                name = "Ground Ring / Magic Circle",
                description =
                    "Flat ground decal for runes, targeting rings and persistent cast circles.",
                family = SpliceFxPresetFamily.Ground,
                maxParticles = 16,
                maxRenderers = 3
            },
            new()
            {
                id = "impact_burst",
                name = "Impact / Explosion",
                description =
                    "One-shot radial burst for hits, explosions and healing impacts.",
                family = SpliceFxPresetFamily.Burst,
                maxParticles = 192,
                maxRenderers = 6
            },
            new()
            {
                id = "dash_trail",
                name = "Dash Trail",
                description =
                    "World-space trail intended to follow a moving hero or projectile.",
                family = SpliceFxPresetFamily.Trail,
                maxParticles = 96,
                maxRenderers = 4
            },
            new()
            {
                id = "projectile",
                name = "Projectile",
                description =
                    "Compact looping core plus sparks for projectile travel.",
                family = SpliceFxPresetFamily.Projectile,
                maxParticles = 128,
                maxRenderers = 4
            },
            new()
            {
                id = "lightning_beam",
                name = "Beam / Lightning",
                description =
                    "Directional line template for beams and lightning textures.",
                family = SpliceFxPresetFamily.Beam,
                maxParticles = 64,
                maxRenderers = 4
            },
            new()
            {
                id = "orbiting_object",
                name = "Orbiting Objects",
                description =
                    "Circular emitter for swords, shards, leaves and other orbiting objects.",
                family = SpliceFxPresetFamily.Orbit,
                maxParticles = 128,
                maxRenderers = 5
            }
        };

        [MenuItem("Splice/FX Studio/Install Starter Library", priority = 1710)]
        public static void InstallFromMenu()
        {
            var registry = Install();
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
            EditorUtility.DisplayDialog("Splice FX Studio",
                "Starter Library is ready. Existing preset assets were preserved.",
                "OK");
        }

        public static SpliceFxPresetRegistry Install()
        {
            SpliceFxAlphaProcessor.EnsureAssetFolder(Root);
            SpliceFxAlphaProcessor.EnsureAssetFolder(Root + "/Presets");
            SpliceFxAlphaProcessor.EnsureAssetFolder(Root + "/Templates");
            SpliceFxAlphaProcessor.EnsureAssetFolder(Root + "/Materials");
            SpliceFxAlphaProcessor.EnsureAssetFolder(Root + "/Authoring");
            SpliceFxAlphaProcessor.EnsureAssetFolder(Root + "/Generated");

            var material = LoadOrCreateMaterial();
            var registry =
                AssetDatabase.LoadAssetAtPath<SpliceFxPresetRegistry>(
                    RegistryPath);
            if (registry == null)
            {
                registry =
                    ScriptableObject.CreateInstance<SpliceFxPresetRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            foreach (var starter in Starters)
            {
                var presetPath =
                    $"{Root}/Presets/Preset_{starter.id}.asset";
                var preset =
                    AssetDatabase.LoadAssetAtPath<SpliceFxPresetDefinition>(
                        presetPath);
                if (preset == null)
                {
                    preset =
                        ScriptableObject.CreateInstance<
                            SpliceFxPresetDefinition>();
                    preset.presetId = starter.id;
                    preset.displayName = starter.name;
                    preset.description = starter.description;
                    preset.family = starter.family;
                    preset.budget.maxParticles = starter.maxParticles;
                    preset.budget.maxRenderers = starter.maxRenderers;
                    preset.templatePrefab =
                        LoadOrCreateTemplate(starter, material);
                    AddDefaultSchemas(preset);
                    AssetDatabase.CreateAsset(preset, presetPath);
                }
                else if (preset.templatePrefab == null)
                {
                    preset.templatePrefab =
                        LoadOrCreateTemplate(starter, material);
                    EditorUtility.SetDirty(preset);
                }
                if (!registry.presets.Contains(preset))
                {
                    registry.presets.Add(preset);
                    EditorUtility.SetDirty(registry);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return registry;
        }

        private static Material LoadOrCreateMaterial()
        {
            var path = Root + "/Materials/M_FXStudio_Additive.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException(
                    "No compatible URP particle shader was found.");
            material = new Material(shader)
            {
                name = "M_FXStudio_Additive",
                renderQueue = (int)RenderQueue.Transparent
            };
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 1f);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject LoadOrCreateTemplate(Starter starter,
            Material material)
        {
            var path =
                $"{Root}/Templates/Template_{starter.id}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject($"Template_{starter.id}");
            try
            {
                switch (starter.family)
                {
                    case SpliceFxPresetFamily.Ground:
                        CreateGround(root, material);
                        break;
                    case SpliceFxPresetFamily.Trail:
                        CreateTrail(root, material);
                        break;
                    case SpliceFxPresetFamily.Beam:
                        CreateBeam(root, material);
                        break;
                    default:
                        CreateParticles(root, material, starter.family);
                        break;
                }
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateGround(GameObject root, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Ground Visual";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (quad.TryGetComponent<Collider>(out var collider))
                UnityEngine.Object.DestroyImmediate(collider);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateTrail(GameObject root, Material material)
        {
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.35f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.45f;
            trail.textureMode = LineTextureMode.Tile;
            trail.alignment = LineAlignment.TransformZ;
            trail.emitting = true;
        }

        private static void CreateBeam(GameObject root, Material material)
        {
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * 4f);
            line.widthMultiplier = 0.2f;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 4;
        }

        private static void CreateParticles(GameObject root,
            Material material, SpliceFxPresetFamily family)
        {
            var particle = root.AddComponent<ParticleSystem>();
            var main = particle.main;
            main.playOnAwake = true;
            main.loop = family != SpliceFxPresetFamily.Burst;
            main.duration = 1f;
            main.startLifetime = family == SpliceFxPresetFamily.Burst
                ? 0.45f
                : 0.8f;
            main.startSpeed = family == SpliceFxPresetFamily.Burst
                ? 5f
                : 0.5f;
            main.startSize = family == SpliceFxPresetFamily.Projectile
                ? 0.35f
                : 0.5f;
            main.maxParticles = family == SpliceFxPresetFamily.Burst
                ? 192
                : 128;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particle.emission;
            if (family == SpliceFxPresetFamily.Burst)
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, 48)
                });
            }
            else
                emission.rateOverTime =
                    family == SpliceFxPresetFamily.Orbit ? 20f : 32f;

            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = family == SpliceFxPresetFamily.Orbit
                ? ParticleSystemShapeType.Circle
                : ParticleSystemShapeType.Sphere;
            shape.radius = family == SpliceFxPresetFamily.Orbit ? 1.5f : 0.25f;

            var rotation = particle.rotationOverLifetime;
            rotation.enabled = family == SpliceFxPresetFamily.Orbit;
            rotation.y = 1.5f;

            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            particle.Stop(true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void AddDefaultSchemas(
            SpliceFxPresetDefinition preset)
        {
            preset.customProperties = new List<SpliceFxPropertySchema>
            {
                new()
                {
                    propertyName = "Dissolve",
                    displayName = "Dissolve",
                    propertyType = SpliceFxPropertyType.Float,
                    minimum = 0f,
                    maximum = 1f,
                    tooltip =
                        "Optional normalized dissolve amount exposed by custom graphs."
                },
                new()
                {
                    propertyName = "Direction",
                    displayName = "Direction",
                    propertyType = SpliceFxPropertyType.Vector3,
                    tooltip =
                        "Optional local travel or emission direction."
                },
                new()
                {
                    propertyName = "RandomSeed",
                    displayName = "Random Seed",
                    propertyType = SpliceFxPropertyType.Int,
                    tooltip =
                        "Stable seed used by deterministic/replay-safe graphs."
                }
            };
        }
    }
}
