using Splice.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // ปุ่มเลือก 1 faction ในจอเริ่มเซสชัน — กดแล้วบอก FactionSelectionController ว่าเลือกเผ่านี้.
    // 1 ปุ่ม/1 faction; ตั้งชื่อ+ไอคอนจาก FactionSO ให้อัตโนมัติ
    public class FactionSelectionButton : MonoBehaviour
    {
        [SerializeField] private FactionSelectionController controller;
        [SerializeField] private FactionSO faction;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image iconImage;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
            if (faction != null)
            {
                if (nameLabel != null) nameLabel.text = faction.displayName;
                if (iconImage != null && faction.icon != null) iconImage.sprite = faction.icon;
            }
        }

        private void OnClick()
        {
            if (controller != null && faction != null) controller.ChooseFaction(faction.factionId);
        }
    }
}
