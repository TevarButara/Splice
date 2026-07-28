#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Veridian.RockGenLite.Runtime;
using System;
using Object = UnityEngine.Object;
using System.Threading; // PHASE 2: Added for CancellationTokenSource

namespace Veridian.RockGenLite.Editor
{
    [InitializeOnLoad]
    public static class RockPrefabFactory
    {
        public static string LastGeneratedRockPath { get; private set; }
        public static void ClearLastGeneratedPath() { LastGeneratedRockPath = null; }
        internal static void SetLastGeneratedPath(string path)
        {
            LastGeneratedRockPath = path;
            OnGenerationFinished?.Invoke();
        }
        public static event Action OnGenerationFinished;

        private static RuntimeRockGenerator _editorGenerator;
        public static bool IsGenerating => _isGenerating;
        private static bool _isGenerating = false;

        private static Action _pendingCleanupAction;

        // PHASE 2: Added CancellationTokenSource to handle safe thread aborting
        private static CancellationTokenSource _cancellationTokenSource;

        static RockPrefabFactory()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            if (_isGenerating && _pendingCleanupAction != null)
            {
                Debug.LogWarning("[Rock Generator Lite] Generation aborted to prevent memory leaks during script recompilation.");

                // PHASE 2: Safely signal the async writing thread to abort
                _cancellationTokenSource?.Cancel();

                _pendingCleanupAction.Invoke();
                _pendingCleanupAction = null;
            }
        }
        private enum RockRenderPipelineKind
        {
            BuiltIn,
            URP,
            HDRP
        }

        internal static bool IsCurrentRenderPipelineHDRP()
        {
            return GetActiveRenderPipelineKind() == RockRenderPipelineKind.HDRP;
        }

        private static RockRenderPipelineKind GetActiveRenderPipelineKind()
        {
            UnityEngine.Rendering.RenderPipelineAsset rpAsset = QualitySettings.renderPipeline;

            if (rpAsset == null)
            {
                rpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            }

            if (rpAsset == null)
            {
                return RockRenderPipelineKind.BuiltIn;
            }

            Type assetType = rpAsset.GetType();
            string typeName = assetType.Name ?? string.Empty;
            string fullName = assetType.FullName ?? string.Empty;
            string combinedName = typeName + " " + fullName;

            if (combinedName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combinedName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RockRenderPipelineKind.HDRP;
            }

            if (combinedName.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combinedName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RockRenderPipelineKind.URP;
            }

            return RockRenderPipelineKind.BuiltIn;
        }

