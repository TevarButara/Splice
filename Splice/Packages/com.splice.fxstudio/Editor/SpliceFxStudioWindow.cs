using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Splice.FxStudio.Editor
{
    public sealed class SpliceFxStudioWindow : EditorWindow
    {
        private const string SettingsPaneWidthKey =
            "Splice.FxStudio.SettingsPaneWidth";
        private const float SplitterWidth = 8f;
        private const float MinimumSettingsPaneWidth = 420f;
        private const float MinimumPreviewPaneWidth = 310f;

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
        private SpliceFxPreviewViewport previewViewport;
        private int previewStageIndex;
        private SpliceFxMotionType motionToAdd =
            SpliceFxMotionType.Spin;
        private float settingsPaneWidth;
        private bool draggingSplitter;

        [MenuItem("Splice/FX Studio/Open Studio", priority = 1700)]
        public static void Open()
        {
            var window = GetWindow<SpliceFxStudioWindow>();
            window.titleContent = new GUIContent("Splice FX Studio");
            window.minSize = new Vector2(1040f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            registry =
                AssetDatabase.LoadAssetAtPath<SpliceFxPresetRegistry>(
                    SpliceFxStarterLibrary.RegistryPath);
            previewViewport ??= new SpliceFxPreviewViewport(Repaint);
            settingsPaneWidth = EditorPrefs.GetFloat(
                SettingsPaneWidthKey, 610f);
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(
                SettingsPaneWidthKey, settingsPaneWidth);
            previewViewport?.Dispose();
            previewViewport = null;
        }

        private void OnGUI()
        {
            DrawHeader();
            tab = (Tab)GUILayout.Toolbar((int)tab, TabLabels,
                GUILayout.Height(30f));
            EditorGUILayout.Space(6f);
            settingsPaneWidth = ClampSettingsPaneWidth(
                settingsPaneWidth, position.width);
            var previewPaneWidth = Mathf.Max(
                MinimumPreviewPaneWidth,
                position.width - settingsPaneWidth -
                SplitterWidth - 6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(
                           GUILayout.Width(settingsPaneWidth)))
                {
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
                DrawPaneSplitter();
                DrawPreviewPanel(previewPaneWidth);
            }
        }

        internal static float ClampSettingsPaneWidth(
            float value, float windowWidth)
        {
            var maximum = Mathf.Max(
                MinimumSettingsPaneWidth,
                windowWidth - MinimumPreviewPaneWidth -
                SplitterWidth - 6f);
            return Mathf.Clamp(value,
                MinimumSettingsPaneWidth, maximum);
        }

        private void DrawPaneSplitter()
        {
            var rect = GUILayoutUtility.GetRect(
                SplitterWidth, 1f,
                GUILayout.Width(SplitterWidth),
                GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(
                rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(
                    new Rect(rect.center.x - 1f, rect.y,
                        2f, rect.height),
                    draggingSplitter
                        ? new Color(1f, 0.55f, 0.12f, 0.95f)
                        : new Color(0.25f, 0.58f, 0.75f, 0.65f));

            var current = Event.current;
            if (current.type == EventType.MouseDown &&
                current.button == 0 && rect.Contains(
                    current.mousePosition))
            {
                if (current.clickCount == 2)
                {
                    settingsPaneWidth = ClampSettingsPaneWidth(
                        position.width * 0.58f, position.width);
                    EditorPrefs.SetFloat(
                        SettingsPaneWidthKey, settingsPaneWidth);
                }
                else
                {
                    draggingSplitter = true;
                }
                current.Use();
                Repaint();
                return;
            }
            if (draggingSplitter &&
                current.type == EventType.MouseDrag &&
                current.button == 0)
            {
                settingsPaneWidth = ClampSettingsPaneWidth(
                    settingsPaneWidth + current.delta.x,
                    position.width);
                current.Use();
                Repaint();
                return;
            }
            if (!draggingSplitter ||
                current.rawType != EventType.MouseUp) return;
            draggingSplitter = false;
            EditorPrefs.SetFloat(
                SettingsPaneWidthKey, settingsPaneWidth);
            current.Use();
            Repaint();
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
                            CreateUniqueId<SpliceFxSubEffectDefinition>(
                                newAssetName, subFx,
                                item => item.subFxId);
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
                            CreateUniqueId<SpliceFxBlendSequence>(
                                newAssetName, sequence,
                                item => item.sequenceId);
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
                            CreateUniqueId<SpliceFxSkillPackage>(
                                newAssetName, skillPackage,
                                item => item.packageId);
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
                    "Starter Library has not been installed. Installation creates editable presets and functional URP fallback templates without overwriting existing assets.",
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

            DrawSerializedAsset(subFx, "motions", "motionModifiers",
                "instanceLayout", "visualLayers",
                "sourceSprite", "sourceTexture",
                "processedTexture",
                "mainColor", "gradientMode", "mainGradient",
                "reverseGradient", "strokeMode", "strokeColor",
                "strokeWidth", "strokeDashFrequency",
                "outerGlowEnabled", "outerGlowColor",
                "outerGlowIntensity", "outerGlowRadius",
                "outerGlowSoftness");
            DrawSourceImageInput(subFx);
            DrawCommonVisualAppearance(subFx);
            DrawInstanceLayout(subFx);
            DrawVisualLayers(subFx);
            DrawMotionStack(subFx);
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled =
                    subFx.SourceTextureForProcessing != null;
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
                if (GUILayout.Button("Replay Preview",
                        GUILayout.Height(34f)))
                    previewViewport?.Replay();
                GUI.enabled = true;
            }
            EditorGUILayout.HelpBox(
                "Alpha processing is non-destructive. Source images are never modified; mobile ASTC textures are generated below Assets/SpliceFXStudio/Generated.",
                MessageType.None);
        }

        private static void DrawSourceImageInput(
            SpliceFxSubEffectDefinition value)
        {
            EditorGUILayout.Space(8f);
            Section("Source Image");
            EditorGUILayout.HelpBox(
                "Assign either a Sprite imported as Sprite (2D and UI), including a sub-sprite, or a regular Texture2D. Sprite takes priority and its atlas rectangle is preserved in Preview and Export.",
                MessageType.Info);

            var nextSprite = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Sprite (2D and UI)"),
                value.sourceSprite, typeof(Sprite), false);
            if (nextSprite != value.sourceSprite)
            {
                Undo.RecordObject(value, "Change FX Source Sprite");
                value.sourceSprite = nextSprite;
                if (nextSprite != null)
                    value.sourceTexture = null;
                value.processedTexture = null;
                EditorUtility.SetDirty(value);
            }

            using (new EditorGUI.DisabledScope(
                       value.sourceSprite != null))
            {
                var nextTexture =
                    (Texture2D)EditorGUILayout.ObjectField(
                        new GUIContent("Texture2D"),
                        value.sourceTexture,
                        typeof(Texture2D), false);
                if (nextTexture != value.sourceTexture)
                {
                    Undo.RecordObject(value,
                        "Change FX Source Texture");
                    value.sourceTexture = nextTexture;
                    if (nextTexture != null)
                        value.sourceSprite = null;
                    value.processedTexture = null;
                    EditorUtility.SetDirty(value);
                }
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(
                    new GUIContent("Processed Output"),
                    value.processedTexture,
                    typeof(Texture2D), false);

            if (value.sourceSprite != null)
            {
                var size = value.EffectivePixelSize;
                EditorGUILayout.LabelField(
                    $"Active Sprite Region: {size.x} × {size.y}px",
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawCommonVisualAppearance(
            SpliceFxSubEffectDefinition value)
        {
            EditorGUILayout.Space(8f);
            Section("Color / Gradient");
            var serialized = new SerializedObject(value);
            serialized.Update();
            var mode = serialized.FindProperty("gradientMode");
            EditorGUILayout.PropertyField(mode,
                new GUIContent("Color Mode"));
            var gradientMode =
                (SpliceFxGradientMode)mode.enumValueIndex;
            if (gradientMode == SpliceFxGradientMode.Solid)
            {
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("mainColor"),
                    new GUIContent("Main Color"));
                EditorGUILayout.HelpBox(
                    "Solid uses the source image multiplied by Main Color.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("mainGradient"),
                    new GUIContent("Main Gradient"));
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("reverseGradient"),
                    new GUIContent("Reverse Gradient"));
                EditorGUILayout.HelpBox(
                    "Gradient replaces Main Color. The image alpha and a small amount of its brightness detail are preserved.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Stroke / Outline",
                EditorStyles.miniBoldLabel);
            var strokeMode =
                serialized.FindProperty("strokeMode");
            EditorGUILayout.PropertyField(strokeMode,
                new GUIContent("Stroke Mode"));
            if ((SpliceFxStrokeMode)strokeMode.enumValueIndex !=
                SpliceFxStrokeMode.None)
            {
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("strokeColor"),
                    new GUIContent("Stroke Color"));
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("strokeWidth"),
                    new GUIContent("Stroke Width (Pixels)"));
                if ((SpliceFxStrokeMode)strokeMode.enumValueIndex ==
                    SpliceFxStrokeMode.Dashed)
                    EditorGUILayout.PropertyField(
                        serialized.FindProperty(
                            "strokeDashFrequency"),
                        new GUIContent("Dash Frequency"));
                EditorGUILayout.HelpBox(
                    "Stroke is generated from the image alpha. Leave transparent padding around the image for a clear outer outline.",
                    MessageType.None);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Outer Glow",
                EditorStyles.miniBoldLabel);
            var outerGlow =
                serialized.FindProperty("outerGlowEnabled");
            EditorGUILayout.PropertyField(outerGlow,
                new GUIContent("Enable Outer Glow"));
            if (outerGlow.boolValue)
            {
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("outerGlowColor"),
                    new GUIContent("Glow Color"));
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("outerGlowIntensity"),
                    new GUIContent("Glow Intensity"));
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("outerGlowRadius"),
                    new GUIContent("Glow Radius (Pixels)"));
                EditorGUILayout.PropertyField(
                    serialized.FindProperty("outerGlowSoftness"),
                    new GUIContent("Glow Softness"));
                EditorGUILayout.HelpBox(
                    "Outer Glow creates a visible soft halo in Live Preview. Emission remains separate and drives URP Bloom when Bloom is enabled on the game camera.",
                    MessageType.Info);
            }

            var needsAdvancedMaterial =
                gradientMode != SpliceFxGradientMode.Solid ||
                (SpliceFxStrokeMode)strokeMode.enumValueIndex !=
                SpliceFxStrokeMode.None ||
                outerGlow.boolValue;
            if (needsAdvancedMaterial &&
                !SpliceFxVisualFactory.CanRenderAdvancedCardVisuals(
                    value.EffectiveTemplate))
                EditorGUILayout.HelpBox(
                    "This template has no card-compatible renderer for Gradient, Stroke or Outer Glow. Use a Mesh, Line or Trail renderer, the Static Sprite / Instance Card preset, or a compatible custom shader.",
                    MessageType.Warning);

            if (serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(value);
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
                if (GUILayout.Button("Replay Preview",
                        GUILayout.Height(30f)))
                    previewViewport?.Replay();
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
            previewStageIndex = Mathf.Clamp(
                previewStageIndex, 0,
                Mathf.Max(0, skillPackage.stages.Count - 1));
            if (skillPackage.stages.Count > 0)
            {
                var stageNames = new string[skillPackage.stages.Count];
                for (var i = 0; i < stageNames.Length; i++)
                {
                    var binding = skillPackage.stages[i];
                    stageNames[i] = binding != null
                        ? $"{i + 1}. {binding.stage}"
                        : $"{i + 1}. Empty";
                }
                previewStageIndex = EditorGUILayout.Popup(
                    "Preview Stage", previewStageIndex, stageNames);
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

        private void DrawMotionStack(
            SpliceFxSubEffectDefinition value)
        {
            EditorGUILayout.Space(10f);
            Section("FX Motion Stack");
            EditorGUILayout.HelpBox(
                "Add one or more motions. They blend from top to bottom and play immediately in Live Preview.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Add FX", GUILayout.Width(58f));
                motionToAdd =
                    (SpliceFxMotionType)EditorGUILayout.EnumPopup(
                        motionToAdd);
                if (GUILayout.Button("Add Motion",
                        GUILayout.Width(105f)))
                    AddMotion(value, motionToAdd);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Quick FX", GUILayout.Width(58f));
                if (GUILayout.Button("Magic Circle"))
                    AddRecipe(value, SpliceFxMotionType.Spin,
                        SpliceFxMotionType.Pulse,
                        SpliceFxMotionType.FadeIn);
                if (GUILayout.Button("Impact Pop"))
                    AddRecipe(value, SpliceFxMotionType.Expand,
                        SpliceFxMotionType.Flicker,
                        SpliceFxMotionType.FadeOut);
                if (GUILayout.Button("Energy Flow"))
                    AddRecipe(value, SpliceFxMotionType.UvScroll,
                        SpliceFxMotionType.Pulse);
                if (GUILayout.Button("Floating Aura"))
                    AddRecipe(value, SpliceFxMotionType.Float,
                        SpliceFxMotionType.Pulse,
                        SpliceFxMotionType.Flicker);
            }

            var serialized = new SerializedObject(value);
            serialized.Update();
            var list = serialized.FindProperty("motions");
            if (list == null || list.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    value.motionModifiers != SpliceFxMotionModifier.None
                        ? $"Legacy motion '{value.motionModifiers}' is active. Add a Motion Stack item to replace it with editable controls."
                        : "No motion yet. The image will remain still until you add an FX motion.",
                    MessageType.Warning);
                return;
            }

            for (var i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(
                           EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(
                            item.FindPropertyRelative("enabled"),
                            GUIContent.none, GUILayout.Width(18f));
                        EditorGUILayout.PropertyField(
                            item.FindPropertyRelative("label"),
                            GUIContent.none);
                        EditorGUILayout.PropertyField(
                            item.FindPropertyRelative("type"),
                            GUIContent.none, GUILayout.Width(92f));
                        GUI.enabled = i > 0;
                        if (GUILayout.Button("↑", GUILayout.Width(24f)))
                        {
                            list.MoveArrayElement(i, i - 1);
                            serialized.ApplyModifiedProperties();
                            GUI.enabled = true;
                            return;
                        }
                        GUI.enabled = i < list.arraySize - 1;
                        if (GUILayout.Button("↓", GUILayout.Width(24f)))
                        {
                            list.MoveArrayElement(i, i + 1);
                            serialized.ApplyModifiedProperties();
                            GUI.enabled = true;
                            return;
                        }
                        GUI.enabled = true;
                        if (GUILayout.Button("×", GUILayout.Width(24f)))
                        {
                            list.DeleteArrayElementAtIndex(i);
                            serialized.ApplyModifiedProperties();
                            return;
                        }
                    }
                    DrawMotionFields(item);
                }
            }
            if (serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(value);
        }

        private void DrawVisualLayers(
            SpliceFxSubEffectDefinition value)
        {
            EditorGUILayout.Space(10f);
            Section("Additional Visual Layers");
            EditorGUILayout.HelpBox(
                "Add multiple Trail or Particle layers inside this SubFX. Each layer has its own texture, gradient, transform and Instance Layout. The main Motion Stack moves the complete SubFX; layer self-spin and stagger live inside its Instance Layout.",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Trail",
                        GUILayout.Height(28f)))
                {
                    Undo.RecordObject(value, "Add FX Trail Layer");
                    value.visualLayers ??=
                        new List<SpliceFxVisualLayerDefinition>();
                    value.visualLayers.Add(
                        SpliceFxVisualLayerDefinition.CreateTrail());
                    EditorUtility.SetDirty(value);
                }
                if (GUILayout.Button("+ Add Particle",
                        GUILayout.Height(28f)))
                {
                    Undo.RecordObject(value, "Add FX Particle Layer");
                    value.visualLayers ??=
                        new List<SpliceFxVisualLayerDefinition>();
                    value.visualLayers.Add(
                        SpliceFxVisualLayerDefinition.CreateParticle());
                    EditorUtility.SetDirty(value);
                }
            }

            var serialized = new SerializedObject(value);
            serialized.Update();
            var list = serialized.FindProperty("visualLayers");
            if (list == null || list.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No extra visual layers. The main SubFX remains unchanged.",
                    MessageType.None);
                return;
            }

            for (var i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(
                           EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(
                            item.FindPropertyRelative("enabled"),
                            GUIContent.none, GUILayout.Width(18f));
                        EditorGUILayout.PropertyField(
                            item.FindPropertyRelative("label"),
                            GUIContent.none);
                        EditorGUILayout.PropertyField(
                            item.FindPropertyRelative("type"),
                            GUIContent.none, GUILayout.Width(86f));
                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button("↑",
                                    GUILayout.Width(24f)))
                            {
                                list.MoveArrayElement(i, i - 1);
                                serialized.ApplyModifiedProperties();
                                return;
                            }
                        using (new EditorGUI.DisabledScope(
                                   i >= list.arraySize - 1))
                            if (GUILayout.Button("↓",
                                    GUILayout.Width(24f)))
                            {
                                list.MoveArrayElement(i, i + 1);
                                serialized.ApplyModifiedProperties();
                                return;
                            }
                        if (GUILayout.Button("×", GUILayout.Width(24f)))
                        {
                            list.DeleteArrayElementAtIndex(i);
                            serialized.ApplyModifiedProperties();
                            return;
                        }
                    }
                    DrawVisualLayerFields(item);
                }
            }
            if (serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(value);
        }

        private void DrawVisualLayerFields(
            SerializedProperty item)
        {
            var type = (SpliceFxVisualLayerType)item
                .FindPropertyRelative("type").enumValueIndex;
            var sprite = item.FindPropertyRelative("sprite");
            EditorGUILayout.PropertyField(sprite,
                new GUIContent("Image / Sprite (2D/UI)",
                    "Optional Sprite used by the trail strip or particle billboard. Sub-sprite atlas UV is preserved and takes priority over Texture2D."));
            using (new EditorGUI.DisabledScope(
                       sprite.objectReferenceValue != null))
                EditorGUILayout.PropertyField(
                    item.FindPropertyRelative("texture"),
                    new GUIContent("Image / Texture2D",
                        "Optional regular texture. Keep the source image alpha-enabled."));
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("color"),
                new GUIContent("Color Gradient"));
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("emission"),
                new GUIContent("Glow / Emission"));
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("quality"),
                new GUIContent("Quality Tiers"));

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Transform / Copies",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("localPosition"),
                new GUIContent("Position"));
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("localEulerAngles"),
                new GUIContent("Rotation"));
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("localScale"),
                new GUIContent("Scale"));
            EditorGUILayout.PropertyField(
                item.FindPropertyRelative("instanceLayout"),
                new GUIContent("Instance Layout"), true);
            DrawLayerMotionStack(
                item.FindPropertyRelative("motions"));

            EditorGUILayout.Space(3f);
            if (type == SpliceFxVisualLayerType.Trail)
            {
                EditorGUILayout.LabelField("Trail Settings",
                    EditorStyles.miniBoldLabel);
                DrawRelative(item, "trailTime", "Trail Lifetime");
                DrawRelative(item, "trailStartWidth", "Start Width");
                DrawRelative(item, "trailEndWidth", "End Width");
                DrawRelative(item, "trailMinVertexDistance",
                    "Vertex Distance");
                DrawRelative(item, "trailTextureMode",
                    "Texture Mode");
                DrawRelative(item, "trailAlignment", "Alignment");
                EditorGUILayout.HelpBox(
                    "A Trail becomes visible while this SubFX or its layer instances move. Add Orbit/Float/Shake in Motion Stack, or attach the exported SubFX to a moving hero/projectile.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Particle Settings",
                EditorStyles.miniBoldLabel);
            var emission = item.FindPropertyRelative(
                "particleEmission");
            EditorGUILayout.PropertyField(emission,
                new GUIContent("Emission Mode"));
            DrawRelative(item, "particleShape", "Shape");
            DrawRelative(item, "particleLoop", "Loop");
            DrawRelative(item, "particleMaxCount", "Maximum Count");
            if ((SpliceFxParticleEmissionMode)emission.enumValueIndex ==
                SpliceFxParticleEmissionMode.Burst)
                DrawRelative(item, "particleBurstCount",
                    "Burst Count");
            else
                DrawRelative(item, "particleRate",
                    "Particles / Second");
            DrawRelative(item, "particleLifetime",
                "Particle Lifetime");
            DrawRelative(item, "particleSpeed", "Start Speed");
            DrawRelative(item, "particleSize", "Start Size");
            DrawRelative(item, "particleShapeRadius",
                "Shape Radius");
            DrawRelative(item, "particleGravity",
                "Force / Gravity");
            DrawRelative(item, "particleWorldSpace",
                "World Space");
        }

        private void DrawLayerMotionStack(
            SerializedProperty list)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Layer Motion Stack",
                EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                motionToAdd =
                    (SpliceFxMotionType)EditorGUILayout.EnumPopup(
                        motionToAdd);
                if (GUILayout.Button("+ Add",
                        GUILayout.Width(62f)))
                {
                    var index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    WriteMotion(
                        list.GetArrayElementAtIndex(index),
                        SpliceFxMotionLayer.Create(motionToAdd));
                }
            }
            for (var i = 0; i < list.arraySize; i++)
            {
                var motion = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(
                           EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(
                            motion.FindPropertyRelative("enabled"),
                            GUIContent.none, GUILayout.Width(18f));
                        EditorGUILayout.PropertyField(
                            motion.FindPropertyRelative("label"),
                            GUIContent.none);
                        EditorGUILayout.PropertyField(
                            motion.FindPropertyRelative("type"),
                            GUIContent.none, GUILayout.Width(86f));
                        if (GUILayout.Button("×",
                                GUILayout.Width(24f)))
                        {
                            list.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }
                    DrawMotionFields(motion);
                }
            }
        }

        private static void WriteMotion(
            SerializedProperty target, SpliceFxMotionLayer source)
        {
            target.FindPropertyRelative("label").stringValue =
                source.label;
            target.FindPropertyRelative("enabled").boolValue =
                source.enabled;
            target.FindPropertyRelative("type").enumValueIndex =
                (int)source.type;
            target.FindPropertyRelative("speed").floatValue =
                source.speed;
            target.FindPropertyRelative("amount").floatValue =
                source.amount;
            target.FindPropertyRelative("delaySeconds").floatValue =
                source.delaySeconds;
            target.FindPropertyRelative("durationSeconds").floatValue =
                source.durationSeconds;
            target.FindPropertyRelative("phase").floatValue =
                source.phase;
            target.FindPropertyRelative("loop").boolValue =
                source.loop;
            target.FindPropertyRelative("axis").vector3Value =
                source.axis;
            target.FindPropertyRelative("uvSpeed").vector2Value =
                source.uvSpeed;
            target.FindPropertyRelative("curve").animationCurveValue =
                source.curve;
        }

        private static void DrawRelative(
            SerializedProperty parent, string name, string label)
        {
            EditorGUILayout.PropertyField(
                parent.FindPropertyRelative(name),
                new GUIContent(label));
        }

        private static void DrawInstanceLayout(
            SpliceFxSubEffectDefinition value)
        {
            EditorGUILayout.Space(10f);
            Section("Instance Layout");
            EditorGUILayout.HelpBox(
                "Duplicate the same image/prefab inside one SubFX. Layout is baked into the exported prefab; no runtime Instantiate is required.",
                MessageType.Info);
            if (value.EffectiveTemplate != null &&
                value.EffectiveTemplate
                    .GetComponentInChildren<ParticleSystem>(true) != null)
            {
                EditorGUILayout.HelpBox(
                    "This preset is a particle emitter, so one image can appear as many short-lived particles. For stable swords or individually placed images, use Static Sprite / Instance Card.",
                    MessageType.Warning);
                var staticCard = AssetDatabase.LoadAssetAtPath<
                    SpliceFxPresetDefinition>(
                    SpliceFxStarterLibrary.Root +
                    "/Presets/Preset_static_sprite_card.asset");
                using (new EditorGUI.DisabledScope(staticCard == null))
                    if (GUILayout.Button("Use Static Sprite Card"))
                    {
                        Undo.RecordObject(value,
                            "Use Static Sprite Card");
                        value.preset = staticCard;
                        EditorUtility.SetDirty(value);
                    }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Quick Layout", GUILayout.Width(82f));
                if (GUILayout.Button("Single"))
                    SetLayout(value, new SpliceFxInstanceLayout());
                if (GUILayout.Button("5 Around"))
                    SetLayout(value,
                        SpliceFxInstanceLayout.RadialFive());
                if (GUILayout.Button("5 Arc"))
                    SetLayout(value, ArcLayout());
                if (GUILayout.Button("Line 5"))
                    SetLayout(value, LineLayout());
                if (GUILayout.Button("Grid 3×3"))
                    SetLayout(value, GridLayout());
                if (GUILayout.Button("Random 8"))
                    SetLayout(value, RandomLayout());
            }

            var serialized = new SerializedObject(value);
            serialized.Update();
            var layout = serialized.FindProperty("instanceLayout");
            if (layout == null) return;
            var modeProperty = layout.FindPropertyRelative("mode");
            EditorGUILayout.PropertyField(modeProperty,
                new GUIContent("Layout Mode"));
            var mode = (SpliceFxInstanceLayoutMode)
                modeProperty.enumValueIndex;

            if (mode == SpliceFxInstanceLayoutMode.Manual)
            {
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("manualInstances"),
                    new GUIContent("Manual Instances"), true);
                EditorGUILayout.HelpBox(
                    "High uses every enabled Manual Instance.",
                    MessageType.None);
                DrawQualityCounts(layout, true, true);
            }
            else
            {
                DrawQualityCounts(layout,
                    mode != SpliceFxInstanceLayoutMode.Single);
            }

            EditorGUILayout.PropertyField(
                layout.FindPropertyRelative("centerOffset"),
                new GUIContent("Center Offset"));
            EditorGUILayout.PropertyField(
                layout.FindPropertyRelative("baseEulerAngles"),
                new GUIContent("Base Rotation"));
            EditorGUILayout.PropertyField(
                layout.FindPropertyRelative("baseScale"),
                new GUIContent("Base Scale"));

            if (mode is SpliceFxInstanceLayoutMode.Radial or
                SpliceFxInstanceLayoutMode.Arc or
                SpliceFxInstanceLayoutMode.RandomRing)
            {
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("planeAxis"),
                    new GUIContent("Circle Axis"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("startDirection"),
                    new GUIContent("First Direction"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("facing"),
                    new GUIContent("Each Item Faces"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("radius"),
                    new GUIContent(mode ==
                                   SpliceFxInstanceLayoutMode.RandomRing
                        ? "Outer Radius"
                        : "Radius"));
                if (mode == SpliceFxInstanceLayoutMode.RandomRing)
                    EditorGUILayout.PropertyField(
                        layout.FindPropertyRelative("innerRadius"),
                        new GUIContent("Inner Radius"));
                if (mode != SpliceFxInstanceLayoutMode.Radial)
                    EditorGUILayout.PropertyField(
                        layout.FindPropertyRelative("arcDegrees"),
                        new GUIContent("Arc Degrees"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("startAngleDegrees"),
                    new GUIContent("Start Angle"));
            }
            else if (mode == SpliceFxInstanceLayoutMode.Line)
            {
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("lineDirection"),
                    new GUIContent("Line Direction"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("spacing"));
            }
            else if (mode == SpliceFxInstanceLayoutMode.Grid)
            {
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("planeAxis"),
                    new GUIContent("Grid Normal"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("startDirection"),
                    new GUIContent("Grid Forward"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("gridColumns"),
                    new GUIContent("Columns"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("gridSpacing"),
                    new GUIContent("Grid Spacing"));
            }

            if (mode is not SpliceFxInstanceLayoutMode.Single and
                not SpliceFxInstanceLayoutMode.Manual)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Per Instance Variation",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("eulerStep"),
                    new GUIContent("Rotation Step"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("uniformScaleStep"),
                    new GUIContent("Scale Step"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("angleJitter"),
                    new GUIContent("Angle Jitter"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("radiusJitter"),
                    new GUIContent("Radius Jitter"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("rotationJitter"),
                    new GUIContent("Rotation Jitter"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("scaleJitter"),
                    new GUIContent("Scale Jitter"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("randomSeed"),
                    new GUIContent("Random Seed"));
            }

            if (mode != SpliceFxInstanceLayoutMode.Single)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Individual Animation",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("motionScope"),
                    new GUIContent("Motion Stack Applies To",
                        "Whole Formation moves the group. Each Instance runs the same Motion Stack independently and respects Delay Per Item."));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative(
                        "selfSpinDegreesPerSecond"),
                    new GUIContent("Each Item Spin °/s"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("selfSpinAxis"),
                    new GUIContent("Self Spin Axis"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative(
                        "alternateSelfSpin"),
                    new GUIContent("Alternate Direction"));
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Stagger / Sequence",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative(
                        "activationDelayStep"),
                    new GUIContent("Delay Per Item"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("activeDuration"),
                    new GUIContent("Visible Duration",
                        "0 keeps each item visible until the SubFX ends."));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative(
                        "reverseActivationOrder"),
                    new GUIContent("Reverse Order"));
            }

            if (serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(value);
        }

        private static void DrawQualityCounts(
            SerializedProperty layout, bool multiple,
            bool manual = false)
        {
            if (!multiple) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!manual)
                    EditorGUILayout.PropertyField(
                        layout.FindPropertyRelative("highCount"),
                        new GUIContent("High Count"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("mediumCount"),
                    new GUIContent("Medium"));
                EditorGUILayout.PropertyField(
                    layout.FindPropertyRelative("lowCount"),
                    new GUIContent("Low"));
            }
        }

        private static void SetLayout(
            SpliceFxSubEffectDefinition value,
            SpliceFxInstanceLayout layout)
        {
            Undo.RecordObject(value, "Set FX Instance Layout");
            value.instanceLayout = layout;
            EditorUtility.SetDirty(value);
        }

        private static SpliceFxInstanceLayout ArcLayout()
        {
            var layout = SpliceFxInstanceLayout.RadialFive();
            layout.mode = SpliceFxInstanceLayoutMode.Arc;
            layout.arcDegrees = 180f;
            layout.startAngleDegrees = -90f;
            return layout;
        }

        private static SpliceFxInstanceLayout LineLayout() =>
            new()
            {
                mode = SpliceFxInstanceLayoutMode.Line,
                highCount = 5,
                mediumCount = 4,
                lowCount = 3,
                spacing = 1f
            };

        private static SpliceFxInstanceLayout GridLayout() =>
            new()
            {
                mode = SpliceFxInstanceLayoutMode.Grid,
                highCount = 9,
                mediumCount = 6,
                lowCount = 4,
                gridColumns = 3,
                gridSpacing = Vector2.one
            };

        private static SpliceFxInstanceLayout RandomLayout() =>
            new()
            {
                mode = SpliceFxInstanceLayoutMode.RandomRing,
                highCount = 8,
                mediumCount = 6,
                lowCount = 4,
                innerRadius = 0.7f,
                radius = 2f,
                facing = SpliceFxInstanceFacing.FaceOutward
            };

        private static void DrawMotionFields(SerializedProperty item)
        {
            var typeProperty = item.FindPropertyRelative("type");
            var type = (SpliceFxMotionType)typeProperty.enumValueIndex;
            var speed = item.FindPropertyRelative("speed");
            var amount = item.FindPropertyRelative("amount");
            var delay = item.FindPropertyRelative("delaySeconds");
            var duration = item.FindPropertyRelative("durationSeconds");
            var phase = item.FindPropertyRelative("phase");
            var loop = item.FindPropertyRelative("loop");
            var axis = item.FindPropertyRelative("axis");
            var uvSpeed = item.FindPropertyRelative("uvSpeed");
            var curve = item.FindPropertyRelative("curve");

            if (type == SpliceFxMotionType.Spin)
                EditorGUILayout.PropertyField(speed,
                    new GUIContent("Angle (Degrees)",
                        "Total angle completed within the Duration below. Use a negative value to reverse direction."));
            else if (UsesCycleTiming(type))
                EditorGUILayout.PropertyField(speed,
                    new GUIContent(CycleCountLabel(type),
                        "How many cycles or movement units are completed within the Duration below."));
            if (type != SpliceFxMotionType.UvScroll &&
                type != SpliceFxMotionType.Spin)
                EditorGUILayout.PropertyField(amount,
                    new GUIContent(MotionAmountLabel(type)));
            EditorGUILayout.PropertyField(delay,
                new GUIContent("Start Delay"));
            EditorGUILayout.PropertyField(duration,
                new GUIContent(DurationLabel(type),
                    DurationTooltip(type)));

            if (UsesCurve(type))
            {
                EditorGUILayout.PropertyField(curve,
                    new GUIContent("Motion Curve"));
            }
            else if (type is SpliceFxMotionType.Pulse or
                     SpliceFxMotionType.Float or
                     SpliceFxMotionType.Flicker)
            {
                EditorGUILayout.PropertyField(phase,
                    new GUIContent("Phase Offset"));
            }
            EditorGUILayout.PropertyField(loop,
                new GUIContent("Loop",
                    "Repeat after Duration. Disable to stop and hold the final evaluated state."));

            if (type is SpliceFxMotionType.Spin or
                SpliceFxMotionType.Float or
                SpliceFxMotionType.Orbit)
                EditorGUILayout.PropertyField(axis,
                    new GUIContent("Axis"));
            if (type == SpliceFxMotionType.UvScroll)
                EditorGUILayout.PropertyField(uvSpeed,
                    new GUIContent("UV Direction / Distance"));

            EditorGUILayout.HelpBox(
                MotionTimingSummary(type, speed.floatValue,
                    duration.floatValue),
                MessageType.None);
        }

        private static bool UsesCycleTiming(
            SpliceFxMotionType type) =>
            type is SpliceFxMotionType.Pulse or
                SpliceFxMotionType.Float or
                SpliceFxMotionType.Orbit or
                SpliceFxMotionType.Flicker or
                SpliceFxMotionType.UvScroll or
                SpliceFxMotionType.Shake;

        private static bool UsesCurve(
            SpliceFxMotionType type) =>
            type is SpliceFxMotionType.Expand or
                SpliceFxMotionType.Contract or
                SpliceFxMotionType.FadeIn or
                SpliceFxMotionType.FadeOut;

        private static string CycleCountLabel(
            SpliceFxMotionType type) =>
            type switch
            {
                SpliceFxMotionType.Orbit =>
                    "Revolutions In Duration",
                SpliceFxMotionType.UvScroll =>
                    "Distance Multiplier",
                SpliceFxMotionType.Shake =>
                    "Noise Cycles In Duration",
                _ => "Cycles In Duration"
            };

        private static string DurationLabel(
            SpliceFxMotionType type) =>
            type switch
            {
                SpliceFxMotionType.Spin =>
                    "Complete Angle In (Seconds)",
                SpliceFxMotionType.Orbit =>
                    "Complete Revolutions In (Seconds)",
                SpliceFxMotionType.Expand or
                    SpliceFxMotionType.Contract or
                    SpliceFxMotionType.FadeIn or
                    SpliceFxMotionType.FadeOut =>
                    "Complete Motion In (Seconds)",
                _ => "Cycle Window (Seconds)"
            };

        private static string DurationTooltip(
            SpliceFxMotionType type) =>
            type == SpliceFxMotionType.Spin
                ? "Example: Angle 360 and Duration 2 rotates at 180 degrees per second."
                : "All speed/frequency values above are evaluated across this many seconds.";

        internal static string MotionTimingSummary(
            SpliceFxMotionType type, float speed, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            return type switch
            {
                SpliceFxMotionType.Spin =>
                    $"{speed:0.##}° / {duration:0.##}s = " +
                    $"{speed / duration:0.##}° per second",
                SpliceFxMotionType.Expand or
                    SpliceFxMotionType.Contract or
                    SpliceFxMotionType.FadeIn or
                    SpliceFxMotionType.FadeOut =>
                    $"Completes once in {duration:0.##} seconds",
                SpliceFxMotionType.Orbit =>
                    $"{speed:0.##} revolution(s) in " +
                    $"{duration:0.##} seconds",
                _ => $"{speed:0.##} cycle/unit(s) in " +
                     $"{duration:0.##} seconds"
            };
        }

        private static string MotionAmountLabel(
            SpliceFxMotionType type) =>
            type switch
            {
                SpliceFxMotionType.Pulse => "Scale Amount",
                SpliceFxMotionType.Expand => "Expand Amount",
                SpliceFxMotionType.Contract => "Contract Amount",
                SpliceFxMotionType.Float => "Move Distance",
                SpliceFxMotionType.Orbit => "Orbit Radius",
                SpliceFxMotionType.Flicker => "Flicker Strength",
                SpliceFxMotionType.FadeIn => "Fade Strength",
                SpliceFxMotionType.FadeOut => "Fade Strength",
                SpliceFxMotionType.Shake => "Shake Distance",
                _ => "Amount"
            };

        private static void AddMotion(
            SpliceFxSubEffectDefinition value,
            SpliceFxMotionType type)
        {
            Undo.RecordObject(value, "Add FX Motion");
            value.motions ??= new List<SpliceFxMotionLayer>();
            value.motions.Add(SpliceFxMotionLayer.Create(type));
            EditorUtility.SetDirty(value);
        }

        private static void AddRecipe(
            SpliceFxSubEffectDefinition value,
            params SpliceFxMotionType[] types)
        {
            Undo.RecordObject(value, "Add FX Motion Recipe");
            value.motions ??= new List<SpliceFxMotionLayer>();
            foreach (var type in types)
                value.motions.Add(SpliceFxMotionLayer.Create(type));
            EditorUtility.SetDirty(value);
        }

        private static void DrawSerializedAsset(UnityEngine.Object asset,
            params string[] excludedPropertyPaths)
        {
            var serialized = new SerializedObject(asset);
            serialized.Update();
            var iterator = serialized.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script") continue;
                if (Array.IndexOf(excludedPropertyPaths,
                        iterator.propertyPath) >= 0)
                    continue;
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

        private static string CreateUniqueId<T>(
            string requestedName,
            T current,
            Func<T, string> selector)
            where T : UnityEngine.Object
        {
            var baseId =
                SpliceFxPresetDefinition.SanitizeId(requestedName);
            var used = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets(
                         $"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset == current) continue;
                var id = selector(asset);
                if (!string.IsNullOrWhiteSpace(id))
                    used.Add(id);
            }

            if (!used.Contains(baseId)) return baseId;
            for (var suffix = 2; suffix < 10000; suffix++)
            {
                var candidate = $"{baseId}_{suffix}";
                if (!used.Contains(candidate))
                    return candidate;
            }
            return $"{baseId}_{Guid.NewGuid():N}";
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

        private void DrawPreviewPanel(float width)
        {
            previewViewport ??= new SpliceFxPreviewViewport(Repaint);
            switch (tab)
            {
                case Tab.Create:
                    previewViewport.SetSource(
                        creationPreset,
                        SpliceFxPreviewSourceKind.Preset);
                    break;
                case Tab.SubFX:
                    previewViewport.SetSource(
                        subFx,
                        SpliceFxPreviewSourceKind.SubFx);
                    break;
                case Tab.Blend:
                    previewViewport.SetSource(
                        sequence,
                        SpliceFxPreviewSourceKind.Blend);
                    break;
                case Tab.BindExport:
                    previewViewport.SetSource(
                        skillPackage,
                        SpliceFxPreviewSourceKind.SkillStage,
                        previewStageIndex);
                    break;
                case Tab.Validate:
                    if (skillPackage != null)
                        previewViewport.SetSource(
                            skillPackage,
                            SpliceFxPreviewSourceKind.SkillStage,
                            previewStageIndex);
                    else if (sequence != null)
                        previewViewport.SetSource(
                            sequence,
                            SpliceFxPreviewSourceKind.Blend);
                    else
                        previewViewport.SetSource(
                            subFx,
                            SpliceFxPreviewSourceKind.SubFx);
                    break;
            }
            previewViewport.Draw(width);
        }
    }
}
