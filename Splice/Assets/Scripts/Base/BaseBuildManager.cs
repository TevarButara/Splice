using System.Collections.Generic;
using Splice.Core;
using Splice.Data;
using Splice.Input;
using UnityEngine;

namespace Splice.Base
{
    // โหมดจัดผังเมืองนอกแมตช์ (Build Mode, architecture 5.10) — offline ล้วน ไม่มี network.
    // วาง/ย้าย/ลบ ป้อม+มอนเฝ้า บนกริดที่ **ขนาดช่อง (footprint) มาจาก SO ของชิ้นที่กำลังวาง** (dynamic ต่อชนิด).
    // occupancy = AABB ตาม footprint (ชิ้นต่างขนาดกันไม่ทับกัน). พื้นที่เมือง = สี่เหลี่ยมตามขนาด floor (world units).
    //
    // เศรษฐกิจ (checkout, ขั้น 5.5): draft → NetCost → Checkout หัก meta gold (PlayerWallet) / Discard.
    public class BaseBuildManager : MonoBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [Tooltip("faction ของเมืองที่กำลังจัด — เว้นว่าง = ใช้ PlayerProfile.ActiveFactionId")]
        [SerializeField] private string factionId;
        [Tooltip("กติกา grid — ใช้ gridOrigin (จุดกลาง+ผิวเมือง) เป็นหลัก; cellSize เป็นแค่ค่า fallback ถ้าไม่มีชิ้น armed")]
        [SerializeField] private BuildGrid grid = new();
        [Tooltip("parent ของ preview ที่วาง (เว้นว่าง = ตัว manager นี้)")]
        [SerializeField] private Transform placedRoot;
        [Tooltip("พื้น bg ของเมือง (Collider ของ Plane) — จัด gridOrigin+ขนาดพื้นที่ให้ปูบนผิวแผ่นอัตโนมัติ")]
        [SerializeField] private Collider floor;
        [Tooltip("กล้อง (CameraPanController) — ให้ปุ่มเอียงหมุนรอบ object ล่าสุดที่วาง/เลือก. เว้นว่างได้")]
        [SerializeField] private CameraPanController cameraPan;
        [Tooltip("คืนเงินตอนลบชิ้นที่ 'จ่ายแล้ว' = floor(ราคา × ค่านี้). 0.5 = คืนครึ่ง")]
        [SerializeField] private float refundFactor = 0.5f;
        [Tooltip("ยกป้อม/มอนจากผิว floor (world units) — กันลอย. 0 = แปะพื้น")]
        [SerializeField] private float placeYOffset = 0f;

        [Header("Defense Capacity (เพดานฝ่ายรับ ผูกกับ base level ไม่ใช่เงิน — architecture §5.10)")]
        [Tooltip("capacity พื้นฐาน (base level 1)")]
        [SerializeField] private int baseCapacity = 10;
        [Tooltip("capacity ที่เพิ่มต่อ 1 base level")]
        [SerializeField] private int capacityPerLevel = 5;

        private readonly List<BaseBuildPiece> placed = new();
        private TowerDefinitionSO armedTower;
        private CardDefinitionSO armedGarrison;
        private BaseBuildPiece selected;
        private int pendingRefund;
        private bool hasUnsavedChanges;
        private Vector2 floorHalfExtent = new(-1f, -1f); // world half-size ของพื้นที่เมือง (set โดย FitGridToFloor)

        public BuildGrid Grid => grid;
        public FactionRegistrySO Registry => registry;
        public string EditingFactionId => CityFactionId;
        public bool HasArmed => armedTower != null || armedGarrison != null;
        public TowerDefinitionSO ArmedTower => armedTower;
        public CardDefinitionSO ArmedGarrison => armedGarrison;
        public BaseBuildPiece Selected => selected;
        public bool WantsPreview => HasArmed || selected != null;
        public bool HasUnsavedChanges => hasUnsavedChanges;
        public Vector3 GridOrigin => grid.gridOrigin;

        public float PreviewRange =>
            armedTower != null ? armedTower.attackRange
            : armedGarrison != null && armedGarrison.linkedMonster != null ? armedGarrison.linkedMonster.attackRange
            : selected != null ? selected.Range
            : 0f;

