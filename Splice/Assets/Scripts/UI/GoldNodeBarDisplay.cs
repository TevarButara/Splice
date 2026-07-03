using Splice.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // World-space bar + text showing how much gold a GoldNode has left (architecture 5.7).
    // Reads the node's networked Remaining value only — same read-only pattern as HealthBarDisplay,
    // and billboards toward the camera so the bar always faces the player.
    public class GoldNodeBarDisplay : MonoBehaviour
    {
        [SerializeField] private GoldNode node;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Camera billboardCamera;

        private void Awake()
        {
            if (node == null) node = GetComponentInParent<GoldNode>();
            if (billboardCamera == null) billboardCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (node == null) return;

            if (fillImage != null)
            {
                fillImage.fillAmount = node.TotalGold > 0
                    ? (float)node.Remaining / node.TotalGold
                    : 0f;
            }

            if (label != null) label.text = node.Remaining.ToString();

            if (billboardCamera != null)
            {
                transform.rotation = billboardCamera.transform.rotation;
            }
        }
    }
}
