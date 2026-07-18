using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.Base
{
    // ปุ่มเป้าหมาย raid 1 อัน (greybox) — โชว์ชื่อ+ทองของ target ที่ index นี้ แล้วกดบุก.
    public class RaidTargetButton : MonoBehaviour
    {
        [SerializeField] private RaidTargetSelectionController controller;
        [Tooltip("ลำดับเป้าหมายในรายการที่ปุ่มนี้ผูก (0,1,2,...)")]
        [SerializeField] private int index;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => { if (controller != null) controller.Raid(index); });
        }

        private void Update()
        {
            if (controller == null || label == null) return;
            var targets = controller.Targets;
            if (index < targets.Count)
            {
                var t = targets[index];
                label.text = $"{t.displayName}\nทอง {t.StoredGold} · Lv{t.baseLevel}";
                if (button != null) button.interactable = true;
            }
            else
            {
                label.text = "-";
                if (button != null) button.interactable = false;
            }
        }
    }
}
