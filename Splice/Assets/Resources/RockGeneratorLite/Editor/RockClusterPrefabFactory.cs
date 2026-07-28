#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veridian.RockGenLite.Runtime;
using Object = UnityEngine.Object;

namespace Veridian.RockGenLite.Editor
{
    /// <summary>
    /// Persists the exact generated preview hierarchy. Unique rock meshes are saved
    /// individually while all rocks share one material/texture set.
    /// </summary>
    public static class RockClusterPrefabFactory
    {
        private const string SurfacePreviewName = "__RockCluster_SurfacePreview";

        public static void SavePreviewAsPrefab(
            RockSettings rockSettings,
            RockClusterSettings clusterSettings,
            GameObject previewRoot)
        {
            if (rockSettings == null || clusterSettings == null || previewRoot == null)
            {
                EditorUtility.DisplayDialog("Rock Cluster", "Generate a valid cluster preview before exporting.", "OK");
                return;
            }

            string directory = CreateClusterFolder(rockSettings);
            string baseName = Path.GetFileName(directory);
            GameObject exportRoot = null;
            var transientObjects = new List<Object>();

            try
            {
                EditorUtility.DisplayProgressBar("Rock Cluster", "Copying preview geometry...", 0.1f);
                exportRoot = Object.Instantiate(previewRoot);
                exportRoot.name = baseName;
                ResetHideFlags(exportRoot);

                Transform surfacePreview = exportRoot.transform.Find(SurfacePreviewName);
                if (surfacePreview != null && !clusterSettings.includeSurfaceInExport)
                {
                    Object.DestroyImmediate(surfacePreview.gameObject);
                }

                RockClusterGroup group = exportRoot.GetComponent<RockClusterGroup>();
                if (group == null) group = exportRoot.AddComponent<RockClusterGroup>();
                group.Configure(clusterSettings.seed, clusterSettings.count, clusterSettings.shape);

                List<MeshFilter> rockFilters = GetRockMeshFilters(exportRoot);
                if (rockFilters.Count == 0 || rockFilters[0].sharedMesh == null)
                {
                    throw new InvalidOperationException("The preview contains no generated rock meshes.");
                }

                EditorUtility.DisplayProgressBar("Rock Cluster", "Saving shared material...", 0.25f);
                Material material = CreateAndSaveSharedMaterial(
                    rockSettings,
                    rockFilters[0].sharedMesh,
                    directory,
                    baseName,
                    transientObjects);

                EditorUtility.DisplayProgressBar("Rock Cluster", "Saving unique meshes...", 0.65f);
                PersistRockMeshes(exportRoot, directory, baseName);

                MeshRenderer[] renderers = exportRoot.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (IsRockObject(renderers[i].transform))
                    {
                        renderers[i].sharedMaterial = material;
                    }
                }

                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(directory, baseName + ".prefab").Replace('\\', '/'));
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(exportRoot, prefabPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    throw new InvalidOperationException($"Unity could not save the prefab at '{prefabPath}'.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorGUIUtility.PingObject(savedPrefab);
                RockPrefabFactory.SetLastGeneratedPath(directory);
                Debug.Log($"<color=green>Success!</color> Rock cluster prefab generated to: <b>{prefabPath}</b>");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Rock Generator Lite] Cluster export failed: {exception.Message}\n{exception.StackTrace}");
                EditorUtility.DisplayDialog("Rock Cluster Export Failed", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (exportRoot != null) Object.DestroyImmediate(exportRoot);
                for (int i = 0; i < transientObjects.Count; i++)
                {
                    if (transientObjects[i] != null && !EditorUtility.IsPersistent(transientObjects[i]))
                    {
                        Object.DestroyImmediate(transientObjects[i]);
                    }
                }
            }
        }

        public static string SurfaceObjectName => SurfacePreviewName;

        private static List<MeshFilter> GetRockMeshFilters(GameObject root)
        {
            var result = new List<MeshFilter>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (IsRockObject(filters[i].transform))
                {
                    result.Add(filters[i]);
                }
            }
            return result;
        }

