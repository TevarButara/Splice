using TMPro;
using UnityEngine;

namespace Splice.Base
{
    // Flow ปุ่ม Checkout (ขั้น 5.5): กด Checkout → เปิด panel ยืนยัน "จ่าย X ทอง?" → ตกลง = หักทองจริง + commit + persist.
    // (BaseBuildManager.Checkout ทำ transaction; อันนี้แค่คุม UI ยืนยัน)
    public class BaseBuildCheckoutController : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [Tooltip("panel ยืนยัน (ปิดไว้ตอนเริ่ม)")]
        [SerializeField] private GameObject confirmPanel;
        [Tooltip("ข้อความยืนยัน เช่น 'จ่าย X ทอง?'")]
        [SerializeField] private TMP_Text confirmLabel;

        private void Awake()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        // wire ปุ่ม "Checkout" → อันนี้ (เปิด panel ยืนยันพร้อมยอด)
        public void OpenConfirm()
        {
            if (buildManager == null) return;
            if (confirmLabel != null) confirmLabel.text = $"จ่าย {buildManager.NetCost} ทอง?";
            if (confirmPanel != null) confirmPanel.SetActive(true);
        }

        // wire ปุ่ม "ตกลง" ใน panel → อันนี้ (หักทองจริง + commit + persist)
        public void Confirm()
        {
            if (buildManager != null) buildManager.Checkout();
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        // wire ปุ่ม "ยกเลิก" ใน panel → อันนี้ (ปิด panel เฉยๆ ไม่จ่าย)
        public void CancelConfirm()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }
    }
}
