using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Network
{
    // Server-authoritative deploy flow (technical-architecture.md 4.1).
    // Clients only ever send intent; the server validates and broadcasts the resulting state.
    public class DeploymentManager : NetworkBehaviour
    {
        [ServerRpc(RequireOwnership = false)]
        public void RequestDeployMonsterServerRpc(FixedString32Bytes cardId, int laneId, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;

            if (!ValidateDeploy(clientId, cardId, laneId, out var reason))
            {
                DeployRejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            // TODO: spend mana, spawn NetworkObject, resolve combat.
            DeployAcceptedClientRpc(cardId, laneId);
        }

        private bool ValidateDeploy(ulong clientId, FixedString32Bytes cardId, int laneId, out string reason)
        {
            // TODO: check mana, cooldown, and card ownership against server-side player state.
            reason = string.Empty;
            return true;
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
