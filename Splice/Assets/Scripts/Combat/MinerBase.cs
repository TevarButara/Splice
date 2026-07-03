using System.Collections.Generic;
using Splice.Core;
using UnityEngine;

namespace Splice.Combat
{
    // The drop-off point a team's miners walk back to (architecture 5.7). Carried gold is added to
    // GoldController only when a miner physically arrives here, so round-trip distance gates income.
    public class MinerBase : MonoBehaviour
    {
        private static readonly Dictionary<Team, MinerBase> byTeam = new();
        public static MinerBase For(Team team) => byTeam.GetValueOrDefault(team);

        [SerializeField] private Team team = Team.Invaders;

        public Team Team => team;

        private void OnEnable() => byTeam[team] = this;

        private void OnDisable()
        {
            if (byTeam.TryGetValue(team, out var existing) && existing == this) byTeam.Remove(team);
        }
    }
}
