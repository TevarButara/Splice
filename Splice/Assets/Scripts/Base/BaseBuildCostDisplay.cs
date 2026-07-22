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

        private void OnEnable() => InvokeRepeating(nameof(Refresh), 0f, .15f);

        private void OnDisable() => CancelInvoke(nameof(Refresh));

        private void Refresh()
        {
            if (buildManager == null) return;
            if (costLabel != null) costLabel.text = $"COST  •  {buildManager.NetCost:N0}";
            if (walletLabel != null) walletLabel.text = $"GOLD  •  {buildManager.WalletGold:N0}";
            if (capacityLabel != null) capacityLabel.text =
                $"DEFENSE  •  {buildManager.UsedCapacity}/{buildManager.DefenseCapacity}";
        }
    }
}
