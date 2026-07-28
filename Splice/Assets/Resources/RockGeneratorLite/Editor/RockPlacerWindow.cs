#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Veridian.RockGenLite.Editor
{
    [Serializable]
    public class PlacerPrefabEntry
    {
        public GameObject Prefab;
        public float Weight = 1.0f;
    }

    public class RockPlacerWindow : EditorWindow
    {
        [SerializeField] private Terrain _targetTerrain;
        [SerializeField] private List<PlacerPrefabEntry> _prefabPalette = new List<PlacerPrefabEntry>();

        [Header("Placement Rules")]
        [SerializeField] private int _spawnCount = 500;
        [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 1.5f);

        [Header("Constraints")]
        [SerializeField] private Vector2 _slopeRange = new Vector2(0f, 60f);
        [SerializeField] private Vector2 _heightRange = new Vector2(0f, 1000f);

        [Header("Alignment & Clustering")]
        [SerializeField] private bool _alignToSurface = true;
        [SerializeField] private float _alignBlend = 0.8f;
        [SerializeField] private float _verticalOffset = -0.1f;
        [SerializeField] private float _clumpScale = 15f;
        [SerializeField] private float _clumpThreshold = 0.4f;

        private Vector2 _scrollPos;
        private GUIStyle _boldButtonStyle;
        // --- UPDATED MENU PATH ---
        [MenuItem("Tools/Veridian/Rock Generator Lite/3. Rock Placer", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<RockPlacerWindow>("Rock Placer Lite");
            window.minSize = new Vector2(350, 600);
            window.AutoAssignTerrain();
            window.Show();
        }

        public static void InjectPrefabs(Terrain targetTerrain, List<GameObject> prefabs)
        {
            var window = GetWindow<RockPlacerWindow>("Rock Placer Lite");
            window.minSize = new Vector2(350, 600);

            if (targetTerrain != null)
            {
                window._targetTerrain = targetTerrain;
            }

            if (window._prefabPalette == null)
            {
                window._prefabPalette = new List<PlacerPrefabEntry>();
            }

            if (prefabs != null)
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab == null) continue;

                    window._prefabPalette.Add(new PlacerPrefabEntry
                    {
                        Prefab = prefab,
                        Weight = 1.0f
                    });
                }
            }

            window.AutoAssignTerrain();
            window.Show();
            window.Focus();
            window.Repaint();
        }

        private void AutoAssignTerrain()
        {
            if (_targetTerrain == null)
            {
                _targetTerrain = FindAnyObjectByType<Terrain>();
            }
        }

        private void OnGUI()
        {
            // PHASE 2 FIX: Lazy Initialization
            if (_boldButtonStyle == null)
            {
                _boldButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(10);
            GUILayout.Label("Target Environment", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", _targetTerrain, typeof(Terrain), true);
            if (_targetTerrain == null)
            {
                EditorGUILayout.HelpBox("Please assign a target Terrain.", MessageType.Warning);
                if (GUILayout.Button("Auto-Find Terrain in Scene"))
                {
                    AutoAssignTerrain();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUILayout.Label("Prefab Palette", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _prefabPalette.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _prefabPalette[i].Prefab = (GameObject)EditorGUILayout.ObjectField(_prefabPalette[i].Prefab, typeof(GameObject), false);
                _prefabPalette[i].Weight = EditorGUILayout.Slider(_prefabPalette[i].Weight, 0.1f, 10f, GUILayout.Width(150));

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    _prefabPalette.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Prefab Slot"))
            {
                _prefabPalette.Add(new PlacerPrefabEntry());
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUILayout.Label("Placement Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _spawnCount = EditorGUILayout.IntSlider("Spawn Count", _spawnCount, 1, 5000);

            float minScale = _scaleRange.x; float maxScale = _scaleRange.y;
            EditorGUILayout.MinMaxSlider(new GUIContent($"Scale Range ({minScale:F1}x - {maxScale:F1}x)"), ref minScale, ref maxScale, 0.1f, 5.0f);
            _scaleRange = new Vector2(minScale, maxScale);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            GUILayout.Label("Constraints & Rules", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float minSlope = _slopeRange.x; float maxSlope = _slopeRange.y;

            EditorGUILayout.MinMaxSlider(new GUIContent($"Slope Limits ({minSlope:F0} deg - {maxSlope:F0} deg)"), ref minSlope, ref maxSlope, 0f, 90f);
            _slopeRange = new Vector2(minSlope, maxSlope);

            float minHeight = _heightRange.x; float maxHeight = _heightRange.y;
            EditorGUILayout.MinMaxSlider(new GUIContent($"Height Limits ({minHeight:F0}m - {maxHeight:F0}m)"), ref minHeight, ref maxHeight, -500f, 5000f);
            _heightRange = new Vector2(minHeight, maxHeight);

            EditorGUILayout.Space(5);
            _alignToSurface = EditorGUILayout.Toggle("Align to Surface", _alignToSurface);
            if (_alignToSurface)
            {
                EditorGUI.indentLevel++;
                _alignBlend = EditorGUILayout.Slider(new GUIContent("Alignment Blend", "0 = Point straight up. 1 = Perfectly flush with slope."), _alignBlend, 0f, 1f);
                EditorGUI.indentLevel--;
            }
            _verticalOffset = EditorGUILayout.Slider(new GUIContent("Vertical Sink Offset", "Pushes rocks slightly into the ground."), _verticalOffset, -5f, 5f);

            EditorGUILayout.Space(5);
            _clumpScale = EditorGUILayout.Slider(new GUIContent("Clump Noise Scale", "Lower is broader clusters, higher is tighter noise."), _clumpScale, 1f, 50f);
            _clumpThreshold = EditorGUILayout.Slider(new GUIContent("Clump Threshold", "Higher values result in sparser, more isolated groups. Set to 0 to disable."), _clumpThreshold, 0f, 1f);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            Color prevColor = GUI.backgroundColor;

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.7f, 0.95f, 0.75f);
            bool canPopulate = _targetTerrain != null && _prefabPalette.Count > 0 && _prefabPalette.Exists(p => p.Prefab != null && p.Weight > 0f);

            GUI.enabled = canPopulate;
            if (GUILayout.Button("Populate Terrain", _boldButtonStyle, GUILayout.Height(40)))
            {
                ExecuteScattering();
            }
            GUI.enabled = true;
            GUI.backgroundColor = prevColor;

            GUILayout.Space(10);

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.4f, 0.4f) : new Color(1.0f, 0.75f, 0.75f);
            if (GUILayout.Button("Clear Generated Scatter (Undo)", GUILayout.Height(30)))
            {
                ClearScatteredRocks();
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndScrollView();
        }

        private void ExecuteScattering()
        {
            if (_targetTerrain == null) return;

            List<PlacerPrefabEntry> validPrefabs = _prefabPalette.FindAll(p => p.Prefab != null && p.Weight > 0f);
            if (validPrefabs.Count == 0) return;

            float totalWeight = 0f;
            foreach (var p in validPrefabs) totalWeight += p.Weight;

            TerrainData tData = _targetTerrain.terrainData;
            Vector3 tPos = _targetTerrain.transform.position;
            float tWidth = tData.size.x;
            float tLength = tData.size.z;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Scatter Rocks");
            int undoGroupIndex = Undo.GetCurrentGroup();

            GameObject root = new GameObject($"RockScatter_{DateTime.Now:MMdd_HHmm}");

            // PHASE 1 FIX: Register object creation BEFORE parenting to prevent Undo hierarchy corruption
            Undo.RegisterCreatedObjectUndo(root, "Scatter Rocks");

            root.transform.position = tPos;
            root.transform.SetParent(_targetTerrain.transform, true);

            root.AddComponent<Veridian.RockGenLite.Runtime.RockScatterGroup>();

            int placed = 0;
            int attempts = 0;
            int maxAttempts = _spawnCount * 10;

            float noiseOffsetX = UnityEngine.Random.Range(0f, 10000f);
            float noiseOffsetZ = UnityEngine.Random.Range(0f, 10000f);

            EditorUtility.DisplayProgressBar("Scattering Rocks", "Calculating placements...", 0f);

            try
            {
                while (placed < _spawnCount && attempts < maxAttempts)
                {
                    attempts++;

                    float normX = UnityEngine.Random.value;
                    float normZ = UnityEngine.Random.value;

                    if (_clumpThreshold > 0f)
                    {
                        float noiseVal = Mathf.PerlinNoise(normX * _clumpScale + noiseOffsetX, normZ * _clumpScale + noiseOffsetZ);
                        if (noiseVal < _clumpThreshold) continue;
                    }

                    float localX = normX * tWidth;
                    float localZ = normZ * tLength;
                    float worldX = tPos.x + localX;
                    float worldZ = tPos.z + localZ;

                    float height = _targetTerrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + tPos.y;
                    if (height < _heightRange.x || height > _heightRange.y) continue;

                    Vector3 normal = tData.GetInterpolatedNormal(normX, normZ);
                    float slope = Vector3.Angle(Vector3.up, normal);
                    if (slope < _slopeRange.x || slope > _slopeRange.y) continue;

                    GameObject selectedPrefab = GetRandomPrefab(validPrefabs, totalWeight);

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);

                    if (instance == null) continue;

                    Undo.RegisterCreatedObjectUndo(instance, "Scatter Rocks");

                    instance.transform.SetParent(root.transform);
                    instance.transform.position = new Vector3(worldX, height + _verticalOffset, worldZ);

                    Quaternion randomYaw = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                    if (_alignToSurface)
                    {
                        Vector3 blendedUp = Vector3.Lerp(Vector3.up, normal, _alignBlend).normalized;
                        Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, blendedUp);
                        instance.transform.rotation = surfaceRot * randomYaw;
                    }
                    else
                    {
                        instance.transform.rotation = randomYaw;
                    }

                    float uniformScale = UnityEngine.Random.Range(_scaleRange.x, _scaleRange.y);
                    instance.transform.localScale = selectedPrefab.transform.localScale * uniformScale;

                    placed++;

                    if (placed % 100 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Scattering Rocks", $"Placing {placed} / {_spawnCount}...", (float)placed / _spawnCount);
                    }
                }

                Debug.Log($"[Rock Placer] Successfully scattered {placed} objects onto {_targetTerrain.name}. (Attempts: {attempts})");
                Selection.activeGameObject = root;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroupIndex);
            }
        }

        private GameObject GetRandomPrefab(List<PlacerPrefabEntry> validPrefabs, float totalWeight)
        {
            float roll = Random.Range(0f, totalWeight);
            float current = 0f;
            foreach (var p in validPrefabs)
            {
                current += p.Weight;
                if (roll <= current) return p.Prefab;
            }
            return validPrefabs[validPrefabs.Count - 1].Prefab;
        }

        private void ClearScatteredRocks()
        {
            var scatters = UnityEngine.Object.FindObjectsByType<Veridian.RockGenLite.Runtime.RockScatterGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (scatters.Length == 0)
            {
                EditorUtility.DisplayDialog("Clear Scatter", "No generated RockScatter groups found in the scene.", "OK");
                return;
            }

            // FIX: Added explicit confirmation dialog to prevent accidental mass deletion of all scatter groups.
            if (!EditorUtility.DisplayDialog("Clear Scattered Rocks", $"Are you sure you want to completely clear {scatters.Length} generated RockScatter groups from the scene?\n\nThis action can be undone, but may temporarily lag the editor if clearing thousands of objects.", "Yes, Clear", "Cancel"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Scattered Rocks");
            int undoGroupIndex = Undo.GetCurrentGroup();

            foreach (var group in scatters)
            {
                if (group != null && group.gameObject != null)
                {
                    Undo.DestroyObjectImmediate(group.gameObject);
                }
            }

            Undo.CollapseUndoOperations(undoGroupIndex);

            Debug.Log($"[Rock Placer] Cleared {scatters.Length} previously scattered rock groups.");
        }
    }
}
#endif