        // ขนาดช่อง (footprint) ของ "สิ่งที่กำลังวาง/เลือก" — dynamic จาก SO
        public float CurrentFootprint
        {
            get
            {
                if (armedTower != null) return Mathf.Max(0.01f, armedTower.footprint);
                if (armedGarrison != null && armedGarrison.linkedMonster != null)
                    return Mathf.Max(0.01f, armedGarrison.linkedMonster.footprint);
                if (selected != null) return selected.Footprint;
                return Mathf.Max(0.01f, grid.cellSize);
            }
        }

        // ---------- economy (คงเดิม) ----------

        public int NetCost
        {
            get
            {
                var buy = 0;
                foreach (var piece in placed)
                    if (piece != null && !piece.Paid) buy += piece.Cost;
                return buy - pendingRefund;
            }
        }

        public int WalletGold => PlayerWallet.MetaGold;
        public bool CanAfford(int additionalCost) => NetCost + additionalCost <= PlayerWallet.MetaGold;

        // ---------- defense capacity (เพดานฝ่ายรับ ผูกกับ base level ไม่ใช่เงิน — กัน defense snowball) ----------
        public int DefenseCapacity => baseCapacity + capacityPerLevel * Mathf.Max(0, PlayerProfile.BaseLevel(CityFactionId) - 1);
        public int UsedCapacity
        {
            get { var u = 0; foreach (var p in placed) if (p != null) u += p.CapacityCost; return u; }
        }
        public bool HasCapacityFor(int capCost) => UsedCapacity + capCost <= DefenseCapacity;

        private string CityFactionId => string.IsNullOrEmpty(factionId) ? PlayerProfile.ActiveFactionId : factionId;

        private void Start()
        {
            if (floor != null) FitGridToFloor();
            var layout = PlayerBaseStore.LoadLayout(CityFactionId);
            if (layout != null) LoadCommitted(layout);
            hasUnsavedChanges = false;
        }

        [ContextMenu("Fit Grid To Floor")]
        public void FitGridToFloor()
        {
            if (floor == null) { Debug.LogWarning("[BaseBuild] ไม่ได้ตั้ง floor (Collider ของ Plane พื้น)"); return; }
            var b = floor.bounds;
            grid.gridOrigin = new Vector3(b.center.x, b.max.y, b.center.z); // จุดกลาง + ผิวบน
            floorHalfExtent = new Vector2(b.extents.x, b.extents.z);        // ขนาดพื้นที่เมือง (world)
        }

        // ครึ่งขนาดพื้นที่เมือง (world) — จาก floor ถ้าตั้ง, ไม่งั้น fallback จาก grid
        public Vector2 BuildHalfExtent
        {
            get
            {
                if (floorHalfExtent.x >= 0f) return floorHalfExtent;
                var h = grid.halfExtentCells > 0 ? grid.halfExtentCells * grid.cellSize : 1000f;
                return new Vector2(h, h);
            }
        }

        // ---------- snapping / bounds / occupancy (footprint-based) ----------

        // snap จุดลงกริดขนาด footprint ปัจจุบัน (จุดกลาง = gridOrigin); y = ผิว floor + offset
        public Vector3 SnapPoint(Vector3 world)
        {
            var foot = CurrentFootprint;
            var sx = Mathf.Round((world.x - grid.gridOrigin.x) / foot) * foot + grid.gridOrigin.x;
            var sz = Mathf.Round((world.z - grid.gridOrigin.z) / foot) * foot + grid.gridOrigin.z;
            return new Vector3(sx, grid.gridOrigin.y + placeYOffset, sz);
        }

        // อยู่ในพื้นที่เมือง (สี่เหลี่ยมตาม floor) ไหม
        public bool IsWithinBuildArea(Vector3 cell, float foot)
        {
            var he = BuildHalfExtent;
            return Mathf.Abs(cell.x - grid.gridOrigin.x) <= he.x
                && Mathf.Abs(cell.z - grid.gridOrigin.z) <= he.y;
        }

        // ทับกับชิ้นที่วางแล้วไหม (AABB ตาม footprint สองชิ้น) — ไม่นับ ignore
        private bool Overlaps(Vector3 cell, float foot, BaseBuildPiece ignore)
        {
            const float eps = 0.01f;
            foreach (var piece in placed)
            {
                if (piece == null || piece == ignore) continue;
                var half = (foot + piece.Footprint) * 0.5f - eps;
                var p = piece.transform.position;
                if (Mathf.Abs(cell.x - p.x) < half && Mathf.Abs(cell.z - p.z) < half) return true;
            }
            return false;
        }

