using Splice.Characters;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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
        [SerializeField] private Team deployTeam = Team.Invaders;
        [Tooltip("เส้นทางต่อเลนของ map นี้ — index = laneId. monster เกิดที่จุดเริ่มเส้นแล้วเดินตาม waypoint")]
        [SerializeField] private LanePath[] lanePaths;

        // FIFO build orders across all lanes; filter by LaneId. Server writes, everyone reads.
        private readonly NetworkList<QueuedUnit> buildQueue = new();

        public Team DeployTeam => deployTeam;

        // Composite id (factionId/cardId) for a card — card UI uses it to send deploy intent + match queue rows.
        public string IdOf(CardDefinitionSO card) => registry != null ? registry.IdOf(card) : null;

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
            GoldController.For(deployTeam).TrySpend(card.goldCost);
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

            if (card.requiredLevel > PlayerProgression.LevelFor(deployTeam))
            {
                reason = "Level too low";
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
