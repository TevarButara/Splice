#if UNITY_EDITOR
using Splice.Placement;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Placement
{
    [CustomEditor(typeof(GroundPlacementProfile))]
    public sealed class GroundPlacementProfileEditor : UnityEditor.Editor
    {
        private const string TargetFootprintPreference =
            "Splice.GroundedPrefab.TargetWorldFootprint";
        private float targetWorldFootprint;

        private void OnEnable()
        {
            targetWorldFootprint = EditorPrefs.GetFloat(TargetFootprintPreference, 45f);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var profile = (GroundPlacementProfile)target;
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("World Size Authoring", EditorStyles.boldLabel);
            if (profile.TryGetRendererBounds(out var bounds))
                EditorGUILayout.HelpBox(
                    $"Current renderer size: {bounds.size.x:0.##} × {bounds.size.y:0.##} × " +
                    $"{bounds.size.z:0.##} world units\n" +
                    "Root remains scale 1; only VisualRoot is resized.",
                    MessageType.Info);
            else
                EditorGUILayout.HelpBox("No Renderer was found under VisualRoot.", MessageType.Warning);

            targetWorldFootprint = Mathf.Max(.01f, EditorGUILayout.FloatField(
                "Target XZ Footprint", targetWorldFootprint));
            EditorPrefs.SetFloat(TargetFootprintPreference, targetWorldFootprint);

            using (new EditorGUI.DisabledScope(!profile.IsComplete))
            {
                if (!GUILayout.Button("Fit VisualRoot To Target Footprint")) return;
                var assetPath = AssetDatabase.GetAssetPath(profile.gameObject);
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        profile.gameObject);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    EditorUtility.DisplayDialog("Splice Grounded Prefab",
                        "Open or select the prefab asset before resizing it.", "OK");
                    return;
                }

                var fitted = GroundedPrefabAuthoringEditor
                    .FitGroundedWrapperToWorldFootprint(assetPath, targetWorldFootprint);
                Selection.activeObject = fitted;
                EditorGUIUtility.PingObject(fitted);
            }
        }
    }
}
#endif