        private static bool IsRockObject(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.StartsWith("Rock_", StringComparison.Ordinal))
                {
                    return true;
                }
                if (current.name == SurfacePreviewName)
                {
                    return false;
                }
                current = current.parent;
            }
            return false;
        }

        private static void PersistRockMeshes(GameObject root, string directory, string baseName)
        {
            var savedMeshes = new Dictionary<Mesh, Mesh>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            int meshIndex = 0;

            for (int i = 0; i < filters.Length; i++)
            {
                if (!IsRockObject(filters[i].transform) || filters[i].sharedMesh == null) continue;
                filters[i].sharedMesh = PersistMesh(
                    filters[i].sharedMesh,
                    savedMeshes,
                    directory,
                    $"{baseName}_Mesh_{meshIndex++:000}");
            }

            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!IsRockObject(colliders[i].transform) || colliders[i].sharedMesh == null) continue;
                colliders[i].sharedMesh = PersistMesh(
                    colliders[i].sharedMesh,
                    savedMeshes,
                    directory,
                    $"{baseName}_Collider_{meshIndex++:000}");
            }
        }

        private static Mesh PersistMesh(
            Mesh source,
            Dictionary<Mesh, Mesh> savedMeshes,
            string directory,
            string assetName)
        {
            if (savedMeshes.TryGetValue(source, out Mesh existing))
            {
                return existing;
            }

            Mesh clone = Object.Instantiate(source);
            clone.name = assetName;
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(directory, assetName + ".asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(clone, path);
            savedMeshes.Add(source, clone);
            return clone;
        }

        private static Material CreateAndSaveSharedMaterial(
            RockSettings settings,
            Mesh sourceMesh,
            string directory,
            string baseName,
            List<Object> transientObjects)
        {
            if (settings.colorizationMethod == RockColorizationMethod.VertexColors)
            {
                Material vertexMaterial = RockPrefabFactory.CreateVertexColorMaterial();
                vertexMaterial.name = baseName + "_Mat";
                AssetDatabase.CreateAsset(
                    vertexMaterial,
                    Path.Combine(directory, vertexMaterial.name + ".mat").Replace('\\', '/'));
                return vertexMaterial;
            }

            RockTextureBaker.BakeTextures(
                sourceMesh,
                settings,
                Mathf.Max(128, settings.textureResolution),
                out Texture2D albedo,
                out Texture2D normal,
                out Texture2D mask,
                out Texture2D metallic,
                out Texture2D ao,
                out Texture2D height,
                out Texture2D smoothness);

            Track(transientObjects, albedo, normal, mask, metallic, ao, height, smoothness);
            if (albedo == null || normal == null)
            {
                throw new InvalidOperationException("Texture baking did not produce the required Albedo and Normal maps.");
            }

            if (RockPrefabFactory.IsCurrentRenderPipelineHDRP() && mask == null)
            {
                mask = RockPrefabFactory.CreateHDRPMaskMapFromAuxiliaryTextures(
                    settings, null, metallic, ao, smoothness, baseName + "_HDRP_MaskMap");
                Track(transientObjects, mask);
            }

            bool packed = settings.textureExportMode == RockTextureExportMode.PackedMaskMap;
            string albedoPath = SaveTexture(albedo, directory, baseName + "_Albedo", true, false);
            string normalPath = SaveTexture(normal, directory, baseName + "_Normal", false, true);
            string maskPath = mask != null ? SaveTexture(mask, directory, baseName + "_MaskMap", false, false) : null;
            string metallicPath = !packed && metallic != null ? SaveTexture(metallic, directory, baseName + "_Metallic", false, false) : null;
            string aoPath = !packed && ao != null ? SaveTexture(ao, directory, baseName + "_AO", false, false) : null;
            string heightPath = height != null ? SaveTexture(height, directory, baseName + "_Height", false, false) : null;
            string smoothnessPath = !packed && smoothness != null ? SaveTexture(smoothness, directory, baseName + "_Smoothness", false, false) : null;

            Material material = RockPrefabFactory.CreateDefaultPBRMaterial();
            material.name = baseName + "_Mat";
            RockPrefabFactory.ApplyTexturesToMaterial(
                material,
                AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                RockPrefabFactory.ShouldApplyBakedNormal(settings));
            RockPrefabFactory.ApplyAuxiliaryTexturesToMaterial(
                material,
                settings,
                LoadTexture(maskPath),
                LoadTexture(metallicPath),
                LoadTexture(aoPath),
                LoadTexture(smoothnessPath));
            RockPrefabFactory.ApplyHeightTextureToMaterial(material, LoadTexture(heightPath));

            AssetDatabase.CreateAsset(
                material,
                Path.Combine(directory, material.name + ".mat").Replace('\\', '/'));
            return material;
        }

        private static string SaveTexture(
            Texture2D texture,
            string directory,
            string name,
            bool sRgb,
            bool normalMap)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(directory, name + ".png").Replace('\\', '/'));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = sRgb;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            return path;
        }

        private static Texture2D LoadTexture(string path)
        {
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Track(List<Object> objects, params Object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && !objects.Contains(values[i]))
                {
                    objects.Add(values[i]);
                }
            }
        }

        private static string CreateClusterFolder(RockSettings settings)
        {
            string rootFolder = string.IsNullOrWhiteSpace(settings.saveFolderPath)
                ? "Assets/VeridianData/RockGenerator/Rocks"
                : settings.saveFolderPath.Replace('\\', '/');
            if (!rootFolder.StartsWith("Assets", StringComparison.Ordinal))
            {
                rootFolder = "Assets/" + rootFolder.TrimStart('/');
            }
            RockPrefabFactory.CreateFolderRecursive(rootFolder);

            string rawName = string.IsNullOrWhiteSpace(settings.exportName)
                ? "Rock_Cluster"
                : settings.exportName + "_Cluster";
            string safeName = string.Join("_", rawName.Split(Path.GetInvalidFileNameChars()));
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(rootFolder, safeName).Replace('\\', '/'));
            string parent = Path.GetDirectoryName(uniquePath)?.Replace('\\', '/');
            string folder = Path.GetFileName(uniquePath);
            string guid = AssetDatabase.CreateFolder(parent, folder);
            string result = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(result))
            {
                throw new IOException($"Could not create the cluster output folder '{uniquePath}'.");
            }
            return result;
        }

        private static void ResetHideFlags(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = HideFlags.None;
                Component[] components = transforms[i].GetComponents<Component>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    if (components[componentIndex] != null)
                    {
                        components[componentIndex].hideFlags = HideFlags.None;
                    }
                }
            }
        }
    }
}
#endif
