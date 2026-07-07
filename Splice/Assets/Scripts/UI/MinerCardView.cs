using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Splice.Network;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // One miner "buy" card (mirrors MonsterCardView, but routes to MinerDeploymentManager and has no lane).
    // Greys out when unaffordable / level-locked, shows a stack badge for queued copies, and a build
    // countdown while it's the one building at the head. Miners work autonomously once they spawn.
    public class MinerCardView : MonoBehaviour
    {
        [SerializeField] private MinerDeploymentManager deployment;
        [SerializeField] private CardDefinitionSO card;

        [Header("Widgets")]
        [SerializeField] private Button button;
        [Tooltip("หรี่เป็นสีเทาเมื่อซื้อไม่ได้ (เงินไม่พอ / level ไม่ถึง)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("แสดงเมื่อ level ยังไม่ถึง (ล็อก)")]
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [Tooltip("ป้ายจำนวนในคิว 'xN' — ซ่อนเมื่อคิวว่าง")]
        [SerializeField] private TMP_Text stackLabel;

        [Header("Countdown")]
        [SerializeField] private GameObject cooldownOverlay;
        [Tooltip("Image แบบ Filled — fillAmount = เวลาเหลือ/เวลาสร้างเต็ม")]
        [SerializeField] private Image cooldownFill;
        [SerializeField] private TMP_Text cooldownLabel;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClickBuy);
        }

        public void OnClickBuy()
        {
            if (deployment == null || card == null) return;
            var id = deployment.IdOf(card);
            if (string.IsNullOrEmpty(id)) return;
            deployment.RequestQueueMinerServerRpc(new FixedString32Bytes(id));
        }

        private void Update()
        {
            if (card == null || deployment == null) return;

            var bank = GoldController.For(deployment.DeployTeam);
            var gold = bank != null ? bank.CurrentGold : 0;
            var level = PlayerProgression.LevelFor(deployment.DeployTeam);

            var unlocked = level >= card.requiredLevel;
            var usable = unlocked && gold >= card.goldCost;

            if (canvasGroup != null) canvasGroup.alpha = usable ? 1f : 0.4f;
            if (button != null) button.interactable = usable;
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (nameLabel != null) nameLabel.text = card.displayName;
            if (costLabel != null) costLabel.text = card.goldCost.ToString();

            var id = deployment.IdOf(card);

            var stack = deployment.GetQueuedCount(id);
            if (stackLabel != null)
            {
                stackLabel.gameObject.SetActive(stack > 0);
                stackLabel.text = $"x{stack}";
            }

            UpdateCooldown(id);
        }

        private void UpdateCooldown(string id)
        {
            var cooking = false;

            if (deployment.TryGetHead(out var headId, out var spawnAt) && headId == id && spawnAt > 0.0)
            {
                var now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0.0;
                var total = card.linkedMiner != null ? Mathf.Max(0.0001f, card.linkedMiner.buildTimeSeconds) : 0.0001f;
                var remaining = Mathf.Max(0f, (float)(spawnAt - now));
                cooking = remaining > 0f;

                if (cooldownFill != null) cooldownFill.fillAmount = remaining / total;
                if (cooldownLabel != null) cooldownLabel.text = remaining.ToString("0.0");
            }
            else if (cooldownFill != null)
            {
                cooldownFill.fillAmount = 0f;
            }

            if (cooldownOverlay != null) cooldownOverlay.SetActive(cooking);
        }
    }
}
