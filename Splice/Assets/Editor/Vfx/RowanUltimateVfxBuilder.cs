#if UNITY_EDITOR
using System;
using Splice.Combat;
using Splice.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.Editor.Vfx
{
    public static class RowanUltimateVfxBuilder
    {
        private const string Root = "Assets/Prefabs/Natural/Heroes/1-Rowan";
        private const string VfxRoot = Root + "/VFX";
        private const string PrefabRoot = VfxRoot + "/Prefabs/Ultimate";
        private const string MaterialRoot = VfxRoot + "/Materials/Ultimate";
        private const string ExecutionPath =
            Root + "/Rowan_Ultimate_MultiDash_Execution.asset";
        private const string AbilityPath = Root + "/Skill3-Wildblade Frenzy.asset";
        private const string UserImpactGraph =
            VfxRoot + "/Graphs/Rowan_Ultimate_v1.vfx";
        private const string LoopGraph = VfxRoot + "/Graphs/Rowan_GPU_Loop.vfx";
        private const string TrailGraph = VfxRoot + "/Graphs/Rowan_GPU_Trail.vfx";
        private const string BurstGraph = VfxRoot + "/Graphs/Rowan_GPU_Burst.vfx";
        private const string AdditiveShader = "Splice/VFX/URP Additive Intensify";
        private const string FlareTexture =
            "Assets/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/03_Texture/Flare.png";

        [MenuItem("Splice/VFX/Rebuild Rowan Ultimate v1...", priority = 1801)]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Rowan Ultimate v1",
                    "This rebuilds only generated Rowan Ultimate wrappers, quality variants and its execution asset. Rowan_Ultimate_v1.vfx and your source textures are preserved.",
                    "REBUILD ULTIMATE",
                    "CANCEL"))
                return;
            var result = BuildWithoutPrompt();
            EditorUtility.DisplayDialog("Rowan Ultimate", result, "OK");
        }

        public static string BuildWithoutPrompt()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);

            var impactGraph = RequireGraph(UserImpactGraph);
            var loopGraph = RequireGraph(LoopGraph);
            var trailGraph = RequireGraph(TrailGraph);
            var burstGraph = RequireGraph(BurstGraph);

            var orange = CreateMaterial(
                "Rowan_Ultimate_Orange",
                new Color(1f, 0.34f, 0.025f, 0.96f),
                2.5f);
            var yellow = CreateMaterial(
                "Rowan_Ultimate_Yellow",
                new Color(1f, 0.9f, 0.18f, 0.98f),
                2.8f);
            var red = CreateMaterial(
                "Rowan_Ultimate_Red",
                new Color(1f, 0.08f, 0.015f, 0.96f),
                2.7f);

            var cast = BuildQualityPrefab(
                "Rowan_Ultimate_Cast_Ring",
                (parent, tier) => BuildCastVariant(
                    parent, tier, loopGraph, orange, yellow));
            var launch = BuildQualityPrefab(
                "Rowan_Ultimate_Launch",
                (parent, tier) => BuildLaunchVariant(
                    parent, tier, burstGraph, orange, yellow));
            var travel = BuildQualityPrefab(
                "Rowan_Ultimate_Travel_Trail",
                (parent, tier) => BuildTravelVariant(
                    parent, tier, trailGraph, orange, yellow));
            var impact = BuildQualityPrefab(
                "Rowan_Ultimate_Impact_Cross",
                (parent, tier) => BuildImpactVariant(
                    parent, tier, impactGraph, red, orange, yellow));
            var end = BuildQualityPrefab(
                "Rowan_Ultimate_End_Return",
                (parent, tier) => BuildEndVariant(
                    parent, tier, burstGraph, orange, yellow));

            var execution = CreateOrUpdateExecution();
            AssignAbility(execution, cast, launch, travel, impact, end);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Rowan Ultimate v1 rebuilt: multi-dash execution + 5 pooled VFX prefabs with High/Medium/Low variants.";
        }

        private static GameObject BuildQualityPrefab(
            string name,
            Action<Transform, VfxQualityTier> buildVariant)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<VfxRuntimeScale>();
                var low = CreateVariant(root.transform, "Low", VfxQualityTier.Low, buildVariant);
                var medium = CreateVariant(
                    root.transform, "Medium", VfxQualityTier.Medium, buildVariant);
                var high = CreateVariant(root.transform, "High", VfxQualityTier.High, buildVariant);
                root.AddComponent<VfxQualityTierController>().Configure(low, medium, high);
                root.SetActive(false);
                var path = PrefabRoot + "/" + name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null)
                    throw new InvalidOperationException("Could not save " + path);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateVariant(
            Transform parent,
            string name,
            VfxQualityTier tier,
            Action<Transform, VfxQualityTier> build)
        {
            var variant = new GameObject(name);
            variant.transform.SetParent(parent, false);
            build(variant.transform, tier);
            return variant;
        }

        private static void BuildCastVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset loopGraph,
            Material orange,
            Material yellow)
        {
            CreateCircle(parent, "Outer Magic Circle", orange, 1f, Width(tier, 0.04f));
            CreateCircle(parent, "Inner Magic Circle", yellow, 0.74f, Width(tier, 0.025f));
            CreateFiveBlades(parent, tier, yellow, orange);
            CreateParticleBurst(
                parent,
                "Orbit Sparks",
                tier == VfxQualityTier.High ? 88 :
                tier == VfxQualityTier.Medium ? 42 : 18,
                orange,
                true,
                1f,
                0.55f,
                0.045f);
            if (tier != VfxQualityTier.Low)
                CreateGraph(parent, "GPU Rune Accent", loopGraph,
                    tier == VfxQualityTier.High ? 1f : 0.72f);
        }

        private static void BuildLaunchVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset burstGraph,
            Material orange,
            Material yellow)
        {
            CreateParticleBurst(
                parent,
                "Launch Sparks",
                tier == VfxQualityTier.High ? 58 :
                tier == VfxQualityTier.Medium ? 30 : 12,
                orange,
                false,
                0.3f,
                5.5f,
                0.12f);
            CreateCross(parent, "Launch Flash", yellow,
                tier == VfxQualityTier.Low ? 0.65f : 0.95f,
                Width(tier, 0.12f));
            if (tier == VfxQualityTier.High)
                CreateGraph(parent, "GPU Launch Burst", burstGraph, 0.9f);
        }

        private static void BuildTravelVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset trailGraph,
            Material orange,
            Material yellow)
        {
            CreateTrail(parent, "Orange Fury Trail", orange,
                tier == VfxQualityTier.High ? 0.95f :
                tier == VfxQualityTier.Medium ? 0.7f : 0.45f,
                tier == VfxQualityTier.High ? 0.42f : 0.3f);
            if (tier != VfxQualityTier.Low)
                CreateTrail(parent, "Yellow Core Trail", yellow,
                    tier == VfxQualityTier.High ? 0.38f : 0.24f,
                    0.2f);
            if (tier == VfxQualityTier.High)
                CreateGraph(parent, "GPU Dash Trail", trailGraph, 0.9f);
        }

        private static void BuildImpactVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset impactGraph,
            Material red,
            Material orange,
            Material yellow)
        {
            CreateCross(parent, "Red Orange Cross", red,
                tier == VfxQualityTier.Low ? 0.75f : 1.15f,
                Width(tier, 0.16f));
            CreateCross(parent, "Yellow Cross Core", yellow,
                tier == VfxQualityTier.High ? 0.78f : 0.6f,
                Width(tier, 0.07f));
            CreateParticleBurst(
                parent,
                "Impact Sparks",
                tier == VfxQualityTier.High ? 68 :
                tier == VfxQualityTier.Medium ? 34 : 14,
                orange,
                false,
                0.45f,
                7.5f,
                0.11f);
            if (tier != VfxQualityTier.Low)
                CreateGraph(parent, "Cross Of Death Graph", impactGraph,
                    tier == VfxQualityTier.High ? 1.15f : 0.82f);
        }

        private static void BuildEndVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset burstGraph,
            Material orange,
            Material yellow)
        {
            CreateCircle(parent, "Contracting Outer Circle", orange,
                1f, Width(tier, 0.055f));
            CreateCircle(parent, "Contracting Core Circle", yellow,
                0.6f, Width(tier, 0.03f));
            CreateParticleBurst(
                parent,
                "Return Embers",
                tier == VfxQualityTier.High ? 64 :
                tier == VfxQualityTier.Medium ? 32 : 14,
                orange,
                false,
                0.7f,
                2.2f,
                0.08f);
            if (tier == VfxQualityTier.High)
                CreateGraph(parent, "GPU Return Burst", burstGraph, 1f);
        }

        private static void CreateFiveBlades(
            Transform parent,
            VfxQualityTier tier,
            Material core,
            Material glow)
        {
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.PI * 2f / 5f;
                var blade = new GameObject("Rune Blade " + (i + 1));
                blade.transform.SetParent(parent, false);
                blade.transform.localPosition =
                    new Vector3(Mathf.Cos(angle), 0.04f, Mathf.Sin(angle)) * 0.9f;
                blade.transform.localRotation =
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, -12f);
                var glowLine = blade.AddComponent<LineRenderer>();
                ConfigureLine(glowLine, glow,
                    Width(tier, 0.11f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0.58f, 0f));
                if (tier == VfxQualityTier.Low) continue;
                var coreObject = new GameObject("Blade Core");
                coreObject.transform.SetParent(blade.transform, false);
                var coreLine = coreObject.AddComponent<LineRenderer>();
                ConfigureLine(coreLine, core,
                    Width(tier, 0.035f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0.62f, 0f));
            }
        }

        private static void CreateCircle(
            Transform parent,
            string name,
            Material material,
            float radius,
            float width)
        {
            const int segments = 64;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i,
                    new Vector3(Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius));
            }
        }

        private static void CreateCross(
            Transform parent,
            string name,
            Material material,
            float size,
            float width)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCrossStroke(root.transform, material, size, width, 45f);
            CreateCrossStroke(root.transform, material, size, width, -45f);
        }

        private static void CreateCrossStroke(
            Transform parent,
            Material material,
            float size,
            float width,
            float angle)
        {
            var go = new GameObject("Slash");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var line = go.AddComponent<LineRenderer>();
            ConfigureLine(line, material, width,
                new Vector3(-size, 0f, 0f),
                new Vector3(size, 0f, 0f));
        }

        private static void ConfigureLine(
            LineRenderer line,
            Material material,
            float width,
            Vector3 from,
            Vector3 to)
        {
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private static void CreateTrail(
            Transform parent,
            string name,
            Material material,
            float width,
            float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = lifetime;
            trail.minVertexDistance = 0.03f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, width, 1f, 0f);
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(material.GetColor("_TintColor"), 0.3f),
                    new GradientColorKey(material.GetColor("_TintColor"), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
            trail.emitting = true;
        }

        private static void CreateParticleBurst(
            Transform parent,
            string name,
            int maxParticles,
            Material material,
            bool loop,
            float lifetime,
            float speed,
            float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particle = go.AddComponent<ParticleSystem>();
            var main = particle.main;
            main.loop = loop;
            main.playOnAwake = true;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = material.GetColor("_TintColor");
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particle.emission;
            emission.rateOverTime = loop ? Mathf.Max(4f, maxParticles * 0.35f) : 0f;
            if (!loop)
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)Mathf.Max(1, maxParticles))
                });
            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = loop
                ? ParticleSystemShapeType.Circle
                : ParticleSystemShapeType.Sphere;
            shape.radius = loop ? 0.92f : 0.18f;
            shape.radiusThickness = loop ? 0.05f : 1f;
            var color = particle.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(material.GetColor("_TintColor"), 0.35f),
                    new GradientColorKey(material.GetColor("_TintColor"), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                }
            });
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private static void CreateGraph(
            Transform parent,
            string name,
            VisualEffectAsset graph,
            float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var visual = go.AddComponent<VisualEffect>();
            visual.visualEffectAsset = graph;
            visual.resetSeedOnPlay = true;
        }

        private static MultiDashHeroAbilityExecutionSO CreateOrUpdateExecution()
        {
            var execution = AssetDatabase.LoadAssetAtPath<
                MultiDashHeroAbilityExecutionSO>(ExecutionPath);
            if (execution == null)
            {
                execution = ScriptableObject.CreateInstance<
                    MultiDashHeroAbilityExecutionSO>();
                execution.name = "Rowan Ultimate Multi Dash Execution";
                AssetDatabase.CreateAsset(execution, ExecutionPath);
            }
            execution.strikeCount = 7;
            execution.dashSpeed = 28f;
            execution.targetOvershootDistance = 1.6f;
            execution.impactHoldSeconds = 0.08f;
            execution.impactVfxLifetimeSeconds = 0.5f;
            execution.returnSpeed = 32f;
            execution.randomizeMultipleTargets = true;
            EditorUtility.SetDirty(execution);
            return execution;
        }

        private static void AssignAbility(
            MultiDashHeroAbilityExecutionSO execution,
            GameObject cast,
            GameObject launch,
            GameObject travel,
            GameObject impact,
            GameObject end)
        {
            var ability = AssetDatabase.LoadAssetAtPath<HeroAbilityDefinitionSO>(
                AbilityPath);
            if (ability == null)
                throw new InvalidOperationException(
                    "Rowan Skill 3 ability is missing: " + AbilityPath);
            var so = new SerializedObject(ability);
            so.FindProperty("castType").enumValueIndex =
                (int)HeroAbilityCastType.LockedTarget;
            so.FindProperty("targeting").enumValueIndex =
                (int)HeroAbilityTargeting.TargetPoint;
            so.FindProperty("damageMode").enumValueIndex =
                (int)HeroAbilityDamageMode.Instant;
            so.FindProperty("execution").objectReferenceValue = execution;
            so.FindProperty("animationState").stringValue = "Skill3";
            AssignCue(so.FindProperty("castVfx"), cast,
                0f, 5f, HeroAbilityEffectPlacement.GroundSurface);
            AssignCue(so.FindProperty("launchVfx"), launch,
                0f, 0.35f, HeroAbilityEffectPlacement.HeroEffectAnchor);
            AssignCue(so.FindProperty("travelVfx"), travel,
                0f, 5f, HeroAbilityEffectPlacement.HeroEffectAnchor);
            AssignCue(so.FindProperty("impactVfx"), impact,
                0f, 0.5f, HeroAbilityEffectPlacement.WorldPoint);
            AssignCue(so.FindProperty("endVfx"), end,
                0f, 0.8f, HeroAbilityEffectPlacement.GroundSurface);
            var persistent = so.FindProperty("persistentVfx");
            if (persistent != null)
            {
                persistent.FindPropertyRelative("enabled").boolValue = false;
                persistent.FindPropertyRelative("prefab").objectReferenceValue = null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ability);
        }

        private static void AssignCue(
            SerializedProperty cue,
            GameObject prefab,
            float delay,
            float lifetime,
            HeroAbilityEffectPlacement placement)
        {
            if (cue == null)
                throw new InvalidOperationException("Ultimate VFX cue field is missing.");
            cue.FindPropertyRelative("enabled").boolValue = true;
            cue.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            cue.FindPropertyRelative("delaySeconds").floatValue = delay;
            cue.FindPropertyRelative("lifetimeSeconds").floatValue = lifetime;
            cue.FindPropertyRelative("placement").enumValueIndex = (int)placement;
            cue.FindPropertyRelative("localOffset").vector3Value = Vector3.zero;
            cue.FindPropertyRelative("groundOffset").floatValue = 0.06f;
            cue.FindPropertyRelative("orientToCastDirection").boolValue = true;
            cue.FindPropertyRelative("travelDurationSeconds").floatValue = 0.2f;
        }

        private static VisualEffectAsset RequireGraph(string path)
        {
            var graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (graph == null)
                throw new InvalidOperationException(
                    "Required Rowan Visual Effect Graph is missing or failed to import: " +
                    path);
            return graph;
        }

        private static Material CreateMaterial(
            string name,
            Color color,
            float glow)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(AdditiveShader);
            if (shader == null)
                throw new InvalidOperationException(
                    "Missing shader: " + AdditiveShader);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            material.SetColor("_TintColor", color);
            material.SetFloat("_Glow", glow);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlareTexture);
            if (texture != null) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static float Width(VfxQualityTier tier, float high)
        {
            return tier switch
            {
                VfxQualityTier.High => high,
                VfxQualityTier.Medium => high * 0.78f,
                _ => high * 0.58f
            };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            var name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
