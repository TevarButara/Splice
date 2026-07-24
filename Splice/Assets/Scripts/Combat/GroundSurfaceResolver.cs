using Splice.Characters;
using UnityEngine;

namespace Splice.Combat
{
    // Shared terrain contract for all ground units and ground-targeted FX.
    // It prefers the project's Ground layer (ThickPlane) and ignores character/trigger colliders.
    public static class GroundSurfaceResolver
    {
        private const float DefaultCastHeight = 64f;
        private const float DefaultCastDistance = 192f;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        public static bool TrySnap(
            Vector3 desiredPosition,
            Transform ignoreRoot,
            out Vector3 snappedPosition,
            float verticalOffset = 0f)
        {
            snappedPosition = desiredPosition;
            var groundLayer = LayerMask.NameToLayer("Ground");
            var mask = groundLayer >= 0 ? 1 << groundLayer : Physics.DefaultRaycastLayers;
            var origin = desiredPosition + Vector3.up * DefaultCastHeight;
            var count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                Hits,
                DefaultCastDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            var bestDistance = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var hitTransform = hit.collider != null ? hit.collider.transform : null;
                if (hitTransform == null || hit.normal.y <= 0.05f) continue;
                if (ignoreRoot != null &&
                    (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
                    continue;
                if (hitTransform.GetComponentInParent<CharacterBase>() != null) continue;
                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                snappedPosition.y = hit.point.y + Mathf.Max(0f, verticalOffset);
                found = true;
            }

            return found;
        }
    }
}
