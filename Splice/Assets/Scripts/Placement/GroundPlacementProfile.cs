using UnityEngine;

namespace Splice.Placement
{
    /// <summary>
    /// Canonical placement contract stored on a reusable prefab root.
    /// The root and GroundAnchor stay at local zero; art-specific pivot correction belongs under VisualRoot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundPlacementProfile : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform groundAnchor;
        [SerializeField] private Transform cameraFocus;
        [SerializeField] private Transform effectAnchor;
        [SerializeField, HideInInspector] private string sourceAssetGuid;
        [SerializeField, HideInInspector, Min(0.0001f)]
        private float authoredUniformVisualScale = 1f;

        public Transform VisualRoot => visualRoot;
        public Transform GroundAnchor => groundAnchor;
        public Transform CameraFocus => cameraFocus;
        public Transform EffectAnchor => effectAnchor;
        public string SourceAssetGuid => sourceAssetGuid;
        public float AuthoredUniformVisualScale =>
            Mathf.Max(0.0001f, authoredUniformVisualScale);
        public float UniformVisualScaleFactor =>
            Mathf.Clamp(CurrentUniformVisualScale / AuthoredUniformVisualScale,
                0.05f, 20f);
        public bool IsComplete =>
            visualRoot != null && groundAnchor != null && cameraFocus != null && effectAnchor != null;

        public void ConfigureEditorReferences(Transform valueVisualRoot, Transform valueGroundAnchor,
            Transform valueCameraFocus, Transform valueEffectAnchor,
            string valueSourceAssetGuid = null)
        {
            visualRoot = valueVisualRoot;
            groundAnchor = valueGroundAnchor;
            cameraFocus = valueCameraFocus;
            effectAnchor = valueEffectAnchor;
            if (!string.IsNullOrWhiteSpace(valueSourceAssetGuid))
                sourceAssetGuid = valueSourceAssetGuid;
        }

        public void ConfigureEditorScaleReference()
        {
            authoredUniformVisualScale = CurrentUniformVisualScale;
        }

        public void ConfigureEditorScaleReference(float authoredScale)
        {
            authoredUniformVisualScale = Mathf.Max(0.0001f,
                Mathf.Abs(authoredScale));
        }

        public static float ResolveScaleFactor(Transform root)
        {
            if (root == null) return 1f;
            var profile = root.GetComponent<GroundPlacementProfile>();
            return profile != null ? profile.UniformVisualScaleFactor : 1f;
        }

        private float CurrentUniformVisualScale
        {
            get
            {
                var scale = visualRoot != null
                    ? visualRoot.lossyScale
                    : transform.lossyScale;
                return Mathf.Max(0.0001f,
                    Mathf.Max(Mathf.Abs(scale.x),
                        Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            }
        }

        public bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            var renderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>(true);
            var found = false;
            foreach (var renderer in renderers)
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

        private void OnDrawGizmosSelected()
        {
            if (groundAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(groundAnchor.position, .35f);
                Gizmos.DrawLine(groundAnchor.position - Vector3.right,
                    groundAnchor.position + Vector3.right);
                Gizmos.DrawLine(groundAnchor.position - Vector3.forward,
                    groundAnchor.position + Vector3.forward);
            }
            if (cameraFocus != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(cameraFocus.position, .3f);
            }
            if (effectAnchor != null)
            {
                Gizmos.color = new Color(1f, .55f, .1f);
                Gizmos.DrawWireSphere(effectAnchor.position, .25f);
            }
        }
    }
}
