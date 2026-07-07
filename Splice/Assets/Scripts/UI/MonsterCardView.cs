using Splice.Combat;
using Splice.Core;
using Splice.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // One monster card inside a LaneDeployPanel. Self-updating (greybox, no events): it greys out when
    // the player can't afford it or hasn't reached its requiredLevel, shows a stack badge for queued
    // copies, and shows a spawn countdown while this card is the one cooking at the lane's head.
    // Reads server-authoritative state (gold/level/queue) through the panel — never mutates it directly.
    public class MonsterCardView : MonoBehaviour
    {
        [SerializeField] private LaneDeployPanel panel;
        [SerializeField] private CardDefinitionSO card;

        [Header("Widgets")]
        [SerializeField] private Button button;
        [Tooltip("หรี่เป็นสีเทาเมื่อเรียกไม่ได้ (เงินไม่พอ / level ไม่ถึง)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("แสดงเมื่อ level ยังไม่ถึง (ล็อก)")]
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [Tooltip("ป้ายจำนวนในคิว 'xN' — ซ่อนเมื่อคิวว่าง")]
        [SerializeField] private TMP_Text stackLabel;

        [Header("Countdown")]
        [Tooltip("รากของ overlay นับถอยหลัง (fill + label) — เปิดเฉพาะตอนการ์ดนี้กำลังถูกสร้างที่หัวคิว")]
        [SerializeField] private GameObject cooldownOverlay;
        [Tooltip("Image แบบ Filled — fillAmount = เวลาเหลือ/เวลาเกิดเต็ม")]
        [SerializeField] private Image cooldownFill;
        [SerializeField] private TMP_Text cooldownLabel;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClickDeploy);
        }

        // Also wire this to the Button's OnClick in the Inspector if you prefer not to rely on Awake.
        public void OnClickDeploy()
        {
            if (panel != null && card != null) panel.RequestDeploy(card);
        }

        private void Update()
        {
            if (card == null || panel == null || panel.Deployment == null) return;

            var team = panel.Deployment.DeployTeam;
            var lane = panel.CurrentLaneId;

            var bank = GoldController.For(team);
            var gold = bank != null ? bank.CurrentGold : 0;
            var level = PlayerProgression.LevelFor(team);

            var unlocked = level >= card.requiredLevel;
            var usable = unlocked && gold >= card.goldCost;

            if (canvasGroup != null) canvasGroup.alpha = usable ? 1f : 0.4f;
            if (button != null) button.interactable = usable;
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (nameLabel != null) nameLabel.text = card.displayName;
            if (costLabel != null) costLabel.text = card.goldCost.ToString();

            var cardId = panel.Deployment.IdOf(card);   // composite id: factionId/cardId

            var stack = panel.Deployment.GetQueuedCount(lane, cardId);
            if (stackLabel != null)
            {
                stackLabel.gameObject.SetActive(stack > 0);
                stackLabel.text = $"x{stack}";
            }

            UpdateCooldown(lane, cardId);
        }

        private void UpdateCooldown(int lane, string cardId)
        {
            var cooking = false;

            if (panel.Deployment.TryGetLaneHead(lane, out var headCardId, out var spawnAt)
                && headCardId == cardId && spawnAt > 0.0)
            {
                var now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0.0;
                var total = card.linkedMonster != null ? Mathf.Max(0.0001f, card.linkedMonster.buildTimeSeconds) : 0.0001f;
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
