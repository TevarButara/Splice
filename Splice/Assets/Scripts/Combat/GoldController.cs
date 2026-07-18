using System.Collections.Generic;
using Splice.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Combat
{
    // Per-side gold balance, server-authoritative (architecture 5.7). Replaces the old mana timer:
    // there is no passive regen — income comes only from MinerCharacter depositing mined gold.
    public class GoldController : NetworkBehaviour
    {
        private static readonly Dictionary<RaidSide, GoldController> bySide = new();
        public static GoldController For(RaidSide side) => bySide.GetValueOrDefault(side);

        [FormerlySerializedAs("team")]
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [SerializeField] private int startingGold = 50;

        private readonly NetworkVariable<int> currentGold = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public RaidSide Side => side;
        public int CurrentGold => currentGold.Value;

        public override void OnNetworkSpawn()
        {
            bySide[side] = this;
            if (IsServer) currentGold.Value = startingGold;
        }

        public override void OnNetworkDespawn()
        {
            if (bySide.TryGetValue(side, out var existing) && existing == this) bySide.Remove(side);
        }

        // Server-only: ตั้งยอดทองตรงๆ แทนที่ค่าเดิม — ใช้ตอนโหลด snapshot ฐาน (RaidSnapshotLoader,
        // architecture 5.10) ที่ทองคลังของฐานต้องมาแทน startingGold
        public void SetBalance(int amount)
        {
            if (!IsServer) return;
            currentGold.Value = Mathf.Max(0, amount);
        }

        public void Add(int amount)
        {
            if (!IsServer || amount <= 0) return;
            currentGold.Value += amount;
        }

        public bool TrySpend(int amount)
        {
            if (!IsServer || currentGold.Value < amount) return false;
            currentGold.Value -= amount;
            return true;
        }
    }
}
