#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using static Veridian.RockGenLite.RockSettings;

namespace Veridian.RockGenLite.Editor
{
    [CustomEditor(typeof(RockSettings))]
    public class RockSettingsEditor : UnityEditor.Editor
    {
        public Action OnSettingsChanged;
        private RockSettings settings;
        private RockPresetType _selectedPreset = RockPresetType.None;
        private SerializedProperty saveFolderPath, exportName, baseShape, rockType, seed, uvScale, uvBlendSharpness;
        private SerializedProperty targetDiameter, prefabScale, randomizeProportions, minRandomProportions, maxRandomProportions, baseProportions;
        private SerializedProperty colorizationMethod, colorPattern, textureResolution;
        private SerializedProperty inputAlbedo, inputNormal, inputTextureScale;
        private SerializedProperty lodLevels, colliderLODIndex, colliderType;
        private SerializedProperty useMacroNoise, useDomainWarping, useVoronoi, useTerracing;
        private SerializedProperty macroNoiseFrequency, macroNoiseStrength, macroNoiseOctaves;
        private SerializedProperty generateAO, aoStrength, generateHeight;
        private SerializedProperty generateSmoothness, baseSmoothness;
        private SerializedProperty noiseFrequency, noiseStrength, octaves, lacunarity, persistence;
        private SerializedProperty macroFractalType, detailFractalType, voronoiOutputType, voronoiMetric;
        private SerializedProperty warpStrength, warpFrequency;
        private SerializedProperty voronoiFrequency, voronoiIntensity;
        private SerializedProperty terraceCount, terraceIntensity;

        private SerializedProperty primaryColor, secondaryColor, tertiaryColor, cavityColor;
        private SerializedProperty texturingNoiseFrequency, texturingNoiseBlend, cavityStrength;
        private SerializedProperty strataWarpFrequency, strataWarpStrength, patchFrequency;

        private SerializedProperty slopeMode, slopeThreshold, slopeSmoothness;

        private SerializedProperty useNormalPerturbation, normalNoiseFrequency, normalNoiseStrength;
        private SerializedProperty normalMapStrength, normalMapResolution;
        private SerializedProperty useMicroDetail, microDetailFrequency, microDetailStrength;

        private SerializedProperty auxiliaryMapResolution, textureExportMode;
        private SerializedProperty metallicStyle, oreColor, oreFrequency, oreCoverage, oreMetallic, oreSmoothness;

