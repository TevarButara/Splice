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
    // One queued build order for a lane. Server-owned; replicated so clients can render the per-card
    // stack badge and the head unit's spawn countdown without any local timing of their own.
    public struct QueuedUnit : INetworkSerializable, System.IEquatable<QueuedUnit>
    {
        public int LaneId;
        public FixedString32Bytes CardId;
        // Absolute server time (NetworkTime seconds) when the unit finishes cooking and spawns.
        // Only the lane's head carries a real value; units waiting behind it stay at 0 until promoted.
        public double SpawnAtServerTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref LaneId);
            serializer.SerializeValue(ref CardId);
            serializer.SerializeValue(ref SpawnAtServerTime);
        }

        public bool Equals(QueuedUnit other) =>
            LaneId == other.LaneId && CardId.Equals(other.CardId) && SpawnAtServerTime.Equals(other.SpawnAtServerTime);
    }

    // Server-authoritative deploy flow (technical-architecture.md 4.1).
    // Clients only ever send intent; the server validates and broadcasts the resulting state.
    //
    // Two deploy paths share the same validation:
    //   - RequestQueueMonsterServerRpc  : invader hut UI. Pays gold up front, then the unit "cooks" for
    //                                     its buildTimeSeconds before spawning. Units queue per lane (FIFO).
    //   - RequestDeployMonsterServerRpc : instant spawn. Kept for BotController and the legacy world-tap
    //                                     DeployInputController — no build time.
    public class DeploymentManager : NetworkBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [FormerlySerializedAs("deployTeam")]
        [SerializeField] private RaidSide deploySide = RaidSide.Attacker;
        [Tooltip("เส้นทางต่อเลนของ map นี้ — index = laneId. monster เกิดที่จุดเริ่มเส้นแล้วเดินตาม waypoint")]
        [SerializeField] private LanePath[] lanePaths;
        [Tooltip("กระจายจุดเกิดมอนด้านข้าง (ตั้งฉากแนวเลน, สุ่ม ±ค่านี้) — กันมอนเกิดซ้อนจุดเดียวจน separation ด้านข้างไม่มี 'seed' ให้ดันแยกตอนเดิน (มอนเดินซ้อนเป็นแถวเดียว). 0 = เกิดกลางเลนเป๊ะ")]
        [SerializeField] private float laneSpawnSpread = 0.75f;

        [Header("Monster management")]
        [Range(0f, 1f)] [SerializeField] private float sellRefundFactor = 0.5f;
        [SerializeField] private GameObject monsterUpgradeEffectPrefab;
        [SerializeField] private GameObject monsterSellEffectPrefab;
        [Min(0.1f)] [SerializeField] private float managementEffectLifetime = 3f;

        // FIFO build orders across all lanes; filter by LaneId. Server writes, everyone reads.
        private readonly NetworkList<QueuedUnit> buildQueue = new();

        public RaidSide DeploySide => deploySide;
        public int LaneCount => lanePaths?.Length ?? 0;

        // Composite id (factionId/cardId) for a card — card UI uses it to send deploy intent + match queue rows.
        public string IdOf(CardDefinitionSO card) => registry != null ? registry.IdOf(card) : null;

        public int SellRefundFor(MonsterCharacter monster) =>
            monster == null || monster.Definition == null
                ? 0
                : UnitEconomyMath.SellRefund(monster.Definition.goldCost, sellRefundFactor);

        public bool CanUpgrade(MonsterCharacter monster)
        {
            if (!CanManage(monster) || monster.Definition.nextTier == null ||
                monster.Definition.nextTier.prefab == null)
                return false;
            var bank = GoldController.For(deploySide);
            return bank != null && bank.CurrentGold >= Mathf.Max(0, monster.Definition.upgradeCost);
        }

        // Server-only scenario hook. IncomingRaidScenarioController uses the same authored lanes, prefab and
        // character initialization as normal deployment, but does not charge the local defender's wallet.
        public bool TrySpawnScenarioMonster(string cardId, int laneId, out string error)
        {
            error = string.Empty;
            if (!IsSpawned || !IsServer)
            {
                error = "Deployment server is not ready.";
                return false;
            }
            var card = registry != null ? registry.ResolveCard(cardId) : null;
            if (card == null || card.linkedMonster == null || card.linkedMonster.prefab == null)
            {
                error = $"Scenario card '{cardId}' is unavailable.";
                return false;
            }
            if (laneId < 0 || laneId >= LaneCount || lanePaths[laneId] == null || lanePaths[laneId].Count == 0)
            {
                error = $"Scenario lane {laneId} is invalid.";
                return false;
            }
            if (RaidManager.Instance != null && RaidManager.Instance.IsOver)
            {
                error = "Raid has already ended.";
                return false;
            }

            SpawnMonster(card.linkedMonster, laneId);
            return true;
        }

        // ---------- UI read helpers (client-safe) ----------

        // How many of this card are still queued/cooking in the lane. The cooking head counts — it isn't
        // on the field yet — so this is "how many left to create". Drives the stack badge.
        public int GetQueuedCount(int laneId, string cardId)
        {
            var count = 0;
            foreach (var unit in buildQueue)
            {
                if (unit.LaneId == laneId && unit.CardId.ToString() == cardId) count++;
            }
            return count;
        }

        // The unit currently cooking at the front of the lane (first match wins — the list is FIFO).
        // spawnAtServerTime <= 0 means it hasn't started cooking yet this frame.
        public bool TryGetLaneHead(int laneId, out string cardId, out double spawnAtServerTime)
        {
            foreach (var unit in buildQueue)
            {
                if (unit.LaneId != laneId) continue;
                cardId = unit.CardId.ToString();
                spawnAtServerTime = unit.SpawnAtServerTime;
                return true;
            }

            cardId = null;
            spawnAtServerTime = 0.0;
            return false;
        }

        // ---------- Queue-based deploy (invader hut UI) ----------

        [ServerRpc(RequireOwnership = false)]
        public void RequestQueueMonsterServerRpc(FixedString32Bytes cardId, int laneId, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var card = registry.ResolveCard(cardId.ToString());

            if (!ValidateDeploy(card, laneId, out var reason))
            {
                DeployRejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            // Charge at queue time so stacking N copies costs N up front — running out of gold is exactly
            // what greys the card out for the next tap.
            GoldController.For(deploySide).TrySpend(card.goldCost);
            buildQueue.Add(new QueuedUnit { LaneId = laneId, CardId = cardId, SpawnAtServerTime = 0.0 });
        }

        // Server cooks one unit at a time per lane: stamp a fresh head with its finish time, then spawn
        // and pop it once server time passes that stamp.
        private void Update()
        {
            if (!IsServer) return;
            // Match over → stop cooking the queue so no new monsters spawn after the game has ended.
            if (RaidManager.Instance != null && RaidManager.Instance.IsOver) return;

            var now = NetworkManager.ServerTime.Time;
            for (var lane = 0; lane < lanePaths.Length; lane++)
            {
                var headIndex = HeadIndex(lane);
                if (headIndex < 0) continue;

                var head = buildQueue[headIndex];
                if (head.SpawnAtServerTime <= 0.0)
                {
                    head.SpawnAtServerTime = now + BuildTimeFor(head.CardId);
                    buildQueue[headIndex] = head;
                    continue;
                }

                if (now < head.SpawnAtServerTime) continue;

                var card = registry.ResolveCard(head.CardId.ToString());
                buildQueue.RemoveAt(headIndex);
                if (card != null && card.linkedMonster != null) SpawnMonster(card.linkedMonster, lane);
            }
        }

        private int HeadIndex(int laneId)
        {
            for (var i = 0; i < buildQueue.Count; i++)
            {
                if (buildQueue[i].LaneId == laneId) return i;
            }
            return -1;
        }

        private float BuildTimeFor(FixedString32Bytes cardId)
        {
            var card = registry.ResolveCard(cardId.ToString());
            if (card == null || card.linkedMonster == null) return 0f;
            return Mathf.Max(0f, card.linkedMonster.buildTimeSeconds);
        }

        // ---------- Instant deploy (bots / legacy world-tap DeployInputController) ----------

        [ServerRpc(RequireOwnership = false)]
        public void RequestDeployMonsterServerRpc(FixedString32Bytes cardId, int laneId, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var card = registry.ResolveCard(cardId.ToString());

            if (!ValidateDeploy(card, laneId, out var reason))
            {
                DeployRejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            GoldController.For(deploySide).TrySpend(card.goldCost);
            SpawnMonster(card.linkedMonster, laneId);
            DeployAcceptedClientRpc(cardId, laneId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestUpgradeMonsterServerRpc(
            NetworkObjectReference monsterReference,
            ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!IsManagementAuthorized(clientId))
            {
                ManagementRejectedClientRpc("Monster management is not authorized", ToClient(clientId));
                return;
            }
            if (!TryResolveManagedMonster(monsterReference, out var monster))
            {
                ManagementRejectedClientRpc("Invalid monster", ToClient(clientId));
                return;
            }

            var next = monster.Definition.nextTier;
            if (next == null || next.prefab == null)
            {
                ManagementRejectedClientRpc("Already max level", ToClient(clientId));
                return;
            }

            var cost = Mathf.Max(0, monster.Definition.upgradeCost);
            var bank = GoldController.For(deploySide);
            if (bank == null || bank.CurrentGold < cost || !bank.TrySpend(cost))
            {
                ManagementRejectedClientRpc("Not enough gold", ToClient(clientId));
                return;
            }

            var position = monster.transform.position;
            var rotation = monster.transform.rotation;
            var upgradedObject = Instantiate(next.prefab, position, rotation);
            var upgradedNetworkObject = upgradedObject.GetComponent<NetworkObject>();
            var upgradedMonster = upgradedObject.GetComponent<MonsterCharacter>();
            if (upgradedNetworkObject == null || upgradedMonster == null)
            {
                bank.Add(cost);
                Destroy(upgradedObject);
                ManagementRejectedClientRpc("Invalid next-tier prefab", ToClient(clientId));
                return;
            }

            upgradedNetworkObject.Spawn();
            upgradedMonster.InitializeUpgradeFrom(next, monster);
            PlayMonsterManagementFxClientRpc(MonsterManagementFx.Upgrade, position, rotation);
            var oldNetworkObject = monster.NetworkObject;
            oldNetworkObject.Despawn(destroy: oldNetworkObject.IsSceneObject != true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSellMonsterServerRpc(
            NetworkObjectReference monsterReference,
            ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            if (!IsManagementAuthorized(clientId))
            {
                ManagementRejectedClientRpc("Monster management is not authorized", ToClient(clientId));
                return;
            }
            if (!TryResolveManagedMonster(monsterReference, out var monster))
            {
                ManagementRejectedClientRpc("Invalid monster", ToClient(clientId));
                return;
            }

            var refund = SellRefundFor(monster);
            if (refund > 0) GoldController.For(deploySide)?.Add(refund);
            var position = monster.transform.position;
            var rotation = monster.transform.rotation;
            PlayMonsterManagementFxClientRpc(MonsterManagementFx.Sell, position, rotation);
            var networkObject = monster.NetworkObject;
            networkObject.Despawn(destroy: networkObject.IsSceneObject != true);
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

            if (card.requiredLevel > PlayerProgression.LevelFor(deploySide))
            {
                reason = "Level too low";
                return false;
            }

            var bank = GoldController.For(deploySide);
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

        private bool CanManage(MonsterCharacter monster) =>
            monster != null && !monster.IsDead && monster.IsSpawned &&
            monster.Definition != null && monster.Side == deploySide;

        private bool IsManagementAuthorized(ulong clientId) =>
            NetworkManager != null &&
            UnitManagementAuthority.IsAuthorized(clientId, NetworkManager.ServerClientId);

        private bool TryResolveManagedMonster(
            NetworkObjectReference monsterReference,
            out MonsterCharacter monster)
        {
            monster = null;
            return monsterReference.TryGet(out var networkObject) &&
                   networkObject.TryGetComponent(out monster) &&
                   CanManage(monster);
        }

        private void SpawnMonster(MonsterDefinitionSO definition, int laneId)
        {
            var lane = lanePaths[laneId];
            var instance = Instantiate(definition.prefab, lane.Start, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn();
            // Initialize ตั้ง position = lane.Start (เป๊ะจุดเดียว) → หลังจากนั้นค่อยเขี่ยด้านข้างแบบสุ่ม
            // ให้แต่ละตัวไม่อยู่บนเส้นกลางเป๊ะ → separation ด้านข้างมี offset ให้ทำงานตอนเดิน (ไม่ซ้อนเป็นแถวเดียว)
            instance.GetComponent<MonsterCharacter>().Initialize(definition, lane);
            if (laneSpawnSpread > 0f)
                instance.transform.position += LaneLateral(lane) * Random.Range(-laneSpawnSpread, laneSpawnSpread);
        }

        // ทิศตั้งฉากแนวเลน (ระนาบ XZ) ที่จุดเริ่ม — ใช้เขี่ยจุดเกิดออกด้านข้าง
        private static Vector3 LaneLateral(LanePath lane)
        {
            var dir = lane.Count >= 2 ? lane.GetPoint(1) - lane.GetPoint(0) : Vector3.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            var lateral = Vector3.Cross(dir.normalized, Vector3.up);
            return lateral.sqrMagnitude > 0.0001f ? lateral.normalized : Vector3.right;
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

        private enum MonsterManagementFx : byte
        {
            Upgrade,
            Sell
        }

        [ClientRpc]
        private void PlayMonsterManagementFxClientRpc(
            MonsterManagementFx action,
            Vector3 position,
            Quaternion rotation)
        {
            var prefab = action == MonsterManagementFx.Upgrade
                ? monsterUpgradeEffectPrefab
                : monsterSellEffectPrefab;
            if (prefab == null) return;
            var instance = Instantiate(prefab, position, rotation);
            Destroy(instance, managementEffectLifetime);
        }

        [ClientRpc]
        private void ManagementRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"Monster management rejected: {reason}");
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
