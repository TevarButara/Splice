using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Splice.Network;
using Splice.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Client-side tap/click -> tower placement intent for the Fort/Defender side (architecture 5.6).
    // Like DeployInputController it only ever calls the ServerRpc — no local gold checks, no prediction.
    // A UI button picks which tower via SelectTower(towerId); the tap picks where on the build surface.
    // While a tower is selected it also drives a RangeIndicator ring at the pointer so you can see the
    // attackRange before committing; the ring hides once the tower is placed.
    // Interacting with an EXISTING tower (repair/upgrade/demolish) lives in TowerInteractionController.
    public class TowerPlacementInputController : MonoBehaviour
    {
        [SerializeField] private TowerDeploymentManager towerDeploymentManager;
        [SerializeField] private Camera raycastCamera;
        [Tooltip("พื้นที่ที่วางป้อมได้ — ตั้ง layer ของพื้น build zone ให้ตรงกับ mask นี้")]
        [SerializeField] private LayerMask buildLayerMask = ~0;
        [Tooltip("กล้อง Fort — ถ้ากำหนดไว้ จะวางป้อมได้เฉพาะตอนกล้องอยู่ที่ฐาน (pan ออกไปต้องกด Home ก่อน). เว้นว่าง = วางได้ทุกที่")]
        [SerializeField] private CameraPanController cameraPan;

        [Header("Range preview")]
        [Tooltip("catalog สำหรับหา attackRange ของป้อมที่เลือก (client-side display เท่านั้น)")]
        [SerializeField] private TowerDatabaseSO towerDatabase;
        [Tooltip("วงระยะ world-space ที่โชว์ตอนกำลังเลือกตำแหน่งวาง — snap ลงช่องกริด, วางจริงแล้วซ่อน. เว้นว่าง = ไม่โชว์ preview")]
        [SerializeField] private RangeIndicator placementPreview;
        [SerializeField] private Color validColor = Color.green;
        [SerializeField] private Color invalidColor = Color.red;

        private string selectedTowerId;

        // A UI button/card picks which tower to place. Placement is inert until a tower is selected.
        public void SelectTower(string towerId)
        {
            selectedTowerId = towerId;
        }

        // Read by TowerCardView to highlight the currently armed tower.
        public string SelectedTowerId => selectedTowerId;

        // Team whose gold pays for towers — lets a card show affordability without its own reference.
        public Team DeployTeam => towerDeploymentManager != null ? towerDeploymentManager.DeployTeam : Team.Defenders;

        private void Update()
        {
            if (string.IsNullOrEmpty(selectedTowerId))
            {
                HidePreview();
                return;
            }

            // Fort may only build while the view is at the base — panned away, the tap is for looking, not placing.
            if (cameraPan != null && !cameraPan.IsAtHome)
            {
                HidePreview();
                return;
            }

            UpdatePreview();

            if (!WasTappedThisFrame(out var screenPosition)) return;
            TryPlaceAt(screenPosition);
        }

        // Follow the pointer across the build surface: snap the range ring to the grid cell it points at and
        // tint it green (placeable + affordable) or red. The server re-checks everything on the actual tap.
        private void UpdatePreview()
        {
            if (placementPreview == null || raycastCamera == null || towerDeploymentManager == null) return;

            if (TryGetPointer(out var pointer))
            {
                var ray = raycastCamera.ScreenPointToRay(pointer);
                if (Physics.Raycast(ray, out var hit, float.MaxValue, buildLayerMask))
                {
                    var placeable = towerDeploymentManager.TryGetBuildCell(hit.point, out var cell);
                    var ok = placeable && IsAffordable();
                    placementPreview.Show(cell, SelectedRange(), ok ? validColor : invalidColor);
                    return;
                }
            }

            placementPreview.Hide();
        }

        private float SelectedRange()
        {
            if (towerDatabase == null) return 0f;
            var definition = towerDatabase.GetById(selectedTowerId);
            return definition != null ? definition.attackRange : 0f;
        }

        private bool IsAffordable()
        {
            if (towerDatabase == null) return false;
            var definition = towerDatabase.GetById(selectedTowerId);
            if (definition == null) return false;
            var bank = GoldController.For(towerDeploymentManager.DeployTeam);
            return bank != null && bank.CurrentGold >= definition.goldCost;
        }

        // Current pointer position for the hover preview. Mouse first (editor tuning); on touch we only
        // have a position while a finger is down.
        private bool TryGetPointer(out Vector2 screenPosition)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
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

            // Deployed → hide the range ring and require re-selecting a tower to place another.
            selectedTowerId = null;
            HidePreview();
        }

        private void HidePreview()
        {
            if (placementPreview != null) placementPreview.Hide();
        }
    }
}
