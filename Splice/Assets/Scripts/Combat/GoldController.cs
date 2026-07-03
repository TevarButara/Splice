using System.Collections.Generic;
using Splice.Core;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // Per-team gold balance, server-authoritative (architecture 5.7). Replaces the old mana timer:
    // there is no passive regen — income comes only from MinerCharacter depositing mined gold.
    public class GoldController : NetworkBehaviour
    {
        private static readonly Dictionary<Team, GoldController> byTeam = new();
        public static GoldController For(Team team) => byTeam.GetValueOrDefault(team);

        [SerializeField] private Team team = Team.Invaders;
        [SerializeField] private int startingGold = 50;

        private readonly NetworkVariable<int> currentGold = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public Team Team => team;
        public int CurrentGold => currentGold.Value;

        public override void OnNetworkSpawn()
        {
            byTeam[team] = this;
            if (IsServer) currentGold.Value = startingGold;
        }

        public override void OnNetworkDespawn()
        {
            if (byTeam.TryGetValue(team, out var existing) && existing == this) byTeam.Remove(team);
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
