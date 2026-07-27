using UnityEngine;

namespace Splice.Placement
{
    public static class GroundPlacementUtility
    {
        public const string GroundLayerName = "Ground";
        public const float DefaultRayHeight = 5000f;
        public const float DefaultRayDistance = 10000f;
        public const float GroundTolerance = .05f;

        public static bool TryFindGround(Vector3 desiredPosition, LayerMask groundMask,
            out RaycastHit hit)
        {
            hit = default;
            if (groundMask.value == 0) return false;
            var origin = new Vector3(desiredPosition.x,
                Mathf.Max(desiredPosition.y + DefaultRayHeight, DefaultRayHeight),
                desiredPosition.z);
            return Physics.Raycast(origin, Vector3.down, out hit, DefaultRayDistance,
                groundMask, QueryTriggerInteraction.Ignore);
        }

        public static bool TrySnapMarkerToGround(Transform marker, LayerMask groundMask,
            out RaycastHit hit)
        {
            hit = default;
            if (marker == null || !TryFindGround(marker.position, groundMask, out hit)) return false;
            marker.position = hit.point;
            return true;
        }

        public static bool TryPlaceOnGround(GameObject instance, Vector3 desiredPosition,
            LayerMask groundMask, out RaycastHit hit)
        {
            hit = default;
            if (instance == null || !TryFindGround(desiredPosition, groundMask, out hit)) return false;
            var profile = instance.GetComponent<GroundPlacementProfile>();
            if (profile == null || !profile.IsComplete) return false;

            instance.transform.position = hit.point;
            var anchorOffset = profile.GroundAnchor.position - instance.transform.position;
            instance.transform.position = hit.point - anchorOffset;
            return true;
        }

        public static bool IsGrounded(GroundPlacementProfile profile, float surfaceY,
            float tolerance = GroundTolerance)
        {
            if (profile == null || !profile.IsComplete ||
                !profile.TryGetRendererBounds(out var bounds)) return false;
            return Mathf.Abs(bounds.min.y - surfaceY) <= Mathf.Max(.001f, tolerance);
        }
    }
}
