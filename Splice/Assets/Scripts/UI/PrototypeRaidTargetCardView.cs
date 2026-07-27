using Splice.Base;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Splice.UI
{
    /// <summary>
    /// Scene-authored raid target card. BuildZone owns three of these views and runtime only fills their data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeRaidTargetCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text sourceText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private TMP_Text reasonText;
        [SerializeField] private Button raidButton;
        [SerializeField] private TMP_Text raidButtonLabel;

        public bool IsComplete =>
            titleText != null && sourceText != null && detailsText != null &&
            reasonText != null && raidButton != null && raidButtonLabel != null;

        public void InitializeEditorReferences(TMP_Text title, TMP_Text source, TMP_Text details,
            TMP_Text reason, Button button, TMP_Text buttonLabel)
        {
            titleText = title;
            sourceText = source;
            detailsText = details;
            reasonText = reason;
            raidButton = button;
            raidButtonLabel = buttonLabel;
        }

        public void Configure(RaidTarget target, bool canRaid, string reason, UnityAction onRaid)
        {
            if (target == null || !IsComplete)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            titleText.text = target.displayName.ToUpperInvariant();
            sourceText.text = target.IsSnapshotBacked
                ? $"PLAYER SNAPSHOT  V{target.snapshotRevision}"
                : "WORLD BOT OUTPOST";
            sourceText.color = target.IsSnapshotBacked
                ? new Color(0.20f, 0.76f, 1f, 1f)
                : new Color(0.64f, 0.70f, 0.78f, 1f);
            detailsText.text =
                $"POWER  <b>{target.basePowerRating:N0}</b>\n" +
                $"DEFENSE  {target.towerCount} towers  •  {target.garrisonCount} garrison\n" +
                $"CAPACITY  {target.usedCapacity}/{target.maxCapacity}\n\n" +
                $"EXPECTED GOLD  <color=#FFB837><b>{target.StoredGold:N0}</b></color>\n" +
                "WAR GEM STAKE  <b>100</b>\nFULL VICTORY  <color=#A6F23F><b>+180</b></color>";

            raidButton.onClick.RemoveAllListeners();
            if (onRaid != null) raidButton.onClick.AddListener(onRaid);
            raidButton.interactable = canRaid;
            raidButtonLabel.text = canRaid ? "REVIEW RAID CONTRACT" : "INSPECTION ONLY";
            raidButtonLabel.color = canRaid
                ? new Color(0.96f, 0.98f, 1f, 1f)
                : new Color(0.64f, 0.70f, 0.78f, 1f);
            reasonText.text = canRaid ? string.Empty : reason;
            reasonText.gameObject.SetActive(!canRaid && !string.IsNullOrWhiteSpace(reason));
        }
    }
}
