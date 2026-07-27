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
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{source.name}_Placeable.prefab");
            var created = CreateGroundedWrapper(source, path, source.name + "_Placeable",
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

        public static GameObject EnsureGroundedWrapper(GameObject source, string assetPath,
            string wrapperName, Vector3 visualScale, Quaternion visualRotation)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                var profile = existing.GetComponent<GroundPlacementProfile>();
                if (profile == null || !profile.IsComplete)
                    throw new MissingReferenceException(
                        $"Grounded wrapper '{assetPath}' exists but its placement anchors are incomplete.");
                NormalizeExistingWrapper(assetPath, visualScale, visualRotation);
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            return CreateGroundedWrapper(source, assetPath, wrapperName, visualScale, visualRotation);
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
                profile.ConfigureEditorReferences(visualRoot, groundAnchor, cameraFocus, effectAnchor);

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
                if (!TryRendererBounds(profile.VisualRoot, out var bounds))
                    throw new MissingReferenceException(
                        $"Grounded wrapper '{assetPath}' has no Renderer under VisualRoot.");
                profile.VisualRoot.position += new Vector3(
                    -bounds.center.x, -bounds.min.y, -bounds.center.z);
                if (!TryRendererBounds(profile.VisualRoot, out var normalizedBounds))
                    throw new MissingReferenceException(
                        $"Could not normalize grounded wrapper '{assetPath}'.");
                profile.GroundAnchor.localPosition = Vector3.zero;
                profile.CameraFocus.localPosition =
                    new Vector3(0f, normalizedBounds.size.y * .5f, 0f);
                profile.EffectAnchor.localPosition =
                    new Vector3(0f, normalizedBounds.size.y * .6f, 0f);
                PrefabUtility.SaveAsPrefabAsset(wrapper, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(wrapper);
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
