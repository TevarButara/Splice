using Splice.Base;
using Splice.Combat;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.UI
{
    // Greybox win/lose screen for the match (architecture 5.6's Fort-vs-Invader objective).
    public class RaidResultUI : MonoBehaviour
    {
        [SerializeField] private RaidManager raidManager;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultLabel;
        [Tooltip("โชว์ loot ที่ได้จากการบุก (5.4) — เว้นว่างได้")]
        [SerializeField] private TMP_Text lootLabel;
        [SerializeField] private Button playAgainButton;
        private Button returnToTownButton;

        private RaidOutcome shownOutcome = RaidOutcome.InProgress;
        private RaidEndReason shownReason = RaidEndReason.None;

        private void Awake()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            if (playAgainButton != null) playAgainButton.onClick.AddListener(ReloadScene);
            BuildReturnToTownButton();
        }

        private void OnEnable()
        {
            if (raidManager != null) raidManager.OnRaidEnded += HandleRaidEnded;
        }

        private void OnDisable()
        {
            if (raidManager != null) raidManager.OnRaidEnded -= HandleRaidEnded;
        }

        private void HandleRaidEnded(RaidOutcome outcome)
        {
            shownOutcome = outcome;
            shownReason = raidManager != null ? raidManager.EndReason : RaidEndReason.None;
            if (resultPanel != null) resultPanel.SetActive(true);
            RefreshResultLabel();
        }

        // อ่าน loot ทุกเฟรมตอน panel โชว์ — กันปัญหาลำดับ event (RaidRewardController อาจ set ทีหลัง)
        private void Update()
        {
            var show = resultPanel != null && resultPanel.activeSelf && RaidContext.LastLootGained > 0;
            if (lootLabel != null) lootLabel.text = show ? $"+{RaidContext.LastLootGained} ทอง" : string.Empty;
            if (resultPanel != null && resultPanel.activeSelf) RefreshResultLabel();
        }

        private void RefreshResultLabel()
        {
            if (resultLabel == null || shownOutcome == RaidOutcome.InProgress) return;
            var incomingDefense = Splice.Core.RaidSessionContext.Current?.isIncomingDefense == true;
            var text = incomingDefense
                ? DefenseResultText(shownOutcome, shownReason)
                : ResultText(shownOutcome, shownReason);
            if (!incomingDefense && RaidContext.HasLastWarGemSettlement)
            {
                // The legacy result headline is intentionally very large. Keep the economy line compact so
                // it remains one readable line and does not collide with the Play Again button.
                text += $"\n<size=45%>WAR GEMS  PAYOUT +{RaidContext.LastWarGemPayout}  •  " +
                        $"NET {Signed(RaidContext.LastWarGemNet)}  •  BAL {RaidContext.LastWarGemBalance}</size>";
            }
            resultLabel.text = text;
        }

        private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

        private static string ResultText(RaidOutcome outcome, RaidEndReason reason)
        {
            if (outcome == RaidOutcome.FullVictory) return "Full Victory! (Core Destroyed)";
            if (outcome == RaidOutcome.Extracted) return "Extraction Successful!";
            return reason switch
            {
                RaidEndReason.TimerExpired => "Raid Failed! (Time)",
                RaidEndReason.AttackerEliminated => "Raid Failed! (Army Eliminated)",
                _ => "Raid Failed!"
            };
        }

        private static string DefenseResultText(RaidOutcome outcome, RaidEndReason reason)
        {
            if (outcome == RaidOutcome.FullVictory) return "Defense Breached! (Core Destroyed)";
            if (outcome == RaidOutcome.Extracted) return "Raiders Escaped!";
            return reason switch
            {
                RaidEndReason.TimerExpired => "Defense Held! (Time Survived)",
                RaidEndReason.AttackerEliminated => "Defense Held! (Raiders Eliminated)",
                _ => "Defense Held!"
            };
        }

        private void ReloadScene()
        {
            // A Netcode session survives a plain scene reload (the NetworkManager keeps listening), which
            // leaves the reloaded scene's networked objects unspawned and the old server state (timer/gold/
            // outcome/HP) stuck — GameBootstrap skips StartHost while already listening. Shut the session
            // down first so the reload comes up with a fresh host and everything resets.
            var net = NetworkManager.Singleton;
            if (net != null && (net.IsListening || net.IsServer || net.IsClient)) net.Shutdown();

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void ReturnToTown() => PrototypeFlowRouter.LoadHub();

        private void BuildReturnToTownButton()
        {
            if (playAgainButton == null || resultPanel == null) return;
            returnToTownButton = Instantiate(playAgainButton, playAgainButton.transform.parent);
            returnToTownButton.name = "ReturnToTownButton";
            returnToTownButton.onClick.RemoveAllListeners();
            returnToTownButton.onClick.AddListener(ReturnToTown);
            var returnLabel = returnToTownButton.GetComponentInChildren<TMP_Text>(true);
            if (returnLabel != null) returnLabel.text = "RETURN TO TOWN";
            var legacyReturnLabel = returnToTownButton.GetComponentInChildren<Text>(true);
            if (legacyReturnLabel != null) legacyReturnLabel.text = "RETURN TO TOWN";

            var retryRect = playAgainButton.GetComponent<RectTransform>();
            var returnRect = returnToTownButton.GetComponent<RectTransform>();
            if (retryRect == null || returnRect == null) return;
            var original = retryRect.anchoredPosition;
            retryRect.anchoredPosition = original + new Vector2(-190f, 0f);
            returnRect.anchoredPosition = original + new Vector2(190f, 0f);
            retryRect.sizeDelta = new Vector2(Mathf.Min(330f, retryRect.sizeDelta.x), retryRect.sizeDelta.y);
            returnRect.sizeDelta = retryRect.sizeDelta;
            var retryLabel = playAgainButton.GetComponentInChildren<TMP_Text>(true);
            if (retryLabel != null) retryLabel.text = "RAID AGAIN";
            var legacyRetryLabel = playAgainButton.GetComponentInChildren<Text>(true);
            if (legacyRetryLabel != null) legacyRetryLabel.text = "RAID AGAIN";
        }
    }
}
