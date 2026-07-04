using UnityEngine;

namespace Splice.Characters
{
    // Shared Scene-view helper: draws a flat wire circle on the XZ plane so attackRange is visible while
    // tuning stats in the Inspector. Gizmos are editor-only (stripped from builds), so no #if needed and
    // zero runtime cost — this just makes "how far does this number actually reach" obvious when a
    // tower/monster/Fort is selected.
    public static class RangeGizmo
    {
        public static void DrawFlatCircle(Vector3 center, float radius, Color color, int segments = 48)
        {
            if (radius <= 0f || segments < 3) return;

            var previous = Gizmos.color;
            Gizmos.color = color;

            var from = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var to = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(from, to);
                from = to;
            }

            Gizmos.color = previous;
        }
    }
}