        // ช่องนี้วางได้ไหม (สำหรับ preview/overlay) — เลือกอยู่ก็ไม่นับตัวเอง
        public bool CanPlaceCell(Vector3 cell)
        {
            var foot = CurrentFootprint;
            var ignore = HasArmed ? null : selected;
            return IsWithinBuildArea(cell, foot) && !Overlaps(cell, foot, ignore);
        }

        // ---------- palette ----------

        public void ArmTower(TowerDefinitionSO tower) { armedTower = tower; armedGarrison = null; selected = null; }
        public void ArmGarrison(CardDefinitionSO monsterCard) { armedGarrison = monsterCard; armedTower = null; selected = null; }
        public void ClearArmed() { armedTower = null; armedGarrison = null; }
        public void CancelSelection() { armedTower = null; armedGarrison = null; selected = null; }

        // ---------- place / move / delete ----------

        public void OnGroundTapped(Vector3 world)
        {
            if (HasArmed) TryPlace(world);
            else if (selected != null) TryMoveSelected(world);
        }

        public bool TryPlace(Vector3 world)
        {
            var foot = CurrentFootprint;
            var cell = SnapPoint(world);
            if (!IsWithinBuildArea(cell, foot) || Overlaps(cell, foot, null)) return false;

            var ok = armedTower != null ? PlaceTower(armedTower, cell)
                : armedGarrison != null && PlaceGarrison(armedGarrison, cell);
            if (ok)
            {
                hasUnsavedChanges = true;
                if (cameraPan != null) cameraPan.SetFocusPoint(cell);
            }
            return ok;
        }

        private bool PlaceTower(TowerDefinitionSO tower, Vector3 cell)
        {
            if (tower.prefab == null) return false;
            if (!CanAfford(tower.goldCost)) return false;
            if (!HasCapacityFor(tower.defenseCapacityCost)) return false; // เต็มเพดานฝ่ายรับ
            var id = registry != null ? registry.IdOf(tower) : null;
            if (string.IsNullOrEmpty(id)) { Debug.LogWarning($"[BaseBuild] ป้อม '{tower.displayName}' ไม่อยู่ใน registry"); return false; }
            SpawnTowerPiece(tower, new PlacedTowerData { towerId = id, position = cell }, paid: false);
            return true;
        }

        private bool PlaceGarrison(CardDefinitionSO card, Vector3 cell)
        {
            if (card.linkedMonster == null || card.linkedMonster.prefab == null)
            { Debug.LogWarning($"[BaseBuild] การ์ด '{card.displayName}' ไม่มี linkedMonster/prefab"); return false; }
            if (!CanAfford(card.goldCost)) return false;
            if (!HasCapacityFor(card.linkedMonster.defenseCapacityCost)) return false; // เต็มเพดานฝ่ายรับ
            var id = registry != null ? registry.IdOf(card) : null;
            if (string.IsNullOrEmpty(id)) { Debug.LogWarning($"[BaseBuild] การ์ด '{card.displayName}' ไม่อยู่ใน registry"); return false; }
            SpawnGarrisonPiece(card.linkedMonster, card.goldCost, new GarrisonMonsterData { cardId = id, position = cell }, paid: false);
            return true;
        }

        public bool TryMoveSelected(Vector3 world)
        {
            if (selected == null) return false;
            var cell = SnapPoint(world);
            if (!IsWithinBuildArea(cell, selected.Footprint) || Overlaps(cell, selected.Footprint, selected)) return false;
            selected.MoveTo(cell); // ย้าย = ฟรี
            hasUnsavedChanges = true;
            if (cameraPan != null) cameraPan.SetFocusPoint(cell);
            return true;
        }

        public void Select(BaseBuildPiece piece)
        {
            selected = piece; armedTower = null; armedGarrison = null;
            if (cameraPan != null && piece != null) cameraPan.SetFocusPoint(piece.transform.position);
        }
        public void Deselect() => selected = null;

        public void DeleteSelected()
        {
            if (selected == null) return;
            AccrueRefund(selected);
            placed.Remove(selected);
            Destroy(selected.gameObject);
            selected = null;
            hasUnsavedChanges = true;
        }

        public void ClearAll()
        {
            var changed = placed.Count > 0;
            foreach (var piece in placed) { AccrueRefund(piece); if (piece != null) Destroy(piece.gameObject); }
            placed.Clear();
            selected = null;
            if (changed) hasUnsavedChanges = true;
        }

        private void AccrueRefund(BaseBuildPiece piece)
        {
            if (piece != null && piece.Paid) pendingRefund += Mathf.FloorToInt(piece.Cost * refundFactor);
        }

