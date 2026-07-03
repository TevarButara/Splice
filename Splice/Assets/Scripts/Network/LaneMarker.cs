using UnityEngine;

namespace Splice.Network
{
    // Tag on each lane's ground collider so DeployInputController can raycast-pick a lane by tap/click.
    public class LaneMarker : MonoBehaviour
    {
        [SerializeField] private int laneId;

        public int LaneId => laneId;
    }
}