        private static bool IsMaterialUsingHDRP(Material material)
        {
            if (material != null && material.shader != null)
            {
                string shaderName = material.shader.name ?? string.Empty;

                if (shaderName.IndexOf("HDRP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    shaderName.IndexOf("High Definition", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return GetActiveRenderPipelineKind() == RockRenderPipelineKind.HDRP;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }
        public static void CreateAndSavePrefab(RockSettings settings, Action<GameObject> onComplete = null, bool showProgressBar = true)
        {
            if (settings == null) { onComplete?.Invoke(null); return; }
            if (_isGenerating) { Debug.LogWarning("Generation active. Please wait."); onComplete?.Invoke(null); return; }
            if (settings.lodLevels.Count == 0) { onComplete?.Invoke(null); return; }

            string directory = CreateOrganizedAssetFolder(settings);
            if (string.IsNullOrEmpty(directory)) { onComplete?.Invoke(null); return; }
            string baseName = Path.GetFileName(directory);

            InitializeEditorGenerator();
            _isGenerating = true;
            EditorGenerationDriver.Register(_editorGenerator);

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            if (showProgressBar) EditorUtility.DisplayProgressBar("Rock Generator", "Starting Generation...", 0.1f);

            Material tempMat = null;

            try
            {
                tempMat = settings.colorizationMethod == RockColorizationMethod.VertexColors
                    ? CreateVertexColorMaterial()
                    : CreateDefaultPBRMaterial();

                RockRequest request = new RockRequest(
                    settings, Vector3.zero, Quaternion.identity, Vector3.one, tempMat,
                    true, // NEW: Always generate colliders when saving the final prefab
                    (generatedGO) => OnGenerationComplete(generatedGO, settings, directory, baseName, tempMat, onComplete, showProgressBar)
                );

                _editorGenerator.GenerateRock(request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Rock Generator Lite] Initialization failed: {ex.Message}");
                if (tempMat != null) Object.DestroyImmediate(tempMat);
                CleanupGeneration(showProgressBar);
                onComplete?.Invoke(null);
            }
        }
        internal static Material CreateVertexColorMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Hidden/Internal-Colored");

            if (shader == null)
            {
                Debug.LogWarning("[Rock Generator Lite] Could not find a vertex color shader. Falling back to the default material.");
                return CreateDefaultPBRMaterial();
            }

            Material material = new Material(shader);

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);

            material.renderQueue = -1;
            material.enableInstancing = true;

            return material;
        }
        private static void OnGenerationComplete(GameObject generatedGO, RockSettings settings, string directory, string baseName, Material tempMat, Action<GameObject> onComplete, bool showProgressBar)
        {
            GameObject savedPrefab = null;
            List<Mesh> clonedMeshes = null;

            bool isEditingAssets = false;

            HashSet<Mesh> originalMeshes = new HashSet<Mesh>();
            if (generatedGO != null)
            {
                MeshFilter[] filters = generatedGO.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in filters)
                {
                    if (mf != null && mf.sharedMesh != null) originalMeshes.Add(mf.sharedMesh);
                }

                MeshCollider mc = generatedGO.GetComponentInChildren<MeshCollider>();
                if (mc != null && mc.sharedMesh != null) originalMeshes.Add(mc.sharedMesh);
            }

            EditorApplication.CallbackFunction checkTask = null;

            void DoCleanup()
            {
                _pendingCleanupAction = null;

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                if (checkTask != null)
                {
                    EditorApplication.update -= checkTask;
                }

                foreach (var m in originalMeshes)
                {
                    if (m != null) Object.DestroyImmediate(m);
                }

                if (clonedMeshes != null)
                {
                    foreach (var m in clonedMeshes)
                    {
                        if (m != null && !EditorUtility.IsPersistent(m)) Object.DestroyImmediate(m);
                    }
                }

                if (generatedGO != null) Object.DestroyImmediate(generatedGO);

                if (tempMat != null && !EditorUtility.IsPersistent(tempMat))
                {
                    Object.DestroyImmediate(tempMat);
                }

                if (isEditingAssets)
                {
                    AssetDatabase.StopAssetEditing();
                    isEditingAssets = false;
                }

                CleanupGeneration(showProgressBar);

                if (!EditorApplication.isCompiling)
                {
                    AssetDatabase.Refresh();
                }

                onComplete?.Invoke(savedPrefab);
            }

            _pendingCleanupAction = () =>
            {
                if (showProgressBar) EditorUtility.ClearProgressBar();
                DoCleanup();
            };

            try
            {
                if (generatedGO == null)
                {
                    DoCleanup();
                    return;
                }

                if (showProgressBar) EditorUtility.DisplayProgressBar("Rock Generator", "Extracting meshes...", 0.3f);
                clonedMeshes = ExtractAndCloneMeshes(generatedGO, baseName);

                List<Material> generatedMaterials = new List<Material>();

                if (settings.colorizationMethod != RockColorizationMethod.VertexColors)
                {
                    Object.DestroyImmediate(tempMat);
                    tempMat = null;

                    if (showProgressBar) EditorUtility.DisplayProgressBar("Rock Generator", "Baking Seamless Textures (GPU)...", 0.4f);

                    int resolution = Mathf.Max(128, settings.textureResolution);

                    RockTextureBaker.BakeTextures(
                        clonedMeshes[0],
                        settings,
                        resolution,
                        out Texture2D albedo,
                        out Texture2D normal,
                        out Texture2D maskMap,
                        out Texture2D metallicMap,
                        out Texture2D aoMap,
                        out Texture2D heightMap,
                        out Texture2D smoothMap
                    );

                    bool isHDRP = IsCurrentRenderPipelineHDRP();
                    bool isPacked = settings.textureExportMode == RockTextureExportMode.PackedMaskMap;

                    if (isHDRP && maskMap == null)
                    {
                        Texture2D hdrpMaskMap = CreateHDRPMaskMapFromAuxiliaryTextures(
                            settings,
                            existingMaskMap: null,
                            metallicMap: metallicMap,
                            aoMap: aoMap,
                            smoothnessMap: smoothMap,
                            textureName: $"{baseName}_HDRP_MaskMap"
                        );

                        if (hdrpMaskMap != null)
                        {
                            maskMap = hdrpMaskMap;
                        }
                    }

                    bool shouldSaveMaterialMaskMap = maskMap != null && (isPacked || isHDRP);

                    string albedoPath = System.IO.Path.Combine(directory, $"{baseName}_Albedo.png").Replace('\\', '/');
                    string normalPath = System.IO.Path.Combine(directory, $"{baseName}_Normal.png").Replace('\\', '/');

                    string maskFileName = isPacked ? $"{baseName}_MaskMap.png" : $"{baseName}_HDRP_MaskMap.png";
                    string maskPath = shouldSaveMaterialMaskMap ? System.IO.Path.Combine(directory, maskFileName).Replace('\\', '/') : null;

                    string metallicPath = !isPacked && metallicMap != null ? System.IO.Path.Combine(directory, $"{baseName}_Metallic.png").Replace('\\', '/') : null;
                    string aoPath = !isPacked && aoMap != null ? System.IO.Path.Combine(directory, $"{baseName}_AO.png").Replace('\\', '/') : null;
                    string heightPath = heightMap != null ? System.IO.Path.Combine(directory, $"{baseName}_Height.png").Replace('\\', '/') : null;
                    string smoothPath = !isPacked && smoothMap != null ? System.IO.Path.Combine(directory, $"{baseName}_Smoothness.png").Replace('\\', '/') : null;

                    byte[] albedoBytes = albedo != null ? albedo.EncodeToPNG() : null;
                    byte[] normalBytes = normal != null ? normal.EncodeToPNG() : null;
                    byte[] maskBytes = shouldSaveMaterialMaskMap && maskMap != null ? maskMap.EncodeToPNG() : null;
                    byte[] metallicBytes = metallicMap != null ? metallicMap.EncodeToPNG() : null;
                    byte[] aoBytes = aoMap != null ? aoMap.EncodeToPNG() : null;
                    byte[] heightBytes = heightMap != null ? heightMap.EncodeToPNG() : null;
                    byte[] smoothBytes = smoothMap != null ? smoothMap.EncodeToPNG() : null;

                    if (albedo != null) Object.DestroyImmediate(albedo);
                    if (normal != null) Object.DestroyImmediate(normal);
                    if (maskMap != null) Object.DestroyImmediate(maskMap);
                    if (metallicMap != null) Object.DestroyImmediate(metallicMap);
                    if (aoMap != null) Object.DestroyImmediate(aoMap);
                    if (heightMap != null) Object.DestroyImmediate(heightMap);
                    if (smoothMap != null) Object.DestroyImmediate(smoothMap);

                    if (albedoBytes == null || normalBytes == null)
                    {
                        Debug.LogError("[Rock Generator Lite] Texture bake did not produce the required albedo or normal texture.");
                        DoCleanup();
                        return;
                    }

                    if (showProgressBar) EditorUtility.DisplayProgressBar("Rock Generator", "Saving Textures to Disk (Async)...", 0.6f);

                    System.Threading.CancellationToken token = _cancellationTokenSource != null ? _cancellationTokenSource.Token : System.Threading.CancellationToken.None;

                    var writeTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        System.IO.File.WriteAllBytes(albedoPath, albedoBytes);

                        if (token.IsCancellationRequested) return;
                        System.IO.File.WriteAllBytes(normalPath, normalBytes);

                        if (maskBytes != null && !string.IsNullOrEmpty(maskPath))
                        {
                            if (token.IsCancellationRequested) return;
                            System.IO.File.WriteAllBytes(maskPath, maskBytes);
                        }

                        if (metallicBytes != null && !string.IsNullOrEmpty(metallicPath))
                        {
                            if (token.IsCancellationRequested) return;
                            System.IO.File.WriteAllBytes(metallicPath, metallicBytes);
                        }

                        if (aoBytes != null && !string.IsNullOrEmpty(aoPath))
                        {
                            if (token.IsCancellationRequested) return;
                            System.IO.File.WriteAllBytes(aoPath, aoBytes);
                        }

                        if (heightBytes != null && !string.IsNullOrEmpty(heightPath))
                        {
                            if (token.IsCancellationRequested) return;
                            System.IO.File.WriteAllBytes(heightPath, heightBytes);
                        }

                        if (smoothBytes != null && !string.IsNullOrEmpty(smoothPath))
                        {
                            if (token.IsCancellationRequested) return;
                            System.IO.File.WriteAllBytes(smoothPath, smoothBytes);
                        }
                    }, token);

                    checkTask = () =>
                    {
                        if (!_isGenerating || token.IsCancellationRequested)
                        {
                            EditorApplication.update -= checkTask;
                            return;
                        }

                        if (!writeTask.IsCompleted) return;
                        EditorApplication.update -= checkTask;

                        try
                        {
                            if (writeTask.IsFaulted || writeTask.IsCanceled)
                            {
                                Debug.LogError("Failed to save textures: " + writeTask.Exception);
                                return;
                            }

                            if (showProgressBar) EditorUtility.DisplayProgressBar("Rock Generator", "Importing Textures...", 0.7f);

                            ConfigureGeneratedTextureImporters(
                                albedoPath,
                                normalPath,
                                maskPath,
                                metallicPath,
                                aoPath,
                                heightPath,
                                smoothPath
                            );

                            if (maskPath != null) AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);
                            if (metallicPath != null) AssetDatabase.ImportAsset(metallicPath, ImportAssetOptions.ForceUpdate);
                            if (aoPath != null) AssetDatabase.ImportAsset(aoPath, ImportAssetOptions.ForceUpdate);
                            if (heightPath != null) AssetDatabase.ImportAsset(heightPath, ImportAssetOptions.ForceUpdate);
                            if (smoothPath != null) AssetDatabase.ImportAsset(smoothPath, ImportAssetOptions.ForceUpdate);

                            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                            if (normalImporter != null)
                            {
                                normalImporter.textureType = TextureImporterType.NormalMap;
                                normalImporter.SaveAndReimport();
                            }

                            if (maskPath != null)
                            {
                                TextureImporter maskImp = AssetImporter.GetAtPath(maskPath) as TextureImporter;
                                if (maskImp != null)
                                {
                                    maskImp.sRGBTexture = false;
                                    maskImp.alphaSource = TextureImporterAlphaSource.FromInput;
                                    maskImp.alphaIsTransparency = false;
                                    maskImp.SaveAndReimport();
                                }
                            }

                            if (metallicPath != null)
                            {
                                TextureImporter metalImp = AssetImporter.GetAtPath(metallicPath) as TextureImporter;
                                if (metalImp != null)
                                {
                                    metalImp.sRGBTexture = false;
                                    metalImp.alphaSource = TextureImporterAlphaSource.FromInput;
                                    metalImp.alphaIsTransparency = false;
                                    metalImp.SaveAndReimport();
                                }
                            }

                            if (aoPath != null)
                            {
                                TextureImporter aoImp = AssetImporter.GetAtPath(aoPath) as TextureImporter;
                                if (aoImp != null)
                                {
                                    aoImp.sRGBTexture = false;
                                    aoImp.SaveAndReimport();
                                }
                            }

                            if (heightPath != null)
                            {
                                TextureImporter heightImp = AssetImporter.GetAtPath(heightPath) as TextureImporter;
                                if (heightImp != null)
                                {
                                    heightImp.sRGBTexture = false;
                                    heightImp.SaveAndReimport();
                                }
                            }

                            if (smoothPath != null)
                            {
                                TextureImporter smoothImp = AssetImporter.GetAtPath(smoothPath) as TextureImporter;
                                if (smoothImp != null)
                                {
                                    smoothImp.sRGBTexture = false;
                                    smoothImp.SaveAndReimport();
                                }
                            }

                            if (isEditingAssets)
                            {
                                AssetDatabase.StopAssetEditing();
                                isEditingAssets = false;
                            }

                            Material sharedMat = CreateDefaultPBRMaterial();
                            tempMat = sharedMat;

                            sharedMat.name = $"{baseName}_Mat";

                            Texture2D loadedAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                            Texture2D loadedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                            Texture2D loadedMask = maskPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath) : null;
                            Texture2D loadedMetallic = metallicPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath) : null;
                            Texture2D loadedAO = aoPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath) : null;
                            Texture2D loadedHeight = heightPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath) : null;
                            Texture2D loadedSmoothness = smoothPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(smoothPath) : null;

                            ApplyTexturesToMaterial(
                                sharedMat,
                                loadedAlbedo,
                                loadedNormal,
                                ShouldApplyBakedNormal(settings)
                            );

                            ApplyAuxiliaryTexturesToMaterial(
                                sharedMat,
                                settings,
                                loadedMask,
                                loadedMetallic,
                                loadedAO,
                                loadedSmoothness
                            );

                            ApplyHeightTextureToMaterial(sharedMat, loadedHeight);

                            AssetDatabase.CreateAsset(sharedMat, System.IO.Path.Combine(directory, sharedMat.name + ".mat").Replace('\\', '/'));

                            for (int i = 0; i < clonedMeshes.Count; i++)
                            {
                                generatedMaterials.Add(sharedMat);
                            }

                            foreach (var mesh in clonedMeshes)
                            {
                                AssetDatabase.CreateAsset(mesh, System.IO.Path.Combine(directory, mesh.name + ".asset").Replace('\\', '/'));
                            }

                            savedPrefab = CreatePrefab(generatedGO, settings, directory, baseName, clonedMeshes, generatedMaterials);

                            if (showProgressBar) Debug.Log($"<color=green>Success!</color> Rock prefab generated to: <b>{directory}</b>");

                            LastGeneratedRockPath = directory;

                            if (showProgressBar)
                            {
                                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(directory));
                            }

                            OnGenerationFinished?.Invoke();
                        }
                        finally
                        {
                            DoCleanup();
                        }
                    };

                    EditorApplication.update += checkTask;
                }
                else
                {
                    AssetDatabase.StartAssetEditing();
                    isEditingAssets = true;

                    tempMat.name = baseName + "_Mat";
                    AssetDatabase.CreateAsset(tempMat, System.IO.Path.Combine(directory, tempMat.name + ".mat").Replace('\\', '/'));

                    for (int i = 0; i < clonedMeshes.Count; i++)
                    {
                        generatedMaterials.Add(tempMat);
                    }

                    tempMat = null;

                    if (isEditingAssets)
                    {
                        AssetDatabase.StopAssetEditing();
                        isEditingAssets = false;
                    }

                    foreach (var mesh in clonedMeshes)
                    {
                        AssetDatabase.CreateAsset(mesh, System.IO.Path.Combine(directory, mesh.name + ".asset").Replace('\\', '/'));
                    }

                    savedPrefab = CreatePrefab(generatedGO, settings, directory, baseName, clonedMeshes, generatedMaterials);

                    if (showProgressBar) Debug.Log($"<color=green>Success!</color> Rock prefab generated to: <b>{directory}</b>");

                    LastGeneratedRockPath = directory;

                    if (showProgressBar)
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(directory));
                    }

                    OnGenerationFinished?.Invoke();

                    DoCleanup();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating rock: {ex.Message}");
                DoCleanup();
            }
        }

        #region Context Methods 
        private static void InitializeEditorGenerator()
        {
            if (_editorGenerator == null)
            {
                GameObject generatorGO = new GameObject("RockGenerator_Factory_Service");
                generatorGO.hideFlags = HideFlags.HideAndDontSave;
                _editorGenerator = generatorGO.AddComponent<RuntimeRockGenerator>();
            }
        }

        private static void CleanupGeneration(bool showProgressBar = true)
        {
            _isGenerating = false;
            if (_editorGenerator != null)
            {
                EditorGenerationDriver.Unregister(_editorGenerator);
                Object.DestroyImmediate(_editorGenerator.gameObject);
                _editorGenerator = null;
            }
            if (showProgressBar) EditorUtility.ClearProgressBar();
        }

        private static List<Mesh> ExtractAndCloneMeshes(GameObject rootGO, string baseName)
        {
            List<Mesh> clonedMeshes = new List<Mesh>();
            MeshFilter[] filters = rootGO.GetComponentsInChildren<MeshFilter>();
            Array.Sort(filters, (a, b) => a.gameObject.name.CompareTo(b.gameObject.name));

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh != null)
                {
                    Mesh meshClone = Object.Instantiate(filter.sharedMesh);
                    meshClone.name = $"{baseName}_LOD{i}";
                    clonedMeshes.Add(meshClone);
                }
            }

            // NEW: Explicitly pull the hidden convex mesh if it exists
            MeshCollider mc = rootGO.GetComponentInChildren<MeshCollider>();
            if (mc != null && mc.sharedMesh != null)
            {
                bool isUnique = true;
                foreach (var filter in filters)
                {
                    if (filter.sharedMesh == mc.sharedMesh) isUnique = false;
                }

                if (isUnique)
                {
                    Mesh meshClone = Object.Instantiate(mc.sharedMesh);
                    meshClone.name = $"{baseName}_ConvexCollider";
                    clonedMeshes.Add(meshClone);
                }
            }

            return clonedMeshes;
        }

        private static GameObject CreatePrefab(GameObject generatedGO, RockSettings settings, string directory, string baseName, List<Mesh> savedMeshes, List<Material> materials)
        {
            LODGroup lodGroup = generatedGO.GetComponent<LODGroup>();
            if (lodGroup == null) return null;

            LOD[] lods = lodGroup.GetLODs();
            int lodCount = Mathf.Min(lods.Length, savedMeshes.Count);

            for (int i = 0; i < lodCount; i++)
            {
                if (lods[i].renderers.Length > 0 && lods[i].renderers[0] != null)
                {
                    MeshRenderer renderer = lods[i].renderers[0] as MeshRenderer;
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = materials[i];

                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null) filter.sharedMesh = savedMeshes[i];
                    }
                }
            }

            MeshCollider rootCollider = generatedGO.GetComponentInChildren<MeshCollider>();
            if (rootCollider != null && savedMeshes.Count > 0)
            {
                // NEW: Ensure the newly saved meshes get hooked up correctly
                if (settings.colliderType == RockColliderType.ConvexMesh)
                {
                    Mesh convexSaved = savedMeshes.Find(m => m.name.EndsWith("_ConvexCollider"));
                    if (convexSaved != null)
                    {
                        rootCollider.sharedMesh = convexSaved;
                    }
                    else
                    {
                        rootCollider.sharedMesh = savedMeshes[0]; // Fallback
                    }
                }
                else if (settings.colliderType == RockColliderType.ExactMesh)
                {
                    int colliderIndex = Mathf.Clamp(settings.colliderLODIndex, 0, lodCount - 1);
                    rootCollider.sharedMesh = savedMeshes[colliderIndex];
                }
            }

            generatedGO.name = baseName;

            string prefabPath = System.IO.Path.Combine(directory, baseName + ".prefab").Replace('\\', '/');
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(generatedGO, prefabPath, out bool success);
            if (!success) Debug.LogError($"Failed to save prefab at {prefabPath}.");
            return savedPrefab;
        }

        private static string CreateOrganizedAssetFolder(RockSettings settings)
        {
            string defaultPath = "Assets/VeridianData/RockGenerator/Rocks";

            if (string.IsNullOrWhiteSpace(settings.saveFolderPath))
            {
                settings.saveFolderPath = defaultPath;
            }

            string rootFolder = settings.saveFolderPath.Replace('\\', '/');

            if (!rootFolder.StartsWith("Assets"))
            {
                rootFolder = "Assets/" + rootFolder.TrimStart('/');
            }

            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                CreateFolderRecursive(rootFolder);
            }

            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                Debug.LogWarning($"[Rock Generator] Could not validate or create custom target path: {rootFolder}. Forcing fallback to default path.");
                rootFolder = defaultPath;
                CreateFolderRecursive(rootFolder);
            }

            if (settings.saveFolderPath != rootFolder)
            {
                settings.saveFolderPath = rootFolder;
                EditorUtility.SetDirty(settings);
            }

            string safeName = string.IsNullOrWhiteSpace(settings.exportName) ? "Rock" : settings.exportName;
            safeName = string.Join("_", safeName.Split(System.IO.Path.GetInvalidFileNameChars()));

            string folderName = $"{settings.rockType}_{safeName}";

            // PHASE 2: Forward slash replacement on GenerateUniqueAssetPath string builders
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(rootFolder, folderName).Replace('\\', '/'));

            string parentFolder = System.IO.Path.GetDirectoryName(uniquePath).Replace('\\', '/');
            string newFolderName = System.IO.Path.GetFileName(uniquePath);

            string guid = AssetDatabase.CreateFolder(parentFolder, newFolderName);
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        public static void CreateFolderRecursive(string path)
        {
            string[] folders = path.Replace('\\', '/').Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }

        internal static Material CreateDefaultPBRMaterial()
        {
            Material material = null;

            RockRenderPipelineKind pipelineKind = GetActiveRenderPipelineKind();

            if (pipelineKind == RockRenderPipelineKind.HDRP)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");

                if (hdrpLit != null)
                {
                    material = new Material(hdrpLit);

                    SetColorIfPresent(material, "_BaseColor", Color.white);
                    SetColorIfPresent(material, "_Color", Color.white);

                    SetFloatIfPresent(material, "_MaterialID", 1.0f);
                    SetFloatIfPresent(material, "_SurfaceType", 0.0f);
                    SetFloatIfPresent(material, "_BlendMode", 0.0f);
                    SetFloatIfPresent(material, "_ZWrite", 1.0f);
                    SetFloatIfPresent(material, "_CullMode", 2.0f);
                    SetFloatIfPresent(material, "_CullModeForward", 2.0f);

                    SetFloatIfPresent(material, "_Metallic", 0.0f);
                    SetFloatIfPresent(material, "_Smoothness", 0.05f);

                    SetFloatIfPresent(material, "_NormalMapSpace", 0.0f);
                    SetFloatIfPresent(material, "_NormalScale", 1.0f);

                    SetFloatIfPresent(material, "_AlphaCutoffEnable", 0.0f);
                    SetFloatIfPresent(material, "_SupportDecals", 1.0f);

                    SetFloatIfPresent(material, "_MetallicRemapMin", 0.0f);
                    SetFloatIfPresent(material, "_MetallicRemapMax", 1.0f);
                    SetFloatIfPresent(material, "_AORemapMin", 0.0f);
                    SetFloatIfPresent(material, "_AORemapMax", 1.0f);
                    SetFloatIfPresent(material, "_SmoothnessRemapMin", 0.0f);
                    SetFloatIfPresent(material, "_SmoothnessRemapMax", 1.0f);
                }
            }

            if (material == null)
            {
                Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

                if (urpLitShader != null)
                {
                    material = new Material(urpLitShader);
                    material.EnableKeyword("_SURFACE_TYPE_OPAQUE");

                    SetFloatIfPresent(material, "_WorkflowMode", 1.0f);
                    SetColorIfPresent(material, "_BaseColor", Color.white);
                    SetColorIfPresent(material, "_Color", Color.white);
                    SetFloatIfPresent(material, "_Metallic", 0.0f);
                    SetFloatIfPresent(material, "_Smoothness", 0.05f);
                    SetFloatIfPresent(material, "_SmoothnessTextureChannel", 0.0f);
                }
                else
                {
                    Shader standardShader = Shader.Find("Standard");

                    if (standardShader == null)
                    {
                        Debug.LogWarning("[Rock Generator Lite] Legacy 'Standard' shader not found. Falling back to InternalErrorShader to prevent crash.");
                        standardShader = Shader.Find("Hidden/InternalErrorShader");
                    }

                    if (standardShader != null)
                    {
                        material = new Material(standardShader);
                        SetColorIfPresent(material, "_Color", Color.white);
                        SetFloatIfPresent(material, "_Mode", 0.0f);
                    }
                }
            }

            if (material != null)
            {
                SetFloatIfPresent(material, "_Smoothness", 0.05f);
                SetFloatIfPresent(material, "_Glossiness", 0.05f);
                SetFloatIfPresent(material, "_OcclusionStrength", 0.5f);

                material.renderQueue = -1;
                material.enableInstancing = true;
            }

            return material;
        }

        public static void ApplyTexturesToMaterial(Material mat, Texture2D albedo, Texture2D normal, bool useNormal)
        {
            if (mat == null) return;

            bool isHDRP = IsMaterialUsingHDRP(mat);

            if (isHDRP)
            {
                SetTextureIfPresent(mat, "_BaseColorMap", albedo);
                SetTextureIfPresent(mat, "_MainTex", albedo);

                SetColorIfPresent(mat, "_BaseColor", Color.white);
                SetColorIfPresent(mat, "_Color", Color.white);

                if (useNormal && normal != null)
                {
                    SetTextureIfPresent(mat, "_NormalMap", normal);
                    SetTextureIfPresent(mat, "_BumpMap", normal);

                    SetFloatIfPresent(mat, "_NormalMapSpace", 0.0f);
                    SetFloatIfPresent(mat, "_NormalScale", 1.0f);

                    mat.EnableKeyword("_NORMALMAP");
                    mat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                }
                else
                {
                    SetTextureIfPresent(mat, "_NormalMap", null);
                    SetTextureIfPresent(mat, "_BumpMap", null);

                    mat.DisableKeyword("_NORMALMAP");
                    mat.DisableKeyword("_NORMALMAP_TANGENT_SPACE");
                }

                return;
            }

            SetTextureIfPresent(mat, "_BaseMap", albedo);
            SetTextureIfPresent(mat, "_MainTex", albedo);

            SetColorIfPresent(mat, "_BaseColor", Color.white);
            SetColorIfPresent(mat, "_Color", Color.white);

            if (useNormal && normal != null)
            {
                SetTextureIfPresent(mat, "_BumpMap", normal);
                SetTextureIfPresent(mat, "_NormalMap", normal);

                SetFloatIfPresent(mat, "_BumpScale", 1.0f);
                SetFloatIfPresent(mat, "_NormalScale", 1.0f);

                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                SetTextureIfPresent(mat, "_BumpMap", null);
                SetTextureIfPresent(mat, "_NormalMap", null);

                mat.DisableKeyword("_NORMALMAP");
                mat.DisableKeyword("_NORMALMAP_TANGENT_SPACE");
            }
        }
        public static void ApplyAuxiliaryTexturesToMaterial(Material mat, RockSettings settings, Texture2D maskMap, Texture2D metallicMap, Texture2D aoMap, Texture2D smoothnessMap = null)
        {
            if (mat == null || settings == null) return;

            bool isHDRP = IsMaterialUsingHDRP(mat);
            bool isPacked = settings.textureExportMode == RockTextureExportMode.PackedMaskMap;

            if (mat.HasProperty("_OcclusionStrength"))
            {
                mat.SetFloat("_OcclusionStrength", settings.generateAO ? settings.aoStrength : 0.0f);
            }

            if (isHDRP)
            {
                bool hasHDRPMaskMap = maskMap != null;

                if (hasHDRPMaskMap)
                {
                    SetTextureIfPresent(mat, "_MaskMap", maskMap);

                    mat.EnableKeyword("_MASKMAP");

                    SetFloatIfPresent(mat, "_Metallic", 1.0f);
                    SetFloatIfPresent(mat, "_Smoothness", 1.0f);

                    SetFloatIfPresent(mat, "_MetallicRemapMin", 0.0f);
                    SetFloatIfPresent(mat, "_MetallicRemapMax", 1.0f);
                    SetFloatIfPresent(mat, "_AORemapMin", 0.0f);
                    SetFloatIfPresent(mat, "_AORemapMax", 1.0f);
                    SetFloatIfPresent(mat, "_SmoothnessRemapMin", 0.0f);
                    SetFloatIfPresent(mat, "_SmoothnessRemapMax", 1.0f);
                }
                else
                {
                    SetTextureIfPresent(mat, "_MaskMap", null);

                    mat.DisableKeyword("_MASKMAP");

                    SetFloatIfPresent(mat, "_Metallic", 0.0f);
                    SetFloatIfPresent(mat, "_Smoothness", settings.baseSmoothness);
                }

                mat.DisableKeyword("_METALLICGLOSSMAP");
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

                return;
            }

            bool hasMaterialMap = false;

            if (isPacked && maskMap != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", maskMap);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                if (mat.HasProperty("_OcclusionMap"))
                {
                    mat.SetTexture("_OcclusionMap", maskMap);
                }

                hasMaterialMap = true;
            }
            else if (!isPacked && metallicMap != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", metallicMap);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                hasMaterialMap = true;

                if (aoMap != null && mat.HasProperty("_OcclusionMap"))
                {
                    mat.SetTexture("_OcclusionMap", aoMap);
                }
            }
            else
            {
                if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
                if (mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", null);

                mat.DisableKeyword("_METALLICGLOSSMAP");
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_MASKMAP");

                if (aoMap != null && mat.HasProperty("_OcclusionMap"))
                {
                    mat.SetTexture("_OcclusionMap", aoMap);
                }
                else if (mat.HasProperty("_OcclusionMap"))
                {
                    mat.SetTexture("_OcclusionMap", null);
                }
            }

            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

            if (mat.HasProperty("_SmoothnessTextureChannel"))
            {
                mat.SetFloat("_SmoothnessTextureChannel", 0.0f);
            }

            if (hasMaterialMap)
            {
                SetFloatIfPresent(mat, "_Metallic", 1.0f);
                SetFloatIfPresent(mat, "_Smoothness", 1.0f);
                SetFloatIfPresent(mat, "_GlossMapScale", 1.0f);
                SetFloatIfPresent(mat, "_Glossiness", 1.0f);
            }
            else
            {
                SetFloatIfPresent(mat, "_Metallic", 0.0f);
                SetFloatIfPresent(mat, "_Smoothness", settings.baseSmoothness);
                SetFloatIfPresent(mat, "_GlossMapScale", 1.0f);
                SetFloatIfPresent(mat, "_Glossiness", settings.baseSmoothness);
            }
        }

        public static void ApplyHeightTextureToMaterial(Material mat, Texture2D heightMap)
        {
            if (mat == null) return;

            bool isHDRP = IsMaterialUsingHDRP(mat);

            if (heightMap == null)
            {
                SetTextureIfPresent(mat, "_HeightMap", null);
                SetTextureIfPresent(mat, "_ParallaxMap", null);

                mat.DisableKeyword("_PARALLAXMAP");

                return;
            }

            if (isHDRP)
            {
                SetTextureIfPresent(mat, "_HeightMap", heightMap);

                SetFloatIfPresent(mat, "_HeightCenter", 0.5f);
                SetFloatIfPresent(mat, "_HeightAmplitude", 0.02f);

                return;
            }

            if (mat.HasProperty("_ParallaxMap"))
            {
                mat.SetTexture("_ParallaxMap", heightMap);
                mat.EnableKeyword("_PARALLAXMAP");
            }

            SetFloatIfPresent(mat, "_Parallax", 0.02f);
        }

        internal static Texture2D CreateHDRPMaskMapFromAuxiliaryTextures(
    RockSettings settings,
    Texture2D existingMaskMap,
    Texture2D metallicMap,
    Texture2D aoMap,
    Texture2D smoothnessMap,
    string textureName)
        {
            if (settings == null)
            {
                return null;
            }

            if (existingMaskMap != null)
            {
                return null;
            }

            bool hasMetallicStyle = settings.metallicStyle != RockMetallicStyle.None;

            bool needsHDRPMaskMap =
                (hasMetallicStyle && (metallicMap != null || smoothnessMap != null)) ||
                (settings.generateAO && aoMap != null);

            if (!needsHDRPMaskMap)
            {
                return null;
            }

            GetMaskMapDimensions(metallicMap, aoMap, smoothnessMap, out int width, out int height);

            Color32[] metallicPixels = TryGetPixels32(metallicMap);
            Color32[] aoPixels = TryGetPixels32(aoMap);
            Color32[] smoothnessPixels = TryGetPixels32(smoothnessMap);

            Texture2D hdrpMask = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            hdrpMask.name = string.IsNullOrWhiteSpace(textureName) ? "Rock_HDRP_MaskMap" : textureName;
            hdrpMask.wrapMode = TextureWrapMode.Repeat;
            hdrpMask.filterMode = FilterMode.Bilinear;
            hdrpMask.anisoLevel = 4;

            Color32[] output = new Color32[width * height];

            byte fallbackSmoothness = Float01ToByte(settings.baseSmoothness);

            int metallicWidth = metallicMap != null ? metallicMap.width : 0;
            int metallicHeight = metallicMap != null ? metallicMap.height : 0;

            int aoWidth = aoMap != null ? aoMap.width : 0;
            int aoHeight = aoMap != null ? aoMap.height : 0;

            int smoothWidth = smoothnessMap != null ? smoothnessMap.width : 0;
            int smoothHeight = smoothnessMap != null ? smoothnessMap.height : 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte metallic = 0;
                    byte ao = 255;
                    byte smoothness = fallbackSmoothness;

                    if (hasMetallicStyle && metallicPixels != null)
                    {
                        metallic = SampleTextureChannel(
                            metallicPixels,
                            metallicWidth,
                            metallicHeight,
                            x,
                            y,
                            width,
                            height,
                            0,
                            fallback: 0
                        );
                    }

                    if (settings.generateAO && aoPixels != null)
                    {
                        ao = SampleTextureChannel(
                            aoPixels,
                            aoWidth,
                            aoHeight,
                            x,
                            y,
                            width,
                            height,
                            0,
                            fallback: 255
                        );
                    }

                    if (hasMetallicStyle)
                    {
                        if (settings.generateSmoothness && smoothnessPixels != null)
                        {
                            smoothness = SampleTextureChannel(
                                smoothnessPixels,
                                smoothWidth,
                                smoothHeight,
                                x,
                                y,
                                width,
                                height,
                                0,
                                fallbackSmoothness
                            );
                        }
                        else if (metallicPixels != null)
                        {
                            smoothness = SampleTextureChannel(
                                metallicPixels,
                                metallicWidth,
                                metallicHeight,
                                x,
                                y,
                                width,
                                height,
                                3,
                                fallbackSmoothness
                            );
                        }
                    }

                    output[y * width + x] = new Color32(
                        metallic,
                        ao,
                        128,
                        smoothness
                    );
                }
            }

            hdrpMask.SetPixels32(output);
            hdrpMask.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            return hdrpMask;
        }

        private static void GetMaskMapDimensions(Texture2D metallicMap, Texture2D aoMap, Texture2D smoothnessMap, out int width, out int height)
        {
            Texture2D source = metallicMap != null ? metallicMap : (aoMap != null ? aoMap : smoothnessMap);

            if (source != null)
            {
                width = Mathf.Clamp(source.width, 32, 4096);
                height = Mathf.Clamp(source.height, 32, 4096);
                return;
            }

            width = 32;
            height = 32;
        }

        private static Color32[] TryGetPixels32(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            try
            {
                return texture.GetPixels32();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rock Generator Lite] Could not read texture pixels for HDRP mask-map packing: {texture.name}. {e.Message}");
                return null;
            }
        }

        private static byte SampleTextureChannel(
            Color32[] pixels,
            int sourceWidth,
            int sourceHeight,
            int targetX,
            int targetY,
            int targetWidth,
            int targetHeight,
            int channel,
            byte fallback)
        {
            if (pixels == null || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return fallback;
            }

            int sourceX = targetWidth <= 1
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt((targetX / Mathf.Max(1f, targetWidth - 1f)) * (sourceWidth - 1)), 0, sourceWidth - 1);

            int sourceY = targetHeight <= 1
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt((targetY / Mathf.Max(1f, targetHeight - 1f)) * (sourceHeight - 1)), 0, sourceHeight - 1);

            int index = sourceY * sourceWidth + sourceX;

            if (index < 0 || index >= pixels.Length)
            {
                return fallback;
            }

            Color32 c = pixels[index];

            switch (channel)
            {
                case 0: return c.r;
                case 1: return c.g;
                case 2: return c.b;
                case 3: return c.a;
                default: return fallback;
            }
        }

        private static byte Float01ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value) * 255f), 0, 255);
        }
        #endregion

        public static bool ShouldApplyBakedNormal(RockSettings settings)
        {
            if (settings == null) return false;
            if (settings.colorizationMethod == RockColorizationMethod.VertexColors) return false;
            if (settings.normalMapStrength <= 0.0001f) return false;

            bool hasProceduralBump =
                settings.useNormalPerturbation &&
                settings.normalNoiseStrength > 0.0001f;

            bool hasMicroDetail =
                settings.useMicroDetail &&
                settings.microDetailStrength > 0.0001f;

            bool hasInputNormal =
                settings.colorizationMethod == RockColorizationMethod.TriplanarInputBake &&
                settings.inputNormal != null;

            return hasProceduralBump || hasMicroDetail || hasInputNormal;
        }
        private static void ConfigureGeneratedTextureImporters(
    string albedoPath,
    string normalPath,
    string maskPath,
    string metallicPath,
    string aoPath,
    string heightPath,
    string smoothPath)
        {
            ConfigureGeneratedAlbedoImporter(albedoPath);
            ConfigureGeneratedNormalImporter(normalPath);

            // Mask and metallic maps store smoothness in alpha, so alpha must be preserved.
            ConfigureGeneratedLinearDataImporter(maskPath, preserveAlpha: true);
            ConfigureGeneratedLinearDataImporter(metallicPath, preserveAlpha: true);

            ConfigureGeneratedLinearDataImporter(aoPath, preserveAlpha: false);
            ConfigureGeneratedLinearDataImporter(heightPath, preserveAlpha: false);
            ConfigureGeneratedLinearDataImporter(smoothPath, preserveAlpha: false);
        }

        private static void ConfigureGeneratedAlbedoImporter(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = false;

            importer.SaveAndReimport();
        }

        private static void ConfigureGeneratedNormalImporter(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;

            importer.SaveAndReimport();

            TextureImporter verifyImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (verifyImporter == null || verifyImporter.textureType != TextureImporterType.NormalMap)
            {
                Debug.LogWarning($"[Rock Generator Lite] Generated normal map was not imported as a Normal Map: {path}");
            }
        }

        private static void ConfigureGeneratedLinearDataImporter(string path, bool preserveAlpha)
        {
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaSource = preserveAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;

            importer.SaveAndReimport();
        }
    }
}
#endif
