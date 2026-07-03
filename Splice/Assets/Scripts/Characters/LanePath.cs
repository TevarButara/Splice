using UnityEngine;

namespace Splice.Characters
{
    // A fixed, map-authored route for one lane (architecture 5.6/5.8): monsters follow these waypoints
    // in order from spawn to the fort. Movement is on rails — towers do NOT reroute it — so paths are
    // deterministic and never snag on geometry the way NavMesh agents can. Miners still use NavMesh (free roam).
    public class LanePath : MonoBehaviour
    {
        [SerializeField] private int laneId;
        [Tooltip("จุดทางเดินเรียงลำดับจากจุดเกิด → ฐาน/Fort. วาง Fort ให้อยู่ในระยะโจมตีของ waypoint สุดท้าย")]
        [SerializeField] private Transform[] waypoints;

        public int LaneId => laneId;
        public int Count => waypoints != null ? waypoints.Length : 0;

        public Vector3 GetPoint(int index) => waypoints[index].position;
        public Vector3 Start => Count > 0 ? waypoints[0].position : transform.position;
    }
}
