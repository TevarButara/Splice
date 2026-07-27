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

        public Transform VisualRoot => visualRoot;
        public Transform GroundAnchor => groundAnchor;
        public Transform CameraFocus => cameraFocus;
        public Transform EffectAnchor => effectAnchor;
        public string SourceAssetGuid => sourceAssetGuid;
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
