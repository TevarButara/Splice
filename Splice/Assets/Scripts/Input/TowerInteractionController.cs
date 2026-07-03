using Splice.Characters;
using Splice.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Client-side "tap an existing tower -> action menu" for the Fort/Defender side (architecture 5.6/5.8).
    // Tapping a tower selects it and pops up an icon menu (Repair / Upgrade / Demolish); each button forwards
    // to the matching ServerRpc for the selected tower. Like every input path this only sends intent — the
    // server does all gold math and validation. Tapping empty space closes the menu.
    public class TowerInteractionController : MonoBehaviour
    {
        [SerializeField] private TowerDeploymentManager towerDeploymentManager;
        [SerializeField] private Camera raycastCamera;
        [Tooltip("layer ของป้อม/Fort ที่ raycast โดนแล้วเปิดเมนู")]
        [SerializeField] private LayerMask towerLayerMask = ~0;
        [Tooltip("Panel เมนู icon (Repair/Upgrade/Demolish) — Screen Space canvas, ปิดไว้ตอนเริ่ม")]
        [SerializeField] private GameObject actionMenu;

        private TowerCharacter selectedTower;

        private void Start()
        {
            HideMenu();
        }

        private void Update()
        {
            if (!WasTappedThisFrame(out var screenPosition)) return;

            // A tap landing on the menu buttons is handled by the UI EventSystem — don't let it close the menu
            // or re-raycast the world underneath.
            if (IsPointerOverUI()) return;

            if (TryPickTower(screenPosition, out var tower))
            {
                selectedTower = tower;
                ShowMenuAt(tower.transform.position);
            }
            else
            {
                HideMenu();
            }
        }

        // Wire to the menu's Repair button.
        public void RepairSelected()
        {
            if (selectedTower != null) towerDeploymentManager.RequestRepairTowerServerRpc(selectedTower.NetworkObject);
            HideMenu();
        }

        // Wire to the menu's Upgrade button.
        public void UpgradeSelected()
        {
            if (selectedTower != null) towerDeploymentManager.RequestUpgradeTowerServerRpc(selectedTower.NetworkObject);
            HideMenu();
        }

        // Wire to the menu's Demolish button.
        public void DemolishSelected()
        {
            if (selectedTower != null) towerDeploymentManager.RequestDemolishTowerServerRpc(selectedTower.NetworkObject);
            HideMenu();
        }

        private bool TryPickTower(Vector2 screenPosition, out TowerCharacter tower)
        {
            tower = null;
            if (raycastCamera == null) return false;

            var ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, float.MaxValue, towerLayerMask)) return false;

            tower = hit.collider.GetComponentInParent<TowerCharacter>();
            return tower != null;
        }

        private void ShowMenuAt(Vector3 worldPosition)
        {
            if (actionMenu == null) return;
            if (raycastCamera != null)
            {
                actionMenu.transform.position = raycastCamera.WorldToScreenPoint(worldPosition);
            }
            actionMenu.SetActive(true);
        }

        private void HideMenu()
        {
            selectedTower = null;
            if (actionMenu != null) actionMenu.SetActive(false);
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return EventSystem.current.IsPointerOverGameObject(touchscreen.primaryTouch.touchId.ReadValue());
            }
            return EventSystem.current.IsPointerOverGameObject();
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
