using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Splice.Characters
{
    // A fixed, map-authored route for one lane (architecture 5.6/5.8): monsters follow these points
    // in order from spawn to the fort. Movement is on rails — towers do NOT reroute it — so paths are
    // deterministic and never snag on geometry the way NavMesh agents can. Miners still use NavMesh (free roam).
    //
    // 2 แหล่งที่มาของจุด (โค้ดที่ใช้ LanePath ไม่รู้ความต่าง — API เดิม Count/GetPoint/Start):
    //   1. Spline (แนะนำ) — ลากเส้น curve ในซีน แล้วคลาสนี้ sample เป็นจุดถี่ให้เอง → แมปกว้าง/หลายเลนทำเร็ว + เดินโค้งลื่น
    //   2. Waypoints (แบบเก่า) — วาง Transform เป็นจุดๆ. ใช้เมื่อไม่ได้ใส่ Spline
    public class LanePath : MonoBehaviour
    {
        [SerializeField] private int laneId;

        [Header("เส้นทาง — ใส่ Spline (แนะนำ) หรือ Waypoints อย่างใดอย่างหนึ่ง")]
        [Tooltip("เส้น curve ของเลนนี้ (ลากในซีน) — ใส่แล้วจะใช้อันนี้แทน Waypoints ด้านล่าง")]
        [SerializeField] private SplineContainer spline;
        [Tooltip("ระยะห่างจุดที่ sample จาก spline (world units) — เล็ก = เกาะโค้งแน่นขึ้น. " +
                 "⚠️ ต้อง 'มากกว่า' Waypoint Arrive Radius ของมอน ไม่งั้นมอนจะข้ามจุดรัวแล้วตัดโค้ง")]
        [SerializeField] private float splineSampleSpacing = 1f;
        [Tooltip("จุดทางเดินแบบเก่า (ใช้เมื่อไม่ได้ใส่ Spline) — เรียงจากจุดเกิด → ฐาน/Fort")]
        [SerializeField] private Transform[] waypoints;

        [Header("เกาะพื้น (ลากเส้นลอยกลางอากาศได้ — ระบบดึงลงให้)")]
        [Tooltip("ยิง ray ลงหาพื้น แล้วดึงจุดที่ sample มาแปะบนผิวพื้น → ลากเส้นสนใจแค่รูปทรงแนวราบ ไม่ต้องจิ้มความสูงทีละ knot. พื้นต้องมี Collider")]
        [SerializeField] private bool projectToGround = true;
        [Tooltip("เลเยอร์ของพื้น/terrain ที่ให้เกาะ")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("เริ่มยิง ray จากเหนือจุดเท่านี้ (กันจุดจมใต้พื้นแล้วยิงไม่โดน)")]
        [SerializeField] private float rayStartHeight = 50f;
        [Tooltip("ระยะ ray รวม (ต้องยาวพอถึงพื้น)")]
        [SerializeField] private float rayDistance = 200f;
        [Tooltip("ยกจุดเหนือผิวพื้นเล็กน้อย (กันจมพื้น)")]
        [SerializeField] private float groundOffset = 0f;

        private Vector3[] points;

        public int LaneId => laneId;

        public int Count
        {
            get { EnsurePoints(); return points.Length; }
        }

        public Vector3 GetPoint(int index)
        {
            EnsurePoints();
            return points[index];
        }

        public Vector3 Start => Count > 0 ? GetPoint(0) : transform.position;

        private void Awake() => Rebuild();

        // แก้ค่าใน inspector (เช่นลาก spline ที่ clone มาใส่) → บังคับ sample ใหม่ ให้ gizmo/ทางเดินอัปเดตทันที
        private void OnValidate() => points = null;

        private void EnsurePoints()
        {
            if (points == null) Rebuild();
        }

#if UNITY_EDITOR
        // ดึง "knot ของ spline เอง" ลงไปเกาะพื้น (ไม่ใช่แค่จุดที่ sample) → เส้นในซีนนั่งบนพื้นจริง แก้ต่อง่าย.
        // ใช้กับ spline ที่ clone มาแล้วลากใส่ได้เลย. เรียกจาก: คลิกขวาที่หัว component LanePath → Snap Spline to Ground
        [ContextMenu("Snap Spline to Ground")]
        private void SnapSplineToGround()
        {
            if (spline == null || spline.Spline == null || spline.Spline.Count == 0)
            {
                Debug.LogWarning("[LanePath] ยังไม่ได้ใส่ Spline (หรือเส้นว่าง) — ลาก SplineContainer เข้าช่อง Spline ก่อน", this);
                return;
            }

            UnityEditor.Undo.RecordObject(spline, "Snap Spline to Ground");

            var s = spline.Spline;
            var xf = spline.transform;
            var moved = 0;
            for (var i = 0; i < s.Count; i++)
            {
                var knot = s[i];
                var world = xf.TransformPoint((Vector3)knot.Position);
                var snapped = ProjectPoint(world);
                if (snapped == world) continue;          // ray ไม่โดนพื้น → ปล่อยไว้
                knot.Position = xf.InverseTransformPoint(snapped);
                s[i] = knot;
                moved++;
            }

            UnityEditor.EditorUtility.SetDirty(spline);
            points = null;                                // sample ใหม่
            Debug.Log($"[LanePath] snap knot ลงพื้นแล้ว {moved}/{s.Count} จุด" +
                      (moved < s.Count ? " (ที่เหลือ ray ไม่โดนพื้น — เช็ค Ground Mask / Collider / Ray Distance)" : ""), this);
        }
#endif

        // แปลง Spline / Waypoints → อาร์เรย์จุด (bake ครั้งเดียว — เส้นทางเป็นข้อมูลแมป ไม่ขยับตอนเล่น)
        public void Rebuild()
        {
            if (spline != null && spline.Spline != null && spline.Spline.Count >= 2)
            {
                points = SampleSpline();
                return;
            }

            var list = new List<Vector3>();
            if (waypoints != null)
            {
                foreach (var w in waypoints)
                    if (w != null) list.Add(ProjectPoint(w.position));
            }
            points = list.ToArray();
        }

        // sample ตาม spline (world space — SplineContainer คิด transform ให้แล้ว) ให้ได้จุดถี่ตาม spacing
        private Vector3[] SampleSpline()
        {
            var length = spline.CalculateLength();
            var spacing = Mathf.Max(0.05f, splineSampleSpacing);
            var count = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);

            var result = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var t = (float)i / (count - 1);
                result[i] = ProjectPoint((Vector3)spline.EvaluatePosition(t));
            }
            return result;
        }

        // ดึงจุดลงเกาะผิวพื้น (ยิง ray ลงจากเหนือจุด) — เก็บ XZ เดิม เอาแต่ความสูงจากพื้น.
        // ยิงไม่โดนพื้น (นอกแมป/ไม่มี collider) → คงความสูงเดิมไว้ ไม่พัง
        private Vector3 ProjectPoint(Vector3 p)
        {
            if (!projectToGround) return p;

            var origin = p + Vector3.up * rayStartHeight;
            if (Physics.Raycast(origin, Vector3.down, out var hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
                return new Vector3(p.x, hit.point.y + groundOffset, p.z);

            return p;
        }

        // เห็นเส้นทางในซีน (ทั้ง 2 แหล่ง) — ช่วยตรวจว่าเส้นลากถูกไหม
        private void OnDrawGizmosSelected()
        {
            Rebuild();   // edit mode: sample สดให้เห็นผลตอนลากเส้น
            if (points == null || points.Length < 2) return;

            Gizmos.color = new Color(0.2f, 0.9f, 1f);
            for (var i = 0; i < points.Length - 1; i++) Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }
}