        // ---------- checkout / discard ----------

        public bool Checkout()
        {
            var fid = CityFactionId;
            if (string.IsNullOrEmpty(fid)) { Debug.LogWarning("[BaseBuild] ไม่มี faction — checkout ไม่ได้"); return false; }

            var net = NetCost;
            if (net > 0)
            {
                if (!PlayerWallet.TrySpend(net))
                { Debug.LogWarning($"[BaseBuild] ทองไม่พอ (ต้อง {net}, มี {PlayerWallet.MetaGold})"); return false; }
            }
            else if (net < 0) PlayerWallet.Add(-net);

            foreach (var piece in placed) if (piece != null) piece.Paid = true;
            pendingRefund = 0;
            PersistCommitted(fid);
            hasUnsavedChanges = false;
            Debug.Log($"[BaseBuild] checkout '{fid}': จ่ายสุทธิ {net}, ทองเหลือ {PlayerWallet.MetaGold}");
            return true;
        }

        public void Discard()
        {
            pendingRefund = 0;
            var layout = PlayerBaseStore.LoadLayout(CityFactionId);
            ClearPlaced();
            if (layout != null) LoadCommitted(layout);
            hasUnsavedChanges = false;
        }

        private void PersistCommitted(string fid)
        {
            var layout = PlayerBaseStore.LoadLayout(fid) ?? new BaseLayout();
            layout.factionId = fid;
            layout.ownerAccountId = PlayerProfile.AccountId;
            layout.towers.Clear();
            layout.garrison.Clear();
            foreach (var piece in placed)
            {
                if (piece == null) continue;
                if (piece.Kind == BuildPieceKind.Tower && piece.TowerData != null) layout.towers.Add(piece.TowerData);
                else if (piece.Kind == BuildPieceKind.Garrison && piece.GarrisonData != null) layout.garrison.Add(piece.GarrisonData);
            }
            PlayerBaseStore.SaveLayout(layout);
        }

        // ---------- load / spawn ----------

        private void LoadCommitted(BaseLayout layout)
        {
            foreach (var data in layout.towers)
            {
                var def = registry != null ? registry.ResolveTower(data.towerId) : null;
                if (def == null || def.prefab == null) { Debug.LogWarning($"[BaseBuild] resolve tower '{data.towerId}' ไม่ได้"); continue; }
                SpawnTowerPiece(def, data, paid: true);
            }
            foreach (var data in layout.garrison)
            {
                var card = registry != null ? registry.ResolveCard(data.cardId) : null;
                if (card == null || card.linkedMonster == null || card.linkedMonster.prefab == null)
                { Debug.LogWarning($"[BaseBuild] resolve garrison '{data.cardId}' ไม่ได้"); continue; }
                SpawnGarrisonPiece(card.linkedMonster, card.goldCost, data, paid: true);
            }
        }

        private void ClearPlaced()
        {
            foreach (var piece in placed) if (piece != null) Destroy(piece.gameObject);
            placed.Clear();
            selected = null;
        }

        private void SpawnTowerPiece(TowerDefinitionSO def, PlacedTowerData data, bool paid)
        {
            var piece = SpawnPrefab(def.prefab, data.position);
            piece.SetupTower(data, def.attackRange, def.goldCost, def.footprint, def.defenseCapacityCost);
            piece.Paid = paid;
            placed.Add(piece);
        }

        private void SpawnGarrisonPiece(MonsterDefinitionSO def, int cost, GarrisonMonsterData data, bool paid)
        {
            var piece = SpawnPrefab(def.prefab, data.position);
            piece.SetupGarrison(data, def.attackRange, cost, def.footprint, def.defenseCapacityCost);
            piece.Paid = paid;
            placed.Add(piece);
        }

        private BaseBuildPiece SpawnPrefab(GameObject prefab, Vector3 position)
        {
            var parent = placedRoot != null ? placedRoot : transform;
            var instance = Instantiate(prefab, position, Quaternion.identity, parent);
            var piece = instance.GetComponent<BaseBuildPiece>();
            if (piece == null) piece = instance.AddComponent<BaseBuildPiece>();
            piece.SetFaceCamera(FaceCam); // หันหน้าเข้ากล้องเสมอ
            return piece;
        }

        // กล้องที่ตัวละครจะหันหน้าเข้าหา — จาก cameraPan (ถ้าผูก) ไม่งั้น Camera.main
        private Camera FaceCam => cameraPan != null ? cameraPan.GetComponent<Camera>() : Camera.main;
    }
}
