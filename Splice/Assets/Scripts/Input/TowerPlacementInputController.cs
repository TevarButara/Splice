using Splice.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Client-side tap/click -> tower placement intent for the Fort/Defender side (architecture 5.6).
    // Like DeployInputController it only ever calls the ServerRpc — no local gold checks, no prediction.
    // A UI button picks which tower via SelectTower(towerId); the tap picks where on the build surface.
    // Interacting with an EXISTING tower (repair/upgrade/demolish) lives in TowerInteractionController.
    public class TowerPlacementInputController : MonoBehaviour
    {
        [SerializeField] private TowerDeploymentManager towerDeploymentManager;
        [SerializeField] private Camera raycastCamera;
        [Tooltip("พื้นที่ที่วางป้อมได้ — ตั้ง layer ของพื้น build zone ให้ตรงกับ mask นี้")]
        [SerializeField] private LayerMask buildLayerMask = ~0;
        [Tooltip("กล้อง Fort — ถ้ากำหนดไว้ จะวางป้อมได้เฉพาะตอนกล้องอยู่ที่ฐาน (pan ออกไปต้องกด Home ก่อน). เว้นว่าง = วางได้ทุกที่")]
        [SerializeField] private CameraPanController cameraPan;

        private string selectedTowerId;

        // A UI button picks which tower to place. Placement is inert until a tower is selected.
        public void SelectTower(string towerId)
        {
            selectedTowerId = towerId;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(selectedTowerId)) return;
            // Fort may only build while the view is at the base — panned away, the tap is for looking, not placing.
            if (cameraPan != null && !cameraPan.IsAtHome) return;
            if (!WasTappedThisFrame(out var screenPosition)) return;
            TryPlaceAt(screenPosition);
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

        private void TryPlaceAt(Vector2 screenPosition)
        {
            if (towerDeploymentManager == null)
            {
                Debug.LogError("TowerPlacementInputController: 'Tower Deployment Manager' is not assigned in the Inspector.", this);
                return;
            }

            if (raycastCamera == null) return;

            var ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, float.MaxValue, buildLayerMask)) return;

            towerDeploymentManager.RequestDeployTowerServerRpc(selectedTowerId, hit.point);
        }
    }
}
