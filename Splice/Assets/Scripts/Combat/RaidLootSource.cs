using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // One physical loot node/chest in the raid. Step 2 exposes a server method + ContextMenu test;
    // step 3's Hero interaction will become the validated caller.
    [RequireComponent(typeof(NetworkObject))]
    public class RaidLootSource : NetworkBehaviour
    {
        [SerializeField] private RaidLootController lootController;
        [Min(1)] [SerializeField] private int lootAmount = 10;
        [Tooltip("ปิด object ภาพนี้เมื่อเก็บหมด — เว้นว่างได้")]
        [SerializeField] private GameObject lootVisual;

        private readonly NetworkVariable<int> remaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int Remaining => remaining.Value;
        public bool IsDepleted => remaining.Value <= 0;

        public override void OnNetworkSpawn()
        {
            remaining.OnValueChanged += HandleRemainingChanged;
            if (lootController == null) lootController = FindFirstObjectByType<RaidLootController>();
            if (IsServer) remaining.Value = Mathf.Max(1, lootAmount);
            ApplyVisual(remaining.Value);
        }

        public override void OnNetworkDespawn()
        {
            remaining.OnValueChanged -= HandleRemainingChanged;
        }

        public int TryCollectAll()
        {
            if (!IsServer || !IsSpawned || IsDepleted || lootController == null) return 0;

            var moved = lootController.CarryFromAvailable(remaining.Value);
            if (moved <= 0) return 0;

            remaining.Value -= moved;
            ApplyVisual(remaining.Value);
            return moved;
        }

        private void HandleRemainingChanged(int previous, int current) => ApplyVisual(current);

        private void ApplyVisual(int amount)
        {
            if (lootVisual != null) lootVisual.SetActive(amount > 0);
        }

        [ContextMenu("Debug/Collect Loot")]
        private void DebugCollect()
        {
            var moved = TryCollectAll();
            Debug.Log($"[RaidLootSource] collected {moved}, remaining={Remaining}", this);
        }
    }
}
