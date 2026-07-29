using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Splice.FxStudio.Editor
{
    public sealed class SpliceFxStudioWindow : EditorWindow
    {
        private enum Tab
        {
            Create,
            SubFX,
            Blend,
            BindExport,
            Validate
        }

        private static readonly string[] TabLabels =
        {
            "Create",
            "SubFX Lab",
            "Blend Timeline",
            "Bind & Export",
            "Validate"
        };

        private Tab tab;
        private Vector2 scroll;
        private string newAssetName = "New Skill";
        private SpliceFxPresetRegistry registry;
        private SpliceFxPresetDefinition creationPreset;
        private SpliceFxSubEffectDefinition subFx;
        private SpliceFxBlendSequence sequence;
        private SpliceFxSkillPackage skillPackage;
        private UnityEngine.Object abilityAsset;

        [MenuItem("Splice/FX Studio/Open Studio", priority = 1700)]
        public static void Open()
        {
            var window = GetWindow<SpliceFxStudioWindow>();
            window.titleContent = new GUIContent("Splice FX Studio");
            window.minSize = new Vector2(720f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            registry =
                AssetDatabase.LoadAssetAtPath<SpliceFxPresetRegistry>(
                    SpliceFxStarterLibrary.RegistryPath);
        }

        private void OnDisable() => SpliceFxPreview.Stop();

        private void OnGUI()
        {
            DrawHeader();
            tab = (Tab)GUILayout.Toolbar((int)tab, TabLabels,
                GUILayout.Height(30f));
            EditorGUILayout.Space(6f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try
            {
                switch (tab)
                {
                    case Tab.Create:
                        DrawCreate();
                        break;
                    case Tab.SubFX:
                        DrawSubFx();
                        break;
                    case Tab.Blend:
                        DrawBlend();
                        break;
                    case Tab.BindExport:
                        DrawBindExport();
                        break;
                    case Tab.Validate:
                        DrawValidate();
                        break;
                }
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message,
                    MessageType.Error);
                Debug.LogException(exception);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.helpBox))
            {
                GUILayout.Label("SPLICE FX STUDIO",
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 17,
                        normal = { textColor = new Color(1f, .62f, .2f) }
                    });
                GUILayout.FlexibleSpace();
                GUILayout.Label("Preset → SubFX → Blend → Bind → Export",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawCreate()
        {
            Section("New FX Content");
            newAssetName = EditorGUILayout.TextField("Name", newAssetName);
            registry = (SpliceFxPresetRegistry)EditorGUILayout.ObjectField(
                "Preset Registry", registry,
                typeof(SpliceFxPresetRegistry), false);
            creationPreset =
                (SpliceFxPresetDefinition)EditorGUILayout.ObjectField(
                    "Starting Preset", creationPreset,
                    typeof(SpliceFxPresetDefinition), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create SubFX", GUILayout.Height(34f)))
                {
                    subFx = CreateAsset<SpliceFxSubEffectDefinition>(
                        "SubFX", newAssetName);
                    if (subFx != null)
                    {
                        subFx.displayName = newAssetName;
                        subFx.subFxId =
                            SpliceFxPresetDefinition.SanitizeId(newAssetName);
                        subFx.preset = creationPreset;
                        EditorUtility.SetDirty(subFx);
                        tab = Tab.SubFX;
                    }
                }
                if (GUILayout.Button("Create Blend",
                        GUILayout.Height(34f)))
                {
                    sequence = CreateAsset<SpliceFxBlendSequence>(
                        "Blend", newAssetName);
                    if (sequence != null)
                    {
                        sequence.displayName = newAssetName;
                        sequence.sequenceId =
                            SpliceFxPresetDefinition.SanitizeId(newAssetName);
                        EditorUtility.SetDirty(sequence);
                        tab = Tab.Blend;
                    }
                }
                if (GUILayout.Button("Create Skill FX Package",
                        GUILayout.Height(34f)))
                {
                    skillPackage = CreateAsset<SpliceFxSkillPackage>(
                        "SkillFx", newAssetName);
                    if (skillPackage != null)
                    {
                        skillPackage.displayName = newAssetName;
                        skillPackage.packageId =
                            SpliceFxPresetDefinition.SanitizeId(newAssetName);
                        AddDefaultStages(skillPackage);
                        EditorUtility.SetDirty(skillPackage);
                        tab = Tab.BindExport;
                    }
                }
            }

            EditorGUILayout.Space(12f);
            Section("Preset Library");
            if (registry == null)
            {
                EditorGUILayout.HelpBox(
                    "Starter Library has not been installed. Installation creates six editable presets and functional URP fallback templates without overwriting existing assets.",
                    MessageType.Info);
                if (GUILayout.Button("Install Starter Library",
                        GUILayout.Height(34f)))
                    registry = SpliceFxStarterLibrary.Install();
                return;
            }

            foreach (var preset in registry.presets)
            {
                if (preset == null) continue;
                using (new EditorGUILayout.HorizontalScope(
                           EditorStyles.helpBox))
                {
                    GUILayout.Label(preset.displayName,
                        EditorStyles.boldLabel, GUILayout.Width(210f));
                    GUILayout.Label(preset.family.ToString(),
                        GUILayout.Width(90f));
                    GUILayout.Label(
                        preset.templatePrefab != null
                            ? preset.templatePrefab.name
                            : "No template",
                        EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Use", GUILayout.Width(64f)))
                        creationPreset = preset;
                    if (GUILayout.Button("Edit", GUILayout.Width(64f)))
                    {
                        Selection.activeObject = preset;
                        EditorGUIUtility.PingObject(preset);
                    }
                }
            }
        }

        private void DrawSubFx()
        {
            Section("SubFX Asset");
            subFx = (SpliceFxSubEffectDefinition)EditorGUILayout.ObjectField(
                "SubFX", subFx,
                typeof(SpliceFxSubEffectDefinition), false);
            if (subFx == null)
            {
                EditorGUILayout.HelpBox(
                    "Choose a SubFX or create one in the Create tab.",
                    MessageType.Info);
                return;
            }

            DrawSerializedAsset(subFx);
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = subFx.sourceTexture != null;
                if (GUILayout.Button("Generate Processed Alpha",
                        GUILayout.Height(34f)))
                {
                    Undo.RecordObject(subFx, "Generate SubFX Alpha");
                    SpliceFxAlphaProcessor.GenerateTextureAsset(subFx);
                    Selection.activeObject = subFx.processedTexture;
                }
                GUI.enabled = subFx.EffectiveTemplate != null;
                if (GUILayout.Button("Export SubFX",
                        GUILayout.Height(34f)))
                {
                    var prefab = SpliceFxExporter.ExportSubFx(subFx);
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
                if (GUILayout.Button("Preview", GUILayout.Height(34f)))
                {
                    var prefab = SpliceFxExporter.ExportSubFx(subFx);
                    SpliceFxPreview.Show(prefab);
                }
                GUI.enabled = true;
                if (GUILayout.Button("Stop Preview",
                        GUILayout.Height(34f)))
                    SpliceFxPreview.Stop();
            }
            EditorGUILayout.HelpBox(
                "Alpha processing is non-destructive. Source images are never modified; mobile ASTC textures are generated below Assets/SpliceFXStudio/Generated.",
                MessageType.None);
        }

        private void DrawBlend()
        {
            Section("Blend Sequence");
            sequence =
                (SpliceFxBlendSequence)EditorGUILayout.ObjectField(
                    "Blend", sequence, typeof(SpliceFxBlendSequence), false);
            if (sequence == null)
            {
                EditorGUILayout.HelpBox(
                    "Choose a Blend Sequence or create one in the Create tab.",
                    MessageType.Info);
                return;
            }

            DrawTimeline(sequence);
            EditorGUILayout.Space(8f);
            subFx = (SpliceFxSubEffectDefinition)EditorGUILayout.ObjectField(
                "SubFX To Add", subFx,
                typeof(SpliceFxSubEffectDefinition), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = subFx != null;
                if (GUILayout.Button("Add SubFX Layer",
                        GUILayout.Height(30f)))
                {
                    Undo.RecordObject(sequence, "Add SubFX Layer");
                    sequence.clips.Add(new SpliceFxSequenceClip
                    {
                        label = subFx.displayName,
                        subFx = subFx,
                        startSeconds = sequence.DurationSeconds,
                        durationSeconds = subFx.lifetime,
                        quality = subFx.quality
                    });
                    EditorUtility.SetDirty(sequence);
                }
                GUI.enabled = true;
                if (GUILayout.Button("Export Blend",
                        GUILayout.Height(30f)))
                {
                    var prefab = SpliceFxExporter.ExportBlend(sequence);
                    Selection.activeObject = prefab;
                }
                if (GUILayout.Button("Preview Blend",
                        GUILayout.Height(30f)))
                    SpliceFxPreview.Show(
                        SpliceFxExporter.ExportBlend(sequence));
                if (GUILayout.Button("Stop Preview",
                        GUILayout.Height(30f)))
                    SpliceFxPreview.Stop();
            }
            DrawSerializedAsset(sequence);
        }

        private void DrawBindExport()
        {
            Section("Execution Stage Binding");
            skillPackage =
                (SpliceFxSkillPackage)EditorGUILayout.ObjectField(
                    "Skill FX Package", skillPackage,
                    typeof(SpliceFxSkillPackage), false);
            if (skillPackage == null)
            {
                EditorGUILayout.HelpBox(
                    "Choose a Skill FX Package or create one in the Create tab.",
                    MessageType.Info);
                return;
            }
            DrawSerializedAsset(skillPackage);

            EditorGUILayout.Space(8f);
            Section("Hero Ability");
            abilityAsset = EditorGUILayout.ObjectField(
                "Hero Ability Asset", abilityAsset,
                typeof(ScriptableObject), false);
            GUI.enabled = abilityAsset != null;
            if (GUILayout.Button("Bind Package To Hero Ability",
                    GUILayout.Height(32f)))
                BindToAbility(abilityAsset, skillPackage);
            GUI.enabled = true;

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Validate + Export All Stages",
                    GUILayout.Height(40f)))
            {
                var result = new SpliceFxValidationResult();
                SpliceFxValidator.ValidatePackage(skillPackage, result);
                if (result.ErrorCount > 0)
                {
                    SpliceFxValidator.Log(result);
                    EditorUtility.DisplayDialog("Splice FX Studio",
                        result.Summary() +
                        "\nFix package errors before export.", "OK");
                }
                else
                {
                    SpliceFxExporter.ExportSkillPackage(skillPackage);
                    EditorUtility.DisplayDialog("Splice FX Studio",
                        "Export complete. Generated prefabs are ready for the shared VFX Pool.",
                        "OK");
                }
            }
        }

        private static void DrawValidate()
        {
            Section("Mobile Content Validator");
            EditorGUILayout.HelpBox(
                "Checks stable IDs, missing templates, empty layers, duplicate stages, particle/renderer/VFX-component budgets, lifetime limits and unexported packages.",
                MessageType.Info);
            if (!GUILayout.Button("Validate Entire FX Library",
                    GUILayout.Height(40f)))
                return;
            var result = SpliceFxValidator.ValidateProject();
            SpliceFxValidator.Log(result);
            EditorUtility.DisplayDialog("Splice FX Studio",
                result.Summary(), "OK");
        }

        private static void DrawTimeline(SpliceFxBlendSequence value)
        {
            var duration = Mathf.Max(1f, value.DurationSeconds);
            var height = Mathf.Max(72f, value.clips.Count * 24f + 28f);
            var rect = GUILayoutUtility.GetRect(100f, height,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.075f, .09f, .13f));
            for (var second = 0; second <= Mathf.CeilToInt(duration);
                 second++)
            {
                var x = Mathf.Lerp(rect.x, rect.xMax, second / duration);
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height),
                    new Color(1f, 1f, 1f, .09f));
                GUI.Label(new Rect(x + 2f, rect.y, 42f, 18f),
                    second + "s", EditorStyles.miniLabel);
            }

            for (var i = 0; i < value.clips.Count; i++)
            {
                var clip = value.clips[i];
                if (clip == null) continue;
                var x = Mathf.Lerp(rect.x, rect.xMax,
                    clip.startSeconds / duration);
                var width = Mathf.Max(5f,
                    rect.width * clip.durationSeconds / duration);
                var row = new Rect(x, rect.y + 22f + i * 24f,
                    Mathf.Min(width, rect.xMax - x), 19f);
                EditorGUI.DrawRect(row,
                    Color.HSVToRGB((i * .137f) % 1f, .65f, .8f));
                GUI.Label(row,
                    string.IsNullOrWhiteSpace(clip.label)
                        ? $"Layer {i + 1}"
                        : clip.label,
                    EditorStyles.miniBoldLabel);
            }
        }

        private static void DrawSerializedAsset(UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            serialized.Update();
            var iterator = serialized.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
            if (serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(asset);
        }

        private static T CreateAsset<T>(string prefix, string name)
            where T : ScriptableObject
        {
            SpliceFxAlphaProcessor.EnsureAssetFolder(
                SpliceFxStarterLibrary.Root);
            SpliceFxAlphaProcessor.EnsureAssetFolder(
                SpliceFxStarterLibrary.Root + "/Authoring");
            var safe = SpliceFxPresetDefinition.SanitizeId(name);
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{SpliceFxStarterLibrary.Root}/Authoring/{prefix}_{safe}.asset");
            var asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        private static void AddDefaultStages(SpliceFxSkillPackage package)
        {
            package.stages = new List<SpliceFxStageBinding>();
            foreach (var stage in new[]
                     {
                         SpliceFxStage.Cast, SpliceFxStage.Launch,
                         SpliceFxStage.Travel, SpliceFxStage.Impact,
                         SpliceFxStage.Persistent, SpliceFxStage.End
                     })
                package.stages.Add(new SpliceFxStageBinding
                {
                    stage = stage,
                    placement = stage switch
                    {
                        SpliceFxStage.Cast => SpliceFxPlacement.GroundSurface,
                        SpliceFxStage.Travel =>
                            SpliceFxPlacement.HeroEffectAnchor,
                        SpliceFxStage.Impact =>
                            SpliceFxPlacement.WorldPoint,
                        _ => SpliceFxPlacement.WorldPoint
                    },
                    scaleMode = stage == SpliceFxStage.Cast
                        ? SpliceFxScaleMode.AbilityCastRange
                        : SpliceFxScaleMode.HeroRelative
                });
        }

        private static void BindToAbility(UnityEngine.Object ability,
            SpliceFxSkillPackage package)
        {
            var serialized = new SerializedObject(ability);
            var property = serialized.FindProperty("fxStudioPackage");
            if (property == null)
            {
                EditorUtility.DisplayDialog("Splice FX Studio",
                    "This asset does not expose an fxStudioPackage field. Use a Splice HeroAbilityDefinitionSO after the integration scripts compile.",
                    "OK");
                return;
            }
            Undo.RecordObject(ability, "Bind Skill FX Package");
            property.objectReferenceValue = package;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(ability);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(ability);
        }

        private static void Section(string label)
        {
            EditorGUILayout.LabelField(label,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13
                });
        }
    }

    internal static class SpliceFxPreview
    {
        private static GameObject instance;

        public static void Show(GameObject prefab)
        {
            Stop();
            if (prefab == null) return;
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "[Splice FX Preview]";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(
                PreviewPosition(), Quaternion.identity);
            instance.SetActive(true);
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        public static void Stop()
        {
            EditorApplication.update -= Repaint;
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
            SceneView.RepaintAll();
        }

        private static Vector3 PreviewPosition()
        {
            var view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        private static void Repaint()
        {
            if (instance == null)
            {
                Stop();
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
