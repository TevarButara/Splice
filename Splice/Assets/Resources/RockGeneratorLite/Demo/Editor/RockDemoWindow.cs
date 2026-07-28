#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Veridian.RockGenLite.Editor;

namespace Veridian.RockGenLite.Demo.Editor
{
    public class RockDemoWindow : EditorWindow
    {

        private const string DefaultDemoBasePath = "Assets/VeridianData/RockGenLite/Demo_Assets";
        private const string DemoFolderMarkerFileName = "_RockGenLite_DemoFolderMarker.txt";
        private const string DemoFolderMarkerToken = "VERIDIAN_ROCK_GENERATOR_LITE_DEMO_FOLDER_MARKER_v1";

        private Vector2 _scrollPos;

        [Header("Output Path")]
        [SerializeField] private string _demoBasePath = DefaultDemoBasePath;

        [Header("Environment Settings")]
        [SerializeField] private int _terrainSize = 256;
        [SerializeField] private float _heightMultiplier = 80f;

        [Header("Terrain Noise Setup")]
        [SerializeField] private float _noiseScale = 0.005f;
        [SerializeField] private int _terrainOctaves = 5;
        [SerializeField] private float _terrainPersistence = 0.45f;
        [SerializeField] private float _terrainLacunarity = 2.0f;
        [SerializeField] private float _terrainRidgeStrength = 0.15f;

        private static readonly int[] _sizeOptions = { 128, 256, 512, 1024 };

        private static readonly GUIContent[] _sizeLabels =
        {
    new GUIContent("128x128", "Small terrain canvas. Fast to generate and useful for quick placement tests."),
    new GUIContent("256x256 (Default)", "Default demo terrain size. Good balance for testing Rock Generator Lite and the Rock Placer."),
    new GUIContent("512x512", "Larger terrain canvas for broader placement tests."),
    new GUIContent("1024x1024", "Large demo terrain canvas. Slower to generate and mainly useful for stress-testing placement.")
};
        [Header("Demo Generator")]
        [SerializeField] private RockPresetType _demoPreset = RockPresetType.DesertSandstone;
        [SerializeField] private RockSettings _customProfile;

        private List<GameObject> _bakedPrefabs = new List<GameObject>();
        private bool _isBaking = false;
        private GUIStyle _boldButtonStyle;
        // ----------------------------------

