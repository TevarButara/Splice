using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    public sealed class TownRegionPurchaseButtonView : MonoBehaviour
    {
        [SerializeField] private string regionId;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        public string RegionId => regionId;
        public Button Button => button;
        public TMP_Text Label => label;
        public bool IsComplete => !string.IsNullOrWhiteSpace(regionId) && button != null && label != null;
    }
}
