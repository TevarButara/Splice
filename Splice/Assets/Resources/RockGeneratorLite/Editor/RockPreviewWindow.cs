#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Veridian.RockGenLite.Runtime;

namespace Veridian.RockGenLite.Editor
{
    public class RockPreviewWindow : EditorWindow
    {
        [SerializeField] private RockSettings _settingsSource;
        [SerializeField] private string _transientStateJson;
        [SerializeField] private RockClusterSettings _clusterSettings = new RockClusterSettings();
        [SerializeField] private bool _clusterFoldout = true;

        private RockSettings _settingsInstance;
        private Material _previewMaterial;
        private RockSettingsEditor _settingsEditor;
        private double _lastChangeTime;
        private bool _isPendingBake = false;

        private Texture2D _previewAlbedo;
        private Texture2D _previewNormal;
        private Texture2D _previewMask;
        private Texture2D _previewMetallic;
        private Texture2D _previewAO;
        private Texture2D _previewSmoothness;

        private float _settingsPanelWidth = 350f;
        private bool _isResizingSettings = false;
        private Vector2 _settingsScrollPos;
        private const float SETTINGS_PANEL_WIDTH = 350f;

        private PreviewRenderUtility _previewRenderUtility;
        private GameObject _previewRockInstance;
        private GameObject _pendingClusterRoot;
        private List<RockClusterPlacement> _pendingClusterPlacements;
        private int _pendingClusterIndex;
        private RockSettings _activeClusterRockSettings;
        private string _clusterWarning;
        private float _cameraDistance = 5.0f;
        private Vector2 _cameraRotationAngles = new Vector2(20f, -135f);
        private Vector3 _cameraPivot = Vector3.zero;

        private RuntimeRockGenerator _generator;
        private bool _isGenerating = false;
        private bool _needsRegeneration = true;
        private int _currentLODIndex = 0;
        private List<Renderer> _lodRenderers = new List<Renderer>();
        public enum WireframeMode { Disabled, Black, White }
        private WireframeMode _wireframeMode = WireframeMode.Disabled;
        private Material _wireframeMaterial;

        private int _previewTextureResolution = 512;
        private GUIStyle _boldButtonStyle;
        private GUIStyle _foldoutStyle;
        private GUIStyle _promoTitleStyle;
        private GUIStyle _promoDescStyle;
        private GUIStyle _loadingStyle;


        [MenuItem("Tools/Veridian/Rock Generator Lite/1. Rock Window", false, 0)]
        public static void ShowWindow()
        {
            RockPreviewWindow window = GetWindow<RockPreviewWindow>("Rock Window Lite");
            window.minSize = new Vector2(800, 600);

            if (window._settingsInstance == null)
            {
                window.InitializeTransientSettings();
            }
        }

        [MenuItem("GameObject/3D Object/Veridian Rock Lite", false, 10)]
        public static void CreateRockInScene(MenuCommand menuCommand)
        {
            ShowWindow();
        }

        public static void Open(RockSettings settings)
        {
            RockPreviewWindow window = GetWindow<RockPreviewWindow>("Rock Window Lite");
            window.minSize = new Vector2(800, 600);
            window.InitializeSettings(settings);
            window.Show();
        }

        private void InitializeTransientSettings()
        {
            InitializeSettings(null);
        }

        private void InitializeSettings(RockSettings settings)
        {
            _settingsSource = settings;

            if (_settingsInstance != null)
            {
                DestroyImmediate(_settingsInstance);
            }

            if (settings != null)
            {
                _settingsInstance = Instantiate(settings);
                _settingsInstance.name = settings.name + " (Preview)";
            }
            else
            {
                _settingsInstance = ScriptableObject.CreateInstance<RockSettings>();
                _settingsInstance.name = "Transient Rock Settings";
            }

            _settingsInstance.hideFlags = HideFlags.DontSave;

            CreateSettingsEditor();

            _currentLODIndex = 0;
            _needsRegeneration = true;
            _transientStateJson = string.Empty;

            UpdatePreviewMaterial();

            // --- UI/UX FIX: Trigger the optimized auto-bake timer when a profile is loaded ---
            if (IsBakeMethod(_settingsInstance))
            {
                _isPendingBake = true;
                _lastChangeTime = EditorApplication.timeSinceStartup;
            }
        }

        private void CreateSettingsEditor()
        {
            if (_settingsEditor != null) DestroyImmediate(_settingsEditor);

            if (_settingsInstance != null)
            {
                _settingsEditor = (RockSettingsEditor)UnityEditor.Editor.CreateEditor(_settingsInstance);
                _settingsEditor.OnSettingsChanged = () =>
                {
                    _needsRegeneration = true;

                    _isPendingBake = true;
                    _lastChangeTime = EditorApplication.timeSinceStartup;
                    Repaint();
                };
            }
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // FIX 6: Hook into play mode state changes to prevent PreviewRenderUtility memory leaks
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            InitializeGenerator();
            InitializePreview();

            if (_settingsInstance == null)
            {
                if (!string.IsNullOrEmpty(_transientStateJson))
                {
                    _settingsInstance = ScriptableObject.CreateInstance<RockSettings>();
                    JsonUtility.FromJsonOverwrite(_transientStateJson, _settingsInstance);
                    _settingsInstance.name = _settingsSource != null ? $"{_settingsSource.name} (Preview)" : "Transient Rock Settings";
                    _settingsInstance.hideFlags = HideFlags.DontSave;
                }
                else if (_settingsSource != null) InitializeSettings(_settingsSource);
                else InitializeTransientSettings();
            }

            if (_settingsInstance != null && _settingsEditor == null) CreateSettingsEditor();
            if (_clusterSettings == null) _clusterSettings = new RockClusterSettings();

            UpdatePreviewMaterial();
            _needsRegeneration = true;

            Undo.undoRedoPerformed += OnUndoRedo;
            RockPrefabFactory.OnGenerationFinished += Repaint;

            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;

            if (IsBakeMethod(_settingsInstance))
            {
                _isPendingBake = true;
                _lastChangeTime = 0;
            }
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            // FIX 6: Unhook play mode state changes
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            Undo.undoRedoPerformed -= OnUndoRedo;
            RockPrefabFactory.OnGenerationFinished -= Repaint;

            EditorApplication.update -= EditorUpdate;

            if (_settingsInstance != null)
            {
                _transientStateJson = JsonUtility.ToJson(_settingsInstance);

                DestroyImmediate(_settingsInstance);
                _settingsInstance = null;
            }

            if (_settingsEditor != null) DestroyImmediate(_settingsEditor);

            CleanupPreview();
            CleanupPendingCluster();
            CleanupGenerator();

            if (_previewMaterial != null)
            {
                DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }

            if (_previewAlbedo != null) DestroyImmediate(_previewAlbedo);
            if (_previewNormal != null) DestroyImmediate(_previewNormal);
            if (_previewMask != null) DestroyImmediate(_previewMask);
            if (_previewMetallic != null) DestroyImmediate(_previewMetallic);
            if (_previewAO != null) DestroyImmediate(_previewAO);
            if (_previewSmoothness != null) DestroyImmediate(_previewSmoothness);
            if (_wireframeMaterial != null) DestroyImmediate(_wireframeMaterial);
        }
        private void OnBeforeAssemblyReload()
        {
            // Forcefully clear the C++ preview scene memory and active generators 
            // before the script domain is wiped.
            CleanupPreview();
            CleanupGenerator();
        }
        private void EditorUpdate()
        {
            if (_isPendingBake && !_isGenerating)
            {
                if (EditorApplication.timeSinceStartup - _lastChangeTime > 0.4)
                {
                    _isPendingBake = false;

                    if (IsBakeMethod(_settingsInstance))
                    {
                        BakePreviewTextures();
                    }
                    else
                    {
                        ApplyPreviewMaterials();
                    }
                    Repaint();
                }
            }
        }

