using UnityEngine;

namespace Splice.Scenes
{
    public enum RaidPresentationMode
    {
        Attacker = 0,
        Defender = 1,
    }

    public static class RaidPresentationCameraContract
    {
        // Mirrors only the horizontal offset between the authored attacker camera and attacker spawn.
        // Height, pitch, orthographic scale and all CameraPan settings remain identical to MonCamera.
        public static void CalculateMirroredPose(Vector3 attackerCameraPosition, Vector3 attackerCameraEuler,
            Vector3 attackerAnchor, Vector3 defenderAnchor, out Vector3 defenderPosition,
            out Vector3 defenderEuler)
        {
            var horizontalOffset = attackerCameraPosition - attackerAnchor;
            var heightAboveAnchor = horizontalOffset.y;
            horizontalOffset.y = 0f;
            defenderPosition = defenderAnchor - horizontalOffset;
            defenderPosition.y = defenderAnchor.y + heightAboveAnchor;
            defenderEuler = new Vector3(attackerCameraEuler.x,
                Mathf.Repeat(attackerCameraEuler.y + 180f, 360f), attackerCameraEuler.z);
        }
    }
}
