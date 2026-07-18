using TMPro;
using UnityEngine;

namespace Splice.Base
{
    // แสดง "เพดานฝ่ายรับ (DefenseCapacity)" ที่ใช้ไป/สูงสุด แบบ standalone — วางตรงไหนก็ได้.
    // เปลี่ยนสีเมื่อเต็มเพดาน (used >= max). (architecture §5.10 — กัน defense snowball)
    public class BaseBuildCapacityDisplay : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [SerializeField] private TMP_Text label;
        [Tooltip("รูปแบบข้อความ — {0}=ใช้ไป, {1}=สูงสุด. เช่น 'Defense {0}/{1}'")]
        [SerializeField] private string format = "Defense {0}/{1}";
        [Tooltip("สีปกติ")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("สีตอนเต็มเพดาน (used >= max)")]
        [SerializeField] private Color fullColor = new(1f, 0.4f, 0.4f);

        private void Update()
        {
            if (buildManager == null || label == null) return;

            var used = buildManager.UsedCapacity;
            var max = buildManager.DefenseCapacity;
            label.text = string.Format(format, used, max);
            label.color = used >= max ? fullColor : normalColor;
        }
    }
}
