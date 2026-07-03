using Splice.Draft;
using Splice.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Client-side tap/click -> deploy intent. Only ever calls the same ServerRpc real validation runs through
    // (technical-architecture.md 4.1) — no local prediction, no client-side mana/lane checks.
    // Uses the Input System package directly since the project's Active Input Handling is set to it exclusively.
    public class DeployInputController : MonoBehaviour
    {
        [SerializeField] private DeploymentManager deploymentManager;
        [SerializeField] private DraftManager draftManager;
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask laneLayerMask = ~0;

        private int selectedCardIndex;

        public void SelectCard(int handIndex)
        {
            selectedCardIndex = handIndex;
        }

        private void Update()
        {
            if (!WasTappedThisFrame(out var screenPosition)) return;
            TryDeployAt(screenPosition);
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

        private void TryDeployAt(Vector2 screenPosition)
        {
            if (deploymentManager == null)
            {
                Debug.LogError("DeployInputController: 'Deployment Manager' is not assigned in the Inspector.", this);
                return;
            }

            if (draftManager == null)
            {
                Debug.LogError("DeployInputController: 'Draft Manager' is not assigned in the Inspector.", this);
                return;
            }

            if (raycastCamera == null || draftManager.CurrentHand.Count == 0) return;
            if (selectedCardIndex < 0 || selectedCardIndex >= draftManager.CurrentHand.Count) return;

            var ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, float.MaxValue, laneLayerMask)) return;

            var lane = hit.collider.GetComponentInParent<LaneMarker>();
            if (lane == null) return;

            var cardId = draftManager.CurrentHand[selectedCardIndex];
            deploymentManager.RequestDeployMonsterServerRpc(cardId, lane.LaneId);
        }
    }
}
