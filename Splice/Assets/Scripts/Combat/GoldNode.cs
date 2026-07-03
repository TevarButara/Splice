using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // A finite gold deposit on the map (architecture 5.7). Miners walk here, mine until it is
    // depleted, then move on to the next-nearest node — so far-apart nodes mean slower income.
    public class GoldNode : NetworkBehaviour
    {
        private static readonly List<GoldNode> active = new();
        public static IReadOnlyList<GoldNode> Active => active;

        [SerializeField] private int totalGold = 500;

        private readonly NetworkVariable<int> remaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Collider bodyCollider;

        public int Remaining => remaining.Value;
        public int TotalGold => totalGold;
        public bool IsDepleted => remaining.Value <= 0;

        private void Awake()
        {
            bodyCollider = GetComponent<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            active.Add(this);
            // Scene-placed nodes seed their reserve from the Inspector value on spawn.
            if (remaining.Value <= 0) remaining.Value = totalGold;
        }

        // Distance from a point to the node's collider surface (0 if the point is inside/touching).
        // Lets a miner count as "arrived" when it bumps the collider, not only at the exact pivot —
        // so arrivalRadius need not scale with node size. Falls back to pivot distance if no collider.
        public float DistanceToSurface(Vector3 from)
        {
            if (bodyCollider == null) return Vector3.Distance(from, transform.position);
            return Vector3.Distance(from, bodyCollider.ClosestPoint(from));
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
        }

        // Server-only: pull up to amount, return how much was actually mined (may be less near depletion).
        public int Mine(int amount)
        {
            if (!IsServer || remaining.Value <= 0 || amount <= 0) return 0;
            var mined = Mathf.Min(amount, remaining.Value);
            remaining.Value -= mined;
            return mined;
        }
    }
}
