using TMPro;
using UnityEngine;

namespace Splice.Base
{
    // แสดงยอด "ต้องจ่าย" (NetCost) + ทองในกระเป๋า (meta gold) ตอนจัดเมือง (checkout economy — ขั้น 5.5).
    // อัปเดตทุกเฟรม (greybox) — ยอดขึ้นตามที่วาง/ลบ; ทองเหลือลดตอน checkout.
    public class BaseBuildCostDisplay : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [Tooltip("ยอดต้องจ่ายตอน checkout (NetCost)")]
        [SerializeField] private TMP_Text costLabel;
        [Tooltip("ทอง meta ในกระเป๋า")]
        [SerializeField] private TMP_Text walletLabel;
        [Tooltip("เพดานฝ่ายรับ (used/max) — DefenseCapacity")]
        [SerializeField] private TMP_Text capacityLabel;

        private void Update()
        {
            if (buildManager == null) return;
            if (costLabel != null) costLabel.text = $"ต้องจ่าย: {buildManager.NetCost}";
            if (walletLabel != null) walletLabel.text = $"ทอง: {buildManager.WalletGold}";
            if (capacityLabel != null) capacityLabel.text = $"Defense: {buildManager.UsedCapacity}/{buildManager.DefenseCapacity}";
        }
    }
}
