#if UNITY_EDITOR
using System.IO;
using Splice.Placement;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Placement
{
    public static class GroundedPrefabAuthoringEditor
    {
        public const string CreateMenuPath =
            "Assets/Splice/Create Grounded Wrapper From Selected Prefab";

        [MenuItem(CreateMenuPath, priority = 2100)]
        private static void CreateFromSelection()
        {
            var source = Selection.activeObject as GameObject;
            if (source == null || PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.NotAPrefab)
            {
                EditorUtility.DisplayDialog("Splice Grounded Prefab",
                    "Select one prefab asset in the Project window.", "OK");
                return;
            }
            if (source.GetComponent<GroundPlacementProfile>() != null)
            {
                EditorUtility.DisplayDialog("Splice Grounded Prefab",
                    $"'{source.name}' already has a GroundPlacementProfile.", "OK");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(source);
            var folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "Assets";
            var existing = FindGroundedWrapperForSource(source, folder);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("Splice Grounded Prefab",
                    $"'{source.name}' already uses wrapper '{existing.name}'.\n\n" +
                    "The existing wrapper was selected instead of creating a duplicate.", "OK");
                return;
            }
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{source.name}_Placeable.prefab");
            GameObject created;
            if (source.GetComponent<Unity.Netcode.NetworkObject>() != null)
            {
                var authoredFootprint = MeasurePrefabWorldFootprint(source);
                created = RebuildGroundedGameplayPrefab(source, path, authoredFootprint);
            }
            else
                created = CreateGroundedWrapper(source, path, source.name + "_Placeable",
                    source.transform.localScale, source.transform.localRotation);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        [MenuItem(CreateMenuPath, true)]
        private static bool CanCreateFromSelection()
        {
            var source = Selection.activeObject as GameObject;
            return source != null &&
                   PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.NotAPrefab;
        }

        public static GameObject FindGroundedWrapperForSource(GameObject source, string folder)
        {
            if (source == null || string.IsNullOrWhiteSpace(folder)) return null;
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(sourcePath)) return null;
            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (candidatePath == sourcePath) continue;
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
                var profile = candidate != null
                    ? candidate.GetComponent<GroundPlacementProfile>()
                    : null;
                if (profile == null || profile.VisualRoot == null) continue;

                if (!string.IsNullOrWhiteSpace(profile.SourceAssetGuid))
                {
                    if (profile.SourceAssetGuid == sourceGuid) return candidate;
                    continue;
                }

                // Legacy wrappers may not have explicit source metadata yet. Only inspect direct
                // dependencies; recursive traversal follows TowerDefinition.nextTier and can
                // falsely match a wrapper from a different level.
                foreach (var dependency in AssetDatabase.GetDependencies(candidatePath, false))
                    if (dependency == sourcePath) return candidate;
            }
            return null;
        }

        public static GameObject EnsureGroundedWrapper(GameObject source, string assetPath,
            string wrapperName, Vector3 visualScale, Quaternion visualRotation,
            bool normalizeExisting = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                var profile = existing.GetComponent<GroundPlacementProfile>();
                if (profile == null || !profile.IsComplete)
                    throw new MissingReferenceException(
                        $"Grounded wrapper '{assetPath}' exists but its placement anchors are incomplete.");
                EnsureSourceAssetGuid(assetPath, AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(source)));
                if (normalizeExisting)
                    NormalizeExistingWrapper(assetPath, visualScale, visualRotation);
                // Existing wrappers are designer-owned. Bake/Ensure must not overwrite the
                // VisualRoot scale, rotation or pivot that was tuned in Prefab Mode.
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            return CreateGroundedWrapper(source, assetPath, wrapperName, visualScale, visualRotation);
        }

        public static GameObject FitGroundedWrapperToWorldFootprint(string assetPath,
            float targetWorldFootprint)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new System.ArgumentException("Prefab asset path is required.", nameof(assetPath));
            if (targetWorldFootprint <= .01f)
                throw new System.ArgumentOutOfRangeException(nameof(targetWorldFootprint),
                    "Target footprint must be greater than 0.01 world units.");

            var wrapper = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var profile = wrapper.GetComponent<GroundPlacementProfile>();
                if (profile == null || !profile.IsComplete)
                    throw new MissingReferenceException(
                        $"'{assetPath}' is not a complete grounded wrapper.");
                if (!TryRendererBounds(profile.VisualRoot, out var before))
                    throw new MissingReferenceException(
                        $"'{assetPath}' has no Renderer under VisualRoot.");
                FitProfileToWorldFootprint(profile, targetWorldFootprint, before);
                EditorUtility.SetDirty(profile);
                var saved = PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
                if (saved == null)
                    throw new System.InvalidOperationException(
                        $"Unity could not save resized wrapper '{assetPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(wrapper);
            }
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        public static GameObject RebuildGroundedGameplayPrefab(GameObject source,
            string assetPath, float targetWorldFootprint,
            bool replaceNetworkPrefabReferences = true)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new System.ArgumentException("Source must be a prefab asset.", nameof(source));
            if (source.GetComponent<Unity.Netcode.NetworkObject>() == null)
                throw new MissingComponentException(
                    $"Gameplay prefab '{source.name}' needs NetworkObject on its source root.");

            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                // Character prefabs are commonly authored as a prefab/model instance root with
                // NetworkObject + NetworkBehaviour overrides. Its children cannot be reparented
                // until that outermost instance is unpacked. This only affects the new _Placeable
                // asset; the selected source prefab remains untouched.
                if (PrefabUtility.IsAnyPrefabInstanceRoot(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction);

                var rootTransform = root.transform;
                var oldPosition = rootTransform.localPosition;
                var oldRotation = rootTransform.localRotation;
                var oldScale = rootTransform.localScale;
                var children = new Transform[rootTransform.childCount];
                for (var index = 0; index < children.Length; index++)
                    children[index] = rootTransform.GetChild(index);

                var rootFilter = root.GetComponent<MeshFilter>();
                var rootRenderer = root.GetComponent<MeshRenderer>();
                if ((rootFilter == null) != (rootRenderer == null))
                    throw new MissingComponentException(
                        $"Gameplay prefab '{source.name}' has only one of MeshFilter/MeshRenderer " +
                        "on its root. Keep the pair together before canonical conversion.");

                rootTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                rootTransform.localScale = Vector3.one;
                root.name = Path.GetFileNameWithoutExtension(assetPath);

                var visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(rootTransform, false);
                visualRoot.localPosition = oldPosition;
                visualRoot.localRotation = oldRotation;
                visualRoot.localScale = oldScale;
                if (rootFilter != null)
                {
                    // Static meshes such as towers keep their root mesh, while skinned characters
                    // usually have all renderers below the root and only need their children moved.
                    var visualFilter = visualRoot.gameObject.AddComponent<MeshFilter>();
                    EditorUtility.CopySerialized(rootFilter, visualFilter);
                    var visualRenderer = visualRoot.gameObject.AddComponent<MeshRenderer>();
                    EditorUtility.CopySerialized(rootRenderer, visualRenderer);
                    Object.DestroyImmediate(rootRenderer);
                    Object.DestroyImmediate(rootFilter);
                }
                foreach (var child in children) child.SetParent(visualRoot, false);

                var groundAnchor = CreateAnchor("GroundAnchor", rootTransform, Vector3.zero);
                var cameraFocus = CreateAnchor("CameraFocus", rootTransform, Vector3.zero);
                var effectAnchor = CreateAnchor("EffectAnchor", rootTransform, Vector3.zero);
                var profile = root.GetComponent<GroundPlacementProfile>() ??
                              root.AddComponent<GroundPlacementProfile>();
                profile.ConfigureEditorReferences(
                    visualRoot, groundAnchor, cameraFocus, effectAnchor,
                    AssetDatabase.AssetPathToGUID(sourcePath));
                if (!TryRendererBounds(visualRoot, out var bounds))
                    throw new MissingReferenceException(
                        $"Gameplay prefab '{source.name}' has no Renderer bounds.");
                FitProfileToWorldFootprint(profile, targetWorldFootprint, bounds);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                if (saved == null)
                    throw new System.InvalidOperationException(
                        $"Unity could not save gameplay wrapper '{assetPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (replaceNetworkPrefabReferences)
                ReplaceNetworkPrefabReferences(sourcePath, assetPath, result);
            return result;
        }

        public static int EnsureGroundLayer()
        {
            var layer = LayerMask.NameToLayer(GroundPlacementUtility.GroundLayerName);
            if (layer >= 0) return layer;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var index = 8; index < layers.arraySize; index++)
            {
                var slot = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrWhiteSpace(slot.stringValue)) continue;
                slot.stringValue = GroundPlacementUtility.GroundLayerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return index;
            }
            throw new System.InvalidOperationException("No free Unity layer is available for 'Ground'.");
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            for (var index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        private static GameObject CreateGroundedWrapper(GameObject source, string assetPath,
            string wrapperName, Vector3 visualScale, Quaternion visualRotation)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            var wrapper = new GameObject(wrapperName);
            try
            {
                wrapper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                wrapper.transform.localScale = Vector3.one;

                var visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(wrapper.transform, false);
                visualRoot.localRotation = visualRotation;
                visualRoot.localScale = visualScale;
                var visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (visual == null) throw new MissingReferenceException(
                    $"Could not instantiate source prefab '{source.name}'.");
                visual.name = source.name;
                visual.transform.SetParent(visualRoot, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                if (!TryRendererBounds(visualRoot, out var sourceBounds))
                    throw new MissingReferenceException(
                        $"Source prefab '{source.name}' has no Renderer to ground.");
                visualRoot.position += new Vector3(
                    -sourceBounds.center.x, -sourceBounds.min.y, -sourceBounds.center.z);
                if (!TryRendererBounds(visualRoot, out var normalizedBounds))
                    throw new MissingReferenceException(
                        $"Could not calculate normalized bounds for '{source.name}'.");

                var groundAnchor = CreateAnchor("GroundAnchor", wrapper.transform, Vector3.zero);
                var cameraFocus = CreateAnchor("CameraFocus", wrapper.transform,
                    new Vector3(0f, normalizedBounds.size.y * .5f, 0f));
                var effectAnchor = CreateAnchor("EffectAnchor", wrapper.transform,
                    new Vector3(0f, normalizedBounds.size.y * .6f, 0f));
                var profile = wrapper.AddComponent<GroundPlacementProfile>();
                profile.ConfigureEditorReferences(
                    visualRoot, groundAnchor, cameraFocus, effectAnchor,
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)));

                var saved = PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
                if (saved == null) throw new System.InvalidOperationException(
                    $"Unity could not save grounded wrapper '{assetPath}'.");
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
            }
        }

        private static void NormalizeExistingWrapper(string assetPath, Vector3 visualScale,
            Quaternion visualRotation)
        {
            var wrapper = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var profile = wrapper.GetComponent<GroundPlacementProfile>();
                if (profile == null || !profile.IsComplete)
                    throw new MissingReferenceException(
                        $"Grounded wrapper '{assetPath}' has incomplete placement anchors.");
                profile.VisualRoot.localPosition = Vector3.zero;
                profile.VisualRoot.localRotation = visualRotation;
                profile.VisualRoot.localScale = visualScale;
                NormalizeVisualAndAnchors(profile);
                PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(wrapper);
            }
        }

        private static void EnsureSourceAssetGuid(string assetPath, string sourceGuid)
        {
            if (string.IsNullOrWhiteSpace(sourceGuid)) return;
            var wrapper = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var profile = wrapper.GetComponent<GroundPlacementProfile>();
                if (profile == null || profile.SourceAssetGuid == sourceGuid) return;
                profile.ConfigureEditorReferences(profile.VisualRoot, profile.GroundAnchor,
                    profile.CameraFocus, profile.EffectAnchor, sourceGuid);
                EditorUtility.SetDirty(profile);
                PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(wrapper);
            }
        }

        private static void NormalizeVisualAndAnchors(GroundPlacementProfile profile)
        {
            if (!TryRendererBounds(profile.VisualRoot, out var bounds))
                throw new MissingReferenceException(
                    $"Grounded wrapper '{profile.name}' has no Renderer under VisualRoot.");
            profile.VisualRoot.position += new Vector3(
                -bounds.center.x, -bounds.min.y, -bounds.center.z);
            if (!TryRendererBounds(profile.VisualRoot, out var normalizedBounds))
                throw new MissingReferenceException(
                    $"Could not normalize grounded wrapper '{profile.name}'.");
            profile.GroundAnchor.localPosition = Vector3.zero;
            profile.CameraFocus.localPosition =
                new Vector3(0f, normalizedBounds.size.y * .5f, 0f);
            profile.EffectAnchor.localPosition =
                new Vector3(0f, normalizedBounds.size.y * .6f, 0f);
        }

        private static void FitProfileToWorldFootprint(GroundPlacementProfile profile,
            float targetWorldFootprint, Bounds before)
        {
            var currentFootprint = Mathf.Max(before.size.x, before.size.z);
            if (currentFootprint <= .0001f)
                throw new System.InvalidOperationException(
                    $"'{profile.name}' has a zero-sized renderer footprint.");
            profile.VisualRoot.localScale *= targetWorldFootprint / currentFootprint;
            NormalizeVisualAndAnchors(profile);
        }

        private static void ReplaceNetworkPrefabReferences(string sourcePath,
            string targetPath, GameObject replacement)
        {
            if (replacement == null) return;
            foreach (var guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var list = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>(path);
                if (list == null) continue;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (!ReconcileNetworkPrefabsList(list, source, replacement)) continue;
                EditorUtility.SetDirty(list);
            }
            AssetDatabase.SaveAssets();
        }

        public static bool ReconcileNetworkPrefabsList(
            Unity.Netcode.NetworkPrefabsList list,
            GameObject source,
            GameObject replacement)
        {
            if (list == null || replacement == null) return false;

            var entries = new System.Collections.Generic.List<Unity.Netcode.NetworkPrefab>(
                list.PrefabList);
            var changed = false;
            var keptDirectReplacement = false;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.Override == Unity.Netcode.NetworkPrefabOverride.None &&
                    (entry.Prefab == source || entry.Prefab == replacement))
                {
                    if (!keptDirectReplacement)
                    {
                        keptDirectReplacement = true;
                        if (entry.Prefab != replacement)
                        {
                            entry.Prefab = replacement;
                            changed = true;
                        }
                    }
                    else
                    {
                        list.Remove(entry);
                        changed = true;
                    }
                    continue;
                }

                if (entry.Prefab == source)
                {
                    entry.Prefab = replacement;
                    changed = true;
                }
                if (entry.SourcePrefabToOverride == source)
                {
                    entry.SourcePrefabToOverride = replacement;
                    changed = true;
                }
                if (entry.OverridingTargetPrefab == source)
                {
                    entry.OverridingTargetPrefab = replacement;
                    changed = true;
                }
            }
            return changed;
        }

        private static float MeasurePrefabWorldFootprint(GameObject prefab)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!TryRendererBounds(root.transform, out var bounds))
                    throw new MissingReferenceException(
                        $"Gameplay prefab '{prefab.name}' has no Renderer bounds.");
                return Mathf.Max(bounds.size.x, bounds.size.z);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform CreateAnchor(string name, Transform parent, Vector3 localPosition)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            value.localPosition = localPosition;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
            return value;
        }

        private static bool TryRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
    }
}
#endif
