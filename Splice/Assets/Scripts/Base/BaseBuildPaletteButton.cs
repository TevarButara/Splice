using Splice.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.Base
{
    // ปุ่ม palette 1 ชนิดในโหมดจัดผังเมือง — กดแล้ว "arm" สิ่งนั้นให้พร้อมวาง.
    // 1 ปุ่ม = ป้อม 1 ชนิด (`tower`) หรือ มอนเฝ้า garrison 1 ชนิด (`garrisonCard`) อย่างใดอย่างหนึ่ง.
    // โชว์ราคา + หรี่เทาเมื่อทองไม่พอ (checkout economy — ขั้น 5.5). ผูก dynamic ผ่าน BaseBuildPalette หรือ Inspector.
    public class BaseBuildPaletteButton : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [Tooltip("ป้อมที่จะวาง — ใส่อันนี้ ถ้าปุ่มนี้เป็นป้อม")]
        [SerializeField] private TowerDefinitionSO tower;
        [Tooltip("การ์ดมอนเฝ้า (garrison) — ใส่แทน tower ถ้าปุ่มนี้เป็นมอน (cardType = Monster, linkedMonster ตั้งไว้)")]
        [SerializeField] private CardDefinitionSO garrisonCard;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("ป้ายราคา (goldCost) — เว้นว่างได้")]
        [SerializeField] private TMP_Text costLabel;
        [Tooltip("หรี่เทาเมื่อทองไม่พอสำหรับวางเพิ่ม (เว้นว่างได้)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("ไฮไลต์เมื่อชนิดนี้คือสิ่งที่กำลังเลือกวางอยู่ (เว้นว่างได้)")]
        [SerializeField] private GameObject selectedHighlight;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
            RefreshLabels();
        }

        // ผูกแบบ dynamic ตอน runtime — BaseBuildPalette สร้างปุ่มจากเผ่าที่เลือกแล้วเรียกอันนี้
        public void BindTower(BaseBuildManager manager, TowerDefinitionSO towerDef)
        {
            buildManager = manager; tower = towerDef; garrisonCard = null; RefreshLabels();
        }

        public void BindGarrison(BaseBuildManager manager, CardDefinitionSO monsterCard)
        {
            buildManager = manager; garrisonCard = monsterCard; tower = null; RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (nameLabel != null) nameLabel.text = DisplayName();
            if (costLabel != null) costLabel.text = Cost().ToString();
        }

        private string DisplayName() =>
            tower != null ? tower.displayName : garrisonCard != null ? garrisonCard.displayName : string.Empty;

        private int Cost() =>
            tower != null ? tower.goldCost : garrisonCard != null ? garrisonCard.goldCost : 0;

        private int CapacityCost() =>
            tower != null ? tower.defenseCapacityCost
            : garrisonCard != null && garrisonCard.linkedMonster != null ? garrisonCard.linkedMonster.defenseCapacityCost
            : 0;

        private void OnClick()
        {
            if (buildManager == null) return;
            if (tower != null) buildManager.ArmTower(tower);
            else if (garrisonCard != null) buildManager.ArmGarrison(garrisonCard);
        }

        private void Update()
        {
            if (buildManager == null) return;

            // เทา/ปิดกด เมื่อทองไม่พอ หรือ เต็มเพดานฝ่ายรับ (DefenseCapacity)
            var usable = buildManager.CanAfford(Cost()) && buildManager.HasCapacityFor(CapacityCost());
            if (button != null) button.interactable = usable;
            if (canvasGroup != null) canvasGroup.alpha = usable ? 1f : 0.4f;

            if (selectedHighlight != null)
            {
                var isThis = tower != null
                    ? buildManager.ArmedTower == tower
                    : garrisonCard != null && buildManager.ArmedGarrison == garrisonCard;
                selectedHighlight.SetActive(isThis);
            }
        }
    }
}
