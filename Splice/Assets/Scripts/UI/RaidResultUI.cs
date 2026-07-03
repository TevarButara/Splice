using Splice.Combat;
using TMPro;
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

        private static string ResultText(RaidOutcome outcome, RaidEndReason reason)
        {
            if (outcome == RaidOutcome.MonstersWin) return "Invaders Win!";
            return reason switch
            {
                RaidEndReason.TimerExpired => "Fort Wins! (Time)",
                RaidEndReason.InvaderEliminated => "Fort Wins! (Invader Eliminated)",
                _ => "Fort Wins!"
            };
        }

        private void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
