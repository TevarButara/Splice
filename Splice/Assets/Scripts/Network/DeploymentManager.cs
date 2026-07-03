using Splice.Characters;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Network
{
    // Server-authoritative deploy flow (technical-architecture.md 4.1).
    // Clients only ever send intent; the server validates and broadcasts the resulting state.
    public class DeploymentManager : NetworkBehaviour
    {
        [SerializeField] private CardDatabaseSO cardDatabase;
        [SerializeField] private Team deployTeam = Team.Invaders;
        [Tooltip("เส้นทางต่อเลนของ map นี้ — index = laneId. monster เกิดที่จุดเริ่มเส้นแล้วเดินตาม waypoint")]
        [SerializeField] private LanePath[] lanePaths;

        [ServerRpc(RequireOwnership = false)]
        public void RequestDeployMonsterServerRpc(FixedString32Bytes cardId, int laneId, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var card = cardDatabase.GetById(cardId.ToString());

            if (!ValidateDeploy(card, laneId, out var reason))
            {
                DeployRejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            GoldController.For(deployTeam).TrySpend(card.goldCost);
            SpawnMonster(card.linkedMonster, laneId);
            DeployAcceptedClientRpc(cardId, laneId);
        }

        private bool ValidateDeploy(CardDefinitionSO card, int laneId, out string reason)
        {
            if (card == null || card.linkedMonster == null)
            {
                reason = "Unknown card";
                return false;
            }

            if (laneId < 0 || laneId >= lanePaths.Length || lanePaths[laneId] == null || lanePaths[laneId].Count == 0)
            {
                reason = "Invalid lane";
                return false;
            }

            var bank = GoldController.For(deployTeam);
            if (bank == null)
            {
                reason = "No gold controller for team";
                return false;
            }

            if (bank.CurrentGold < card.goldCost)
            {
                reason = "Not enough gold";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void SpawnMonster(MonsterDefinitionSO definition, int laneId)
        {
            var lane = lanePaths[laneId];
            var instance = Instantiate(definition.prefab, lane.Start, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn();
            instance.GetComponent<MonsterCharacter>().Initialize(definition, lane);
        }

        [ClientRpc]
        private void DeployAcceptedClientRpc(FixedString32Bytes cardId, int laneId)
        {
            // TODO: play spawn feedback on all clients.
        }

        [ClientRpc]
        private void DeployRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"Deploy rejected: {reason}");
        }

        private ClientRpcParams ToClient(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
        }
    }
}
