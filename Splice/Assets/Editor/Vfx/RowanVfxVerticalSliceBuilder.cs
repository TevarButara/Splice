#if UNITY_EDITOR
using System;
using Splice.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.Editor.Vfx
{
    public static class RowanVfxVerticalSliceBuilder
    {
        private const string Root = "Assets/Prefabs/Natural/Heroes/1-Rowan";
        private const string VfxRoot = Root + "/VFX";
        private const string PrefabRoot = VfxRoot + "/Prefabs";
        private const string MaterialRoot = VfxRoot + "/Materials";
        private const string GraphRoot = VfxRoot + "/Graphs";
        private const string AdditiveShader = "Splice/VFX/URP Additive Intensify";
        private const string FlareTexture =
            "Assets/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/03_Texture/Flare.png";

        [MenuItem("Splice/VFX/Rebuild Rowan VFX Vertical Slice...", priority = 1800)]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Rowan VFX Vertical Slice",
                    "This updates generated Rowan VFX wrapper prefabs and staged ability references. Original source effects are preserved.",
                    "REBUILD", "CANCEL")) return;
            BuildWithoutPrompt();
            EditorUtility.DisplayDialog("Rowan VFX", "Vertical slice rebuilt and validated.", "OK");
        }

        public static string BuildWithoutPrompt()
        {
            EnsureFolder(VfxRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);
            var green = CreateMaterial("Rowan_Leaf_Green", new Color(0.18f, 1f, 0.32f, 0.9f));
            var teal = CreateMaterial("Rowan_Blink_Teal", new Color(0.08f, 0.9f, 0.78f, 0.9f));
            var gold = CreateMaterial("Rowan_Wild_Gold", new Color(1f, 0.62f, 0.12f, 0.92f));

            var burst = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                GraphRoot + "/Rowan_GPU_Burst.vfx");
            var loop = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                GraphRoot + "/Rowan_GPU_Loop.vfx");
            var trail = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                GraphRoot + "/Rowan_GPU_Trail.vfx");
            if (burst == null || loop == null || trail == null)
                throw new InvalidOperationException(
                    "Rowan VFX Graph assets are missing. Create Burst, Loop and Trail graphs first.");

            var normal = BuildPrefab("Rowan_Normal_Swing",
                Root + "/NormalAttack-Sword Trail.prefab", burst, green, false, false, 0.6f);
            var blinkCast = BuildPrefab("Rowan_Blink_Cast", null, burst, teal, false, false, 0.75f);
            var blinkTravel = BuildPrefab("Rowan_Blink_Travel",
                "Assets/Prefabs/===FX/vfx_Projectile_Blink.prefab", trail, teal, true, false, 0.55f);
            var blinkImpact = BuildPrefab("Rowan_Blink_Impact", null, burst, teal, false, false, 0.9f);
            var healCast = BuildPrefab("Rowan_Heal_Cast", null, burst, green, false, false, 0.8f);
            var healPersistent = BuildPrefab("Rowan_Heal_Persistent",
                "Assets/Prefabs/===FX/vfx_Heal_02.prefab", loop, green, false, true, 0.45f);
            var healEnd = BuildPrefab("Rowan_Heal_End", null, burst, gold, false, false, 0.65f);
            var skill1Launch = BuildPrefab("Rowan_Skill1_Launch",
                Root + "/skill1-FX_Orange_Slash_1.prefab", burst, gold, false, false, 0.65f);
            var skill1Travel = BuildPrefab("Rowan_Skill1_Travel", null, trail, green, true, false, 0.5f);
            var skill1Impact = BuildPrefab("Rowan_Skill1_Impact", null, burst, gold, false, false, 0.95f);
            var skill2Cast = BuildPrefab("Rowan_Skill2_Cast",
                Root + "/skill2-Sword Trail FIRE (360 Spiral).prefab", burst, gold, false, false, 0.55f);
            var skill2Persistent = BuildPrefab("Rowan_Skill2_Persistent", null, loop, green, false, true, 0.55f);
            var skill2End = BuildPrefab("Rowan_Skill2_End", null, burst, green, false, false, 0.85f);
            var skill3Cast = BuildPrefab("Rowan_Skill3_Cast", null, burst, gold, false, false, 0.75f);
            var skill3Persistent = BuildPrefab("Rowan_Skill3_Persistent",
                Root + "/skill3-Shield Leaves.prefab", loop, green, false, true, 0.5f);
            var skill3End = BuildPrefab("Rowan_Skill3_End", null, burst, gold, false, false, 1f);

            AssignNormal(normal);
            AssignAbility("Assets/Prefabs/Heroes/Universal/Universal_Blink.asset",
                ("castVfx", blinkCast, 0f, 0.35f, HeroAbilityEffectPlacement.HeroEffectAnchor, 0.05f, 0.25f),
                ("travelVfx", blinkTravel, 0f, 0.45f, HeroAbilityEffectPlacement.WorldPoint, 0.05f, 0.28f),
                ("impactVfx", blinkImpact, 0f, 0.65f, HeroAbilityEffectPlacement.WorldPoint, 0.05f, 0.25f));
            AssignAbility("Assets/Prefabs/Heroes/Universal/Universal_Heal.asset",
                ("castVfx", healCast, 0f, 0.45f, HeroAbilityEffectPlacement.HeroRoot, 0.05f, 0.25f),
                ("persistentVfx", healPersistent, 0f, 2f, HeroAbilityEffectPlacement.HeroRoot, 0.05f, 0.25f),
                ("endVfx", healEnd, 0f, 0.65f, HeroAbilityEffectPlacement.HeroRoot, 0.05f, 0.25f));
            AssignAbility(Root + "/Skill1-Leaf Slash.asset",
                ("launchVfx", skill1Launch, 0.05f, 0.65f, HeroAbilityEffectPlacement.HeroEffectAnchor, 0.05f, 0.25f),
                ("travelVfx", skill1Travel, 0.08f, 0.5f, HeroAbilityEffectPlacement.WorldPoint, 0.05f, 0.3f),
                ("impactVfx", skill1Impact, 0f, 0.75f, HeroAbilityEffectPlacement.GroundSurface, 0.08f, 0.25f));
            AssignAbility(Root + "/Skill2-Whirlbloom.asset",
                ("castVfx", skill2Cast, 0f, 1.1f, HeroAbilityEffectPlacement.HeroEffectAnchor, 0.05f, 0.25f),
                ("persistentVfx", skill2Persistent, 0f, 1.1f, HeroAbilityEffectPlacement.HeroRoot, 0.05f, 0.25f),
                ("endVfx", skill2End, 0f, 0.7f, HeroAbilityEffectPlacement.HeroRoot, 0.05f, 0.25f));
            AssignAbility(Root + "/Skill3-Wildblade Frenzy.asset",
                ("castVfx", skill3Cast, 0f, 0.55f, HeroAbilityEffectPlacement.HeroEffectAnchor, 0.05f, 0.25f),
                ("persistentVfx", skill3Persistent, 0f, 0f, HeroAbilityEffectPlacement.GroundSurface, 0.08f, 0.25f),
                ("endVfx", skill3End, 0f, 0.8f, HeroAbilityEffectPlacement.GroundSurface, 0.08f, 0.25f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Rowan VFX vertical slice: 16 prefabs, 3 graphs, 3 materials, 5 staged abilities.";
        }

        private static GameObject BuildPrefab(string name, string sourcePath,
            VisualEffectAsset graph, Material material, bool addTrail, bool looping,
            float scale)
        {
            var root = new GameObject(name);
            try
            {
                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (sourcePrefab == null)
                        throw new InvalidOperationException("Missing source VFX: " + sourcePath);
                    var source = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
                    source.name = "Legacy Art Layer";
                    source.transform.SetParent(root.transform, false);
                    CleanSource(source);
                }

                var gpu = new GameObject("VFX Graph Accent");
                gpu.transform.SetParent(root.transform, false);
                gpu.transform.localScale = Vector3.one * scale;
                var visual = gpu.AddComponent<VisualEffect>();
                visual.visualEffectAsset = graph;
                visual.resetSeedOnPlay = true;

                if (addTrail) CreateTrail(root.transform, material, scale);

                root.SetActive(false);
                var path = PrefabRoot + "/" + name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new InvalidOperationException("Could not save " + path);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateTrail(Transform parent, Material material, float scale)
        {
            var go = new GameObject("Motion Trail");
            go.transform.SetParent(parent, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.32f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.42f * scale, 1f, 0f);
            var tint = material.GetColor("_TintColor");
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(tint, 0.3f),
                    new GradientColorKey(tint, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
            trail.emitting = true;
        }

        private static void CleanSource(GameObject source)
        {
            foreach (var tf in source.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        tf.gameObject) > 0)
                    throw new InvalidOperationException(
                        "Source VFX contains a missing script: " + source.name);

            foreach (var behaviour in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var ns = behaviour.GetType().Namespace ?? string.Empty;
                var typeName = behaviour.GetType().Name;
                if (!ns.StartsWith("CartoonFX", StringComparison.Ordinal) &&
                    typeName.IndexOf("AutoDestroy",
                        StringComparison.OrdinalIgnoreCase) < 0) continue;
                behaviour.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            }

            var sourceParticles = source.GetComponentsInChildren<ParticleSystem>(true);
            const int sourceParticleBudget = 192;
            var enabledCount = Mathf.Min(sourceParticles.Length, sourceParticleBudget);
            var perSystemBudget = enabledCount > 0
                ? Mathf.Max(1, sourceParticleBudget / enabledCount)
                : 0;
            for (var i = 0; i < sourceParticles.Length; i++)
            {
                if (i >= sourceParticleBudget)
                {
                    sourceParticles[i].gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        sourceParticles[i].gameObject);
                    continue;
                }
                var main = sourceParticles[i].main;
                main.maxParticles = Mathf.Min(main.maxParticles, perSystemBudget);
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    sourceParticles[i]);
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(AdditiveShader);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + AdditiveShader);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor("_TintColor", color);
            material.SetFloat("_Glow", 1.8f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlareTexture);
            if (texture != null) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignNormal(GameObject normalPrefab)
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(
                Root + "/Rowan_Definition.asset");
            if (hero == null) throw new InvalidOperationException("Rowan definition is missing.");
            var so = new SerializedObject(hero);
            so.FindProperty("normalAttackEffectPrefab").objectReferenceValue = normalPrefab;
            so.FindProperty("normalAttackEffectLifetime").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hero);
        }

        private static void AssignAbility(string path,
            params (string field, GameObject prefab, float delay, float lifetime,
                HeroAbilityEffectPlacement placement, float groundOffset,
                float travelSeconds)[] cues)
        {
            var ability = AssetDatabase.LoadAssetAtPath<HeroAbilityDefinitionSO>(path);
            if (ability == null) throw new InvalidOperationException("Ability is missing: " + path);
            var so = new SerializedObject(ability);
            foreach (var cue in cues)
            {
                var property = so.FindProperty(cue.field);
                if (property == null)
                    throw new InvalidOperationException("Missing staged VFX field: " + cue.field);
                property.FindPropertyRelative("enabled").boolValue = true;
                property.FindPropertyRelative("prefab").objectReferenceValue = cue.prefab;
                property.FindPropertyRelative("delaySeconds").floatValue = cue.delay;
                property.FindPropertyRelative("lifetimeSeconds").floatValue = cue.lifetime;
                property.FindPropertyRelative("placement").enumValueIndex = (int)cue.placement;
                property.FindPropertyRelative("localOffset").vector3Value = Vector3.zero;
                property.FindPropertyRelative("groundOffset").floatValue = cue.groundOffset;
                property.FindPropertyRelative("orientToCastDirection").boolValue = true;
                property.FindPropertyRelative("travelDurationSeconds").floatValue =
                    cue.travelSeconds;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ability);
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
