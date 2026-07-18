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
    // One queued miner build order. Server-owned; replicated so the miner card UI can show the stack count
    // and the head unit's build countdown (mirrors QueuedUnit for monsters, but there are no lanes).
    public struct QueuedMiner : INetworkSerializable, System.IEquatable<QueuedMiner>
    {
        public FixedString32Bytes CardId;
        public double SpawnAtServerTime; // >0 only for the head (currently building)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CardId);
            serializer.SerializeValue(ref SpawnAtServerTime);
        }

        public bool Equals(QueuedMiner other) =>
            CardId.Equals(other.CardId) && SpawnAtServerTime.Equals(other.SpawnAtServerTime);
    }

    // Server-authoritative miner purchasing (architecture 5.7). Same buy → build-time → spawn flow as the
    // invader monster hut, but for the team's economy: pay gold up front, the miner "builds" for its
    // buildTimeSeconds, then spawns at this team's miner spawn point and works autonomously (MinerCharacter).
    // One FIFO queue (no lanes); you can stack purchases. Faction-aware via the FactionRegistry.
    public class MinerDeploymentManager : NetworkBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [FormerlySerializedAs("deployTeam")]
        [SerializeField] private RaidSide deploySide = RaidSide.Attacker;
        [Tooltip("จุดเกิด miner บนแมป (ของทีมนี้) — เว้นว่าง = เกิดที่ตำแหน่ง manager นี้")]
        [SerializeField] private Transform spawnPoint;

        private readonly NetworkList<QueuedMiner> buildQueue = new();

        public RaidSide DeploySide => deploySide;

        // Composite id (factionId/cardId) — the miner card UI uses it to send intent + match queue rows.
        public string IdOf(CardDefinitionSO card) => registry != null ? registry.IdOf(card) : null;

        // ---------- UI read helpers (client-safe) ----------

        public int GetQueuedCount(string cardId)
        {
            var count = 0;
            foreach (var unit in buildQueue)
                if (unit.CardId.ToString() == cardId) count++;
            return count;
        }

        // The miner currently building at the front of the queue (FIFO — first entry).
        public bool TryGetHead(out string cardId, out double spawnAtServerTime)
        {
            if (buildQueue.Count > 0)
            {
                cardId = buildQueue[0].CardId.ToString();
                spawnAtServerTime = buildQueue[0].SpawnAtServerTime;
                return true;
            }

            cardId = null;
            spawnAtServerTime = 0.0;
            return false;
        }

        // ---------- Buy ----------

        [ServerRpc(RequireOwnership = false)]
        public void RequestQueueMinerServerRpc(FixedString32Bytes cardId, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var card = registry.ResolveCard(cardId.ToString());

            if (!Validate(card, out var reason))
            {
                RejectedClientRpc(reason, ToClient(clientId));
                return;
            }

            // Charge up front so stacking N costs N — running out of gold greys the card for the next tap.
            GoldController.For(deploySide).TrySpend(card.goldCost);
            buildQueue.Add(new QueuedMiner { CardId = cardId, SpawnAtServerTime = 0.0 });
        }

        // Server builds one miner at a time: stamp the head's finish time, then spawn & pop it when reached.
        private void Update()
        {
            if (!IsServer) return;
            if (RaidManager.Instance != null && RaidManager.Instance.IsOver) return;
            if (buildQueue.Count == 0) return;

            var now = NetworkManager.ServerTime.Time;
            var head = buildQueue[0];

            if (head.SpawnAtServerTime <= 0.0)
            {
                head.SpawnAtServerTime = now + BuildTimeFor(head.CardId);
                buildQueue[0] = head;
                return;
            }

            if (now < head.SpawnAtServerTime) return;

            var card = registry.ResolveCard(head.CardId.ToString());
            buildQueue.RemoveAt(0);
            if (card != null && card.linkedMiner != null) SpawnMiner(card.linkedMiner);
        }

        private float BuildTimeFor(FixedString32Bytes cardId)
        {
            var card = registry.ResolveCard(cardId.ToString());
            if (card == null || card.linkedMiner == null) return 0f;
            return Mathf.Max(0f, card.linkedMiner.buildTimeSeconds);
        }

        private void SpawnMiner(MinerDefinitionSO definition)
        {
            var pos = spawnPoint != null ? spawnPoint.position : transform.position;
            var instance = Instantiate(definition.prefab, pos, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn();
            instance.GetComponent<MinerCharacter>().Initialize(definition, deploySide);
        }

        private bool Validate(CardDefinitionSO card, out string reason)
        {
            if (card == null || card.linkedMiner == null)
            {
                reason = "Unknown miner card";
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

        [ClientRpc]
        private void RejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"Miner buy rejected: {reason}");
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
