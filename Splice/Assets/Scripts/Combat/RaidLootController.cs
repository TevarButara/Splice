using Splice.Base;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // Server-authoritative ledger for loot inside one raid (new direction §8).
    // Amounts move in one direction during normal play:
    // Available -> Carried -> Secured -> Banked.
    // Dropping carried loot is the one explicit rollback: Carried -> Available.
    public class RaidLootController : NetworkBehaviour
    {
        [Tooltip("เปอร์เซ็นต์ทองคลังเป้าหมายที่กลายเป็น loot สูงสุดของ raid นี้")]
        [Range(0f, 1f)]
        [SerializeField] private float targetLootPercent = 0.2f;

        [Tooltip("จำนวน loot สำหรับทดสอบเมื่อเข้า raid โดยไม่มี RaidContext.Target")]
        [Min(0)]
        [SerializeField] private int debugAvailableLoot = 100;

        private readonly NetworkVariable<int> available = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> carried = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> secured = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> banked = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool settled;

        public int Available => available.Value;
        public int Carried => carried.Value;
        public int Secured => secured.Value;
        public int Banked => banked.Value;
        public int PotentialTotal => available.Value + carried.Value + secured.Value + banked.Value;
        public bool IsSettled => settled;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            var initial = RaidContext.HasTarget
                ? Mathf.FloorToInt(RaidContext.Target.StoredGold * targetLootPercent)
                : debugAvailableLoot;

            available.Value = Mathf.Max(0, initial);
            carried.Value = 0;
            secured.Value = 0;
            banked.Value = 0;
            settled = false;
        }

        // Called by a server-side loot source after validating the actor/interact request.
        // Returns the amount actually moved so a partially depleted source can update itself correctly.
        public int CarryFromAvailable(int requestedAmount)
        {
            if (!CanMutate() || requestedAmount <= 0) return 0;

            var moved = Mathf.Min(requestedAmount, available.Value);
            available.Value -= moved;
            carried.Value += moved;
            return moved;
        }

        // Checkpoints/vault caches call this on the server. Returns the amount newly secured.
        public int SecureCarried()
        {
            if (!CanMutate() || carried.Value <= 0) return 0;

            var moved = carried.Value;
            carried.Value = 0;
            secured.Value += moved;
            return moved;
        }

        // Future Hero death/porter death path. In this prototype it returns loot to the abstract
        // available city pool; a later visual step can respawn a physical drop at the death position.
        public int DropCarried()
        {
            if (!CanMutate() || carried.Value <= 0) return 0;

            var moved = carried.Value;
            carried.Value = 0;
            available.Value += moved;
            return moved;
        }

        // Exactly-once local prototype settlement. RaidRewardController is the only caller that credits
        // PlayerWallet; this ledger only decides how much the outcome is allowed to bank.
        public bool TrySettle(RaidOutcome outcome, out int amount)
        {
            amount = 0;
            if (!IsServer || settled || outcome == RaidOutcome.InProgress) return false;

            settled = true;
            amount = outcome switch
            {
                RaidOutcome.FullVictory => available.Value + carried.Value + secured.Value,
                RaidOutcome.Extracted => secured.Value,
                _ => 0
            };

            banked.Value = Mathf.Max(0, amount);
            available.Value = 0;
            carried.Value = 0;
            secured.Value = 0;
            return true;
        }

        private bool CanMutate() => IsServer && IsSpawned && !settled &&
                                    (RaidManager.Instance == null || !RaidManager.Instance.IsOver);

        [ContextMenu("Debug/Carry 10 Loot")]
        private void DebugCarryTen()
        {
            var moved = CarryFromAvailable(10);
            Debug.Log($"[RaidLoot] carried +{moved} | available={Available}, carried={Carried}", this);
        }

        [ContextMenu("Debug/Secure Carried Loot")]
        private void DebugSecure()
        {
            var moved = SecureCarried();
            Debug.Log($"[RaidLoot] secured +{moved} | secured={Secured}", this);
        }

        [ContextMenu("Debug/Drop Carried Loot")]
        private void DebugDrop()
        {
            var moved = DropCarried();
            Debug.Log($"[RaidLoot] dropped {moved} | available={Available}", this);
        }
    }
}
