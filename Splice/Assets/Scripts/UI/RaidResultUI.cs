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

        private void Awake()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            if (playAgainButton != null) playAgainButton.onClick.AddListener(ReloadScene);
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
            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultLabel != null) resultLabel.text = ResultText(outcome, raidManager.EndReason);
        }

        // อ่าน loot ทุกเฟรมตอน panel โชว์ — กันปัญหาลำดับ event (RaidRewardController อาจ set ทีหลัง)
        private void Update()
        {
            if (lootLabel == null) return;
            var show = resultPanel != null && resultPanel.activeSelf && RaidContext.LastLootGained > 0;
            lootLabel.text = show ? $"+{RaidContext.LastLootGained} ทอง" : string.Empty;
        }

        private static string ResultText(RaidOutcome outcome, RaidEndReason reason)
        {
            if (outcome == RaidOutcome.MonstersWin) return "Attackers Win!";
            return reason switch
            {
                RaidEndReason.TimerExpired => "Fort Wins! (Time)",
                RaidEndReason.AttackerEliminated => "Fort Wins! (Attacker Eliminated)",
                _ => "Fort Wins!"
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
    }
}
