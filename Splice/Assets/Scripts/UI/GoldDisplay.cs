using Splice.Combat;
using Splice.Core;
using TMPro;
using UnityEngine;

namespace Splice.UI
{
    // Greybox gold readout; reads the server-authoritative NetworkVariable via GoldController (architecture 5.7).
    public class GoldDisplay : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Invaders;
        [SerializeField] private TMP_Text label;

        private void Update()
        {
            if (label == null) return;
            var bank = GoldController.For(team);
            label.text = bank != null ? $"Gold: {bank.CurrentGold}" : "Gold: -";
        }
    }
}