        private void OnDestroy()
        {
            if (_settingsInstance != null) DestroyImmediate(_settingsInstance);
            if (_previewMaterial != null) DestroyImmediate(_previewMaterial);
            if (_previewAlbedo != null) DestroyImmediate(_previewAlbedo);
            if (_previewNormal != null) DestroyImmediate(_previewNormal);
            if (_previewMask != null) DestroyImmediate(_previewMask);
            if (_previewMetallic != null) DestroyImmediate(_previewMetallic);
            if (_previewAO != null) DestroyImmediate(_previewAO);
            if (_previewSmoothness != null) DestroyImmediate(_previewSmoothness);
            if (_wireframeMaterial != null) DestroyImmediate(_wireframeMaterial);
        }
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                CleanupPreview();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                InitializePreview();
                _needsRegeneration = true;
                Repaint();
            }
        }
        private void OnUndoRedo()
        {
            if (_settingsInstance != null)
            {
                if (_settingsEditor != null) _settingsEditor.serializedObject.Update();
                _needsRegeneration = true;
                Repaint();
            }
        }

        #region Initialization and Cleanup
        private void InitializePreview()
        {
            if (_previewRenderUtility == null)
            {
                _previewRenderUtility = new PreviewRenderUtility();
                _previewRenderUtility.camera.fieldOfView = 60f;
                _previewRenderUtility.camera.nearClipPlane = 0.1f;
                _previewRenderUtility.camera.farClipPlane = 1000f;

                _previewRenderUtility.lights[0].intensity = 0.85f;
                _previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(30f, -30f, 0f);

                if (_previewRenderUtility.lights.Length > 1)
                {
                    _previewRenderUtility.lights[1].type = LightType.Directional;
                    _previewRenderUtility.lights[1].intensity = 0.5f;
                    _previewRenderUtility.lights[1].transform.rotation = Quaternion.Euler(-20f, 150f, 0f);
                    _previewRenderUtility.lights[1].color = new Color(0.9f, 0.95f, 1.0f);
                }

                _previewRenderUtility.ambientColor = new Color(0.15f, 0.15f, 0.15f);
            }
        }

        private void InitializeGenerator()
        {
            if (_generator == null)
            {
                GameObject generatorGO = new GameObject("RockPreviewWindow_Generator");
                generatorGO.hideFlags = HideFlags.HideAndDontSave;
                _generator = generatorGO.AddComponent<RuntimeRockGenerator>();
            }
            EditorGenerationDriver.Register(_generator);
        }
        private void DestroyPreviewRock(GameObject rockGO)
        {
            if (rockGO != null)
            {
                HashSet<Mesh> meshesToDestroy = new HashSet<Mesh>();
                MeshFilter[] filters = rockGO.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in filters)
                {
                    if (mf != null && mf.sharedMesh != null) meshesToDestroy.Add(mf.sharedMesh);
                }
                MeshCollider[] colliders = rockGO.GetComponentsInChildren<MeshCollider>(true);
                foreach (var collider in colliders)
                {
                    if (collider != null && collider.sharedMesh != null) meshesToDestroy.Add(collider.sharedMesh);
                }

                foreach (var m in meshesToDestroy)
                {
                    // FIX: Ensure the mesh isn't a persistent asset in the project before blindly destroying it
                    if (m != null && !EditorUtility.IsPersistent(m))
                    {
                        DestroyImmediate(m);
                    }
                }
                DestroyImmediate(rockGO);
            }
        }
        private void CleanupPreview()
        {
            if (_previewRenderUtility != null)
            {
                _previewRenderUtility.Cleanup();
                _previewRenderUtility = null;
            }

            if (_previewRockInstance != null)
            {
                DestroyPreviewRock(_previewRockInstance);
                _previewRockInstance = null;
            }

            _lodRenderers.Clear();
        }

        private void CleanupPendingCluster()
        {
            if (_pendingClusterRoot != null)
            {
                DestroyPreviewRock(_pendingClusterRoot);
                _pendingClusterRoot = null;
            }
            _pendingClusterPlacements = null;
            _pendingClusterIndex = 0;
            if (_activeClusterRockSettings != null)
            {
                DestroyImmediate(_activeClusterRockSettings);
                _activeClusterRockSettings = null;
            }
        }

        private void CleanupGenerator()
        {
            if (_generator != null)
            {
                EditorGenerationDriver.Unregister(_generator);
                DestroyImmediate(_generator.gameObject);
                _generator = null;
            }
        }

        private void UpdatePreviewMaterial()
        {
            if (_previewMaterial != null) DestroyImmediate(_previewMaterial);

            bool useVC = (_settingsInstance != null && _settingsInstance.colorizationMethod == RockColorizationMethod.VertexColors) || _isPendingBake;

            if (useVC)
            {
                Shader vcShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Hidden/Internal-Colored");

                _previewMaterial = new Material(vcShader);
                if (_previewMaterial.HasProperty("_BaseColor")) _previewMaterial.SetColor("_BaseColor", Color.white);
                if (_previewMaterial.HasProperty("_Color")) _previewMaterial.SetColor("_Color", Color.white);
            }
            else
            {
                _previewMaterial = RockPrefabFactory.CreateDefaultPBRMaterial();

                if (_previewMaterial.HasProperty("_EnvironmentReflections"))
                {
                    _previewMaterial.SetFloat("_EnvironmentReflections", 0f);
                    _previewMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
            }

            _previewMaterial.hideFlags = HideFlags.DontSave;
        }
        #endregion

        #region GUI Layout
        private void OnGUI()
        {
            HandleDragAndDrop();

            if (_settingsInstance == null || _settingsEditor == null) InitializeTransientSettings();

            // FIX: Added ExpandHeight to allow window to fill all space
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            // FIX: Added ExpandHeight(true) so the scrollview doesn't restrict the height of the whole window
            _settingsScrollPos = EditorGUILayout.BeginScrollView(_settingsScrollPos, GUILayout.Width(_settingsPanelWidth), GUILayout.ExpandHeight(true));
            DrawQuickStartGuide();
            DrawSettingsPanel();
            DrawUndoButton();
            DrawPromotionalFooter();
            EditorGUILayout.EndScrollView();

            HandleSplitter();

            // FIX: Allow right side to expand fully
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            DrawPreviewPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (_needsRegeneration && Event.current.type == EventType.Repaint)
            {
                if (!_isGenerating)
                {
                    _needsRegeneration = false;
                    EditorApplication.delayCall += GeneratePreviewRock;
                }
            }
        }

        private void HandleSplitter()
        {
            Rect splitterRect = GUILayoutUtility.GetRect(5f, 5f, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(splitterRect, new Color(0.15f, 0.15f, 0.15f));

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition) && e.button == 0)
            {
                _isResizingSettings = true;

                GUIUtility.hotControl = controlID;
                e.Use();
            }
            if (_isResizingSettings && e.type == EventType.MouseDrag)
            {
                _settingsPanelWidth += e.delta.x;

                float maxClamp = Mathf.Max(250f, position.width - 300f);
                _settingsPanelWidth = Mathf.Clamp(_settingsPanelWidth, 250f, maxClamp);
                Repaint();
                e.Use();
            }
            // PHASE 1 FIX: Use rawType to catch mouse release anywhere globally
            if (_isResizingSettings && e.rawType == EventType.MouseUp)
            {
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                }
                _isResizingSettings = false;
                e.Use();
            }
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (DragAndDrop.objectReferences.Any(obj => obj is RockSettings))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is RockSettings settings)
                            {
                                InitializeSettings(settings);
                                evt.Use();
                                return;
                            }
                        }
                    }
                    evt.Use();
                }
            }
        }

        private void DrawSettingsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToolbar();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                new GUIContent(
                    "Base Asset",
                    "Optional RockSettings asset used as the source for this preview. If no asset is assigned, the window edits temporary transient settings until you save them."
                ),
                EditorStyles.boldLabel
            );

            EditorGUI.BeginChangeCheck();
            RockSettings newSource = (RockSettings)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Profile",
                    "Assign an existing RockSettings asset, or leave this empty to work with temporary settings that can be saved later."
                ),
                _settingsSource,
                typeof(RockSettings),
                false
            );

            if (EditorGUI.EndChangeCheck())
            {
                if (newSource != _settingsSource)
                {
                    InitializeSettings(newSource);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            if (_settingsEditor != null)
            {
                _settingsEditor.OnInspectorGUI();
            }

            DrawClusterControls();
            DrawSaveControls();
            EditorGUILayout.EndVertical();
        }

        private void DrawClusterControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _clusterFoldout = EditorGUILayout.Foldout(
                _clusterFoldout,
                "Rock Cluster (Multi-Rock Export)",
                true,
                EditorStyles.foldoutHeader);

            if (_clusterFoldout)
            {
                EditorGUILayout.HelpBox(
                    "Preview and export use the same deterministic layout. Every rock receives a derived seed, so shapes differ while the same Cluster Seed always rebuilds the same result.",
                    MessageType.Info);

                EditorGUI.BeginChangeCheck();
                _clusterSettings.enabled = EditorGUILayout.Toggle(
                    new GUIContent("Enable Cluster", "Off preserves the original single-rock workflow."),
                    _clusterSettings.enabled);

                using (new EditorGUI.DisabledScope(!_clusterSettings.enabled))
                {
                    _clusterSettings.count = EditorGUILayout.IntSlider(
                        new GUIContent("Rock Count", $"Preview/export safety limit: {RockClusterSettings.MaxRockCount}."),
                        _clusterSettings.count,
                        1,
                        RockClusterSettings.MaxRockCount);

                    EditorGUILayout.BeginHorizontal();
                    _clusterSettings.seed = EditorGUILayout.IntField("Cluster Seed", _clusterSettings.seed);
                    if (GUILayout.Button("Random", GUILayout.Width(65)))
                    {
                        _clusterSettings.seed = UnityEngine.Random.Range(1, int.MaxValue);
                        GUI.changed = true;
                    }
                    EditorGUILayout.EndHorizontal();

                    _clusterSettings.shape = (RockClusterShape)EditorGUILayout.EnumPopup("Distribution Shape", _clusterSettings.shape);
                    DrawClusterShapeControls();

                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
                    _clusterSettings.spread = EditorGUILayout.Slider(
                        new GUIContent("Spread", "Scales placement away from the center without changing rock size."),
                        _clusterSettings.spread, 0.05f, 1f);
                    _clusterSettings.centerBias = EditorGUILayout.Slider(
                        new GUIContent("Center Bias", "-1 favors the edge, 0 is area-uniform, +1 concentrates rocks near the center."),
                        _clusterSettings.centerBias, -1f, 1f);
                    _clusterSettings.positionVariance = EditorGUILayout.Slider(
                        new GUIContent("Position Variance", "Adds deterministic organic jitter to planar layouts."),
                        _clusterSettings.positionVariance, 0f, 1f);
                    _clusterSettings.heightVariance = EditorGUILayout.FloatField(
                        new GUIContent("Height Variance", "Random offset along the placement surface normal."),
                        _clusterSettings.heightVariance);
                    _clusterSettings.minimumSpacing = EditorGUILayout.FloatField(
                        new GUIContent("Minimum Spacing", "Rejects samples that are too close. 0 allows overlapping piles."),
                        _clusterSettings.minimumSpacing);

                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Rock Variation", EditorStyles.boldLabel);
                    EditorGUILayout.MinMaxSlider(
                        new GUIContent("Uniform Scale Range"),
                        ref _clusterSettings.minimumScale,
                        ref _clusterSettings.maximumScale,
                        0.05f,
                        4f);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Scale Min / Max");
                    _clusterSettings.minimumScale = EditorGUILayout.FloatField(_clusterSettings.minimumScale);
                    _clusterSettings.maximumScale = EditorGUILayout.FloatField(_clusterSettings.maximumScale);
                    EditorGUILayout.EndHorizontal();
                    _clusterSettings.nonUniformScaleVariance = EditorGUILayout.Slider(
                        "Shape Scale Variance",
                        _clusterSettings.nonUniformScaleVariance, 0f, 0.75f);
                    _clusterSettings.tiltVariance = EditorGUILayout.Slider(
                        "Tilt Variance",
                        _clusterSettings.tiltVariance, 0f, 180f);
                    _clusterSettings.alignToSurface = EditorGUILayout.Toggle("Align To Surface", _clusterSettings.alignToSurface);
                    _clusterSettings.surfaceOffset = EditorGUILayout.FloatField(
                        new GUIContent("Ground Offset", "Positive lifts rocks; negative embeds them into the surface."),
                        _clusterSettings.surfaceOffset);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _clusterSettings.Sanitize();
                    _clusterWarning = null;
                    _needsRegeneration = true;
                    Repaint();
                }

                if (!string.IsNullOrEmpty(_clusterWarning))
                {
                    EditorGUILayout.HelpBox(_clusterWarning, MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawClusterShapeControls()
        {
            switch (_clusterSettings.shape)
            {
                case RockClusterShape.Disk:
                    _clusterSettings.radius = EditorGUILayout.FloatField("Radius", _clusterSettings.radius);
                    break;
                case RockClusterShape.Ring:
                    _clusterSettings.radius = EditorGUILayout.FloatField("Outer Radius", _clusterSettings.radius);
                    _clusterSettings.innerRadius = EditorGUILayout.FloatField("Inner Radius", _clusterSettings.innerRadius);
                    break;
                case RockClusterShape.Rectangle:
                    _clusterSettings.rectangleSize = EditorGUILayout.Vector2Field("Rectangle Size", _clusterSettings.rectangleSize);
                    break;
                case RockClusterShape.Line:
                    _clusterSettings.lineLength = EditorGUILayout.FloatField("Line Length", _clusterSettings.lineLength);
                    _clusterSettings.lineWidth = EditorGUILayout.FloatField("Line Width", _clusterSettings.lineWidth);
                    break;
                case RockClusterShape.Mound:
                    _clusterSettings.radius = EditorGUILayout.FloatField("Pile Radius", _clusterSettings.radius);
                    _clusterSettings.moundHeight = EditorGUILayout.FloatField("Pile Height", _clusterSettings.moundHeight);
                    break;
                case RockClusterShape.SphereVolume:
                    _clusterSettings.radius = EditorGUILayout.FloatField("Volume Radius", _clusterSettings.radius);
                    break;
                case RockClusterShape.MeshSurface:
                    _clusterSettings.surfaceObject = (GameObject)EditorGUILayout.ObjectField(
                        new GUIContent("Surface / Prefab", "A scene object or prefab containing readable MeshFilter meshes. Sampling is weighted by triangle area and does not require a Collider."),
                        _clusterSettings.surfaceObject,
                        typeof(GameObject),
                        true);
                    _clusterSettings.minimumSurfaceUpDot = EditorGUILayout.Slider(
                        new GUIContent("Minimum Upward Normal", "-1 allows every face; 0 allows upward-facing slopes; 1 allows only flat upward faces."),
                        _clusterSettings.minimumSurfaceUpDot,
                        -1f,
                        1f);
                    _clusterSettings.invertSurfaceNormals = EditorGUILayout.Toggle(
                        new GUIContent("Invert Surface Normals", "Use when the source mesh winding points inward/downward."),
                        _clusterSettings.invertSurfaceNormals);
                    _clusterSettings.showSurfaceInPreview = EditorGUILayout.Toggle("Show Surface In Preview", _clusterSettings.showSurfaceInPreview);
                    _clusterSettings.includeSurfaceInExport = EditorGUILayout.Toggle(
                        new GUIContent("Include Surface In Export", "Off exports only the generated rock cluster."),
                        _clusterSettings.includeSurfaceInExport);
                    break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(new GUIContent("Refresh", "Rebuilds the temporary preview renderer, generator, material, mesh instance, wireframe material, and baked preview textures."), EditorStyles.toolbarButton))
            {
                RepairPreviewState();
            }

            if (GUILayout.Button("New Seed", EditorStyles.toolbarButton))
            {
                Undo.RecordObject(_settingsInstance, "New Rock Seed");
                _settingsInstance.seed = UnityEngine.Random.Range(1, 1000000);

                if (_settingsEditor != null) _settingsEditor.serializedObject.Update();

                _needsRegeneration = true;

                if (IsBakeMethod(_settingsInstance))
                {
                    ClearPreviewBakedTextures();
                    _isPendingBake = true;
                    _lastChangeTime = EditorApplication.timeSinceStartup;
                }

                Repaint();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset Camera", EditorStyles.toolbarButton))
            {
                FocusCameraOnObject(true);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSaveControls()
        {
            if (_boldButtonStyle == null)
            {
                _boldButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);

            if (_settingsSource != null)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button($"Apply to '{_settingsSource.name}'", _boldButtonStyle, GUILayout.Height(30)))
                {
                    string json = JsonUtility.ToJson(_settingsInstance);
                    JsonUtility.FromJsonOverwrite(json, _settingsSource);

                    EditorUtility.SetDirty(_settingsSource);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Applied changes to '{_settingsSource.name}'");
                }

                if (GUILayout.Button("Save As...", _boldButtonStyle, GUILayout.Height(30), GUILayout.Width(80)))
                {
                    SaveAsNewAsset();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("Save As New Asset...", _boldButtonStyle, GUILayout.Height(30)))
                {
                    SaveAsNewAsset();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space(10);

            Color prevColor = GUI.backgroundColor;

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.8f, 0.5f) : new Color(0.7f, 0.95f, 0.75f);
            string exportLabel = _clusterSettings != null && _clusterSettings.enabled
                ? "Generate Cluster Prefab (Save Exact Preview)"
                : "Generate Prefab (Save to Project)";
            using (new EditorGUI.DisabledScope(_isGenerating || (_clusterSettings != null && _clusterSettings.enabled && _previewRockInstance == null)))
            {
                if (GUILayout.Button(exportLabel, _boldButtonStyle, GUILayout.Height(40)))
                {
                    if (_clusterSettings != null && _clusterSettings.enabled)
                    {
                        RockClusterPrefabFactory.SavePreviewAsPrefab(
                            _settingsInstance,
                            _clusterSettings,
                            _previewRockInstance);
                    }
                    else
                    {
                        RockPrefabFactory.CreateAndSavePrefab(_settingsInstance);
                    }
                }
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.Space(15);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Rock Generator Pro Features [PRO]", EditorStyles.boldLabel);

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.8f, 0.8f, 0.8f);

            if (GUILayout.Button("Mass Batch Generator Orchestrator [PRO]", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Pro Feature", "Rock Generator Pro includes a Mass Batch Orchestrator that automatically generates and organizes hundreds of randomized rock variants into your project.\n\nWould you like to view Rock Generator Pro on the Asset Store?", "View on Asset Store", "Cancel"))
                {
                    Application.OpenURL("https://assetstore.unity.com/publishers/120204");
                }
            }

            if (GUILayout.Button("Material & Texture Combiner [PRO]", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Pro Feature", "Rock Generator Pro includes an Intelligent Material Combiner. It merges your generated textures into atlases, significantly reducing draw calls and optimizing performance for your game.\n\nWould you like to view Rock Generator Pro on the Asset Store?", "View on Asset Store", "Cancel"))
                {
                    Application.OpenURL("https://assetstore.unity.com/publishers/120204");
                }
            }
            GUI.backgroundColor = prevColor;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("World Building", EditorStyles.boldLabel);

            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.8f, 0.5f) : new Color(0.7f, 0.95f, 0.75f);
            if (GUILayout.Button("Open Rock Placer (Scatter Tool)", _boldButtonStyle, GUILayout.Height(30)))
            {
                RockPlacerWindow.ShowWindow();
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.Space(5);
        }

        private void SaveAsNewAsset()
        {
            string defaultName = _settingsInstance.name.Replace(" (Preview)", "").Replace("Transient Rock Settings", "NewRockSettings");
            if (string.IsNullOrWhiteSpace(defaultName)) defaultName = "NewRockSettings";

            string defaultPath = "Assets/VeridianData/RockGenLite/Profiles";
            if (!AssetDatabase.IsValidFolder(defaultPath))
            {
                RockPrefabFactory.CreateFolderRecursive(defaultPath);
                AssetDatabase.Refresh();
            }

            string path = EditorUtility.SaveFilePanelInProject("Save Rock Settings As", defaultName, "asset", "Save the current settings as a new profile.", defaultPath);
            if (!string.IsNullOrEmpty(path))
            {
                RockSettings newAsset = Instantiate(_settingsInstance);
                newAsset.hideFlags = HideFlags.None;
                newAsset.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(newAsset, path);
                AssetDatabase.SaveAssets();
                InitializeSettings(newAsset);
            }
        }

        private void DrawQuickStartGuide()
        {
            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            bool showHelp = EditorPrefs.GetBool("Veridian_RockGen_QuickStart", true);

            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            bool newShowHelp = EditorGUILayout.Foldout(showHelp, " Quick Start Guide", true, _foldoutStyle);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("Veridian_RockGen_QuickStart", newShowHelp);
            }

            if (newShowHelp)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("1. Start from a preset or adjust the generation and texturing parameters on the left.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("2. Lite presets are tuned around a 2m Target Diameter. Use Prefab Scale when you only want the same generated rock larger or smaller.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("3. Rotate the preview with Left Click + Drag. Scroll to zoom.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("4. In Procedural Texture Bake mode, color, bump, micro-detail, AO, metallic, and smoothness changes are previewed by baking temporary textures.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("5. Use Force Update Colors if the baked texture preview appears out of date after editing texture settings.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("6. Use Refresh if the preview window needs to rebuild its temporary renderer, material, mesh instance, or baked preview textures.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("7. Press Generate Prefab when ready to export the configured rock into the project.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }
        private void DrawUndoButton()
        {
            string lastPath = RockPrefabFactory.LastGeneratedRockPath;
            if (string.IsNullOrEmpty(lastPath) || !AssetDatabase.IsValidFolder(lastPath)) return;

            GUILayout.Space(10);

            Color prevColor = GUI.backgroundColor;

            // PHASE 3: Light Theme legibility support
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.4f, 0.4f) : new Color(1.0f, 0.75f, 0.75f);

            if (GUILayout.Button(new GUIContent($"Undo Last Generation\n(Deletes: {Path.GetFileName(lastPath)})", "Permanently deletes the folder and assets of the most recently generated rock."), GUILayout.Height(45)))
            {
                if (EditorUtility.DisplayDialog("Undo Last Generation", $"Are you sure you want to permanently delete the rock assets at:\n\n{lastPath}\n\nThis completely bypasses the Unity Undo stack.", "Yes, Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(lastPath);
                    RockPrefabFactory.ClearLastGeneratedPath();
                    AssetDatabase.Refresh();
                    Debug.Log($"[Rock Generator] Safely deleted generated rock assets.");
                }
            }

            GUI.backgroundColor = prevColor;
            GUILayout.Space(10);
        }

        private void DrawPromotionalFooter()
        {
            if (_promoTitleStyle == null)
            {
                _promoTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _promoDescStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 11 };
                _boldButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }

            bool isPro = EditorGUIUtility.isProSkin;

            _promoTitleStyle.normal.textColor = isPro ? new Color(0.6f, 0.8f, 1f) : new Color(0.1f, 0.3f, 0.5f);
            _promoDescStyle.normal.textColor = isPro ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);

            GUILayout.Space(15);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Enjoying Rock Generator Lite?", _promoTitleStyle);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Rock Generator Pro expands the Lite workflow with higher-end generation and production tools:", _promoDescStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("• 50+ Pro profiles and expanded biome/mineral looks", _promoDescStyle);
            EditorGUILayout.LabelField("• Mass Batch Generator Orchestrator", _promoDescStyle);
            EditorGUILayout.LabelField("• Intelligent Material & Texture Combiner for optimized atlases", _promoDescStyle);
            EditorGUILayout.LabelField("• Flat-Bottom Terrain Slicing", _promoDescStyle);
            EditorGUILayout.LabelField("• Multi-Texture Splat Maps", _promoDescStyle);
            EditorGUILayout.LabelField("• Physical Crystalline Extrusions and advanced rock features", _promoDescStyle);

            EditorGUILayout.Space(10);

            Color prevColor = GUI.backgroundColor;

            GUI.backgroundColor = isPro ? new Color(0.4f, 0.7f, 1.0f) : new Color(0.7f, 0.85f, 1.0f);
            if (GUILayout.Button("View Rock Generator Pro on Asset Store", _boldButtonStyle, GUILayout.Height(30)))
            {
                Application.OpenURL("https://assetstore.unity.com/publishers/120204");
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }
        #endregion

        #region Preview Drawing & Baking Logic
        private void DrawPreviewPanel()
        {
            if (_loadingStyle == null)
            {
                _loadingStyle = new GUIStyle(EditorStyles.whiteLargeLabel) { alignment = TextAnchor.MiddleCenter };
            }

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            DrawLODToolbar();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (IsBakeMethod(_settingsInstance))
            {
                if (_isPendingBake)
                {
                    EditorGUILayout.LabelField("[Auto-Baking Textures...]", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(260), GUILayout.Height(28));
                }
                else
                {
                    GUILayout.Label("Quality:", GUILayout.Width(50));

                    int[] sizes = { 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000 };
                    string[] sizeLabels = { "Fast (128)", "Draft (256)", "Medium (512)", "High (1024)", "Pro (2048)", "Pro (4096)" };

                    EditorGUI.BeginChangeCheck();
                    int selectedRes = EditorGUILayout.IntPopup(_previewTextureResolution, sizeLabels, sizes, GUILayout.Width(110));

                    if (EditorGUI.EndChangeCheck() && !_isGenerating)
                    {
                        if (selectedRes > 0x400)
                        {
                            EditorUtility.DisplayDialog(
                                "Pro Feature",
                                "High-resolution 2K and 4K texture preview baking is included in Rock Generator Pro.\n\nLite supports preview baking up to 1024.",
                                "OK"
                            );
                            _previewTextureResolution = 0x400;
                        }
                        else
                        {
                            _previewTextureResolution = selectedRes;
                            EditorApplication.delayCall += BakePreviewTextures;
                        }
                    }

                    GUILayout.Space(10);

                    Color prevColor = GUI.backgroundColor;

                    GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.8f, 1.0f) : new Color(0.6f, 0.9f, 1.0f);
                    if (GUILayout.Button(new GUIContent(
                        "Force Update Colors",
                        "Manually rebakes the temporary preview textures for color, bump, micro-detail, AO, metallic, and smoothness changes.\n\n" +
                        "Use this if the baked texture preview appears out of date after editing texture settings. Use Refresh in the toolbar when you want to rebuild the temporary preview renderer, material, textures, and generator state."
                    ), GUILayout.Width(170), GUILayout.Height(28)))
                    {
                        EditorApplication.delayCall += BakePreviewTextures;
                    }

                    GUI.backgroundColor = prevColor;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Previewing Live Vertex Colors", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(260), GUILayout.Height(28));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(3);

            Rect previewRect = GUILayoutUtility.GetRect(100, 10000, 100, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.Repaint)
            {
                if (_previewRockInstance != null && _previewRenderUtility != null)
                {
                    Draw3DPreview(previewRect);
                }
                else if (_isGenerating)
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                    EditorGUI.LabelField(previewRect, "Generating...", _loadingStyle);
                }
                else
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                    EditorGUI.LabelField(previewRect, "No preview available.", EditorStyles.centeredGreyMiniLabel);
                }
            }

            HandleCameraControls(previewRect);

            EditorGUILayout.EndVertical();
        }
        private void DrawLODToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("View LOD:", GUILayout.Width(70));

            if (_previewRockInstance != null && _lodRenderers.Count > 0)
            {
                string[] lodOptions = new string[_lodRenderers.Count];
                for (int i = 0; i < _lodRenderers.Count; i++)
                {
                    MeshFilter mf = _lodRenderers[i] != null ? _lodRenderers[i].GetComponent<MeshFilter>() : null;

                    int tris = mf != null && mf.sharedMesh != null ? (int)(mf.sharedMesh.GetIndexCount(0) / 3) : 0;

                    lodOptions[i] = $"LOD {i} ({tris} Tris)";
                }

                int selectedLOD = EditorGUILayout.Popup(_currentLODIndex, lodOptions, EditorStyles.toolbarPopup, GUILayout.Width(180));

                if (selectedLOD != _currentLODIndex) SwitchLOD(selectedLOD);
            }
            else
            {
                GUILayout.Label("N/A", EditorStyles.toolbarButton, GUILayout.Width(180));
            }

            GUILayout.FlexibleSpace();

            // FIX 9: Replaced brittle Apple Metal check with robust engine-level Geometry Shader check.
            if (SystemInfo.supportsGeometryShaders)
            {
                GUILayout.Label("Wireframe:", GUILayout.Width(65));
                EditorGUI.BeginChangeCheck();
                _wireframeMode = (WireframeMode)EditorGUILayout.EnumPopup(_wireframeMode, EditorStyles.toolbarPopup, GUILayout.Width(75));
                if (EditorGUI.EndChangeCheck())
                {
                    Repaint();
                }
            }
            else
            {
                _wireframeMode = WireframeMode.Disabled;
            }

            EditorGUILayout.EndHorizontal();
        }
        private void ConfigurePreviewBakedTextures()
        {
            ConfigurePreviewTexture(_previewAlbedo, "Preview Albedo", TextureWrapMode.Repeat, FilterMode.Bilinear, 4);
            ConfigurePreviewTexture(_previewNormal, "Preview Normal", TextureWrapMode.Repeat, FilterMode.Bilinear, 4);
            ConfigurePreviewTexture(_previewMask, "Preview Material Mask", TextureWrapMode.Repeat, FilterMode.Bilinear, 4);
            ConfigurePreviewTexture(_previewMetallic, "Preview Metallic", TextureWrapMode.Repeat, FilterMode.Bilinear, 4);
            ConfigurePreviewTexture(_previewAO, "Preview AO", TextureWrapMode.Repeat, FilterMode.Bilinear, 4);
            ConfigurePreviewTexture(_previewSmoothness, "Preview Smoothness", TextureWrapMode.Repeat, FilterMode.Bilinear, 4);

            RepackPreviewNormalForUnityMaterial(_previewNormal);
        }

        private void ConfigurePreviewTexture(Texture2D texture, string textureName, TextureWrapMode wrapMode, FilterMode filterMode, int anisoLevel)
        {
            if (texture == null) return;

            texture.name = textureName;
            texture.hideFlags = HideFlags.DontSave;
            texture.wrapMode = wrapMode;
            texture.filterMode = filterMode;
            texture.anisoLevel = anisoLevel;
        }

        private void RepackPreviewNormalForUnityMaterial(Texture2D normalTexture)
        {
            if (normalTexture == null) return;

            // Imported Unity normal maps are repacked/interpreted by the TextureImporter.
            // Preview textures are temporary in-memory Texture2Ds, so they never pass through that importer.
            //
            // The baker writes normal data as:
            // R = encoded X, G = encoded Y, B = encoded Z, A = encoded X.
            //
            // Unity Lit shaders commonly decode normal maps in an AG/DXT5nm-compatible layout.
            // If the raw preview texture is used as-is, X can be decoded incorrectly and can expose
            // UV/tangent chart boundaries as visible cube-sphere or icosphere regions.
            //
            // This converts the temporary preview texture into the material-facing layout:
            // R = 1, G = encoded Y, B = 1, A = encoded X.
            Color32[] pixels = normalTexture.GetPixels32();

            for (int i = 0; i < pixels.Length; i++)
            {
                byte encodedX = pixels[i].a;
                byte encodedY = pixels[i].g;

                // Fallback for older baked preview textures where alpha may not have been populated.
                if (encodedX == 0)
                {
                    encodedX = pixels[i].r;
                }

                pixels[i] = new Color32(255, encodedY, 255, encodedX);
            }

            normalTexture.SetPixels32(pixels);
            normalTexture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
        }
        private void BakePreviewTextures()
        {
            if (_previewRockInstance == null || _lodRenderers.Count == 0) return;

            MeshFilter mf = _lodRenderers[0].GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            SwitchLOD(0);
            Mesh previewMesh = mf.sharedMesh;

            if (_previewAlbedo != null) DestroyImmediate(_previewAlbedo);
            if (_previewNormal != null) DestroyImmediate(_previewNormal);
            if (_previewMask != null) DestroyImmediate(_previewMask);
            if (_previewMetallic != null) DestroyImmediate(_previewMetallic);
            if (_previewAO != null) DestroyImmediate(_previewAO);
            if (_previewSmoothness != null) DestroyImmediate(_previewSmoothness);

            RockTextureBaker.BakeTextures(
                previewMesh,
                _settingsInstance,
                _previewTextureResolution,
                out _previewAlbedo,
                out _previewNormal,
                out _previewMask,
                out _previewMetallic,
                out _previewAO,
                out Texture2D dummyHeight,
                out _previewSmoothness
            );

            if (dummyHeight != null) DestroyImmediate(dummyHeight);

            if (RockPrefabFactory.IsCurrentRenderPipelineHDRP() && _previewMask == null)
            {
                _previewMask = RockPrefabFactory.CreateHDRPMaskMapFromAuxiliaryTextures(
                    _settingsInstance,
                    existingMaskMap: null,
                    metallicMap: _previewMetallic,
                    aoMap: _previewAO,
                    smoothnessMap: _previewSmoothness,
                    textureName: "Preview HDRP Mask Map"
                );
            }

            ConfigurePreviewBakedTextures();

            ApplyPreviewMaterials();
            Repaint();
        }
        private void ApplyPreviewMaterials()
        {
            if (!_isPendingBake && IsBakeMethod(_settingsInstance))
            {
                bool isVCMat = _previewMaterial != null &&
                               (_previewMaterial.shader.name.Contains("Particles") ||
                                _previewMaterial.shader.name.Contains("Internal"));

                if (isVCMat || _previewMaterial == null)
                {
                    UpdatePreviewMaterial();
                }
            }
            else if (_previewMaterial == null)
            {
                UpdatePreviewMaterial();
            }

            if (IsBakeMethod(_settingsInstance))
            {
                bool useBakedNormal = RockPrefabFactory.ShouldApplyBakedNormal(_settingsInstance);

                RockPrefabFactory.ApplyTexturesToMaterial(
                    _previewMaterial,
                    _previewAlbedo,
                    _previewNormal,
                    useBakedNormal
                );

                RockPrefabFactory.ApplyAuxiliaryTexturesToMaterial(
                    _previewMaterial,
                    _settingsInstance,
                    _previewMask,
                    _previewMetallic,
                    _previewAO,
                    _previewSmoothness
                );
            }

            Renderer[] allRenderers = _previewRockInstance != null
                ? _previewRockInstance.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            foreach (var renderer in allRenderers)
            {
                if (renderer != null &&
                    ((_clusterSettings == null || !_clusterSettings.enabled) ||
                     IsGeneratedRockTransform(renderer.transform)))
                {
                    renderer.sharedMaterial = _previewMaterial;
                }
            }
        }

        private static bool IsGeneratedRockTransform(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.StartsWith("Rock_", System.StringComparison.Ordinal)) return true;
                if (current.name == RockClusterPrefabFactory.SurfaceObjectName) return false;
                current = current.parent;
            }
            return false;
        }

        private void Draw3DPreview(Rect r)
        {
            _previewRenderUtility.BeginPreview(r, GUIStyle.none);
            try
            {
                UpdateCameraTransform();

                if (_wireframeMode != WireframeMode.Disabled && _previewRockInstance != null && _lodRenderers.Count > 0)
                {
                    if (_wireframeMaterial == null)
                    {
                        // FIXED: Appended "Lite" to match the actual shader name at the bottom of the script
                        Shader wfShader = Shader.Find("Hidden/Veridian/WireframeOverlayLite");
                        if (wfShader != null)
                        {
                            _wireframeMaterial = new Material(wfShader);
                            _wireframeMaterial.hideFlags = HideFlags.HideAndDontSave;
                        }
                    }

                    if (_wireframeMaterial != null && _currentLODIndex >= 0 && _currentLODIndex < _lodRenderers.Count)
                    {
                        Color wireColor = _wireframeMode == WireframeMode.Black ? Color.black : Color.white;
                        if (_wireframeMaterial.HasProperty("_WireColor")) _wireframeMaterial.SetColor("_WireColor", wireColor);

                        Renderer activeRenderer = _lodRenderers[_currentLODIndex];
                        if (activeRenderer != null)
                        {
                            MeshFilter mf = activeRenderer.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                            {
                                Vector3 pos = activeRenderer.transform.position;
                                Quaternion rot = activeRenderer.transform.rotation;
                                Vector3 scale = activeRenderer.transform.lossyScale * 1.002f;
                                Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, scale);

                                _previewRenderUtility.DrawMesh(mf.sharedMesh, matrix, _wireframeMaterial, 0);
                            }
                        }
                    }
                }

                _previewRenderUtility.Render(true);
            }
            finally
            {
                // FIXED: Wrapped in try/finally to guarantee the clip stack is restored even if rendering throws an exception
                _previewRenderUtility.EndAndDrawPreview(r);
            }
        }

        private void UpdateCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(_cameraRotationAngles.x, _cameraRotationAngles.y, 0);
            Vector3 position = _cameraPivot + rotation * Vector3.back * _cameraDistance;
            _previewRenderUtility.camera.transform.position = position;
            _previewRenderUtility.camera.transform.rotation = rotation;
        }

        private void HandleCameraControls(Rect previewRect)
        {
            Event e = Event.current;
            int controlID = GUIUtility.GetControlID("RockPreviewCamera".GetHashCode(), FocusType.Passive);

            if (previewRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    _cameraDistance *= 1f + e.delta.y * 0.05f;
                    _cameraDistance = Mathf.Max(0.1f, _cameraDistance);
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                {
                    EditorGUIUtility.SetWantsMouseJumping(1);
                    GUIUtility.hotControl = controlID;
                    e.Use();
                }
            }

            if (GUIUtility.hotControl == controlID)
            {
                // PHASE 1 FIX: Use rawType to catch mouse release anywhere globally
                if (e.rawType == EventType.MouseUp)
                {
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag)
                {
                    Vector2 delta = e.delta;
                    _cameraRotationAngles.x -= delta.y * 0.5f;
                    _cameraRotationAngles.y += delta.x * 0.5f;
                    _cameraRotationAngles.x = Mathf.Clamp(_cameraRotationAngles.x, -89f, 89f);
                    e.Use();
                    Repaint();
                }
            }
        }

        private void FocusCameraOnObject(bool resetRotation = false)
        {
            if (_previewRockInstance == null) return;

            Bounds bounds = new Bounds(_previewRockInstance.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer r in _previewRockInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(r.bounds);
            }

            if (hasBounds)
            {
                _cameraPivot = bounds.center;
                float objectSize = bounds.size.magnitude;
                if (objectSize < 0.01f) objectSize = 1f;

                float cameraView = 2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad * _previewRenderUtility.camera.fieldOfView);

                // --- UI/UX FIX: Changed the framing distance multiplier from 1.5f down to 1.15f. 
                // This pulls the camera closer and makes the rock appear significantly larger!
                _cameraDistance = 1.15f * objectSize / cameraView;
                _cameraDistance = Mathf.Max(_cameraDistance, 0.1f);
            }

            if (resetRotation) _cameraRotationAngles = new Vector2(20f, -135f);
            Repaint();
        }

        private void GeneratePreviewRock()
        {
            if (_isGenerating || _generator == null || _settingsInstance == null) return;
            if (_settingsInstance.lodLevels.Count == 0) { _needsRegeneration = false; return; }

            if (_clusterSettings != null && _clusterSettings.enabled)
            {
                GenerateClusterPreview();
                return;
            }

            _needsRegeneration = false;
            _isGenerating = true;

            bool isVCMode = _settingsInstance.colorizationMethod == RockColorizationMethod.VertexColors || _isPendingBake;
            bool isVCMat = _previewMaterial != null && (_previewMaterial.shader.name.Contains("Particles") || _previewMaterial.shader.name.Contains("Internal"));

            if (_previewMaterial == null || isVCMode != isVCMat)
            {
                UpdatePreviewMaterial();
            }

            RockRequest request = new RockRequest(
                _settingsInstance,
                Vector3.zero, Quaternion.identity, Vector3.one,
                _previewMaterial,
                false, // NEW: Do NOT generate colliders for the live preview window
                OnPreviewRockGenerated
            );

            _generator.GenerateRock(request);
        }

        private void GenerateClusterPreview()
        {
            _needsRegeneration = false;
            CleanupPendingCluster();

            _pendingClusterPlacements = RockClusterLayoutGenerator.Generate(
                _clusterSettings,
                _settingsInstance,
                out _clusterWarning);
            if (_pendingClusterPlacements.Count == 0)
            {
                _isGenerating = false;
                Repaint();
                return;
            }

            _isGenerating = true;
            _pendingClusterIndex = 0;
            _pendingClusterRoot = new GameObject("Rock_Cluster_Preview");
            _pendingClusterRoot.hideFlags = HideFlags.HideAndDontSave;

            if (_clusterSettings.shape == RockClusterShape.MeshSurface &&
                _clusterSettings.showSurfaceInPreview &&
                _clusterSettings.surfaceObject != null)
            {
                GameObject surface = Instantiate(_clusterSettings.surfaceObject);
                surface.name = RockClusterPrefabFactory.SurfaceObjectName;
                surface.transform.SetParent(_pendingClusterRoot.transform, false);
                surface.transform.localPosition = Vector3.zero;
                surface.transform.localRotation = Quaternion.identity;
                surface.transform.localScale = Vector3.one;
                SetHideFlagsRecursive(surface, HideFlags.HideAndDontSave);
            }

            GenerateNextClusterRock();
        }

        private void GenerateNextClusterRock()
        {
            if (!_isGenerating || _pendingClusterRoot == null || _pendingClusterPlacements == null)
            {
                CleanupPendingCluster();
                _isGenerating = false;
                return;
            }

            if (_pendingClusterIndex >= _pendingClusterPlacements.Count)
            {
                CompleteClusterPreview();
                return;
            }

            int index = _pendingClusterIndex;
            RockClusterPlacement placement = _pendingClusterPlacements[index];
            RockSettings perRockSettings = Instantiate(_settingsInstance);
            perRockSettings.name = $"{_settingsInstance.name}_Cluster_{index:000}";
            perRockSettings.seed = placement.rockSeed;
            perRockSettings.hideFlags = HideFlags.HideAndDontSave;
            _activeClusterRockSettings = perRockSettings;

            RockRequest request = new RockRequest(
                perRockSettings,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                _previewMaterial,
                true,
                generatedRock => OnClusterRockGenerated(index, placement, perRockSettings, generatedRock));
            _generator.GenerateRock(request);
        }

        private void OnClusterRockGenerated(
            int index,
            RockClusterPlacement placement,
            RockSettings perRockSettings,
            GameObject generatedRock)
        {
            if (perRockSettings != null)
            {
                DestroyImmediate(perRockSettings);
            }
            if (_activeClusterRockSettings == perRockSettings)
            {
                _activeClusterRockSettings = null;
            }

            if (!_isGenerating || _pendingClusterRoot == null)
            {
                if (generatedRock != null) DestroyPreviewRock(generatedRock);
                return;
            }

            if (generatedRock == null)
            {
                _clusterWarning = $"Rock {index + 1} could not be generated. The previous valid preview was kept.";
                CleanupPendingCluster();
                _isGenerating = false;
                Repaint();
                return;
            }

            generatedRock.name = $"Rock_{index:000}_Seed_{placement.rockSeed}";
            generatedRock.transform.SetParent(_pendingClusterRoot.transform, false);
            generatedRock.transform.localPosition = placement.localPosition;
            generatedRock.transform.localRotation = placement.localRotation;
            generatedRock.transform.localScale = placement.localScale;
            SetHideFlagsRecursive(generatedRock, HideFlags.HideAndDontSave);

            _pendingClusterIndex++;
            GenerateNextClusterRock();
        }

        private void CompleteClusterPreview()
        {
            GameObject completedRoot = _pendingClusterRoot;
            _pendingClusterRoot = null;
            _pendingClusterPlacements = null;
            _pendingClusterIndex = 0;
            _isGenerating = false;

            if (_previewRockInstance != null || _previewRenderUtility != null)
            {
                CleanupPreview();
                InitializePreview();
            }

            _previewRockInstance = completedRoot;
            if (_previewRockInstance != null && _previewRenderUtility != null)
            {
                SetHideFlagsRecursive(_previewRockInstance, HideFlags.HideAndDontSave);
                _previewRenderUtility.AddSingleGO(_previewRockInstance);
                UpdateLODRenderers();
                ApplyPreviewMaterials();
                SwitchLOD(0);
                FocusCameraOnObject();

                if (IsBakeMethod(_settingsInstance))
                {
                    _isPendingBake = true;
                    _lastChangeTime = EditorApplication.timeSinceStartup;
                }
            }

            Repaint();
        }

        private static void SetHideFlagsRecursive(GameObject root, HideFlags hideFlags)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = hideFlags;
            }
        }

        private void OnPreviewRockGenerated(GameObject generatedRock)
        {
            _isGenerating = false;

            if (_previewRenderUtility == null)
            {
                if (generatedRock != null)
                {
                    DestroyPreviewRock(generatedRock);
                }
                return;
            }

            if (_previewRockInstance != null)
            {
                CleanupPreview();
                InitializePreview();
            }

            _previewRockInstance = generatedRock;

            if (_previewRockInstance != null)
            {
                _previewRockInstance.hideFlags = HideFlags.HideAndDontSave;
                _previewRenderUtility.AddSingleGO(_previewRockInstance);

                UpdateLODRenderers();

                ApplyPreviewMaterials();
                SwitchLOD(0);

                FocusCameraOnObject();
            }

            Repaint();
        }
        private void UpdateLODRenderers()
        {
            _lodRenderers.Clear();
            if (_previewRockInstance == null) return;

            LODGroup lodGroup = _previewRockInstance.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                LODGroup[] childGroups = _previewRockInstance.GetComponentsInChildren<LODGroup>(true);
                if (childGroups.Length > 0) lodGroup = childGroups[0];
            }
            if (lodGroup != null)
            {
                lodGroup.enabled = false;

                LOD[] lods = lodGroup.GetLODs();
                foreach (var lod in lods)
                {
                    if (lod.renderers != null && lod.renderers.Length > 0 && lod.renderers[0] != null)
                    {
                        _lodRenderers.Add(lod.renderers[0]);
                    }
                }
            }
        }

        private void SwitchLOD(int lodIndex)
        {
            if (_previewRockInstance == null || _lodRenderers.Count == 0) return;

            lodIndex = Mathf.Clamp(lodIndex, 0, _lodRenderers.Count - 1);
            _currentLODIndex = lodIndex;

            LODGroup[] groups = _previewRockInstance.GetComponentsInChildren<LODGroup>(true);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                groups[groupIndex].enabled = false;
                LOD[] lods = groups[groupIndex].GetLODs();
                for (int lodIndexInGroup = 0; lodIndexInGroup < lods.Length; lodIndexInGroup++)
                {
                    Renderer[] renderers = lods[lodIndexInGroup].renderers;
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        if (renderers[rendererIndex] != null)
                        {
                            renderers[rendererIndex].enabled = lodIndexInGroup == _currentLODIndex;
                        }
                    }
                }
            }

            Repaint();
        }
        #endregion

        private bool IsBakeMethod(RockSettings settings)
        {
            return settings != null && settings.colorizationMethod != RockColorizationMethod.VertexColors;
        }

        private void RepairPreviewState()
        {
            _isGenerating = false;
            _needsRegeneration = true;
            _currentLODIndex = 0;

            _isPendingBake = IsBakeMethod(_settingsInstance);
            _lastChangeTime = EditorApplication.timeSinceStartup;

            ClearPreviewBakedTextures();

            CleanupPreview();
            CleanupGenerator();

            if (_previewMaterial != null)
            {
                DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }

            if (_wireframeMaterial != null)
            {
                DestroyImmediate(_wireframeMaterial);
                _wireframeMaterial = null;
            }

            InitializeGenerator();
            InitializePreview();
            UpdatePreviewMaterial();

            Repaint();
        }

        private void ClearPreviewBakedTextures()
        {
            ClearPreviewMaterialTextureReferences();

            DestroyPreviewTexture(ref _previewAlbedo);
            DestroyPreviewTexture(ref _previewNormal);
            DestroyPreviewTexture(ref _previewMask);
            DestroyPreviewTexture(ref _previewMetallic);
            DestroyPreviewTexture(ref _previewAO);
            DestroyPreviewTexture(ref _previewSmoothness);
        }

        private void DestroyPreviewTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                DestroyImmediate(texture);
                texture = null;
            }
        }

        private void ClearPreviewMaterialTextureReferences()
        {
            if (_previewMaterial == null) return;

            if (_previewMaterial.HasProperty("_BaseMap")) _previewMaterial.SetTexture("_BaseMap", null);
            if (_previewMaterial.HasProperty("_MainTex")) _previewMaterial.SetTexture("_MainTex", null);
            if (_previewMaterial.HasProperty("_BaseColorMap")) _previewMaterial.SetTexture("_BaseColorMap", null);

            if (_previewMaterial.HasProperty("_BumpMap")) _previewMaterial.SetTexture("_BumpMap", null);
            if (_previewMaterial.HasProperty("_NormalMap")) _previewMaterial.SetTexture("_NormalMap", null);

            if (_previewMaterial.HasProperty("_MetallicGlossMap")) _previewMaterial.SetTexture("_MetallicGlossMap", null);
            if (_previewMaterial.HasProperty("_OcclusionMap")) _previewMaterial.SetTexture("_OcclusionMap", null);
            if (_previewMaterial.HasProperty("_MaskMap")) _previewMaterial.SetTexture("_MaskMap", null);
            if (_previewMaterial.HasProperty("_ParallaxMap")) _previewMaterial.SetTexture("_ParallaxMap", null);
            if (_previewMaterial.HasProperty("_HeightMap")) _previewMaterial.SetTexture("_HeightMap", null);

            _previewMaterial.DisableKeyword("_NORMALMAP");
            _previewMaterial.DisableKeyword("_NORMALMAP_TANGENT_SPACE");
            _previewMaterial.DisableKeyword("_METALLICGLOSSMAP");
            _previewMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");
            _previewMaterial.DisableKeyword("_MASKMAP");
            _previewMaterial.DisableKeyword("_PARALLAXMAP");
            _previewMaterial.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
    }
}
#endif
