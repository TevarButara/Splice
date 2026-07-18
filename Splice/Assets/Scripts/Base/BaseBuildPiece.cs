using Unity.Netcode;
using UnityEngine;

namespace Splice.Base
{
    public enum BuildPieceKind { Tower, Garrison }

    // preview runtime ของ 1 ชิ้นในผังเมือง (ป้อม หรือ มอนเฝ้า garrison) ในโหมดจัดผัง (Build Mode, architecture 5.10).
    // ใช้ prefab จริงเพื่อเห็นหน้าตา/ขนาดตรงกับตอน raid แต่ปิด network/combat (โหมดนี้ offline).
    // ถือ data ที่จะ save + ราคา (Cost) + สถานะจ่ายเงิน (Paid) + ขนาด footprint (dynamic ต่อชนิด).
    [DisallowMultipleComponent]
    public class BaseBuildPiece : MonoBehaviour
    {
        public BuildPieceKind Kind { get; private set; }
        public float Range { get; private set; }      // ไว้ preview วงระยะ
        public int Cost { get; private set; }          // ราคาซื้อ — ใช้คิด checkout/refund
        public bool Paid { get; set; }                 // true = จ่ายแล้ว (committed) / false = draft
        public float Footprint { get; private set; }   // ขนาดที่กินบน grid (world units) — จาก SO
        public int CapacityCost { get; private set; }  // กิน DefenseCapacity เท่าไหร่ (เพดานฝ่ายรับ) — จาก SO
        public PlacedTowerData TowerData { get; private set; }
        public GarrisonMonsterData GarrisonData { get; private set; }

        private Camera faceCamera;

        // ให้ตัวหันหน้าเข้ากล้องเสมอ (Y-only) — set โดย BaseBuildManager
        public void SetFaceCamera(Camera cam) => faceCamera = cam;

        private void Update()
        {
            if (faceCamera == null) faceCamera = Camera.main;
            if (faceCamera == null) return;

            var dir = faceCamera.transform.position - transform.position;
            dir.y = 0f;
            // กล้องมองดิ่ง (dir แนวตั้ง) → หันเข้าหาผู้เล่น = ทาง "ล่างจอ" (−up ของกล้องบนพื้น)
            if (dir.sqrMagnitude < 0.0001f) { dir = -faceCamera.transform.up; dir.y = 0f; }
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        public void SetupTower(PlacedTowerData data, float range, int cost, float footprint, int capacityCost)
        {
            Kind = BuildPieceKind.Tower;
            TowerData = data;
            Range = range;
            Cost = cost;
            Footprint = Mathf.Max(0.01f, footprint);
            CapacityCost = Mathf.Max(0, capacityCost);
            DisableRuntimeComponents();
        }

        public void SetupGarrison(GarrisonMonsterData data, float range, int cost, float footprint, int capacityCost)
        {
            Kind = BuildPieceKind.Garrison;
            GarrisonData = data;
            Range = range;
            Cost = cost;
            Footprint = Mathf.Max(0.01f, footprint);
            CapacityCost = Mathf.Max(0, capacityCost);
            DisableRuntimeComponents();
        }

        public void MoveTo(Vector3 position)
        {
            transform.position = position;
            if (TowerData != null) TowerData.position = position;
            if (GarrisonData != null) GarrisonData.position = position;
        }

        private void DisableRuntimeComponents()
        {
            foreach (var behaviour in GetComponentsInChildren<NetworkBehaviour>(true))
                behaviour.enabled = false;
            foreach (var netObj in GetComponentsInChildren<NetworkObject>(true))
                netObj.enabled = false;
        }
    }
}
