using System.Collections.Generic;
using Splice.Characters;
using Splice.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Combat
{
    // The drop-off point a side's miners walk back to (architecture 5.7). Carried gold is added to
    // GoldController when a miner comes within depositRadius, so round-trip distance gates income.
    public class MinerBase : MonoBehaviour
    {
        private static readonly Dictionary<RaidSide, MinerBase> bySide = new();
        public static MinerBase For(RaidSide side) => bySide.GetValueOrDefault(side);

        [FormerlySerializedAs("team")]
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [Tooltip("ระยะรอบฐานที่ miner เข้ามาถึงแล้วฝากทองได้เลย — ไม่ต้องชนจุดกลาง (กันแย่งจุดเดียว). วงเขียวใน Scene view")]
        [SerializeField] private float depositRadius = 2.5f;

        public RaidSide Side => side;
        public float DepositRadius => depositRadius;

        private void OnEnable() => bySide[side] = this;

        private void OnDisable()
        {
            if (bySide.TryGetValue(side, out var existing) && existing == this) bySide.Remove(side);
        }

        // Show the deposit radius in the Scene view (green) so it's easy to size the drop-off zone.
        private void OnDrawGizmos()
        {
            RangeGizmo.DrawFlatCircle(transform.position, depositRadius, Color.green);
        }
    }
}
