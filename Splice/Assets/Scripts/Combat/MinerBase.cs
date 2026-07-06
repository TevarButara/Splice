using System.Collections.Generic;
using Splice.Characters;
using Splice.Core;
using UnityEngine;

namespace Splice.Combat
{
    // The drop-off point a team's miners walk back to (architecture 5.7). Carried gold is added to
    // GoldController when a miner comes within depositRadius, so round-trip distance gates income.
    public class MinerBase : MonoBehaviour
    {
        private static readonly Dictionary<Team, MinerBase> byTeam = new();
        public static MinerBase For(Team team) => byTeam.GetValueOrDefault(team);

        [SerializeField] private Team team = Team.Invaders;
        [Tooltip("ระยะรอบฐานที่ miner เข้ามาถึงแล้วฝากทองได้เลย — ไม่ต้องชนจุดกลาง (กันแย่งจุดเดียว). วงเขียวใน Scene view")]
        [SerializeField] private float depositRadius = 2.5f;

        public Team Team => team;
        public float DepositRadius => depositRadius;

        private void OnEnable() => byTeam[team] = this;

        private void OnDisable()
        {
            if (byTeam.TryGetValue(team, out var existing) && existing == this) byTeam.Remove(team);
        }

        // Show the deposit radius in the Scene view (green) so it's easy to size the drop-off zone.
        private void OnDrawGizmos()
        {
            RangeGizmo.DrawFlatCircle(transform.position, depositRadius, Color.green);
        }
    }
}
