using Splice.Combat;
using TMPro;
using UnityEngine;

namespace Splice.UI
{
    // Optional greybox HUD for the step-2 loot ledger.
    public class RaidLootDisplay : MonoBehaviour
    {
        [SerializeField] private RaidLootController lootController;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "Loot  Available {0}  Carried {1}  Secured {2}";

        private void Update()
        {
            if (lootController == null) lootController = FindFirstObjectByType<RaidLootController>();
            if (lootController == null || label == null) return;

            label.text = string.Format(
                format,
                lootController.Available,
                lootController.Carried,
                lootController.Secured);
        }
    }
}
