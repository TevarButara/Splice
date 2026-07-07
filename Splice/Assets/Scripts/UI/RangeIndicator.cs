using UnityEngine;

namespace Splice.UI
{
    // World-space attack-range ring drawn with a LineRenderer (greybox-friendly, fully code-driven).
    // Shown by TowerPlacementInputController while choosing where to build, hidden once placed. One shared
    // instance in the scene is moved/resized per frame — no per-radius prefab needed.
    [RequireComponent(typeof(LineRenderer))]
    public class RangeIndicator : MonoBehaviour
    {
        [Tooltip("จำนวนด้านของวง — มากขึ้น = กลมขึ้น")]
        [SerializeField] private int segments = 48;
        [Tooltip("ยกวงลอยเหนือพื้นเล็กน้อยกัน z-fighting กับ build zone")]
        [SerializeField] private float yOffset = 0.05f;

        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            Hide();
        }

        // Redraw the ring and tint it (e.g. green = placeable, red = blocked).
        public void Show(Vector3 center, float radius, Color color)
        {
            Show(center, radius);
            if (line == null) return;
            line.startColor = color;
            line.endColor = color;
        }

        // Redraw the ring centred at `center` with the given world-space radius.
        public void Show(Vector3 center, float radius)
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (radius <= 0f || segments < 3)
            {
                Hide();
                return;
            }

            line.enabled = true;
            line.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + yOffset,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }

        public void Hide()
        {
            if (line != null) line.enabled = false;
        }
    }
}
