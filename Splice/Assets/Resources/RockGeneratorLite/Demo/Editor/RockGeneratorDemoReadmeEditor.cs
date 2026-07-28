// RockGeneratorDemoReadmeEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Veridian.RockGenLite.Demo.Editor
{
    [CustomEditor(typeof(RockGeneratorDemoReadme))]
    public class RockGeneratorDemoReadmeEditor : UnityEditor.Editor
    {
        private GUIStyle _headerStyle;
        private GUIStyle _boldButtonStyle;
        private GUIStyle _stepTitleStyle;
        private GUIStyle _stepWrapStyle;

        public override void OnInspectorGUI()
        {
            GUILayout.Space(10);

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }

            _headerStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.6f, 0.8f, 1f)
                : new Color(0.1f, 0.3f, 0.5f);

            if (_boldButtonStyle == null)
            {
                _boldButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }

            GUILayout.Label("Veridian Rock Generator Lite | Quick Start Guide", _headerStyle);
            GUILayout.Space(15);

            DrawStep("1. Generate an Environment Canvas",
                "Open the Demo Orchestrator and click 'Generate Realistic Terrain Canvas'. This creates a small procedural terrain so you can test generated rocks without shipping bulky demo content.");

            DrawStep("2. Design or Select a Rock",
                "Open the Rock Window to tweak a rock manually, or use one of the preset buttons below to send a Lite preset to the Demo Orchestrator.");

            DrawStep("3. Generate a Demo Variation",
                "Return to the Demo Orchestrator and click 'Bake Demo Rock'. The Orchestrator generates a random-seeded prefab variant of the selected preset or custom profile.");

            DrawStep("4. Scatter & Populate",
                "After baking, click 'Push to Rock Placer Window'. The Placer appends the generated prefab to the palette so you can scatter it across the terrain.");

            DrawStep("5. Size Note",
                "Lite presets are authored around a 2m Target Diameter. Use Target Diameter when you want to regenerate a rock at a different physical size. Use Prefab Scale when you want the same generated rock larger or smaller.");

            DrawStep("6. Optimization Note (Pro)",
                "Rock Generator Pro expands the Lite workflow with 50+ Pro profiles, mass batching, material/texture combining, terrain slicing, splat-map workflows, and more advanced placement/generation tools.");

            GUILayout.Space(20);

            Color prevColor = GUI.backgroundColor;

            GUILayout.BeginVertical("box");
            GUILayout.Label("Tool Shortcuts", EditorStyles.boldLabel);
            GUILayout.Space(5);

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.7f, 0.85f, 1.0f);
            if (GUILayout.Button("1. Open Demo Orchestrator", _boldButtonStyle, GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Veridian/Rock Generator Lite/2. Demo Orchestrator");
            }

            GUILayout.Space(5);
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.6f, 0.2f) : new Color(1.0f, 0.85f, 0.6f);
            if (GUILayout.Button("2. Open Rock Window (Generator)", _boldButtonStyle, GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Veridian/Rock Generator Lite/1. Rock Window");
            }

            GUILayout.Space(5);
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.8f, 0.5f) : new Color(0.7f, 0.95f, 0.75f);
            if (GUILayout.Button("3. Open Rock Placer (Scatter Tool)", _boldButtonStyle, GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Veridian/Rock Generator Lite/3. Rock Placer");
            }

            GUI.backgroundColor = prevColor;
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUILayout.Space(15);

            DrawDemoPresetGrid(prevColor);

            GUI.backgroundColor = prevColor;
        }
        private void DrawDemoPresetGrid(Color prevColor)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Load Demo Presets", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Hover over a preset to see what it is intended for. Presets are tuned around a 2m generated rock; use Prefab Scale for simple scene-size variation.",
                MessageType.Info
            );

            RockPresetType[] presets = GetVisibleDemoPresets();

            const int columns = 3;

            for (int i = 0; i < presets.Length; i += columns)
            {
                GUILayout.BeginHorizontal();

                for (int col = 0; col < columns; col++)
                {
                    int index = i + col;

                    if (index < presets.Length)
                    {
                        RockPresetType preset = presets[index];
                        string displayName = GetPresetDisplayName(preset);

                        GUI.backgroundColor = GetPresetButtonColor(index);

                        GUIContent content = new GUIContent(
                            displayName,
                            Veridian.RockGenLite.Editor.RockPresetUtility.GetPresetTooltip(preset)
                        );

                        if (GUILayout.Button(content, _boldButtonStyle, GUILayout.Height(30)))
                        {
                            HandlePresetSelection(preset, displayName);
                        }
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUI.backgroundColor = prevColor;
            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        private RockPresetType[] GetVisibleDemoPresets()
        {
            System.Array rawValues = System.Enum.GetValues(typeof(RockPresetType));
            System.Collections.Generic.List<RockPresetType> presets = new System.Collections.Generic.List<RockPresetType>();

            foreach (object rawValue in rawValues)
            {
                RockPresetType preset = (RockPresetType)rawValue;

                if (preset == RockPresetType.None)
                {
                    continue;
                }

                presets.Add(preset);
            }

            return presets.ToArray();
        }

        private string GetPresetDisplayName(RockPresetType preset)
        {
            return ObjectNames.NicifyVariableName(preset.ToString());
        }

        private Color GetPresetButtonColor(int index)
        {
            bool proSkin = EditorGUIUtility.isProSkin;

            Color[] proPalette =
            {
        new Color(0.70f, 0.50f, 0.30f),
        new Color(0.32f, 0.34f, 0.38f),
        new Color(0.45f, 0.58f, 0.42f),
        new Color(0.65f, 0.58f, 0.72f),
        new Color(0.42f, 0.55f, 0.68f),
        new Color(0.68f, 0.48f, 0.36f)
    };

            Color[] lightPalette =
            {
        new Color(0.90f, 0.70f, 0.50f),
        new Color(0.55f, 0.58f, 0.64f),
        new Color(0.70f, 0.86f, 0.66f),
        new Color(0.82f, 0.72f, 0.90f),
        new Color(0.68f, 0.78f, 0.90f),
        new Color(0.90f, 0.68f, 0.55f)
    };

            Color[] palette = proSkin ? proPalette : lightPalette;
            return palette[index % palette.Length];
        }

        private void DrawPresetButton(RockPresetType preset, string presetName, Color proSkinColor, Color lightSkinColor)
        {
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? proSkinColor : lightSkinColor;

            if (GUILayout.Button(presetName, _boldButtonStyle, GUILayout.Height(30)))
            {
                HandlePresetSelection(preset, presetName);
            }
        }



        private void HandlePresetSelection(RockPresetType preset, string presetName)
        {
            string description = Veridian.RockGenLite.Editor.RockPresetUtility.GetPresetDescription(preset);

            if (EditorUtility.DisplayDialog(
                "Preset Selected",
                $"{presetName}\n\n{description}\n\nWould you like to open the Demo Orchestrator to bake a random-seeded 3D rock variant from this preset?",
                "Open Orchestrator",
                "Cancel"))
            {
                RockDemoWindow window = EditorWindow.GetWindow<RockDemoWindow>("Demo Orchestrator Lite");
                window.minSize = new Vector2(400, 650);
                window.LoadPreset(preset);
                window.Show();
                window.Focus();
            }
        }

        private void DrawStep(string title, string description)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            if (_stepTitleStyle == null) _stepTitleStyle = new GUIStyle(EditorStyles.boldLabel);
            GUILayout.Label(title, _stepTitleStyle);

            if (_stepWrapStyle == null) _stepWrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _stepWrapStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);

            GUILayout.Label(description, _stepWrapStyle);
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
    }
}
#endif