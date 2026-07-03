using Splice.Combat;
using TMPro;
using UnityEngine;

namespace Splice.UI
{
    // Counts down the match timer (architecture 5.6). Reads the server-authoritative RemainingSeconds
    // NetworkVariable via RaidManager — display only, no local timing — so every client shows the same clock.
    public class MatchTimerDisplay : MonoBehaviour
    {
        [SerializeField] private RaidManager raidManager;
        [SerializeField] private TMP_Text label;

        private void Update()
        {
            if (raidManager == null || label == null) return;

            var total = Mathf.CeilToInt(raidManager.RemainingSeconds);
            var minutes = total / 60;
            var seconds = total % 60;
            label.text = $"{minutes:0}:{seconds:00}";
        }
    }
}