        [MenuItem("Tools/Veridian/Rock Generator Lite/2. Demo Orchestrator", false, 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<RockDemoWindow>("Demo Orchestrator Lite");
            window.minSize = new Vector2(400, 650);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            _demoBasePath = EditorPrefs.GetString("Veridian_RockDemoGenerator_Path", DefaultDemoBasePath);
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;

            // Save global output path
            EditorPrefs.SetString("Veridian_RockDemoGenerator_Path", _demoBasePath);
        }

        private void OnEditorUpdate()
        {
            if (_isBaking)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (_boldButtonStyle == null)
            {
                _boldButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            Color prevColor = GUI.backgroundColor;

            EditorGUILayout.Space(10);
            GUILayout.Label("Global Output Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            _demoBasePath = EditorGUILayout.TextField(
                new GUIContent(
                    "Demo Output Path",
                    "Folder where the Demo Orchestrator writes generated terrain, textures, rock prefabs, and demo assets."
                ),
                _demoBasePath
            );

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString("Veridian_RockDemoGenerator_Path", _demoBasePath);
            }

            if (GUILayout.Button("Browse...", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Demo Output Directory", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        _demoBasePath = "Assets" + path.Substring(Application.dataPath.Length);
                        EditorPrefs.SetString("Veridian_RockDemoGenerator_Path", _demoBasePath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Path", "Please select a directory inside the Assets folder.", "OK");
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUILayout.Label("Step 1: Generate Environment", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("Builds a fresh, procedurally textured terrain with cliffs and valleys. This gives you a simple test canvas for generated rocks and the Rock Placer.", MessageType.Info);

            GUI.enabled = !_isBaking;
            _terrainSize = EditorGUILayout.IntPopup(
           new GUIContent("Terrain Size", "Resolution of the generated demo terrain canvas."),
           _terrainSize,
           _sizeLabels,
           _sizeOptions
       );
            _heightMultiplier = EditorGUILayout.Slider(new GUIContent("Height Multiplier", "Vertical height range of the generated terrain."), _heightMultiplier, 10f, 300f);

            EditorGUILayout.Space(5);
            GUILayout.Label("Cliff Fractal Settings", EditorStyles.miniBoldLabel);
            _noiseScale = EditorGUILayout.Slider(new GUIContent("Noise Scale", "Lower values create broader terrain features; higher values create tighter terrain variation."), _noiseScale, 0.001f, 0.03f);
            _terrainOctaves = EditorGUILayout.IntSlider(new GUIContent("Octaves", "Number of stacked noise layers used for terrain height variation."), _terrainOctaves, 1, 8);
            _terrainPersistence = EditorGUILayout.Slider(new GUIContent("Persistence", "Controls how strongly smaller terrain noise layers contribute."), _terrainPersistence, 0.1f, 1.0f);
            _terrainLacunarity = EditorGUILayout.Slider(new GUIContent("Lacunarity", "Controls how quickly terrain noise frequency increases between octaves."), _terrainLacunarity, 1.0f, 4.0f);
            _terrainRidgeStrength = EditorGUILayout.Slider(new GUIContent("Ridge Strength", "Adds sharper cliff/ridge breakup to the demo terrain."), _terrainRidgeStrength, 0.0f, 0.5f);

            EditorGUILayout.Space(5);

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.7f, 0.85f, 1.0f);
            if (GUILayout.Button("Generate Realistic Terrain Canvas", _boldButtonStyle, GUILayout.Height(35)))
            {
                GenerateTerrain();
            }

            GUI.backgroundColor = prevColor;
            EditorGUILayout.EndVertical();
            GUI.enabled = true;

            EditorGUILayout.Space(15);
            GUILayout.Label("Step 2: Generate Demo Rock", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawProfileGenerators(prevColor);

            EditorGUILayout.HelpBox(
                "Select a Lite preset or assign a custom Rock Profile, then bake a random-seeded prefab variant. Lite presets are authored around a 2m generated rock; use the Rock Window's Prefab Scale when you want the same rock larger or smaller.",
                MessageType.Info
            );

            GUI.enabled = !_isBaking;

            _demoPreset = (RockPresetType)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Demo Preset",
                    "Built-in Lite preset to bake through the Demo Orchestrator."
                ),
                _demoPreset
            );

            if (_demoPreset != RockPresetType.None)
            {
                EditorGUILayout.HelpBox(RockPresetUtility.GetPresetDescription(_demoPreset), MessageType.None);
            }

            _customProfile = (RockSettings)EditorGUILayout.ObjectField(
                new GUIContent(
                    "OR Custom Profile",
                    "Optional custom RockSettings asset. If assigned, this overrides the Demo Preset selection."
                ),
                _customProfile,
                typeof(RockSettings),
                false
            );

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "PRO FEATURE: Rock Generator Pro expands Lite with 50+ Pro profiles, higher-end generation features, mass batching, material/texture combining, and advanced placement workflows.",
                MessageType.Warning
            );

            EditorGUILayout.Space(10);

            bool hasValidProfile = _demoPreset != RockPresetType.None || _customProfile != null;
            GUI.enabled = !_isBaking && hasValidProfile;

            string bakeBtnText = _isBaking ? "Baking..." : "Bake Demo Rock";

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.6f, 0.2f) : new Color(1.0f, 0.85f, 0.6f);
            if (GUILayout.Button(bakeBtnText, _boldButtonStyle, GUILayout.Height(35)))
            {
                BakeSingleRock();
            }

            GUI.backgroundColor = prevColor;
            GUI.enabled = true;

            if (_isBaking)
            {
                EditorGUILayout.Space(5);
                Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(r, 0.5f, "Baking...");
            }

            EditorGUILayout.EndVertical();

            if (_bakedPrefabs.Count > 0 && !_isBaking)
            {
                EditorGUILayout.Space(15);
                GUILayout.Label("Step 3: Scatter & Populate", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("A rock prefab was baked successfully. Push it to the Rock Placer to scatter it across the selected terrain.", MessageType.Info);

                GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.8f, 0.5f) : new Color(0.7f, 0.95f, 0.75f);
                if (GUILayout.Button("Push to Rock Placer Window", _boldButtonStyle, GUILayout.Height(40)))
                {
                    PushToPlacer();
                }

                GUI.backgroundColor = prevColor;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(30);

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.4f, 0.4f) : new Color(1.0f, 0.75f, 0.75f);
            GUI.enabled = !_isBaking;
            if (GUILayout.Button(new GUIContent("Purge All Demo Assets", "Deletes the marker-approved demo output folder and generated demo terrain objects."), GUILayout.Height(30)))
            {
                PurgeDemoAssets();
            }

            GUI.enabled = true;
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndScrollView();
        }
        private void DrawProfileGenerators(Color prevColor)
        {
            GUILayout.Label("Select Quick Preset", EditorStyles.boldLabel);

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

                        GUI.backgroundColor = GetPresetButtonColor(index);

                        GUIContent presetContent = new GUIContent(
                            GetPresetDisplayName(preset),
                            RockPresetUtility.GetPresetTooltip(preset)
                        );

                        if (GUILayout.Button(presetContent, _boldButtonStyle, GUILayout.Height(30)))
                        {
                            _demoPreset = preset;
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
            GUILayout.Space(10);
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
            return Veridian.RockGenLite.Editor.RockPresetUtility.GetPresetDisplayName(preset);
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
        #region Bake Logic
        private void BakeSingleRock()
        {
            if (_demoPreset == RockPresetType.None && _customProfile == null) return;

            EnsureDirectories();
            string rockOutputFolder = $"{_demoBasePath}/Rocks";

            _bakedPrefabs.Clear();
            _isBaking = true;
            Repaint();

            RockSettings clone;
            if (_customProfile != null)
            {
                clone = Instantiate(_customProfile);
                clone.name = $"{_customProfile.name}_DemoVar";
                clone.exportName = $"{_customProfile.exportName}_Demo";
            }
            else
            {
                clone = ScriptableObject.CreateInstance<RockSettings>();
                RockPresetUtility.ApplyPreset(clone, _demoPreset);
                clone.name = $"{_demoPreset}_DemoVar";
                clone.exportName = $"{_demoPreset}_Demo";
            }

            clone.hideFlags = HideFlags.DontSave;
            clone.seed = UnityEngine.Random.Range(1, 9999999);
            clone.saveFolderPath = rockOutputFolder;

            RockPrefabFactory.CreateAndSavePrefab(clone, (generatedPrefab) =>
            {
                if (generatedPrefab != null)
                {
                    _bakedPrefabs.Add(generatedPrefab);
                }

                _isBaking = false;
                DestroyImmediate(clone);
                Debug.Log($"[Demo Orchestrator Lite] Successfully baked rock!");
                Repaint();

            }, true);
        }


        

        private void PushToPlacer()
        {
            _bakedPrefabs.RemoveAll(p => p == null);

            if (_bakedPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No valid prefabs found. You may need to bake them again.", "OK");
                return;
            }

            string normalizedPath = _demoBasePath.Replace('\\', '/');
            Terrain targetTerrain = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
                                .FirstOrDefault(t => t.terrainData != null && IsSameOrChildAssetPath(AssetDatabase.GetAssetPath(t.terrainData), normalizedPath));

            if (targetTerrain == null) targetTerrain = FindAnyObjectByType<Terrain>();

            RockPlacerWindow.InjectPrefabs(targetTerrain, _bakedPrefabs);
        }
        #endregion

        #region Terrain & Procedural Texture Generation
        private void GenerateTerrain()
        {
            EnsureDirectories();

            string normalizedPath = NormalizeDemoAssetPath(_demoBasePath);
            _demoBasePath = normalizedPath;

            if (!HasValidDemoFolderMarker(normalizedPath))
            {
                EditorUtility.DisplayDialog(
                    "Terrain Generation Blocked",
                    "The selected Demo Output Path is not marker-approved, so the terrain generator will not run destructive cleanup or overwrite fixed demo terrain assets there.\n\n" +
                    "Use the default RockGenLite demo folder, choose a new empty folder, or choose a folder that already contains the Rock Generator Lite demo marker asset.",
                    "OK"
                );
                return;
            }

            string texPath = $"{normalizedPath}/Textures";
            string layerPath = $"{normalizedPath}/Layers";

            try
            {
                EditorUtility.DisplayProgressBar("Demo Generator", "Cleaning up old terrains...", 0.1f);

                Terrain[] oldTerrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
                foreach (Terrain t in oldTerrains)
                {
                    if (t != null && t.terrainData != null)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(t.terrainData);
                        if (IsSameOrChildAssetPath(assetPath, normalizedPath))
                        {
                            DestroyImmediate(t.gameObject);
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Demo Generator", "Baking Seamless Textures...", 0.2f);

                string pathStone = GenerateProceduralTexture("Stone", new Color(0.65f, 0.68f, 0.72f), new Color(0.15f, 0.18f, 0.20f), 12f, texPath);
                string pathDirt = GenerateProceduralTexture("Dirt", new Color(0.55f, 0.40f, 0.25f), new Color(0.15f, 0.10f, 0.05f), 18f, texPath);
                string pathGrass = GenerateProceduralTexture("Grass", new Color(0.40f, 0.60f, 0.25f), new Color(0.10f, 0.20f, 0.05f), 24f, texPath);

                AssetDatabase.Refresh();

                Texture2D texStone = ConfigureAndLoadTexture(pathStone);
                Texture2D texDirt = ConfigureAndLoadTexture(pathDirt);
                Texture2D texGrass = ConfigureAndLoadTexture(pathGrass);

                EditorUtility.DisplayProgressBar("Demo Generator", "Building Terrain Layers...", 0.4f);

                TerrainLayer lStone = CreateLayer("Stone", texStone, layerPath);
                TerrainLayer lDirt = CreateLayer("Dirt", texDirt, layerPath);
                TerrainLayer lGrass = CreateLayer("Grass", texGrass, layerPath);
                TerrainLayer[] layers = new TerrainLayer[] { lGrass, lDirt, lStone };

                EditorUtility.DisplayProgressBar("Demo Generator", "Sculpting Heights...", 0.6f);

                string tdPath = $"{normalizedPath}/Demo_TerrainData.asset";
                TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
                if (td != null)
                {
                    AssetDatabase.DeleteAsset(tdPath);
                }

                td = new TerrainData();
                AssetDatabase.CreateAsset(td, tdPath);

                int heightRes = _terrainSize + 1;

                td.heightmapResolution = heightRes;
                td.alphamapResolution = _terrainSize;
                td.size = new Vector3(_terrainSize, _heightMultiplier, _terrainSize);
                td.terrainLayers = layers;

                float[,] heights = new float[heightRes, heightRes];
                float offsetX = UnityEngine.Random.Range(0f, 9999f);
                float offsetZ = UnityEngine.Random.Range(0f, 9999f);

                for (int z = 0; z < heightRes; z++)
                {
                    for (int x = 0; x < heightRes; x++)
                    {
                        float h = 0f;
                        float amplitude = 1f;
                        float frequency = _noiseScale;
                        float maxValue = 0f;

                        for (int i = 0; i < _terrainOctaves; i++)
                        {
                            float sampleX = (x + offsetX) * frequency;
                            float sampleZ = (z + offsetZ) * frequency;

                            h += Mathf.PerlinNoise(sampleX, sampleZ) * amplitude;

                            maxValue += amplitude;
                            amplitude *= _terrainPersistence;
                            frequency *= _terrainLacunarity;
                        }

                        h /= maxValue;

                        float ridgeNoise = Mathf.PerlinNoise((x + offsetX) * _noiseScale * 4f, (z + offsetZ) * _noiseScale * 4f);
                        h += (ridgeNoise - 0.5f) * _terrainRidgeStrength;

                        h = Mathf.SmoothStep(0.2f, 0.8f, h);

                        heights[z, x] = Mathf.Clamp01(h);
                    }
                }
                td.SetHeights(0, 0, heights);

                EditorUtility.DisplayProgressBar("Demo Generator", "Painting Splatmaps...", 0.8f);

                float[,,] splatmap = new float[_terrainSize, _terrainSize, 3];

                for (int z = 0; z < _terrainSize; z++)
                {
                    for (int x = 0; x < _terrainSize; x++)
                    {
                        float normX = (float)x / (_terrainSize - 1);
                        float normZ = (float)z / (_terrainSize - 1);
                        float steepness = td.GetSteepness(normX, normZ);

                        float noiseBlend = (Mathf.PerlinNoise(x * 0.05f, z * 0.05f) - 0.5f) * 20f;
                        float adjustedSteepness = steepness + noiseBlend;

                        float stoneWeight = Mathf.Clamp01((adjustedSteepness - 30f) / 15f);
                        float dirtWeight = Mathf.Clamp01((adjustedSteepness - 15f) / 10f) * (1f - stoneWeight);
                        float grassWeight = Mathf.Max(0f, 1f - stoneWeight - dirtWeight);

                        float total = stoneWeight + dirtWeight + grassWeight;
                        if (total > 0.001f)
                        {
                            stoneWeight /= total;
                            dirtWeight /= total;
                            grassWeight /= total;
                        }
                        else
                        {
                            grassWeight = 1f;
                            stoneWeight = 0f;
                            dirtWeight = 0f;
                        }

                        splatmap[z, x, 0] = grassWeight;
                        splatmap[z, x, 1] = dirtWeight;
                        splatmap[z, x, 2] = stoneWeight;
                    }
                }
                td.SetAlphamaps(0, 0, splatmap);

                GameObject terrainObj = Terrain.CreateTerrainGameObject(td);
                terrainObj.name = $"Demo_Terrain_{_terrainSize}";
                Selection.activeGameObject = terrainObj;
                SceneView.lastActiveSceneView?.FrameSelected();

                EditorUtility.SetDirty(td);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Demo Generator] Failed to generate terrain: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private string GenerateProceduralTexture(string name, Color lightColor, Color darkColor, float noiseFreq, string path)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float offsetX = UnityEngine.Random.Range(0f, 1000f);
            float offsetY = UnityEngine.Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;

                    float n = SeamlessNoiseMulti(u, v, noiseFreq, offsetX, offsetY);

                    n = Mathf.SmoothStep(0.2f, 0.8f, n);

                    pixels[y * size + x] = Color.Lerp(darkColor, lightColor, n);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            string fullPath = $"{path}/DemoTex_{name}.png";
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            return fullPath;
        }

        private Texture2D ConfigureAndLoadTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private float SeamlessNoise(float u, float v, float freq, float offsetX, float offsetY)
        {
            float s = u * freq;
            float t = v * freq;

            float n00 = Mathf.PerlinNoise(s + offsetX, t + offsetY);
            float n10 = Mathf.PerlinNoise(s - freq + offsetX, t + offsetY);
            float n01 = Mathf.PerlinNoise(s + offsetX, t - freq + offsetY);
            float n11 = Mathf.PerlinNoise(s - freq + offsetX, t - freq + offsetY);

            float uBlend = Mathf.SmoothStep(0f, 1f, u);
            float vBlend = Mathf.SmoothStep(0f, 1f, v);

            float valTop = Mathf.Lerp(n00, n10, uBlend);
            float valBot = Mathf.Lerp(n01, n11, uBlend);
            return Mathf.Lerp(valTop, valBot, vBlend);
        }

        private float SeamlessNoiseMulti(float u, float v, float freq, float offsetX, float offsetY)
        {
            float n = SeamlessNoise(u, v, freq, offsetX, offsetY) * 0.57f
                    + SeamlessNoise(u, v, freq * 2f, offsetX, offsetY) * 0.28f
                    + SeamlessNoise(u, v, freq * 4f, offsetX, offsetY) * 0.15f;

            return Mathf.Clamp01(n / 1.0f);
        }

        private TerrainLayer CreateLayer(string name, Texture2D tex, string path)
        {
            string fullPath = $"{path}/DemoLayer_{name}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(fullPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, fullPath);
            }

            layer.name = name;
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(15, 15);
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();
            return layer;
        }
        #endregion

        #region Utility

        private static bool IsSameOrChildAssetPath(string assetPath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            rootPath = rootPath.Replace('\\', '/').TrimEnd('/');

            return assetPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
                   assetPath.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDemoAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return DefaultDemoBasePath;
            }

            string normalized = path.Replace('\\', '/').Trim();

            string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                normalized = "Assets" + normalized.Substring(dataPath.Length);
            }

            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            normalized = normalized.TrimEnd('/');

            bool startsWithAssets =
                normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);

            if (!startsWithAssets)
            {
                normalized = "Assets/" + normalized.TrimStart('/');
            }

            normalized = normalized.TrimEnd('/');

            if (ContainsParentTraversal(normalized))
            {
                return DefaultDemoBasePath;
            }

            return normalized;
        }

        private static bool ContainsParentTraversal(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return true;
            }

            string normalized = assetPath.Replace('\\', '/');

            return normalized.Equals("..", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("../", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("/../", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/..", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeDemoRootPath(string normalizedRootPath)
        {
            if (string.IsNullOrWhiteSpace(normalizedRootPath))
            {
                return false;
            }

            normalizedRootPath = normalizedRootPath.Replace('\\', '/').Trim().TrimEnd('/');

            if (ContainsParentTraversal(normalizedRootPath))
            {
                return false;
            }

            if (normalizedRootPath.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!normalizedRootPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            string normalized = assetPath.Replace('\\', '/').Trim().TrimEnd('/');

            if (normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return Application.dataPath.Replace('\\', '/');
            }

            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string relativeToAssets = normalized.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, relativeToAssets).Replace('\\', '/');
        }

        private static string GetDemoFolderMarkerAssetPath(string normalizedRootPath)
        {
            return $"{normalizedRootPath.TrimEnd('/')}/{DemoFolderMarkerFileName}";
        }

        private static bool HasValidDemoFolderMarker(string normalizedRootPath)
        {
            normalizedRootPath = NormalizeDemoAssetPath(normalizedRootPath);

            if (!IsSafeDemoRootPath(normalizedRootPath))
            {
                return false;
            }

            string markerAssetPath = GetDemoFolderMarkerAssetPath(normalizedRootPath);
            string markerFullPath = AssetPathToFullPath(markerAssetPath);

            if (string.IsNullOrEmpty(markerFullPath) || !File.Exists(markerFullPath))
            {
                return false;
            }

            try
            {
                string markerContents = File.ReadAllText(markerFullPath).Trim();
                return markerContents.Equals(DemoFolderMarkerToken, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFolderSafeToMarkAsDemoOutput(string normalizedRootPath)
        {
            normalizedRootPath = NormalizeDemoAssetPath(normalizedRootPath);

            if (!IsSafeDemoRootPath(normalizedRootPath))
            {
                return false;
            }

            if (normalizedRootPath.Equals(DefaultDemoBasePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string fullRootPath = AssetPathToFullPath(normalizedRootPath);

            if (string.IsNullOrEmpty(fullRootPath))
            {
                return false;
            }

            if (!Directory.Exists(fullRootPath))
            {
                return true;
            }

            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(fullRootPath))
                {
                    string name = Path.GetFileName(entry);

                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (name.Equals(DemoFolderMarkerFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (name.Equals("Textures", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Layers", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Rocks", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Demo_TerrainData.asset", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCreateOrRepairDemoFolderMarker(string normalizedRootPath)
        {
            normalizedRootPath = NormalizeDemoAssetPath(normalizedRootPath);

            if (!IsSafeDemoRootPath(normalizedRootPath))
            {
                return false;
            }

            string fullRootPath = AssetPathToFullPath(normalizedRootPath);

            if (string.IsNullOrEmpty(fullRootPath))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(fullRootPath))
                {
                    Directory.CreateDirectory(fullRootPath);
                }

                string markerAssetPath = GetDemoFolderMarkerAssetPath(normalizedRootPath);
                string markerFullPath = AssetPathToFullPath(markerAssetPath);

                if (string.IsNullOrEmpty(markerFullPath))
                {
                    return false;
                }

                File.WriteAllText(markerFullPath, DemoFolderMarkerToken + Environment.NewLine);

                AssetDatabase.ImportAsset(markerAssetPath, ImportAssetOptions.ForceUpdate);

                return HasValidDemoFolderMarker(normalizedRootPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Demo Orchestrator Lite] Could not create demo folder marker at '{normalizedRootPath}': {e.Message}");
                return false;
            }
        }

        private static bool TryEnsureDemoFolderMarkerIfSafe(string normalizedRootPath)
        {
            normalizedRootPath = NormalizeDemoAssetPath(normalizedRootPath);

            if (HasValidDemoFolderMarker(normalizedRootPath))
            {
                return true;
            }

            if (!IsFolderSafeToMarkAsDemoOutput(normalizedRootPath))
            {
                return false;
            }

            return TryCreateOrRepairDemoFolderMarker(normalizedRootPath);
        }

        private static bool ValidateDemoFolderForDestructiveAction(string normalizedRootPath, out string errorMessage)
        {
            normalizedRootPath = NormalizeDemoAssetPath(normalizedRootPath);

            if (!IsSafeDemoRootPath(normalizedRootPath))
            {
                errorMessage = "For safety, the Demo Output Path must be a project folder inside Assets and cannot be the root Assets folder.";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(normalizedRootPath))
            {
                AssetDatabase.Refresh();
            }

            if (!AssetDatabase.IsValidFolder(normalizedRootPath))
            {
                errorMessage = $"The selected Demo Output Path does not exist as a Unity asset folder:\n\n{normalizedRootPath}";
                return false;
            }

            if (!HasValidDemoFolderMarker(normalizedRootPath))
            {
                TryEnsureDemoFolderMarkerIfSafe(normalizedRootPath);
            }

            if (!HasValidDemoFolderMarker(normalizedRootPath))
            {
                errorMessage =
                    "This folder does not contain a valid Rock Generator Lite demo marker asset, so the tool will not purge it.\n\n" +
                    $"Expected marker:\n{GetDemoFolderMarkerAssetPath(normalizedRootPath)}\n\n" +
                    "Use the default demo folder, choose a new empty folder, or manually move your important assets elsewhere before using this folder as a purgeable demo output folder.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private void EnsureDirectories()
        {
            string rootFolder = NormalizeDemoAssetPath(_demoBasePath);

            if (!IsSafeDemoRootPath(rootFolder))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Demo Output Path",
                    "For safety, the Demo Output Path must be inside Assets and cannot be the root Assets folder.\n\nThe path has been reset to the default RockGenLite demo folder.",
                    "OK"
                );

                rootFolder = DefaultDemoBasePath;
            }

            string fullRootPath = AssetPathToFullPath(rootFolder);
            bool rootExistedBefore =
                AssetDatabase.IsValidFolder(rootFolder) ||
                (!string.IsNullOrEmpty(fullRootPath) && Directory.Exists(fullRootPath));

            bool markerWasValidBefore = HasValidDemoFolderMarker(rootFolder);
            bool canCreateMarker = markerWasValidBefore || !rootExistedBefore || IsFolderSafeToMarkAsDemoOutput(rootFolder);

            _demoBasePath = rootFolder;
            EditorPrefs.SetString("Veridian_RockDemoGenerator_Path", _demoBasePath);

            RockPrefabFactory.CreateFolderRecursive(_demoBasePath);
            RockPrefabFactory.CreateFolderRecursive($"{_demoBasePath}/Textures");
            RockPrefabFactory.CreateFolderRecursive($"{_demoBasePath}/Layers");
            RockPrefabFactory.CreateFolderRecursive($"{_demoBasePath}/Rocks");

            if (canCreateMarker)
            {
                TryCreateOrRepairDemoFolderMarker(_demoBasePath);
            }
            else
            {
                Debug.LogWarning(
                    "[Demo Orchestrator Lite] Demo output folder was not marker-approved because it already contains unrelated assets. " +
                    "Generation can still write new rock assets, but purge and destructive terrain cleanup will remain blocked for this folder.\n" +
                    $"Path: {_demoBasePath}"
                );
            }
        }

        private void PurgeDemoAssets()
        {
            string normalizedPath = NormalizeDemoAssetPath(_demoBasePath);
            _demoBasePath = normalizedPath;
            EditorPrefs.SetString("Veridian_RockDemoGenerator_Path", _demoBasePath);

            if (!ValidateDemoFolderForDestructiveAction(normalizedPath, out string validationError))
            {
                EditorUtility.DisplayDialog("Purge Aborted", validationError, "OK");
                return;
            }

            if (EditorUtility.DisplayDialog(
                            "Purge Demo Assets",
                            $"Are you sure you want to permanently delete all Rock Generator Lite demo terrains, textures, rocks, and generated demo assets?\n\nPath:\n{normalizedPath}\n\nThis folder contains the Rock Generator Lite demo marker, so it is treated as tool-owned demo output.",
                            "Yes, Purge",
                            "Cancel"))
            {
                Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

                foreach (Terrain t in terrains)
                {
                    if (t != null && t.terrainData != null)
                    {
                        string tPath = AssetDatabase.GetAssetPath(t.terrainData);
                        if (IsSameOrChildAssetPath(tPath, normalizedPath))
                        {
                            DestroyImmediate(t.gameObject);
                        }
                    }
                }

                if (AssetDatabase.IsValidFolder(normalizedPath))
                {
                    AssetDatabase.DeleteAsset(normalizedPath);
                }

                _bakedPrefabs.Clear();
                AssetDatabase.Refresh();

                Debug.Log($"[Demo Orchestrator Lite] Purged marker-approved demo assets at: {normalizedPath}");
            }
        }

        #endregion

        public void LoadPreset(RockPresetType preset)
        {
            _demoPreset = preset;
            _customProfile = null;
            Repaint();
        }
    }
}
#endif