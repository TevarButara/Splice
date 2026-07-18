using Splice.Combat;
using Splice.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.UI
{
    // Greybox gold readout; reads the server-authoritative NetworkVariable via GoldController (architecture 5.7).
    public class GoldDisplay : MonoBehaviour
    {
        [FormerlySerializedAs("team")]
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [SerializeField] private TMP_Text label;

        private void Update()
        {
            if (label == null) return;
            var bank = GoldController.For(side);
            label.text = bank != null ? $"Gold: {bank.CurrentGold}" : "Gold: -";
        }
    }
}
