using Splice.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Client tap -> raycast a SoldierHut -> open the lane deploy panel bound to that lane. Taps that land
    // on UI are ignored, so tapping cards inside the already-open panel doesn't re-trigger a hut. Mirrors
    // DeployInputController's Input System tap read (client intent only — no server state touched here).
    public class SoldierHutInputController : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask hutLayerMask = ~0;
        [SerializeField] private LaneDeployPanel deployPanel;

        private void Update()
        {
            if (deployPanel == null || raycastCamera == null) return;
            if (!WasTappedThisFrame(out var screenPosition)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, float.MaxValue, hutLayerMask)) return;

            var hut = hit.collider.GetComponentInParent<SoldierHut>();
            if (hut != null) deployPanel.OpenForLane(hut.LaneId);
        }

        private bool WasTappedThisFrame(out Vector2 screenPosition)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }
    }
}
