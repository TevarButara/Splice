#if UNITY_EDITOR
using System;
using Splice.Combat;
using Splice.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace Splice.Editor.Vfx
{
    public static class RowanUltimateVfxBuilder
    {
        private const string Root = "Assets/Prefabs/Natural/Heroes/1-Rowan";
        private const string VfxRoot = Root + "/VFX";
        private const string PrefabRoot = VfxRoot + "/Prefabs/Ultimate";
        private const string MaterialRoot = VfxRoot + "/Materials/Ultimate";
        private const string GeneratedTextureRoot =
            VfxRoot + "/Textures/UltimateGenerated";
        private const string RuneCircleTexture =
            GeneratedTextureRoot + "/Rowan_Ultimate_RuneCircle.png";
        private const string SwordTexture =
            GeneratedTextureRoot + "/Rowan_Ultimate_Sword.png";
        private const string ImpactTexture =
            GeneratedTextureRoot + "/Rowan_Ultimate_ImpactX.png";
        private const string TrailTexture =
            GeneratedTextureRoot + "/Rowan_Ultimate_Trail.png";
        private const string ExecutionPath =
            Root + "/Rowan_Ultimate_MultiDash_Execution.asset";
        private const string AbilityPath = Root + "/Skill3-Wildblade Frenzy.asset";
        private const string UserImpactGraph =
            VfxRoot + "/Graphs/Rowan_Ultimate_v1.vfx";
        private const string LoopGraph = VfxRoot + "/Graphs/Rowan_GPU_Loop.vfx";
        private const string TrailGraph = VfxRoot + "/Graphs/Rowan_GPU_Trail.vfx";
        private const string BurstGraph = VfxRoot + "/Graphs/Rowan_GPU_Burst.vfx";
        private const string AdditiveShader = "Splice/VFX/URP Additive Intensify";
        private const string AlphaGlowShader = "Splice/VFX/URP Alpha Glow";
        private const string FlareTexture =
            "Assets/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/03_Texture/Flare.png";
        private const float UltimateCastRange = 24f;

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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureGeneratedTexture(RuneCircleTexture);
            ConfigureGeneratedTexture(SwordTexture);
            ConfigureGeneratedTexture(ImpactTexture);
            ConfigureGeneratedTexture(TrailTexture);

            RequireGraph(UserImpactGraph);
            var loopGraph = RequireGraph(LoopGraph);
            var trailGraph = RequireGraph(TrailGraph);
            var burstGraph = RequireGraph(BurstGraph);

            var orange = CreateMaterial(
                "Rowan_Ultimate_Orange",
                new Color(1f, 0.16f, 0.015f, 0.92f),
                1.8f,
                true);
            var yellow = CreateMaterial(
                "Rowan_Ultimate_Yellow",
                new Color(1f, 0.58f, 0.07f, 0.94f),
                1.8f,
                true);
            var red = CreateMaterial(
                "Rowan_Ultimate_Red",
                new Color(1f, 0.025f, 0.005f, 0.92f),
                2f,
                true);
            var orangeLine = CreateMaterial(
                "Rowan_Ultimate_Orange_Line",
                new Color(1f, 0.12f, 0.01f, 0.96f),
                1.8f,
                false);
            var yellowLine = CreateMaterial(
                "Rowan_Ultimate_Yellow_Line",
                new Color(1f, 0.3f, 0.03f, 0.98f),
                1.5f,
                false);
            var redLine = CreateMaterial(
                "Rowan_Ultimate_Red_Line",
                new Color(1f, 0.018f, 0.003f, 0.96f),
                2f,
                false);
            var runeDetail = CreateGeneratedMaterial(
                "Rowan_Ultimate_RuneCircle_Detail",
                RuneCircleTexture,
                false,
                new Color(1f, 0.62f, 0.24f, 0.78f),
                1.35f,
                0.82f);
            var runeGlow = CreateGeneratedMaterial(
                "Rowan_Ultimate_RuneCircle_Glow",
                RuneCircleTexture,
                true,
                new Color(1f, 0.18f, 0.015f, 0.34f),
                1.4f,
                0.34f);
            var swordDetail = CreateGeneratedMaterial(
                "Rowan_Ultimate_Sword_Detail",
                SwordTexture,
                false,
                new Color(1f, 0.68f, 0.3f, 0.9f),
                1.45f,
                0.9f);
            var swordGlow = CreateGeneratedMaterial(
                "Rowan_Ultimate_Sword_Glow",
                SwordTexture,
                true,
                new Color(1f, 0.16f, 0.01f, 0.45f),
                1.45f,
                0.45f);
            var impactDetail = CreateGeneratedMaterial(
                "Rowan_Ultimate_ImpactX_Detail",
                ImpactTexture,
                false,
                new Color(1f, 0.58f, 0.2f, 0.92f),
                1.7f,
                0.92f);
            var impactGlow = CreateGeneratedMaterial(
                "Rowan_Ultimate_ImpactX_Glow",
                ImpactTexture,
                true,
                new Color(1f, 0.08f, 0.005f, 0.7f),
                1.8f,
                0.7f);
            var trailRibbon = CreateGeneratedMaterial(
                "Rowan_Ultimate_Trail_Ribbon",
                TrailTexture,
                true,
                new Color(1f, 0.3f, 0.035f, 0.86f),
                1.7f,
                0.86f);

            var cast = BuildQualityPrefab(
                "Rowan_Ultimate_Cast_Ring",
                UltimateVfxMotionMode.Cast,
                5f,
                (parent, tier) => BuildCastVariant(
                    parent, tier, loopGraph, runeDetail, runeGlow,
                    swordDetail, swordGlow, orange));
            var launch = BuildQualityPrefab(
                "Rowan_Ultimate_Launch",
                UltimateVfxMotionMode.Launch,
                0.35f,
                (parent, tier) => BuildLaunchVariant(
                    parent, tier, burstGraph, orange, yellowLine));
            var travel = BuildQualityPrefab(
                "Rowan_Ultimate_Travel_Trail",
                UltimateVfxMotionMode.Travel,
                5f,
                (parent, tier) => BuildTravelVariant(
                    parent, tier, trailGraph, trailRibbon, yellowLine));
            var impact = BuildQualityPrefab(
                "Rowan_Ultimate_Impact_Cross",
                UltimateVfxMotionMode.Impact,
                0.5f,
                (parent, tier) => BuildImpactVariant(
                    parent, tier, burstGraph, impactDetail,
                    impactGlow, orange));
            var end = BuildQualityPrefab(
                "Rowan_Ultimate_End_Return",
                UltimateVfxMotionMode.End,
                0.8f,
                (parent, tier) => BuildEndVariant(
                    parent, tier, burstGraph, runeGlow, orange));

            var execution = CreateOrUpdateExecution();
            AssignAbility(execution, cast, launch, travel, impact, end);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Rowan Ultimate v1 rebuilt: multi-dash execution + 5 pooled VFX prefabs with High/Medium/Low variants.";
        }

        private static GameObject BuildQualityPrefab(
            string name,
            UltimateVfxMotionMode motionMode,
            float lifetimeSeconds,
            Action<Transform, VfxQualityTier> buildVariant)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<VfxRuntimeScale>();
                var low = CreateVariant(root.transform, "Low", VfxQualityTier.Low,
                    motionMode, lifetimeSeconds, buildVariant);
                var medium = CreateVariant(
                    root.transform, "Medium", VfxQualityTier.Medium,
                    motionMode, lifetimeSeconds, buildVariant);
                var high = CreateVariant(root.transform, "High", VfxQualityTier.High,
                    motionMode, lifetimeSeconds, buildVariant);
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
            UltimateVfxMotionMode motionMode,
            float lifetimeSeconds,
            Action<Transform, VfxQualityTier> build)
        {
            var variant = new GameObject(name);
            variant.transform.SetParent(parent, false);
            build(variant.transform, tier);
            variant.AddComponent<UltimateVfxMotion>().ConfigureEditor(
                motionMode,
                lifetimeSeconds,
                motionMode == UltimateVfxMotionMode.End ? -36f : 28f);
            return variant;
        }

        private static void BuildCastVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset loopGraph,
            Material runeDetail,
            Material runeGlow,
            Material swordDetail,
            Material swordGlow,
            Material orangeParticle)
        {
            CreateGroundQuad(
                parent,
                "Generated Rune Circle Detail",
                runeDetail,
                2f,
                0.035f,
                0);
            if (tier != VfxQualityTier.Low)
                CreateGroundQuad(
                    parent,
                    "Generated Rune Circle Glow",
                    runeGlow,
                    tier == VfxQualityTier.High ? 2.08f : 2.04f,
                    0.05f,
                    1);
            CreateFiveTexturedBlades(
                parent, tier, swordDetail, swordGlow);
            CreateParticleBurst(
                parent,
                "Orbit Sparks",
                tier == VfxQualityTier.High ? 88 :
                tier == VfxQualityTier.Medium ? 42 : 18,
                orangeParticle,
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
            Material orangeParticle,
            Material yellowLine)
        {
            CreateParticleBurst(
                parent,
                "Launch Sparks",
                tier == VfxQualityTier.High ? 58 :
                tier == VfxQualityTier.Medium ? 30 : 12,
                orangeParticle,
                false,
                0.3f,
                5.5f,
                0.12f);
            CreateCross(parent, "Launch Flash", yellowLine,
                tier == VfxQualityTier.Low ? 0.65f : 0.95f,
                Width(tier, 0.12f));
            if (tier == VfxQualityTier.High)
                CreateGraph(parent, "GPU Launch Burst", burstGraph, 0.9f);
        }

        private static void BuildTravelVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset trailGraph,
            Material trailRibbon,
            Material yellowLine)
        {
            CreateTrail(parent, "Generated Fury Trail", trailRibbon,
                tier == VfxQualityTier.High ? 0.95f :
                tier == VfxQualityTier.Medium ? 0.7f : 0.45f,
                tier == VfxQualityTier.High ? 0.42f : 0.3f);
            if (tier != VfxQualityTier.Low)
                CreateTrail(parent, "Yellow Core Trail", yellowLine,
                    tier == VfxQualityTier.High ? 0.38f : 0.24f,
                    0.2f);
            if (tier == VfxQualityTier.High)
                CreateGraph(parent, "GPU Dash Trail", trailGraph, 0.9f);
        }

        private static void BuildImpactVariant(
            Transform parent,
            VfxQualityTier tier,
            VisualEffectAsset impactGraph,
            Material impactDetail,
            Material impactGlow,
            Material orangeParticle,
            Material unused = null)
        {
            CreateGroundQuad(
                parent,
                "Generated Impact X Detail",
                impactDetail,
                tier == VfxQualityTier.Low ? 1.55f : 2.1f,
                0.13f,
                8);
            if (tier != VfxQualityTier.Low)
                CreateGroundQuad(
                    parent,
                    "Generated Impact X Glow",
                    impactGlow,
                    tier == VfxQualityTier.High ? 2.24f : 2.16f,
                    0.15f,
                    9);
            CreateParticleBurst(
                parent,
                "Impact Sparks",
                tier == VfxQualityTier.High ? 68 :
                tier == VfxQualityTier.Medium ? 34 : 14,
                orangeParticle,
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
            Material runeGlow,
            Material orangeParticle)
        {
            CreateGroundQuad(
                parent,
                "Return Rune Flash",
                runeGlow,
                tier == VfxQualityTier.Low ? 1.1f : 1.35f,
                0.08f,
                4);
            CreateParticleBurst(
                parent,
                "Return Embers",
                tier == VfxQualityTier.High ? 64 :
                tier == VfxQualityTier.Medium ? 32 : 14,
                orangeParticle,
                false,
                0.7f,
                2.2f,
                0.08f);
            if (tier == VfxQualityTier.High)
                CreateGraph(parent, "GPU Return Burst", burstGraph, 1f);
        }

        private static void CreateGroundQuad(
            Transform parent,
            string name,
            Material material,
            float size,
            float height,
            int sortingOrder)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
        }

        private static void CreateFiveTexturedBlades(
            Transform parent,
            VfxQualityTier tier,
            Material detail,
            Material glow)
        {
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.PI * 2f / 5f;
                var anchor = new GameObject("Generated Rune Sword " + (i + 1));
                anchor.transform.SetParent(parent, false);
                anchor.transform.localPosition =
                    new Vector3(Mathf.Cos(angle), 0.02f, Mathf.Sin(angle)) * 0.78f;
                anchor.transform.localRotation =
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
                CreateSwordPlane(anchor.transform, "Sword Face A", detail, 0f, 2);
                if (tier != VfxQualityTier.Low)
                    CreateSwordPlane(
                        anchor.transform, "Sword Face B", detail, 90f, 2);
                if (tier == VfxQualityTier.High)
                {
                    CreateSwordPlane(
                        anchor.transform, "Sword Glow A", glow, 0f, 3, 1.08f);
                    CreateSwordPlane(
                        anchor.transform, "Sword Glow B", glow, 90f, 3, 1.08f);
                }
            }
        }

        private static void CreateSwordPlane(
            Transform parent,
            string name,
            Material material,
            float yaw,
            int sortingOrder,
            float scale = 1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale =
                new Vector3(0.22f, 0.44f, 1f) * scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
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
                var crossguard = new GameObject("Crossguard");
                crossguard.transform.SetParent(blade.transform, false);
                var guardLine = crossguard.AddComponent<LineRenderer>();
                ConfigureLine(guardLine, core,
                    Width(tier, 0.045f),
                    new Vector3(-0.15f, 0.13f, 0f),
                    new Vector3(0.15f, 0.13f, 0f));
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

        private static void CreatePentagram(
            Transform parent,
            Material material,
            float width)
        {
            var go = new GameObject("Wildblade Pentagram");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 5;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            for (var i = 0; i < 5; i++)
            {
                var point = i * 2 % 5;
                var angle = point * Mathf.PI * 2f / 5f - Mathf.PI * 0.5f;
                line.SetPosition(i,
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.52f);
            }
        }

        private static void CreateRuneTicks(
            Transform parent,
            VfxQualityTier tier,
            Material orange,
            Material yellow)
        {
            var count = tier == VfxQualityTier.Low ? 10 :
                tier == VfxQualityTier.Medium ? 15 : 20;
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var go = new GameObject("Rune Tick " + (i + 1));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0.043f, 0f);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = i % 2 == 0 ? orange : yellow;
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.widthMultiplier = Width(tier, 0.012f);
                var direction = new Vector3(
                    Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                line.SetPosition(0, direction * 0.82f);
                line.SetPosition(1, direction *
                    (i % 5 == 0 ? 0.97f : 0.9f));
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
                    new GradientColorKey(
                        new Color(1f, 0.68f, 0.12f), 0f),
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
                    new GradientColorKey(
                        new Color(1f, 0.68f, 0.12f), 0f),
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
            so.FindProperty("castRange").floatValue = UltimateCastRange;
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
            float glow,
            bool useFlareTexture)
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
            material.SetFloat("_UseSoftParticles", useFlareTexture ? 1f : 0f);
            var texture = useFlareTexture
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(FlareTexture)
                : Texture2D.whiteTexture;
            if (texture != null)
                material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateGeneratedMaterial(
            string name,
            string texturePath,
            bool additive,
            Color tint,
            float brightness,
            float opacity)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
                throw new InvalidOperationException(
                    "Generated Rowan Ultimate texture is missing: " + texturePath);
            var shaderName = additive ? AdditiveShader : AlphaGlowShader;
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + shaderName);
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_MainTex", texture);
            material.SetColor("_TintColor", tint);
            if (additive)
            {
                material.SetFloat("_Glow", brightness);
                material.SetFloat("_UseSoftParticles", 0f);
            }
            else
            {
                material.SetFloat("_Brightness", brightness);
                material.SetFloat("_Opacity", opacity);
                material.SetFloat("_PulseSpeed", 4.2f);
                material.SetFloat("_PulseAmount", 0.08f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureGeneratedTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    "Generated Rowan Ultimate texture failed to import: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            ConfigureMobileTexture(importer, "Android");
            ConfigureMobileTexture(importer, "iPhone");
            importer.SaveAndReimport();
        }

        private static void ConfigureMobileTexture(
            TextureImporter importer,
            string platform)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
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
