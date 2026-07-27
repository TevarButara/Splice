using Splice.Characters;
using Splice.Data;
using Splice.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [SerializeField] private Vector2 popupScreenOffset = new(0f, 80f);
        [SerializeField] private Button repairButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button destroyButton;
        private TMP_Text repairLabel;
        private TMP_Text upgradeLabel;
        private TMP_Text destroyLabel;

        private TowerCharacter selectedTower;

        public TowerCharacter SelectedTower => selectedTower;
        public bool HasCompleteBinding =>
            towerDeploymentManager != null && raycastCamera != null && actionMenu != null &&
            repairButton != null && upgradeButton != null && destroyButton != null;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            HideMenu();
        }

        private void Update()
        {
            ResolveReferences();
            if (selectedTower != null)
            {
                if (selectedTower.IsDead)
                    HideMenu();
                else
                {
                    ShowMenuAt(selectedTower.transform.position);
                    RefreshActionState();
                }
            }

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
            if (selectedTower != null && towerDeploymentManager != null &&
                towerDeploymentManager.CanRepair(selectedTower))
                towerDeploymentManager.RequestRepairTowerServerRpc(selectedTower.NetworkObject);
            RefreshActionState();
        }

        // Wire to the menu's tier-Upgrade button (swaps to nextTier — a separate system from per-stat).
        public void UpgradeSelected()
        {
            if (selectedTower != null && towerDeploymentManager != null &&
                towerDeploymentManager.CanUpgrade(selectedTower))
                towerDeploymentManager.RequestUpgradeTowerServerRpc(selectedTower.NetworkObject);
            HideMenu();
        }

        // Wire each of these to its per-stat upgrade button (attack / HP / armor / range / targets).
        public void UpgradeAttack() => UpgradeStat(TowerStat.Attack);
        public void UpgradeHealth() => UpgradeStat(TowerStat.Health);
        public void UpgradeArmor() => UpgradeStat(TowerStat.Armor);
        public void UpgradeRange() => UpgradeStat(TowerStat.Range);
        public void UpgradeTargets() => UpgradeStat(TowerStat.Targets);

        private void UpgradeStat(TowerStat stat)
        {
            if (selectedTower != null) towerDeploymentManager.RequestUpgradeStatServerRpc(selectedTower.NetworkObject, stat);
            HideMenu();
        }

        // Wire to the menu's Demolish button.
        public void DemolishSelected()
        {
            if (selectedTower != null && towerDeploymentManager != null)
                towerDeploymentManager.RequestDemolishTowerServerRpc(selectedTower.NetworkObject);
            HideMenu();
        }

        private bool TryPickTower(Vector2 screenPosition, out TowerCharacter tower)
        {
            tower = null;
            if (raycastCamera == null) raycastCamera = Camera.main;
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
                var screen = (Vector2)raycastCamera.WorldToScreenPoint(worldPosition) + popupScreenOffset;
                screen.x = Mathf.Clamp(screen.x, 20f, Mathf.Max(20f, Screen.width - 20f));
                screen.y = Mathf.Clamp(screen.y, 20f, Mathf.Max(20f, Screen.height - 20f));
                actionMenu.transform.position = screen;
            }
            actionMenu.SetActive(true);
        }

        private void HideMenu()
        {
            selectedTower = null;
            if (actionMenu != null) actionMenu.SetActive(false);
        }

        public void EnsureBinding()
        {
            ResolveReferences();
            RefreshActionState();
        }

        private void ResolveReferences()
        {
            if (towerDeploymentManager == null)
                towerDeploymentManager = FindAnyObjectByType<TowerDeploymentManager>();
            if (raycastCamera == null) raycastCamera = Camera.main;

            // Older RaidArena revisions pointed actionMenu at the whole TOWER canvas root. Always bind the
            // user's exact Editor-authored popup so hiding/moving it cannot affect unrelated Fort UI.
            if (actionMenu == null || !string.Equals(
                    actionMenu.name,
                    "Panel_Tower",
                    System.StringComparison.OrdinalIgnoreCase))
                actionMenu = FindSceneObject("Panel_Tower");

            if (actionMenu == null) return;
            if (repairButton == null) repairButton = FindButton(actionMenu, "BTRepair");
            if (upgradeButton == null) upgradeButton = FindButton(actionMenu, "BTUpgrade");
            if (destroyButton == null) destroyButton = FindButton(actionMenu, "BTDestroy");
            if (repairLabel == null) repairLabel = repairButton?.GetComponentInChildren<TMP_Text>(true);
            if (upgradeLabel == null) upgradeLabel = upgradeButton?.GetComponentInChildren<TMP_Text>(true);
            if (destroyLabel == null) destroyLabel = destroyButton?.GetComponentInChildren<TMP_Text>(true);
        }

        private void RefreshActionState()
        {
            if (selectedTower == null || towerDeploymentManager == null) return;
            var damaged = selectedTower.CurrentHealth < selectedTower.MaxHealth;
            if (repairButton != null)
            {
                var showRepair = damaged && !selectedTower.IsRepairing;
                if (repairButton.gameObject.activeSelf != showRepair)
                    repairButton.gameObject.SetActive(showRepair);
                repairButton.interactable = showRepair && towerDeploymentManager.CanRepair(selectedTower);
                if (repairLabel != null)
                    repairLabel.text = $"Repair\n{towerDeploymentManager.RepairCostFor(selectedTower)}";
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = towerDeploymentManager.CanUpgrade(selectedTower);
                if (upgradeLabel != null)
                    upgradeLabel.text = selectedTower.Definition != null &&
                                        selectedTower.Definition.nextTier != null
                        ? $"Upgrade\n{selectedTower.Definition.upgradeCost}"
                        : "MAX";
            }
            if (destroyButton != null)
            {
                destroyButton.interactable = !(selectedTower is FortCore);
                if (destroyLabel != null)
                    destroyLabel.text = $"Sell\n+{towerDeploymentManager.SellRefundFor(selectedTower)}";
            }
        }

        private GameObject FindSceneObject(string objectName)
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(transform.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                    return transform.gameObject;
            return null;
        }

        private static Button FindButton(GameObject root, string buttonName)
        {
            if (root == null) return null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (string.Equals(button.name, buttonName, System.StringComparison.OrdinalIgnoreCase))
                    return button;
            return null;
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
