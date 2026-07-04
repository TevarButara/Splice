using Splice.Data;
using Splice.Network;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace Splice.UI
{
    // Card panel bound to one lane's hut. Opened by SoldierHutInputController; the MonsterCardView
    // children read CurrentLaneId/Deployment from here to render their state and route deploy taps.
    //
    // Put this script on an ALWAYS-ACTIVE GameObject and assign `content` to the visual root that holds
    // the cards — OpenForLane/Close only toggle `content`, so this component keeps running (and can be
    // re-opened) while the cards themselves are hidden.
    public class LaneDeployPanel : MonoBehaviour
    {
        [SerializeField] private DeploymentManager deploymentManager;
        [Tooltip("รากของภาพ panel (ที่รวมการ์ด) — ถูกเปิด/ปิดตอน Open/Close. ปล่อยว่างได้ถ้าใช้ทั้ง GameObject นี้")]
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text laneLabel;

        public DeploymentManager Deployment => deploymentManager;
        public int CurrentLaneId { get; private set; }

        private void Awake()
        {
            if (content != null) content.SetActive(false);
        }

        public void OpenForLane(int laneId)
        {
            CurrentLaneId = laneId;
            if (laneLabel != null) laneLabel.text = $"Lane {laneId}";
            if (content != null) content.SetActive(true);
        }

        // Wire a close button's OnClick to this.
        public void Close()
        {
            if (content != null) content.SetActive(false);
        }

        // Called by a MonsterCardView tap. Queues one unit for the current lane; the server validates
        // gold/level and starts the build countdown.
        public void RequestDeploy(CardDefinitionSO card)
        {
            if (deploymentManager == null || card == null) return;
            deploymentManager.RequestQueueMonsterServerRpc(new FixedString32Bytes(card.cardId), CurrentLaneId);
        }
    }
}
