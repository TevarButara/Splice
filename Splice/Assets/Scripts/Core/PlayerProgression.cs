using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Core
{
    // Per-side player level, server-authoritative. Placeholder source for card-unlock gating in the
    // greybox: the future run/Lair progression system should drive SetLevel()/AddLevel(). Mirrors the
    // GoldController.For(side) registry pattern so any system can look level up by RaidSide.
    public class PlayerProgression : NetworkBehaviour
    {
        private static readonly Dictionary<RaidSide, PlayerProgression> bySide = new();
        public static PlayerProgression For(RaidSide side) => bySide.GetValueOrDefault(side);

        // Level lookup that stays safe when no progression object exists in the scene (defaults to 1),
        // so level gating is opt-in for greybox scenes that don't place one yet.
        public static int LevelFor(RaidSide side) => bySide.TryGetValue(side, out var progression) ? progression.Level : 1;

        [FormerlySerializedAs("team")]
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [SerializeField] private int startingLevel = 1;

        private readonly NetworkVariable<int> level = new(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public RaidSide Side => side;
        public int Level => level.Value;

        public override void OnNetworkSpawn()
        {
            bySide[side] = this;
            if (IsServer) level.Value = Mathf.Max(1, startingLevel);
        }

        public override void OnNetworkDespawn()
        {
            if (bySide.TryGetValue(side, out var existing) && existing == this) bySide.Remove(side);
        }

        public void SetLevel(int value)
        {
            if (IsServer) level.Value = Mathf.Max(1, value);
        }

        public void AddLevel(int delta)
        {
            if (IsServer) level.Value = Mathf.Max(1, level.Value + delta);
        }
    }
}
