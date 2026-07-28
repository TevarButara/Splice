using Splice.Core;
using Splice.Data;
using Splice.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.World
{
    // The world map is intentionally a node/sector UI, not a second real-time open world.
    // It keeps matchmaking, travel cost and content streaming deterministic and cheap at scale.
    public sealed class WorldMapController : MonoBehaviour
    {
        [SerializeField] private WorldMapDefinitionSO definition;
        [SerializeField] private Button townButton;
        [SerializeField] private Button forestButton;
        [SerializeField] private Button raidButton;
        [SerializeField] private TMP_Text playerSummary;
        [SerializeField] private TMP_Text forestSummary;

        public bool HasEditorAuthoredUi =>
            townButton != null && forestButton != null && raidButton != null &&
            playerSummary != null && forestSummary != null;
        public WorldMapDefinitionSO Definition => definition;

        private void Awake()
        {
            if (!HasEditorAuthoredUi)
            {
                Debug.LogError("[WorldMap] Editor-authored UI is incomplete. Rebuild the World Map prototype.");
                enabled = false;
                return;
            }
            Bind(townButton, PrototypeFlowRouter.LoadHub);
            Bind(forestButton, PrototypeFlowRouter.LoadForest);
            Bind(raidButton, PrototypeFlowRouter.LoadRaidHub);
            Refresh();
        }

        public void Refresh()
        {
            var progress = ForestHuntProgressStore.Load();
            playerSummary.text =
                $"COMMANDER  {Short(PlayerProfile.AccountId)}\n" +
                $"FACTION  {PlayerProfile.ActiveFactionId.ToUpperInvariant()}  •  BASE LV.{PlayerProfile.BaseLevel(PlayerProfile.ActiveFactionId)}";
            forestSummary.text =
                $"FOREST HUNT\n{progress.fragments:N0} / {progress.fragmentsPerDiamond:N0} fragments  •  " +
                $"{progress.diamondsEarnedThisWeek}/{progress.weeklyDiamondCap} weekly diamonds";
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static string Short(string value) =>
            string.IsNullOrWhiteSpace(value) ? "GUEST" :
            value.Length <= 8 ? value : value[..8].ToUpperInvariant();
    }
}
