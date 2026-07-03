using System.Collections.Generic;
using UnityEngine;

namespace Splice.Characters
{
    internal static class TargetingUtility
    {
        public static T FindNearest<T>(IReadOnlyList<T> candidates, Vector3 origin, float maxRange = float.MaxValue)
            where T : CharacterBase
        {
            T nearest = null;
            var nearestDistance = maxRange;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.IsDead) continue;

                var distance = Vector3.Distance(origin, candidate.transform.position);
                if (distance > nearestDistance) continue;

                nearest = candidate;
                nearestDistance = distance;
            }

            return nearest;
        }
    }
}