        private bool _showGeneral = true, _showShape = true, _showTexturing = true, _showLOD = true, _showNoise = false, _showPhysics = true;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _foldoutStyle;
        private void OnEnable()
        {
            settings = (RockSettings)target;
            InitializeProperties();

            if (settings != null && (RockType)rockType.enumValueIndex != RockType.Custom)
            {
                HandleRockTypePresets(true);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void InitializeProperties()
        {
            saveFolderPath = serializedObject.FindProperty("saveFolderPath");
            exportName = serializedObject.FindProperty("exportName");
            colorPattern = serializedObject.FindProperty("colorPattern");
            tertiaryColor = serializedObject.FindProperty("tertiaryColor");
            baseShape = serializedObject.FindProperty("baseShape");
            rockType = serializedObject.FindProperty("rockType");
            seed = serializedObject.FindProperty("seed");

            targetDiameter = serializedObject.FindProperty("targetDiameter");
            prefabScale = serializedObject.FindProperty("prefabScale");
            randomizeProportions = serializedObject.FindProperty("randomizeProportions");
            minRandomProportions = serializedObject.FindProperty("minRandomProportions");
            maxRandomProportions = serializedObject.FindProperty("maxRandomProportions");
            baseProportions = serializedObject.FindProperty("baseProportions");

            colorizationMethod = serializedObject.FindProperty("colorizationMethod");
            textureResolution = serializedObject.FindProperty("textureResolution");
            inputAlbedo = serializedObject.FindProperty("inputAlbedo");
            inputNormal = serializedObject.FindProperty("inputNormal");
            inputTextureScale = serializedObject.FindProperty("inputTextureScale");
            uvScale = serializedObject.FindProperty("uvScale");
            uvBlendSharpness = serializedObject.FindProperty("uvBlendSharpness");

            primaryColor = serializedObject.FindProperty("primaryColor");
            secondaryColor = serializedObject.FindProperty("secondaryColor");
            cavityColor = serializedObject.FindProperty("cavityColor");
            texturingNoiseFrequency = serializedObject.FindProperty("texturingNoiseFrequency");
            texturingNoiseBlend = serializedObject.FindProperty("texturingNoiseBlend");
            cavityStrength = serializedObject.FindProperty("cavityStrength");

            strataWarpFrequency = serializedObject.FindProperty("strataWarpFrequency");
            strataWarpStrength = serializedObject.FindProperty("strataWarpStrength");
            patchFrequency = serializedObject.FindProperty("patchFrequency");

            slopeMode = serializedObject.FindProperty("slopeMode");
            slopeThreshold = serializedObject.FindProperty("slopeThreshold");
            slopeSmoothness = serializedObject.FindProperty("slopeSmoothness");

            useNormalPerturbation = serializedObject.FindProperty("useNormalPerturbation");
            normalNoiseFrequency = serializedObject.FindProperty("normalNoiseFrequency");
            normalNoiseStrength = serializedObject.FindProperty("normalNoiseStrength");
            useMicroDetail = serializedObject.FindProperty("useMicroDetail");
            microDetailFrequency = serializedObject.FindProperty("microDetailFrequency");
            microDetailStrength = serializedObject.FindProperty("microDetailStrength");

            lodLevels = serializedObject.FindProperty("lodLevels");
            colliderLODIndex = serializedObject.FindProperty("colliderLODIndex");
            colliderType = serializedObject.FindProperty("colliderType");


            useMacroNoise = serializedObject.FindProperty("useMacroNoise");
            useDomainWarping = serializedObject.FindProperty("useDomainWarping");
            useVoronoi = serializedObject.FindProperty("useVoronoi");
            useTerracing = serializedObject.FindProperty("useTerracing");

            macroFractalType = serializedObject.FindProperty("macroFractalType");
            detailFractalType = serializedObject.FindProperty("detailFractalType");
            voronoiOutputType = serializedObject.FindProperty("voronoiOutputType");
            voronoiMetric = serializedObject.FindProperty("voronoiMetric");

            macroNoiseFrequency = serializedObject.FindProperty("macroNoiseFrequency");
            macroNoiseStrength = serializedObject.FindProperty("macroNoiseStrength");
            macroNoiseOctaves = serializedObject.FindProperty("macroNoiseOctaves");

            noiseFrequency = serializedObject.FindProperty("noiseFrequency");
            noiseStrength = serializedObject.FindProperty("noiseStrength");
            octaves = serializedObject.FindProperty("octaves");
            lacunarity = serializedObject.FindProperty("lacunarity");
            persistence = serializedObject.FindProperty("persistence");

            warpStrength = serializedObject.FindProperty("warpStrength");
            warpFrequency = serializedObject.FindProperty("warpFrequency");

            voronoiFrequency = serializedObject.FindProperty("voronoiFrequency");
            voronoiIntensity = serializedObject.FindProperty("voronoiIntensity");

            terraceCount = serializedObject.FindProperty("terraceCount");
            terraceIntensity = serializedObject.FindProperty("terraceIntensity");
            normalMapStrength = serializedObject.FindProperty("normalMapStrength");

            normalMapResolution = serializedObject.FindProperty("normalMapResolution");
            auxiliaryMapResolution = serializedObject.FindProperty("auxiliaryMapResolution");
            textureExportMode = serializedObject.FindProperty("textureExportMode");

            generateAO = serializedObject.FindProperty("generateAO");
            aoStrength = serializedObject.FindProperty("aoStrength");
            generateHeight = serializedObject.FindProperty("generateHeight");
            generateSmoothness = serializedObject.FindProperty("generateSmoothness");
            baseSmoothness = serializedObject.FindProperty("baseSmoothness");

            metallicStyle = serializedObject.FindProperty("metallicStyle");
            oreColor = serializedObject.FindProperty("oreColor");
            oreFrequency = serializedObject.FindProperty("oreFrequency");
            oreCoverage = serializedObject.FindProperty("oreCoverage");
            oreMetallic = serializedObject.FindProperty("oreMetallic");
            oreSmoothness = serializedObject.FindProperty("oreSmoothness");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 230f;

            if (EditorUtility.IsPersistent(target)) DrawHeaderButtons();

            // Added Preset Dropdown here
            DrawPresetSelection();

            EditorGUI.BeginChangeCheck();

            DrawGeneralSettings();
            DrawCoreShapeSettings();
            DrawNoiseConfiguration();
            DrawProceduralTexturingSettings();
            DrawLODSettings();
            DrawPhysicsSettings();

            bool propertiesChanged = serializedObject.ApplyModifiedProperties();
            bool guiChanged = EditorGUI.EndChangeCheck();

            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (propertiesChanged || guiChanged)
            {
                if (EditorUtility.IsPersistent(target)) EditorUtility.SetDirty(settings);
                OnSettingsChanged?.Invoke();
            }
        }
        // Standard linear slider for general use (restored)
        private void DrawInverseSlider(SerializedProperty prop, string label, float minScale, float maxScale)
        {
            if (prop == null) return;

            if (prop.propertyPath == "normalNoiseFrequency")
            {
                DrawDiameterRelativeNormalizedScaleSlider(
                    prop,
                    "Bump Scale",
                    0.005f,
                    0.15f,
                    0.005f,
                    "0 = smallest usable bump scale. 1 = largest bump scale allowed for the current Target Diameter. For a 2m rock, the largest value is about 0.3m.\n\n" +
                    "Procedural bump gives baked rocks surface relief. Most rocks benefit from at least a small amount of bump; without it, baked textures can look flat. Very high strength or a poorly matched scale can make the surface look noisy, lumpy, or stylized, so tune Bump Scale and Bump Strength together."
                );
                return;
            }

            if (prop.propertyPath == "microDetailFrequency")
            {
                DrawDiameterRelativeNormalizedScaleSlider(
                    prop,
                    "Micro Detail Scale",
                    0.0025f,
                    0.05f,
                    0.002f,
                    "0 = smallest usable micro-detail scale. 1 = largest micro-detail scale allowed for the current Target Diameter. For a 2m rock, the largest value is about 0.1m.\n\n" +
                    "Micro-detail adds fine grit, pores, and mineral grain on top of the main bump layer. Keep it subtle; high values can make surfaces look noisy or overly sharp."
                );
                return;
            }

            float currentFreq = prop.floatValue;
            float currentScale = currentFreq > 0.00001f ? 1.0f / currentFreq : maxScale;

            EditorGUI.BeginChangeCheck();
            float newScale = EditorGUILayout.Slider(new GUIContent(label, prop.tooltip), currentScale, minScale, maxScale);
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = 1.0f / Mathf.Max(0.00001f, newScale);
            }
        }


        private void DrawDiameterRelativeNormalizedScaleSlider(
    SerializedProperty prop,
    string label,
    float minScaleFractionOfDiameter,
    float maxScaleFractionOfDiameter,
    float absoluteMinimumScaleMeters,
    string extraTooltip)
        {
            if (prop == null) return;

            float diameter = targetDiameter != null ? Mathf.Max(0.1f, targetDiameter.floatValue) : 2.0f;

            float minScaleMeters = Mathf.Max(absoluteMinimumScaleMeters, diameter * minScaleFractionOfDiameter);
            float maxScaleMeters = Mathf.Max(minScaleMeters + 0.0001f, diameter * maxScaleFractionOfDiameter);

            float currentFreq = Mathf.Max(0.00001f, prop.floatValue);
            float currentScaleMeters = 1.0f / currentFreq;

            float clampedScaleMeters = Mathf.Clamp(currentScaleMeters, minScaleMeters, maxScaleMeters);

            if (!Mathf.Approximately(currentScaleMeters, clampedScaleMeters))
            {
                prop.floatValue = 1.0f / Mathf.Max(0.00001f, clampedScaleMeters);
                currentScaleMeters = clampedScaleMeters;
            }

            float currentNormalized = Mathf.InverseLerp(minScaleMeters, maxScaleMeters, currentScaleMeters);

            GUIContent content = new GUIContent(
                $"{label} (0-1)",
                $"{prop.tooltip}\n\n{extraTooltip}\n\nCurrent physical range: {minScaleMeters:0.###}m to {maxScaleMeters:0.###}m."
            );

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            float newNormalized = EditorGUILayout.Slider(content, currentNormalized, 0f, 1f);
            float newScaleMeters = Mathf.Lerp(minScaleMeters, maxScaleMeters, newNormalized);

            GUILayout.Label($"{newScaleMeters:0.###} m", GUILayout.Width(65));

            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = 1.0f / Mathf.Max(0.00001f, newScaleMeters);
            }

            EditorGUILayout.EndHorizontal();
        }

