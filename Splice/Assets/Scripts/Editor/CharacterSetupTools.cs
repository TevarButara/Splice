#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Splice.EditorTools
{
    // Mobile-friendly setup helpers for character assets. Menu commands so you can batch-select and apply.
    //  - "Apply Mobile Import Settings" : run on selected model (.fbx) assets in the Project window.
    //  - "Retune LOD Thresholds"        : run on a selected scene/prefab GameObject that has a LODGroup
    //                                     (e.g. after AutomaticLOD generated the LODs).
    public static class CharacterSetupTools
    {
        // Screen-relative transition heights per LOD (0 = fills screen). Tuned for small units in a fairly
        // fixed camera — LOD0 close, then drop fast, cull when tiny/far. Adjust to your camera if needed.
        private static readonly float[] LodThresholds = { 0.35f, 0.14f, 0.05f };
        private const float CullBelow = 0.015f;

        // ---------------------------------------------------------------- Import settings

        [MenuItem("Splice/Characters/Apply Mobile Import Settings")]
        private static void ApplyMobileImportSettings()
        {
            var changed = 0;
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;

                // Mesh compression (Unity already optimizes vertex/index order on import by default).
                importer.meshCompression = ModelImporterMeshCompression.Medium;

                // Normals: recompute smoothly so low-poly / LOD meshes don't look faceted (pair with a baked
                // normal map for detail). Smoothing 60° keeps hard edges hard, curves smooth.
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                importer.normalSmoothingAngle = 60f;

                // Mixamo characters are humanoid.
                importer.animationType = ModelImporterAnimationType.Human;

                // Keep bones in the hierarchy for now: weapon-attach-by-bone + MeshSimplify need them.
                // Flip to true + expose only the hand bones once art is final for a perf win.
                importer.optimizeGameObjects = false;

                // NOTE: isReadable is intentionally NOT forced here — MeshSimplify needs it ON while you author
                // LODs; turn it OFF per model before the final build to save runtime memory.

                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log($"[Splice] Applied mobile import settings to {changed} model(s).");
        }

        [MenuItem("Splice/Characters/Apply Mobile Import Settings", true)]
        private static bool ApplyMobileImportSettings_Validate()
        {
            foreach (var obj in Selection.objects)
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(obj)) is ModelImporter) return true;
            return false;
        }

        // ---------------------------------------------------------------- LOD Group thresholds

        [MenuItem("Splice/Characters/Retune LOD Thresholds (mobile)")]
        private static void RetuneLodThresholds()
        {
            var go = Selection.activeGameObject;
            var lodGroup = go != null ? go.GetComponent<LODGroup>() : null;
            if (lodGroup == null)
            {
                Debug.LogWarning("[Splice] Select a GameObject that has a LODGroup (generate LODs first, e.g. AutomaticLOD).");
                return;
            }

            Undo.RecordObject(lodGroup, "Retune LOD Thresholds");

            var lods = lodGroup.GetLODs();
            for (var i = 0; i < lods.Length; i++)
            {
                // Last LOD uses the cull threshold; the rest use the tuned bands (fall back to cull if we run out).
                var last = i == lods.Length - 1;
                lods[i].screenRelativeTransitionHeight = last
                    ? CullBelow
                    : (i < LodThresholds.Length ? LodThresholds[i] : CullBelow);
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);
            Debug.Log($"[Splice] Retuned {lods.Length} LOD level(s) on '{go.name}' for mobile.");
        }
    }
}
#endif
