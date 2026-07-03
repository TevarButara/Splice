using Splice.Characters;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Network
{
    // Server-authoritative tower placement for the Fort/Defender side (architecture 5.6).
    // Mirrors DeploymentManager, but towers are placed at a free world position (not a lane) since
    // towers don't reroute monsters — they just need to sit within attackRange of a lane to shoot.
    // Clients send intent (towerId + position); the server validates gold and spawns.
    public class TowerDeploymentManager : NetworkBehaviour
    {
        [SerializeField] private TowerDatabaseSO towerDatabase;
        [SerializeField] private Team deployTeam = Team.Defenders;

        // TEMP: tunable factors live here for now. Part B (main balance config) will feed these instead of the Inspector.
        [Header("Cost factors (จะย้ายเข้า main config — ข้อ B)")]
        [Tooltip("ค่าซ่อม = ceil(goldCost × HPที่หาย/maxHP × ค่านี้). 0.5 = ซ่อมถูกกว่าสร้างใหม่")]
        [SerializeField] private float repairFactor = 0.5f;
        [Tooltip("คืนเงินตอนทำลาย = floor(goldCost × HPเหลือ/maxHP × ค่านี้). 1 = คืนตามสัดส่วน HP เต็มที่")]
        [SerializeField] private float demolishRefundFactor = 1f;

        [ServerRpc(RequireOwnership = false)]
        public void RequestDeployTowerServerRpc(FixedString32Bytes towerId, Vector3 position, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var tower = towerDatabase.GetById(towerId.ToString());

            if (!ValidateDeploy(tower, out var reason))
            {
                DeployRejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            GoldController.For(deployTeam).TrySpend(tower.goldCost);
            SpawnTower(tower, position, Quaternion.identity);
            DeployAcceptedClientRpc(towerId, position);
        }

        // Repair a damaged tower/Fort back to full HP. Cost scales with the fraction of HP lost and the
        // tower's build price: ceil(goldCost × missing/maxHP × repairFactor). Rounds UP (never free while damaged).
        [ServerRpc(RequireOwnership = false)]
        public void RequestRepairTowerServerRpc(NetworkObjectReference towerRef, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;

            if (!TryResolveTower(towerRef, out var tower))
            {
                TowerActionRejectedClientRpc("Invalid tower", ToClient(clientId));
                return;
            }

            var missing = tower.MaxHealth - tower.CurrentHealth;
            if (missing <= 0) return; // already full — no-op, no cost

            var cost = Mathf.CeilToInt(tower.Definition.goldCost * ((float)missing / tower.MaxHealth) * repairFactor);
            var bank = GoldController.For(deployTeam);
            if (bank == null || bank.CurrentGold < cost)
            {
                TowerActionRejectedClientRpc("Not enough gold", ToClient(clientId));
                return;
            }

            bank.TrySpend(cost);
            tower.Heal(missing);
        }

        // Demolish a tower to free up space, refunding gold by remaining HP:
        // floor(goldCost × current/maxHP × demolishRefundFactor). Rounds DOWN (0.5 → 0). The Fort can't be demolished.
        [ServerRpc(RequireOwnership = false)]
        public void RequestDemolishTowerServerRpc(NetworkObjectReference towerRef, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;

            if (!TryResolveTower(towerRef, out var tower))
            {
                TowerActionRejectedClientRpc("Invalid tower", ToClient(clientId));
                return;
            }

            if (tower is FortCore)
            {
                TowerActionRejectedClientRpc("Cannot demolish the Fort", ToClient(clientId));
                return;
            }

            var refund = Mathf.FloorToInt(tower.Definition.goldCost * ((float)tower.CurrentHealth / tower.MaxHealth) * demolishRefundFactor);
            if (refund > 0) GoldController.For(deployTeam)?.Add(refund);

            var netObj = tower.NetworkObject;
            netObj.Despawn(destroy: netObj.IsSceneObject != true);
        }

        // Upgrade a tower to its next tier for a flat upgradeCost. The old tower is replaced in place by the
        // next-tier prefab, spawned at FULL HP. The Fort has no upgrade path here.
        [ServerRpc(RequireOwnership = false)]
        public void RequestUpgradeTowerServerRpc(NetworkObjectReference towerRef, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;

            if (!TryResolveTower(towerRef, out var tower))
            {
                TowerActionRejectedClientRpc("Invalid tower", ToClient(clientId));
                return;
            }

            if (tower is FortCore)
            {
                TowerActionRejectedClientRpc("Cannot upgrade the Fort", ToClient(clientId));
                return;
            }

            var next = tower.Definition.nextTier;
            if (next == null || next.prefab == null)
            {
                TowerActionRejectedClientRpc("Already max level", ToClient(clientId));
                return;
            }

            var cost = tower.Definition.upgradeCost;
            var bank = GoldController.For(deployTeam);
            if (bank == null || bank.CurrentGold < cost)
            {
                TowerActionRejectedClientRpc("Not enough gold", ToClient(clientId));
                return;
            }

            var position = tower.transform.position;
            var rotation = tower.transform.rotation;
            bank.TrySpend(cost);

            var oldNetObj = tower.NetworkObject;
            oldNetObj.Despawn(destroy: oldNetObj.IsSceneObject != true);
            SpawnTower(next, position, rotation);
        }

        private bool TryResolveTower(NetworkObjectReference towerRef, out TowerCharacter tower)
        {
            tower = null;
            if (!towerRef.TryGet(out var netObj) || !netObj.TryGetComponent(out tower) || tower.IsDead) return false;
            return tower.Definition != null;
        }

        private bool ValidateDeploy(TowerDefinitionSO tower, out string reason)
        {
            if (tower == null || tower.prefab == null)
            {
                reason = "Unknown tower";
                return false;
            }

            var bank = GoldController.For(deployTeam);
            if (bank == null)
            {
                reason = "No gold controller for team";
                return false;
            }

            if (bank.CurrentGold < tower.goldCost)
            {
                reason = "Not enough gold";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void SpawnTower(TowerDefinitionSO definition, Vector3 position, Quaternion rotation)
        {
            var instance = Instantiate(definition.prefab, position, rotation);
            instance.GetComponent<NetworkObject>().Spawn();
            instance.GetComponent<TowerCharacter>().Initialize(definition);
        }

        [ClientRpc]
        private void DeployAcceptedClientRpc(FixedString32Bytes towerId, Vector3 position)
        {
            // TODO: play build feedback on all clients.
        }

        [ClientRpc]
        private void DeployRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"Tower deploy rejected: {reason}");
        }

        [ClientRpc]
        private void TowerActionRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"Tower action rejected: {reason}");
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
