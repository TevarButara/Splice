using UnityEngine;

namespace Splice.Base
{
    // สร้างปุ่ม palette แบบ "dynamic ตาม faction ที่กำลังจัดเมือง" (architecture §1.1/§5.10, roadmap 5.2/5.3) —
    // ป้อมจาก FactionSO.towers, มอนเฝ้าจาก FactionSO.cards (เฉพาะการ์ดที่มี linkedMonster).
    // ไม่ต้องทำปุ่มมือทีละอัน/ทีละเผ่า: มีปุ่ม prefab 1 อัน แล้ว Instantiate ต่อ 1 ชนิดตามเผ่าที่เลือก.
    // สลับเผ่า (loadout) แล้วเรียก Rebuild() ใหม่ palette จะเปลี่ยนตามเมืองที่กำลังจัด.
    public class BaseBuildPalette : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [Tooltip("prefab ปุ่ม 1 อัน (มี BaseBuildPaletteButton + label/highlight ผูกไว้) — Instantiate ต่อ 1 ชนิด")]
        [SerializeField] private BaseBuildPaletteButton buttonPrefab;
        [Tooltip("ที่วางปุ่มป้อม (แนะนำมี Layout Group)")]
        [SerializeField] private Transform towerContainer;
        [Tooltip("ที่วางปุ่มมอนเฝ้า — เว้นว่าง = ใช้ towerContainer อันเดียวกัน")]
        [SerializeField] private Transform garrisonContainer;

        private void Start() => Rebuild();

        // เรียกซ้ำได้เมื่อสลับ faction (เช่นหลังเลือก loadout ใหม่)
        public void Rebuild()
        {
            if (buildManager == null || buttonPrefab == null) return;

            var registry = buildManager.Registry;
            var towerParent = towerContainer != null ? towerContainer : transform;
            var garrisonParent = garrisonContainer != null ? garrisonContainer : towerParent;

            Clear(towerParent);
            if (garrisonParent != towerParent) Clear(garrisonParent);

            var faction = registry != null ? registry.GetFaction(buildManager.EditingFactionId) : null;
            if (faction == null)
            {
                Debug.LogWarning($"[BaseBuildPalette] ไม่พบ faction '{buildManager.EditingFactionId}' ใน registry — palette ว่าง (เลือก faction หรือ set factionId ก่อน)");
                return;
            }

            // ป้อมของเผ่า → ปุ่มวางป้อม
            foreach (var tower in faction.towers)
            {
                if (tower == null) continue;
                var btn = Instantiate(buttonPrefab, towerParent);
                btn.BindTower(buildManager, tower);
            }

            // มอนของเผ่า (การ์ดที่มี linkedMonster) → ปุ่มวางมอนเฝ้า garrison
            foreach (var card in faction.cards)
            {
                if (card == null || card.linkedMonster == null) continue;
                var btn = Instantiate(buttonPrefab, garrisonParent);
                btn.BindGarrison(buildManager, card);
            }
        }

        private static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
