using UnityEngine;

namespace Splice.Input
{
    // Per-lane invader building (กระท่อมสร้างทหาร) — one placed per lane in the world. Tapping it opens
    // the deploy card UI bound to this lane. Just a laneId marker; SoldierHutInputController does the tap
    // raycast (same split as LaneMarker vs DeployInputController).
    public class SoldierHut : MonoBehaviour
    {
        [Tooltip("ต้องตรงกับ laneId ของ LanePath/LaneMarker เลนเดียวกัน")]
        [SerializeField] private int laneId;

        public int LaneId => laneId;
    }
}
