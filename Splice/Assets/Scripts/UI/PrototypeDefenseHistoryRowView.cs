using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Splice.UI
{
    [DisallowMultipleComponent]
    public sealed class PrototypeDefenseHistoryRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private Button replayButton;
        [SerializeField] private TMP_Text replayLabel;
        [SerializeField] private Button revengeButton;
        [SerializeField] private TMP_Text revengeLabel;

        public bool IsComplete => outcomeText != null && detailsText != null &&
            replayButton != null && replayLabel != null && revengeButton != null && revengeLabel != null;

        public void InitializeEditorReferences(TMP_Text outcome, TMP_Text details, Button replay,
            TMP_Text replayText, Button revenge, TMP_Text revengeText)
        {
            outcomeText = outcome;
            detailsText = details;
            replayButton = replay;
            replayLabel = replayText;
            revengeButton = revenge;
            revengeLabel = revengeText;
        }

        public void Configure(string outcome, string details, Color outcomeColor,
            bool replayAvailable, string replayText, UnityAction replay,
            bool revengeAvailable, string revengeText, UnityAction revenge)
        {
            gameObject.SetActive(true);
            outcomeText.text = outcome;
            outcomeText.color = outcomeColor;
            detailsText.text = details;
            Bind(replayButton, replayAvailable, replay);
            replayLabel.text = replayText;
            Bind(revengeButton, revengeAvailable, revenge);
            revengeLabel.text = revengeText;
        }

        private static void Bind(Button button, bool interactable, UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
            button.interactable = interactable;
        }
    }
}
