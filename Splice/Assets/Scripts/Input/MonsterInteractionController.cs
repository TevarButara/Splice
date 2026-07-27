using Splice.Characters;
using Splice.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Splice.Input
{
    // Editor-authored popup binding for deployed monsters. The client only selects and sends intent;
    // DeploymentManager validates side, tier, balance and performs replacement/refund on the server.
    public sealed class MonsterInteractionController : MonoBehaviour
    {
        [SerializeField] private DeploymentManager deploymentManager;
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask monsterLayerMask = ~0;
        [SerializeField] private GameObject actionMenu;
        [SerializeField] private Vector2 popupScreenOffset = new(0f, 80f);
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button sellButton;
        private TMP_Text upgradeLabel;
        private TMP_Text sellLabel;

        private MonsterCharacter selectedMonster;

        public MonsterCharacter SelectedMonster => selectedMonster;
        public bool HasCompleteBinding =>
            deploymentManager != null && raycastCamera != null && actionMenu != null &&
            upgradeButton != null && sellButton != null;

        private void Awake() => ResolveReferences();
        private void Start() => HideMenu();

        private void Update()
        {
            ResolveReferences();
            if (selectedMonster != null)
            {
                if (selectedMonster.IsDead || !selectedMonster.IsSpawned)
                    HideMenu();
                else
                {
                    ShowMenuAt(selectedMonster.transform.position);
                    RefreshActionState();
                }
            }

            if (!WasTappedThisFrame(out var screenPosition) || IsPointerOverUI()) return;
            if (TryPickMonster(screenPosition, out var monster))
            {
                selectedMonster = monster;
                ShowMenuAt(monster.transform.position);
                RefreshActionState();
            }
            else
            {
                HideMenu();
            }
        }

        public void UpgradeSelected()
        {
            if (selectedMonster != null && deploymentManager != null &&
                deploymentManager.CanUpgrade(selectedMonster))
                deploymentManager.RequestUpgradeMonsterServerRpc(selectedMonster.NetworkObject);
            HideMenu();
        }

        public void SellSelected()
        {
            if (selectedMonster != null && deploymentManager != null)
                deploymentManager.RequestSellMonsterServerRpc(selectedMonster.NetworkObject);
            HideMenu();
        }

        public void EnsureBinding()
        {
            ResolveReferences();
            RefreshActionState();
        }

        private bool TryPickMonster(Vector2 screenPosition, out MonsterCharacter monster)
        {
            monster = null;
            if (raycastCamera == null) raycastCamera = Camera.main;
            if (raycastCamera == null || deploymentManager == null) return false;
            var ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, float.MaxValue, monsterLayerMask)) return false;
            monster = hit.collider.GetComponentInParent<MonsterCharacter>();
            return monster != null && monster.Side == deploymentManager.DeploySide;
        }

        private void ShowMenuAt(Vector3 worldPosition)
        {
            if (actionMenu == null || raycastCamera == null) return;
            var screen = (Vector2)raycastCamera.WorldToScreenPoint(worldPosition) + popupScreenOffset;
            screen.x = Mathf.Clamp(screen.x, 20f, Mathf.Max(20f, Screen.width - 20f));
            screen.y = Mathf.Clamp(screen.y, 20f, Mathf.Max(20f, Screen.height - 20f));
            actionMenu.transform.position = screen;
            actionMenu.SetActive(true);
        }

        private void HideMenu()
        {
            selectedMonster = null;
            if (actionMenu != null) actionMenu.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (deploymentManager == null)
                deploymentManager = FindAnyObjectByType<DeploymentManager>();
            if (raycastCamera == null) raycastCamera = Camera.main;
            if (actionMenu == null) actionMenu = FindSceneObject("Panel_Monster");
            if (actionMenu == null) return;
            if (upgradeButton == null) upgradeButton = FindButton(actionMenu, "BTMonUpgrade");
            if (sellButton == null) sellButton = FindButton(actionMenu, "BTMonSell");
            if (upgradeLabel == null) upgradeLabel = upgradeButton?.GetComponentInChildren<TMP_Text>(true);
            if (sellLabel == null) sellLabel = sellButton?.GetComponentInChildren<TMP_Text>(true);
        }

        private void RefreshActionState()
        {
            if (selectedMonster == null || deploymentManager == null) return;
            if (upgradeButton != null)
            {
                upgradeButton.interactable = deploymentManager.CanUpgrade(selectedMonster);
                if (upgradeLabel != null)
                    upgradeLabel.text = selectedMonster.Definition != null &&
                                        selectedMonster.Definition.nextTier != null
                        ? $"Upgrade\n{selectedMonster.Definition.upgradeCost}"
                        : "MAX";
            }
            if (sellButton != null)
            {
                sellButton.interactable = true;
                if (sellLabel != null)
                    sellLabel.text = $"Sell\n+{deploymentManager.SellRefundFor(selectedMonster)}";
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
                return EventSystem.current.IsPointerOverGameObject(
                    touchscreen.primaryTouch.touchId.ReadValue());
            return EventSystem.current.IsPointerOverGameObject();
        }

        private static bool WasTappedThisFrame(out Vector2 screenPosition)
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
