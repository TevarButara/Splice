using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Core
{
    // Per-team player level, server-authoritative. Placeholder source for card-unlock gating in the
    // greybox: the future run/Lair progression system should drive SetLevel()/AddLevel(). Mirrors the
    // GoldController.For(team) registry pattern so any system can look level up by team.
    public class PlayerProgression : NetworkBehaviour
    {
        private static readonly Dictionary<Team, PlayerProgression> byTeam = new();
        public static PlayerProgression For(Team team) => byTeam.GetValueOrDefault(team);

        // Level lookup that stays safe when no progression object exists in the scene (defaults to 1),
        // so level gating is opt-in for greybox scenes that don't place one yet.
        public static int LevelFor(Team team) => byTeam.TryGetValue(team, out var progression) ? progression.Level : 1;

        [SerializeField] private Team team = Team.Invaders;
        [SerializeField] private int startingLevel = 1;

        private readonly NetworkVariable<int> level = new(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public Team Team => team;
        public int Level => level.Value;

        public override void OnNetworkSpawn()
        {
            byTeam[team] = this;
            if (IsServer) level.Value = Mathf.Max(1, startingLevel);
        }

        public override void OnNetworkDespawn()
        {
            if (byTeam.TryGetValue(team, out var existing) && existing == this) byTeam.Remove(team);
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
