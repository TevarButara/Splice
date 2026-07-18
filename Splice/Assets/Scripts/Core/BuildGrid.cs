using UnityEngine;

namespace Splice.Core
{
    // กติกา grid วางป้อม (architecture 5.8/5.10) — snap ตำแหน่งลงกลางช่อง + ยืนยันว่าช่องอยู่เหนือ build zone.
    // แชร์ระหว่าง "วางป้อมสด" (TowerDeploymentManager, ฝั่ง Defender ในแมตช์) กับ "จัดผังฐาน" (BaseBuildManager,
    // Build Mode นอกแมตช์) เพื่อให้กติกา "ช่องไหนวางได้" เป็นอันเดียวกันทั้งสองที่.
    // เรื่อง occupancy (ช่องนี้มีป้อมอยู่แล้วไหม) แยกให้ผู้เรียกเช็คเอง เพราะบริบทต่างกัน —
    // ในแมตช์เทียบกับ network towers ที่ยังไม่ตาย, ใน Build Mode เทียบกับ preview list.
    [System.Serializable]
    public class BuildGrid
    {
        [Tooltip("ขนาด 1 ช่อง (world units) = footprint ของป้อม/มอน 1 ตัว — **ตั้งครั้งเดียวให้ตรงอาร์ต ไม่ต้องจูนตาม floor**. " +
                 "ขยายพื้นที่เมือง = ขยาย floor แล้วจำนวนช่องเพิ่มเอง (floor÷cellSize)")]
        public float cellSize = 2f;
        [Tooltip("จุดอ้างอิงกริด — ขยับให้แนวช่องตรงกับ build zone/เลน")]
        public Vector3 gridOrigin = Vector3.zero;
        [Tooltip("layer ของพื้นที่วางได้ (build zone) — ศูนย์กลางช่องต้องอยู่เหนือ collider นี้ถึงวางได้")]
        public LayerMask buildLayerMask = ~0;

        [Tooltip("ครึ่งขนาดพื้นที่วาง (จำนวนช่องจากจุดกลางไปแต่ละด้าน) — สี่เหลี่ยม (2×ค่านี้+1)² ช่อง. " +
                 "**ถ้าตั้ง floor ใน BaseBuildManager ค่านี้ auto จาก floor÷cellSize** (ไม่ต้องกรอกเอง). 0 = ไม่จำกัด")]
        public int halfExtentCells = 4;

        private const float RayUp = 100f; // ความสูงที่ยิง ray ลงมายืนยันว่าช่องอยู่เหนือ build zone

        public Vector2Int CellIndex(Vector3 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt((world.x - gridOrigin.x) / cellSize),
                Mathf.RoundToInt((world.z - gridOrigin.z) / cellSize));
        }

        // Snap XZ ลงกลางช่อง (y คงค่าเดิมไว้ก่อน — ความสูงจริงของพื้นมาจาก TryGetGroundCell)
        public Vector3 SnapToCell(Vector3 world)
        {
            var idx = CellIndex(world);
            return new Vector3(idx.x * cellSize + gridOrigin.x, world.y, idx.y * cellSize + gridOrigin.z);
        }

        public bool SameCell(Vector3 a, Vector3 b) => CellIndex(a) == CellIndex(b);

        // อยู่ในสี่เหลี่ยมพื้นที่เมืองไหม (|cellIndex| ≤ halfExtentCells). 0 = ไม่จำกัด
        public bool IsWithinBounds(Vector3 world)
        {
            if (halfExtentCells <= 0) return true;
            var idx = CellIndex(world);
            return Mathf.Abs(idx.x) <= halfExtentCells && Mathf.Abs(idx.y) <= halfExtentCells;
        }

        // snap แล้วยิง ray ลงยืนยันว่าช่องอยู่เหนือ build zone; สำเร็จ → `cell.y` = ผิวพื้น (ยังไม่เช็ค occupancy)
        public bool TryGetGroundCell(Vector3 world, out Vector3 cell)
        {
            cell = SnapToCell(world);
            var probe = new Vector3(cell.x, cell.y + RayUp, cell.z);
            var overGround = Physics.Raycast(probe, Vector3.down, out var hit, RayUp * 2f, buildLayerMask);

            if (halfExtentCells > 0)
            {
                // โหมดกรอบสี่เหลี่ยม (Build Mode): พื้นที่วางได้ = กรอบเมือง; collider แค่ให้ "ความสูงพื้น"
                // ไม่มี collider ก็วางได้ (ใช้ y ของจุดที่แตะ) — กันเคส mask/collider ไม่ได้ตั้งแล้ววางไม่ลง
                if (!IsWithinBounds(cell)) return false;
                cell.y = overGround ? hit.point.y : world.y;
                return true;
            }

            // โหมดไม่จำกัดกรอบ (halfExtentCells = 0): ต้องอยู่เหนือ build zone collider จริง (พฤติกรรมเดิม)
            if (!overGround) return false;
            cell.y = hit.point.y;
            return true;
        }
    }
}
