using System.Collections;
using System.Collections.Generic;
using Splice.Characters;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Network
{
    // Server-authoritative tower placement for the Fort/Defender side (architecture 5.6).
    // Mirrors DeploymentManager, but towers are placed at a free world position (not a lane) since
    // towers don't reroute monsters — they just need to sit within attackRange of a lane to shoot.
    // Clients send intent (towerId + position); the server validates gold and spawns.
    public class TowerDeploymentManager : NetworkBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [FormerlySerializedAs("deployTeam")]
        [SerializeField] private RaidSide deploySide = RaidSide.Defender;

        // TEMP: tunable factors live here for now. Part B (main balance config) will feed these instead of the Inspector.
        [Header("Cost factors (จะย้ายเข้า main config — ข้อ B)")]
        [Tooltip("ค่าซ่อม = ceil(goldCost × HPที่หาย/maxHP × ค่านี้). 0.5 = ซ่อมถูกกว่าสร้างใหม่")]
        [SerializeField] private float repairFactor = 0.5f;
        [Tooltip("คืนเงินตอนทำลาย = floor(goldCost × HPเหลือ/maxHP × ค่านี้). 1 = คืนตามสัดส่วน HP เต็มที่")]
        [SerializeField] private float demolishRefundFactor = 1f;

        [Header("Repair timing")]
        [Tooltip("เวลาซ่อมจาก 0 → HP เต็ม. ถ้าเสีย 50% จะใช้ครึ่งหนึ่งของเวลานี้")]
        [Min(0.1f)] [SerializeField] private float fullRepairDurationSeconds = 10f;
        [Tooltip("ความถี่ที่ server เติม HP ระหว่างซ่อม")]
        [Min(0.1f)] [SerializeField] private float repairTickSeconds = 1f;

        [Header("Tower action FX (presentation only)")]
        [SerializeField] private GameObject repairLoopPrefab;
        [SerializeField] private GameObject repairCompleteEffectPrefab;
        [SerializeField] private GameObject upgradeEffectPrefab;
        [SerializeField] private GameObject destroyEffectPrefab;
        [SerializeField] private Vector3 repairVisualOffset = new(1.5f, 0f, 0f);
        [Min(0.1f)] [SerializeField] private float oneShotEffectLifetime = 3f;

        [Header("Placement grid")]
        [Tooltip("กติกา grid วางป้อม — แชร์โค้ดเดียวกับ Build Mode (BaseBuildManager) ผ่าน BuildGrid")]
        [SerializeField] private BuildGrid grid = new();

        public RaidSide DeploySide => deploySide;
        private readonly Dictionary<ulong, Coroutine> repairJobs = new();
        private readonly Dictionary<ulong, GameObject> repairVisuals = new();

        // Composite id (factionId/towerId) ↔ definition — used by the tower card UI + placement preview.
        public string IdOf(TowerDefinitionSO tower) => registry != null ? registry.IdOf(tower) : null;
        public TowerDefinitionSO Resolve(string id) => registry != null ? registry.ResolveTower(id) : null;

        public int RepairCostFor(TowerCharacter tower) =>
            tower == null || tower.Definition == null
                ? 0
                : UnitEconomyMath.RepairCost(
                    tower.Definition.goldCost,
                    tower.CurrentHealth,
                    tower.MaxHealth,
                    repairFactor);

        public int SellRefundFor(TowerCharacter tower) =>
            tower == null || tower.Definition == null
                ? 0
                : UnitEconomyMath.SellRefund(tower.Definition.goldCost, demolishRefundFactor);

        public bool CanRepair(TowerCharacter tower)
        {
            if (tower == null || tower.IsDead || tower.IsRepairing || tower.CurrentHealth >= tower.MaxHealth)
                return false;
            var bank = GoldController.For(deploySide);
            return bank != null && bank.CurrentGold >= RepairCostFor(tower);
        }

        public bool CanUpgrade(TowerCharacter tower)
        {
            if (tower == null || tower.IsDead || tower is FortCore || tower.Definition == null ||
                tower.Definition.nextTier == null || tower.Definition.nextTier.prefab == null)
                return false;
            var bank = GoldController.For(deploySide);
            return bank != null && bank.CurrentGold >= Mathf.Max(0, tower.Definition.upgradeCost);
        }

        // Snap a world position to the centre of its grid cell (XZ; y is resolved later by the build-zone probe).
        public Vector3 SnapToCell(Vector3 world) => grid.SnapToCell(world);

        // Snap to a cell and confirm it's buildable: centre sits over the build zone AND no tower already
        // occupies it. On success `cell` carries the ground height to spawn at. Shared by the server RPC
        // (authority) and the client preview (green/red), so the rule lives in exactly one place.
        public bool TryGetBuildCell(Vector3 world, out Vector3 cell)
        {
            if (!grid.TryGetGroundCell(world, out cell)) return false;
            return !IsCellOccupied(cell);
        }

        private bool IsCellOccupied(Vector3 cell)
        {
            var towers = TowerCharacter.Active;
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null || tower.IsDead) continue;
                if (grid.SameCell(tower.transform.position, cell)) return true;
            }
            return false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestDeployTowerServerRpc(FixedString32Bytes towerId, Vector3 position, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var tower = registry.ResolveTower(towerId.ToString());

            if (!ValidateDeploy(tower, out var reason))
            {
                DeployRejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            // Grid rule: snap to a cell that sits over the build zone and isn't already taken.
            if (!TryGetBuildCell(position, out var cell))
            {
                DeployRejectedClientRpc("Cannot build here", ToClient(clientId));
                return;
            }

            GoldController.For(deploySide).TrySpend(tower.goldCost);
            SpawnTower(tower, cell, Quaternion.identity);
            DeployAcceptedClientRpc(towerId, cell);
        }

        // Repair a damaged tower/Fort back to full HP. Cost scales with the fraction of HP lost and the
        // tower's build price: ceil(goldCost × missing/maxHP × repairFactor). Rounds UP (never free while damaged).
        [ServerRpc(RequireOwnership = false)]
        public void RequestRepairTowerServerRpc(NetworkObjectReference towerRef, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!IsManagementAuthorized(clientId))
            {
                TowerActionRejectedClientRpc("Tower management is not authorized", ToClient(clientId));
                return;
            }

            if (!TryResolveTower(towerRef, out var tower))
            {
                TowerActionRejectedClientRpc("Invalid tower", ToClient(clientId));
                return;
            }

            var missing = tower.MaxHealth - tower.CurrentHealth;
            if (missing <= 0) return; // already full — no-op, no cost
            if (tower.IsRepairing) return;

            var cost = RepairCostFor(tower);
            var bank = GoldController.For(deploySide);
            if (bank == null || bank.CurrentGold < cost)
            {
                TowerActionRejectedClientRpc("Not enough gold", ToClient(clientId));
                return;
            }

            bank.TrySpend(cost);
            var duration = Mathf.Max(
                repairTickSeconds,
                fullRepairDurationSeconds * missing / Mathf.Max(1f, tower.MaxHealth));
            tower.SetRepairing(true);
            var networkId = tower.NetworkObjectId;
            repairJobs[networkId] = StartCoroutine(RepairOverTime(tower, missing, duration));
            StartRepairFeedbackClientRpc(tower.NetworkObject, duration);
        }

        // Demolish a tower to free up space, refunding gold by remaining HP:
        // floor(goldCost × current/maxHP × demolishRefundFactor). Rounds DOWN (0.5 → 0). The Fort can't be demolished.
        [ServerRpc(RequireOwnership = false)]
        public void RequestDemolishTowerServerRpc(NetworkObjectReference towerRef, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!IsManagementAuthorized(clientId))
            {
                TowerActionRejectedClientRpc("Tower management is not authorized", ToClient(clientId));
                return;
            }

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

            var refund = SellRefundFor(tower);
            if (refund > 0) GoldController.For(deploySide)?.Add(refund);

            var netObj = tower.NetworkObject;
            CancelRepair(tower, false);
            PlayOneShotFeedbackClientRpc(TowerActionFeedback.Destroy, tower.transform.position, tower.transform.rotation);
            netObj.Despawn(destroy: netObj.IsSceneObject != true);
        }

        // Upgrade a tower to its next tier for a flat upgradeCost. The old tower is replaced in place by the
        // next-tier prefab, spawned at FULL HP. The Fort has no upgrade path here.
        [ServerRpc(RequireOwnership = false)]
        public void RequestUpgradeTowerServerRpc(NetworkObjectReference towerRef, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!IsManagementAuthorized(clientId))
            {
                TowerActionRejectedClientRpc("Tower management is not authorized", ToClient(clientId));
                return;
            }

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

            var cost = Mathf.Max(0, tower.Definition.upgradeCost);
            var bank = GoldController.For(deploySide);
            if (bank == null || bank.CurrentGold < cost)
            {
                TowerActionRejectedClientRpc("Not enough gold", ToClient(clientId));
                return;
            }

            var position = tower.transform.position;
            var rotation = tower.transform.rotation;
            bank.TrySpend(cost);

            var oldNetObj = tower.NetworkObject;
            CancelRepair(tower, false);
            PlayOneShotFeedbackClientRpc(TowerActionFeedback.Upgrade, position, rotation);
            oldNetObj.Despawn(destroy: oldNetObj.IsSceneObject != true);
            SpawnTower(next, position, rotation);
        }

        // Upgrade ONE stat of a tower (attack/HP/armor/range/targets). Cost grows each level
        // (baseCost × growth^level). Separate from the tier chain — this keeps the same tower. Not the Fort.
        [ServerRpc(RequireOwnership = false)]
        public void RequestUpgradeStatServerRpc(NetworkObjectReference towerRef, TowerStat stat, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!IsManagementAuthorized(clientId))
            {
                TowerActionRejectedClientRpc("Tower management is not authorized", ToClient(clientId));
                return;
            }

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

            var upgrade = tower.Definition.UpgradeFor(stat);
            var level = tower.UpgradeLevel(stat);
            if (upgrade.maxLevel <= 0 || level >= upgrade.maxLevel)
            {
                TowerActionRejectedClientRpc("Already max level", ToClient(clientId));
                return;
            }

            var cost = upgrade.CostForLevel(level);
            var bank = GoldController.For(deploySide);
            if (bank == null || bank.CurrentGold < cost)
            {
                TowerActionRejectedClientRpc("Not enough gold", ToClient(clientId));
                return;
            }

            bank.TrySpend(cost);
            tower.ApplyStatUpgrade(stat);
        }

        private bool TryResolveTower(NetworkObjectReference towerRef, out TowerCharacter tower)
        {
            tower = null;
            if (!towerRef.TryGet(out var netObj) || !netObj.TryGetComponent(out tower) || tower.IsDead) return false;
            return tower.Definition != null;
        }

        private bool IsManagementAuthorized(ulong clientId) =>
            NetworkManager != null &&
            UnitManagementAuthority.IsAuthorized(clientId, NetworkManager.ServerClientId);

        private bool ValidateDeploy(TowerDefinitionSO tower, out string reason)
        {
            if (tower == null || tower.prefab == null)
            {
                reason = "Unknown tower";
                return false;
            }

            var bank = GoldController.For(deploySide);
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

        private IEnumerator RepairOverTime(TowerCharacter tower, int missingHealth, float duration)
        {
            var networkId = tower.NetworkObjectId;
            var stepCount = Mathf.Max(1, Mathf.CeilToInt(duration / repairTickSeconds));
            for (var step = 1; step <= stepCount; step++)
            {
                yield return new WaitForSeconds(duration / stepCount);
                if (tower == null || tower.IsDead || !tower.IsSpawned)
                {
                    repairJobs.Remove(networkId);
                    StopRepairFeedbackClientRpc(networkId, false, default);
                    yield break;
                }

                tower.Heal(UnitEconomyMath.RepairAmountAtStep(missingHealth, step, stepCount));
            }

            tower.SetRepairing(false);
            repairJobs.Remove(networkId);
            StopRepairFeedbackClientRpc(networkId, true, tower.transform.position);
        }

        private void CancelRepair(TowerCharacter tower, bool completed)
        {
            if (tower == null) return;
            var networkId = tower.NetworkObjectId;
            if (repairJobs.Remove(networkId, out var job) && job != null) StopCoroutine(job);
            tower.SetRepairing(false);
            StopRepairFeedbackClientRpc(networkId, completed, tower.transform.position);
        }

        private enum TowerActionFeedback : byte
        {
            Upgrade,
            Destroy
        }

        [ClientRpc]
        private void StartRepairFeedbackClientRpc(NetworkObjectReference towerReference, float duration)
        {
            if (!towerReference.TryGet(out var networkObject)) return;
            var networkId = networkObject.NetworkObjectId;
            RemoveRepairVisual(networkId);
            if (repairLoopPrefab == null) return;
            var visual = Instantiate(
                repairLoopPrefab,
                networkObject.transform.position + repairVisualOffset,
                networkObject.transform.rotation);
            visual.transform.SetParent(networkObject.transform, true);
            repairVisuals[networkId] = visual;
            Destroy(visual, duration + 1f);
        }

        [ClientRpc]
        private void StopRepairFeedbackClientRpc(ulong networkId, bool completed, Vector3 position)
        {
            RemoveRepairVisual(networkId);
            if (completed) SpawnLocalEffect(repairCompleteEffectPrefab, position, Quaternion.identity);
        }

        [ClientRpc]
        private void PlayOneShotFeedbackClientRpc(
            TowerActionFeedback action,
            Vector3 position,
            Quaternion rotation)
        {
            SpawnLocalEffect(
                action == TowerActionFeedback.Upgrade ? upgradeEffectPrefab : destroyEffectPrefab,
                position,
                rotation);
        }

        private void RemoveRepairVisual(ulong networkId)
        {
            if (!repairVisuals.Remove(networkId, out var visual) || visual == null) return;
            Destroy(visual);
        }

        private void SpawnLocalEffect(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return;
            var instance = Instantiate(prefab, position, rotation);
            Destroy(instance, oneShotEffectLifetime);
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
