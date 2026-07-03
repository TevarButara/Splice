using System.Collections.Generic;
using Splice.Network;
using Unity.Collections;
using UnityEngine;

namespace Splice.Bot
{
    // Calls the exact same ServerRpc real players use, no shortcut code path (architecture 5.4).
    // Fills empty slots in dev-test and in production when CCU is too low for a real match.
    public class BotController : MonoBehaviour
    {
        [SerializeField] private DeploymentManager deploymentManager;
        [SerializeField] private List<string> hand = new();
        [SerializeField] private float decisionIntervalSeconds = 3f;
        [SerializeField] private int laneCount = 3;

        private float timer;

        private void Update()
        {
            if (!deploymentManager.IsServer || hand.Count == 0) return;

            timer += Time.deltaTime;
            if (timer < decisionIntervalSeconds) return;

            timer = 0f;
            var cardId = hand[Random.Range(0, hand.Count)];
            var laneId = Random.Range(0, laneCount);
            deploymentManager.RequestDeployMonsterServerRpc(new FixedString32Bytes(cardId), laneId);
        }
    }
}