        // Exponential non-linear slider designed specifically for Macro Scale

        private void DrawPresetSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(
                new GUIContent(
                    "Quick Start Presets",
                    "Built-in Rock Generator Lite presets. Applying a preset overwrites the current settings in this preview/profile."
                ),
                EditorStyles.boldLabel
            );

            EditorGUILayout.BeginHorizontal();

            _selectedPreset = (RockPresetType)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Preset",
                    _selectedPreset == RockPresetType.None
                        ? "Select a built-in Lite preset. Applying a preset overwrites the current rock settings and randomizes the seed."
                        : RockPresetUtility.GetPresetTooltip(_selectedPreset)
                ),
                _selectedPreset
            );

            GUI.enabled = _selectedPreset != RockPresetType.None;

            if (GUILayout.Button(
                    new GUIContent(
                        "Apply Preset",
                        "Applies the selected preset, overwrites the current settings, and randomizes the seed."
                    ),
                    EditorStyles.miniButtonRight,
                    GUILayout.Width(100)))
            {
                Undo.RecordObject(settings, "Apply Rock Preset");
                RockPresetUtility.ApplyPreset(settings, _selectedPreset);

                settings.seed = UnityEngine.Random.Range(1, 1000000);

                EditorUtility.SetDirty(settings);
                serializedObject.Update();
                OnSettingsChanged?.Invoke();

                _selectedPreset = RockPresetType.None;
                GUIUtility.keyboardControl = 0;
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        private void DrawExponentialInverseSlider(SerializedProperty prop, string label, float minScale, float maxScale, float power = 4f)
        {
            float currentFreq = prop.floatValue;
            float currentScale = currentFreq > 0.00001f ? 1.0f / currentFreq : maxScale;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, prop.tooltip));

            float t = Mathf.Pow(Mathf.Clamp01((currentScale - minScale) / (maxScale - minScale)), 1f / power);

            EditorGUI.BeginChangeCheck();
            float newT = GUILayout.HorizontalSlider(t, 0f, 1f);
            float newScaleBox = EditorGUILayout.FloatField((float)Math.Round(currentScale, 2), GUILayout.Width(50));

            if (EditorGUI.EndChangeCheck())
            {
                float finalScale = currentScale;

                if (Mathf.Abs(newScaleBox - (float)Math.Round(currentScale, 2)) > 0.0001f)
                {
                    finalScale = Mathf.Clamp(newScaleBox, minScale, maxScale);
                }
                else if (Mathf.Abs(newT - t) > 0.0001f)
                {
                    finalScale = minScale + (maxScale - minScale) * Mathf.Pow(newT, power);
                }

                prop.floatValue = 1.0f / Mathf.Max(0.00001f, finalScale);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeaderButtons()
        {
            if (_primaryButtonStyle == null)
            {
                _primaryButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Open in Live Preview Window", _primaryButtonStyle, GUILayout.Height(50)))
            {
                RockPreviewWindow.Open(settings);
            }
            EditorGUILayout.Space(10);
        }

        private void DrawGeneralSettings()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _showGeneral = EditorGUILayout.Foldout(_showGeneral, "General & Output", true, _foldoutStyle);
            if (_showGeneral)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(saveFolderPath, new GUIContent("Output Directory"));
                if (GUILayout.Button("Browse...", EditorStyles.miniButton, GUILayout.Width(75)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Output Directory", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (path.StartsWith(Application.dataPath)) saveFolderPath.stringValue = "Assets" + path.Substring(Application.dataPath.Length);
                        else Debug.LogWarning("Please select a directory inside the Assets folder.");
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(exportName, new GUIContent("Export Name"));
                if (baseShape != null) EditorGUILayout.PropertyField(baseShape, new GUIContent("Base Mesh Shape"));

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(rockType);
                if (EditorGUI.EndChangeCheck()) HandleRockTypePresets(false);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(seed, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Randomize", EditorStyles.miniButton, GUILayout.Width(80))) seed.intValue = UnityEngine.Random.Range(1, 1000000);
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(5);
        }

        private void DrawCoreShapeSettings()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _showShape = EditorGUILayout.Foldout(_showShape, "Core Shape & Sizing", true, _foldoutStyle);
            if (_showShape)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(
                    targetDiameter,
                    new GUIContent(
                        "Target Diameter (Meters)",
                        "Approximate generated mesh diameter before Prefab Scale is applied.\n\n" +
                        "Lite presets are authored around a 2 meter generated rock. Changing Target Diameter regenerates the mesh at a different physical size and is used by diameter-relative systems such as baked bump and micro-detail scale limits.\n\n" +
                        "Because procedural texture and noise patterns are evaluated more like mesh/world-space patterns, changing Target Diameter can make surface detail feel broader, tighter, stronger, or weaker. Use Prefab Scale when you only want the same finished rock larger or smaller."
                    )
                );

                EditorGUILayout.PropertyField(
                    prefabScale,
                    new GUIContent(
                        "Prefab Scale",
                        "Final uniform scale applied after the rock has been generated.\n\n" +
                        "Use this when you want the same generated rock larger or smaller without recalculating procedural noise, baked bump scale limits, texture relationships, or mesh detail. This is usually the best control for simple scene-size variation."
                    )
                );

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Base Proportions (Pre-Noise Stretch)", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(
                    randomizeProportions,
                    new GUIContent(
                        "Randomize per Seed",
                        "If enabled, each seed can stretch the base rock differently within the Min/Max limits before noise deformation is applied."
                    )
                );

                if (randomizeProportions.boolValue)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(
                        minRandomProportions,
                        new GUIContent("Min Limits", "The smallest allowed X/Y/Z proportions when proportions are randomized per seed.")
                    );

                    EditorGUILayout.PropertyField(
                        maxRandomProportions,
                        new GUIContent("Max Limits", "The largest allowed X/Y/Z proportions when proportions are randomized per seed.")
                    );

                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        baseProportions,
                        new GUIContent(
                            "Fixed Proportions (X, Y, Z)",
                            "Manually flattens, stretches, or elongates the base shape before procedural displacement is applied."
                        )
                    );
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
        }

        private void DrawProceduralTexturingSettings()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _showTexturing = EditorGUILayout.Foldout(_showTexturing, "Procedural Texturing & Bump", true, _foldoutStyle);
            if (_showTexturing)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);

                GUIContent colorMethodTooltip = new GUIContent(
                    "Output Texturing Method",
                    "Vertex Colors: lightweight color output with no baked texture files. Useful for mobile/VR or very large scatter counts.\n\n" +
                    "Procedural Texture Bake: generates baked PBR texture files for higher visual detail.\n\n" +
                    "Triplanar Input Bake: bakes supplied albedo/normal textures together with the procedural color and bump system."
                );
                EditorGUILayout.PropertyField(colorizationMethod, colorMethodTooltip);

                bool isBaking = colorizationMethod.enumValueIndex != (int)RockColorizationMethod.VertexColors;

                if (isBaking)
                {
                    int[] sizes = { 0x100, 0x200, 0x400, 0x800, 0x1000 };

                    GUIContent[] sizeLabels =
                    {
                new GUIContent("256", "Low-resolution bake. Fast and lightweight."),
                new GUIContent("512", "Medium-resolution bake. Good for quick iteration."),
                new GUIContent("1024", "Maximum Lite bake resolution. Recommended for final Lite exports."),
                new GUIContent("Pro (2048)", "2K texture baking is available in Rock Generator Pro."),
                new GUIContent("Pro (4096)", "4K texture baking is available in Rock Generator Pro.")
            };

                    EditorGUI.BeginChangeCheck();
                    int selectedRes = EditorGUILayout.IntPopup(
                        new GUIContent(
                            "Bake Resolution (Albedo)",
                            "Resolution of the baked albedo texture. Lite supports up to 1024. Higher bake resolutions are Pro features."
                        ),
                        textureResolution.intValue,
                        sizeLabels,
                        sizes
                    );

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (selectedRes > 0x400)
                        {
                            EditorUtility.DisplayDialog(
                                "Pro Feature",
                                "High-resolution 2K and 4K texture baking is included in Rock Generator Pro.\n\nLite supports up to 1024 for baked texture output.",
                                "OK"
                            );
                            textureResolution.intValue = 0x400;
                        }
                        else
                        {
                            textureResolution.intValue = selectedRes;
                        }
                    }

                    EditorGUILayout.PropertyField(
                        normalMapResolution,
                        new GUIContent(
                            "Normal Map Resolution Scale",
                            "Resolution scale of the baked normal map relative to the albedo map. Full matches the albedo resolution; lower settings save memory."
                        )
                    );

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Map Generation & Export", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(
                        auxiliaryMapResolution,
                        new GUIContent(
                            "Auxiliary Map Resolution Scale",
                            "Resolution scale of metallic, AO, height, and smoothness-related maps relative to the albedo map. Lower settings save memory and disk space."
                        )
                    );

                    EditorGUILayout.PropertyField(
                        textureExportMode,
                        new GUIContent(
                            "Texture Export Mode",
                            "Packed Mask Map stores Metallic in R, Ambient Occlusion in G, and Smoothness in A. This is the safest Unity material workflow, especially for HDRP.\n\n" +
                            "Individual Maps exports separate grayscale utility textures for custom/manual material workflows."
                        )
                    );

                    EditorGUILayout.Space(5);

                    EditorGUILayout.PropertyField(
                        generateAO,
                        new GUIContent(
                            "Generate AO Map",
                            "Generates ambient occlusion based on cavities and crevices. AO can help baked rocks feel more grounded and less flat."
                        )
                    );

                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        aoStrength,
                        new GUIContent(
                            "AO Strength",
                            "Controls how dark the generated cavity occlusion becomes. Strong AO can add depth, but very high values may look dirty or painted."
                        )
                    );
                    EditorGUI.indentLevel--;

                    EditorGUILayout.PropertyField(
                        generateHeight,
                        new GUIContent(
                            "Generate Height Map",
                            "Exports a height-style utility map based on the procedural bump pattern. In Lite this is mainly for custom material workflows; generated materials do not rely on advanced displacement."
                        )
                    );

                    EditorGUILayout.PropertyField(
                        generateSmoothness,
                        new GUIContent(
                            "Generate Smoothness Map",
                            "Exports smoothness information. In Lite, texture-driven smoothness is mainly useful for metallic/mineral rocks or custom material editing. Ordinary dry stone usually uses Base Smoothness."
                        )
                    );

                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        baseSmoothness,
                        new GUIContent(
                            "Base Smoothness",
                            "Scalar smoothness used for ordinary non-metal stone and as a fallback when no material smoothness map is active. Low values are usually best for dry rock.\n\n" +
                            "Values above roughly 0.15 can make ordinary dry stone look wet, polished, or plastic. Higher values can still be useful for obsidian, marble, wet rocks, or minerals."
                        )
                    );
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Metals & Minerals", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(
                        metallicStyle,
                        new GUIContent(
                            "Metallic Style",
                            "Adds procedural metallic/mineral deposits such as veins, cavity deposits, or crystalline nodules. Smoothness maps are most useful when a metallic style is active."
                        )
                    );

                    if (metallicStyle.enumValueIndex != (int)RockMetallicStyle.None)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(
                            oreColor,
                            new GUIContent("Ore Color", "Color tint used for metallic/mineral deposits.")
                        );
                        DrawInverseSlider(oreFrequency, "Ore Scale", 0.05f, 10f);
                        EditorGUILayout.PropertyField(
                            oreCoverage,
                            new GUIContent("Ore Coverage", "Approximate amount of the surface covered by ore/mineral deposits.")
                        );
                        EditorGUILayout.PropertyField(
                            oreMetallic,
                            new GUIContent("Ore Metallic", "Metallic value of the ore/mineral deposits. 1.0 is fully metallic.")
                        );
                        EditorGUILayout.PropertyField(
                            oreSmoothness,
                            new GUIContent("Ore Smoothness", "Smoothness of the ore/mineral deposits. Higher values look more polished or reflective.")
                        );
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.Space(5);

                if (colorizationMethod.enumValueIndex == (int)RockColorizationMethod.TriplanarInputBake)
                {
                    EditorGUILayout.LabelField("Triplanar Input Textures", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(
                        inputAlbedo,
                        new GUIContent("Albedo Map (Tiled)", "Input albedo texture to project across the rock during Triplanar Input Bake mode.")
                    );
                    EditorGUILayout.PropertyField(
                        inputNormal,
                        new GUIContent("Normal Map (Tiled)", "Input normal map to blend with the generated procedural bump during Triplanar Input Bake mode.")
                    );
                    EditorGUILayout.PropertyField(
                        inputTextureScale,
                        new GUIContent("Triplanar Tiling Scale", "How often the input textures tile across the rock. Higher values make the input texture details smaller.")
                    );
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Procedural Overlays (Multipliers)", EditorStyles.boldLabel);
                }

                EditorGUILayout.PropertyField(
                    colorPattern,
                    new GUIContent(
                        "Color Pattern Style",
                        "Controls how procedural colors are distributed: slope/cavity tinting, sedimentary strata, or organic patching."
                    )
                );
                EditorGUILayout.Space(5);

                if (colorPattern.enumValueIndex == (int)RockColorPattern.SedimentaryStrata)
                {
                    EditorGUILayout.PropertyField(primaryColor, new GUIContent("Base Strata Color"));
                    EditorGUILayout.PropertyField(secondaryColor, new GUIContent("Mid Strata Color"));
                    EditorGUILayout.PropertyField(tertiaryColor, new GUIContent("Dark Strata Color"));

                    EditorGUILayout.Space(5);
                    DrawInverseSlider(texturingNoiseFrequency, "Strata Band Scale", 0.05f, 10f);
                    EditorGUILayout.PropertyField(
                        texturingNoiseBlend,
                        new GUIContent("Strata Roughness", "Adds breakup and irregularity to the sedimentary bands.")
                    );

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Domain Warping (Wavy Bands)", EditorStyles.miniBoldLabel);
                    DrawInverseSlider(strataWarpFrequency, "Warp Scale", 0.05f, 10f);
                    EditorGUILayout.PropertyField(
                        strataWarpStrength,
                        new GUIContent("Warp Strength", "How strongly the strata bands bend and flow.")
                    );
                }
                else if (colorPattern.enumValueIndex == (int)RockColorPattern.OrganicPatches)
                {
                    EditorGUILayout.PropertyField(primaryColor, new GUIContent("Base Rock Color"));
                    EditorGUILayout.PropertyField(secondaryColor, new GUIContent("Primary Patch Color"));
                    EditorGUILayout.PropertyField(tertiaryColor, new GUIContent("Secondary Patch Color"));

                    EditorGUILayout.Space(5);
                    DrawInverseSlider(patchFrequency, "Patch Scale", 0.05f, 10f);
                    DrawInverseSlider(texturingNoiseFrequency, "Noise Breakup Scale", 0.05f, 10f);
                    EditorGUILayout.PropertyField(
                        texturingNoiseBlend,
                        new GUIContent("Edge Breakup Intensity", "Controls how noisy or blended the patch edges become.")
                    );
                }
                else
                {
                    EditorGUILayout.PropertyField(primaryColor, new GUIContent("Primary Color (Base)"));
                    EditorGUILayout.PropertyField(secondaryColor, new GUIContent("Secondary Color (Slopes)"));

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Slope Placement Settings", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(
                        slopeMode,
                        new GUIContent("Slope Mode", "Controls whether secondary color appears on upward-facing slopes, both upward/downward slopes, or not at all.")
                    );

                    if (slopeMode.enumValueIndex != (int)RockSlopeMode.None)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(
                            slopeThreshold,
                            new GUIContent("Spread Amount", "0 = barely visible on flat ground. 1 = spreads widely over the rock.")
                        );
                        EditorGUILayout.PropertyField(
                            slopeSmoothness,
                            new GUIContent("Blend Smoothness", "Softens the transition between base color and slope color.")
                        );
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Pattern Breakup", EditorStyles.miniBoldLabel);
                    DrawInverseSlider(texturingNoiseFrequency, "Pattern Scale", 0.05f, 10f);
                    EditorGUILayout.PropertyField(
                        texturingNoiseBlend,
                        new GUIContent("Pattern Breakup Intensity", "Adds procedural variation to the color blend so the surface feels less uniform.")
                    );
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(
                    cavityColor,
                    new GUIContent("Cavity Color (Crevices)", "Color blended into crevices and cavity-like surface areas.")
                );
                EditorGUILayout.PropertyField(
                    cavityStrength,
                    new GUIContent("Cavity Shadow Intensity", "Controls how strongly cavity coloration darkens or tints cracks and recessed areas.")
                );

                if (isBaking)
                {
                    EditorGUILayout.Space(10);

                    GUIContent bumpToggleTooltip = new GUIContent(
                        "Enable Procedural Bump Map",
                        "Bakes procedural surface relief into the normal map. Most baked-texture rocks benefit from at least a small amount of bump; without it, surfaces can look flat.\n\n" +
                        "Very high strength or a poorly matched bump scale can look noisy, lumpy, or stylized. Tune Bump Scale and Bump Strength together."
                    );
                    EditorGUILayout.PropertyField(useNormalPerturbation, bumpToggleTooltip);

                    if (useNormalPerturbation.boolValue)
                    {
                        EditorGUI.indentLevel++;

                        DrawInverseSlider(normalNoiseFrequency, "Bump Scale", 0.05f, 10f);

                        EditorGUILayout.PropertyField(
                            normalNoiseStrength,
                            new GUIContent(
                                "Bump Strength",
                                "Controls how strongly the procedural bump affects the baked normal map. Moderate values are usually best for natural stone. Reduce this if the surface looks too noisy, too lumpy, or too stylized."
                            )
                        );

                        EditorGUILayout.PropertyField(
                            normalMapStrength,
                            new GUIContent(
                                "Baked Normal Strength",
                                "Final multiplier for the baked normal map. Around 1.0 is a good default. Increase only when you deliberately want stronger surface lighting."
                            )
                        );

                        EditorGUI.indentLevel--;

                        EditorGUILayout.Space(5);

                        GUIContent microTooltip = new GUIContent(
                            "Add Micro-Detail Bump",
                            "Adds a fine grit/pore layer on top of the main bump. Use low strength for subtle texture. Too much micro-detail can make the surface look noisy or overly sharp."
                        );
                        EditorGUILayout.PropertyField(useMicroDetail, microTooltip);

                        if (useMicroDetail.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            DrawInverseSlider(microDetailFrequency, "Micro Detail Scale", 0.005f, 0.1f);
                            EditorGUILayout.PropertyField(
                                microDetailStrength,
                                new GUIContent(
                                    "Micro Strength",
                                    "Strength of the fine-grain micro-detail layer. Keep this low unless the rock specifically needs gritty pores, mineral sparkle, or rough sand-like texture."
                                )
                            );
                            EditorGUI.indentLevel--;
                        }
                    }
                    else if (useMicroDetail.boolValue)
                    {
                        useMicroDetail.boolValue = false;
                    }
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(
                    uvScale,
                    new GUIContent(
                        "Triplanar UV Scale (Vertex Color)",
                        "Visual scale for generated triplanar-style UVs. Higher values make projected texture/detail patterns repeat more often."
                    )
                );
                EditorGUILayout.PropertyField(
                    uvBlendSharpness,
                    new GUIContent(
                        "Triplanar Blend Sharpness",
                        "Controls how sharply texture projections blend between directions. Higher values create harder projection transitions."
                    )
                );

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
        }
        private void HandleRockTypePresets(bool forceApply)
        {
            RockType currentType = (RockType)rockType.enumValueIndex;
            if (currentType == RockType.Custom && !forceApply) return;

            useMacroNoise.boolValue = true;
            colorPattern.enumValueIndex = (int)RockColorPattern.SlopeAndCavity;
            if (detailFractalType != null) detailFractalType.enumValueIndex = (int)RockFractalMode.Standard;

            switch (currentType)
            {
                case RockType.Igneous:
                    useVoronoi.boolValue = true; useDomainWarping.boolValue = false; useTerracing.boolValue = false;
                    if (detailFractalType != null) detailFractalType.enumValueIndex = (int)RockFractalMode.SwissErosion;
                    if (voronoiOutputType != null) voronoiOutputType.enumValueIndex = (int)RockVoronoiOutputType.F2MinusF1;
                    break;
                case RockType.Metamorphic:
                    useDomainWarping.boolValue = true; useVoronoi.boolValue = false; useTerracing.boolValue = false;
                    if (detailFractalType != null) detailFractalType.enumValueIndex = (int)RockFractalMode.Ridged;
                    break;
                case RockType.Sedimentary:
                    useTerracing.boolValue = true; useDomainWarping.boolValue = false; useVoronoi.boolValue = false;
                    colorPattern.enumValueIndex = (int)RockColorPattern.SedimentaryStrata;
                    if (detailFractalType != null) detailFractalType.enumValueIndex = (int)RockFractalMode.Ridged;
                    break;
            }
        }

        private void DrawLODSettings()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _showLOD = EditorGUILayout.Foldout(_showLOD, "LOD Settings", true, _foldoutStyle);
            if (_showLOD)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(lodLevels, true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(5);
        }

        private void DrawPhysicsSettings()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _showPhysics = EditorGUILayout.Foldout(_showPhysics, "Physics & Collisions", true, _foldoutStyle);
            if (_showPhysics)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(
                    colliderType,
                    new GUIContent(
                        "Collider Type",
                        "Type of collider generated on the root rock object.\n\n" +
                        "Convex Mesh uses a dedicated low-poly convex source and is the recommended default for normal physics objects.\n\n" +
                        "Exact Mesh uses a non-convex mesh collider. Use it only for large static cliffs or boulders where players need to walk inside crevices."
                    )
                );
                bool typeChanged = EditorGUI.EndChangeCheck();

                int typeIndex = colliderType.enumValueIndex;

                if (typeChanged && typeIndex == (int)RockColliderType.ExactMesh)
                {
                    if (lodLevels.arraySize > 0)
                    {
                        colliderLODIndex.intValue = lodLevels.arraySize - 1;
                    }
                }

                if (typeIndex == (int)RockColliderType.ExactMesh)
                {
                    if (lodLevels.arraySize > 0)
                    {
                        GUIContent[] options = new GUIContent[lodLevels.arraySize];
                        int[] values = new int[lodLevels.arraySize];

                        for (int i = 0; i < lodLevels.arraySize; i++)
                        {
                            int tris = settings.GetTriangleCountForLOD(i);

                            string label = $"Use LOD {i} (~{tris:N0} Tris)";
                            if (i == 0) label += " (Highest Detail)";
                            else if (i == lodLevels.arraySize - 1) label += " (Lowest Detail)";

                            options[i] = new GUIContent(
                                label,
                                "Mesh used by the non-convex Exact Mesh collider. Lower-detail LODs are usually safer and cheaper for collision."
                            );
                            values[i] = i;
                        }

                        colliderLODIndex.intValue = EditorGUILayout.IntPopup(
                            new GUIContent(
                                "Mesh Source",
                                "LOD mesh used for the Exact Mesh collider. Exact Mesh colliders are non-convex and should usually be reserved for large static rocks or cliffs."
                            ),
                            colliderLODIndex.intValue,
                            options,
                            values
                        );
                    }
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
        }

        private void DrawNoiseConfiguration()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _showNoise = EditorGUILayout.Foldout(_showNoise, "Noise Configuration", true, _foldoutStyle);
            if (_showNoise)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                EditorGUI.BeginChangeCheck();

                DrawFeatureToggle(useMacroNoise, "Macro Noise (Large Shapes)");
                if (useMacroNoise.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(macroFractalType, new GUIContent("Fractal Type"));

                    DrawExponentialInverseSlider(macroNoiseFrequency, "Macro Scale", 0.1f, 100f, 4f);

                    EditorGUILayout.PropertyField(macroNoiseStrength, new GUIContent("Displacement Strength"));
                    EditorGUILayout.PropertyField(macroNoiseOctaves, new GUIContent("Octaves"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(15);

                EditorGUILayout.LabelField("Detail Noise (FBM)", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(detailFractalType, new GUIContent("Fractal Type"));
                DrawInverseSlider(noiseFrequency, "Detail Scale", 0.05f, 10f);
                EditorGUILayout.PropertyField(noiseStrength, new GUIContent("Displacement Strength"));
                EditorGUILayout.PropertyField(octaves, new GUIContent("Octaves"));
                EditorGUILayout.PropertyField(lacunarity, new GUIContent("Lacunarity"));
                EditorGUILayout.PropertyField(persistence, new GUIContent("Persistence"));
                EditorGUILayout.Space(15);

                EditorGUILayout.LabelField("Modifiers", EditorStyles.miniBoldLabel);

                DrawFeatureToggle(useDomainWarping, "Domain Warping (Flow/Twisting)");
                if (useDomainWarping.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(warpStrength, new GUIContent("Warp Strength"));
                    DrawInverseSlider(warpFrequency, "Warp Scale", 0.05f, 10f);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(5);

                DrawFeatureToggle(useVoronoi, "Voronoi (Cracks & Crystals)");
                if (useVoronoi.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(voronoiOutputType, new GUIContent("Pattern Output"));
                    EditorGUILayout.PropertyField(voronoiMetric, new GUIContent("Distance Metric"));
                    DrawInverseSlider(voronoiFrequency, "Voronoi Scale", 0.05f, 10f);
                    EditorGUILayout.PropertyField(voronoiIntensity, new GUIContent("Blend Intensity"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(5);

                DrawFeatureToggle(useTerracing, "Terracing (Layered/Stratified)");
                if (useTerracing.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(terraceCount);
                    EditorGUILayout.PropertyField(terraceIntensity);
                    EditorGUI.indentLevel--;
                }

                if (EditorGUI.EndChangeCheck() && (RockType)rockType.enumValueIndex != RockType.Custom)
                {
                    rockType.enumValueIndex = (int)RockType.Custom;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(10);
        }
        private void DrawFeatureToggle(SerializedProperty toggleProperty, string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(toggleProperty, GUIContent.none, GUILayout.Width(20));
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }
    }

    [CustomPropertyDrawer(typeof(LODLevel))]
    public class LODLevelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty baseShapeProp = property.serializedObject.FindProperty("baseShape");
            bool isCube = baseShapeProp != null && baseShapeProp.enumValueIndex == (int)RockBaseShape.CubeSphere;
            Rect levelRect = new Rect(position.x, position.y, position.width - 90, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(position.xMax - 85, position.y, 85, EditorGUIUtility.singleLineHeight);
            Rect transRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);

            if (isCube)
            {
                SerializedProperty resProp = property.FindPropertyRelative("resolution");
                EditorGUI.PropertyField(levelRect, resProp, new GUIContent("Grid Resolution"));
                int tris = resProp.intValue * resProp.intValue * 12;
                EditorGUI.LabelField(labelRect, $"~{tris:N0} Tris", EditorStyles.miniLabel);
            }
            else
            {
                SerializedProperty subProp = property.FindPropertyRelative("subdivisionLevel");
                EditorGUI.PropertyField(levelRect, subProp, new GUIContent("Subdivisions (0-6)"));
                int tris = (int)(20 * Mathf.Pow(4, subProp.intValue));
                EditorGUI.LabelField(labelRect, $"~{tris:N0} Tris", EditorStyles.miniLabel);
            }
            EditorGUI.PropertyField(transRect, property.FindPropertyRelative("screenRelativeTransitionHeight"), new GUIContent("Screen Transition %"));
            EditorGUI.EndProperty();
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return (EditorGUIUtility.singleLineHeight * 2) + 4; }
    }
}
#endif