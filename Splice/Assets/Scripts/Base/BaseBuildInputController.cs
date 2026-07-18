using System.Collections.Generic;
using Splice.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.Base
{
    // Input ของโหมดจัดผังเมือง (Build Mode, offline) — แตะเพื่อ วาง/เลือก/ย้าย (ป้อม + มอนเฝ้า garrison).
    // ต่างจาก TowerPlacementInputController: ไม่มี ServerRpc (offline) เรียก BaseBuildManager ตรงๆ.
    //
    // มือถือ-friendly: "วาง" จะเกิดตอน **ปล่อยนิ้ว** และเฉพาะเมื่อเป็น tap จริง (นิ้วขยับน้อย + ไม่ได้เริ่ม/จบบน UI
    // + ไม่ใช่ pinch 2 นิ้ว) → ระหว่าง pan/zoom จะไม่วางของหลุด. เลิกเลือกด้วยปุ่ม Cancel (BaseBuildManager.CancelSelection).
    // ปุ่มลบ/clear wire onClick ไป DeleteSelected()/ClearAll() ใน Inspector.
    public class BaseBuildInputController : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [SerializeField] private Camera raycastCamera;
        [Tooltip("นิ้วขยับเกินกี่ pixel ถือว่า 'ลาก' (pan) ไม่ใช่ tap วาง")]
        [SerializeField] private float tapMoveThreshold = 12f;

        [Header("Preview (optional)")]
        [Tooltip("วงระยะ world-space โชว์ช่องที่จะวาง/ย้าย — เว้นว่าง = ไม่โชว์")]
        [SerializeField] private RangeIndicator placementPreview;
        [SerializeField] private Color validColor = Color.green;
        [SerializeField] private Color invalidColor = Color.red;

        private bool wasPressed;
        private Vector2 pressStartPos;
        private Vector2 lastPos;
        private bool pressStartedOverUI;
        private bool wasMultiTouch;

        private static readonly List<RaycastResult> uiHits = new();

        private void Update()
        {
            UpdatePreview();
            TrackTap();
        }

        // วาง/เลือก/ย้าย เกิดตอนปล่อยนิ้ว และเฉพาะ tap จริง (กัน pan/zoom วางของหลุด)
        private void TrackTap()
        {
            var pressed = TryGetPointer(out var pos);
            var multi = IsMultiTouch();

            if (pressed && !wasPressed)          // เริ่มกด
            {
                pressStartPos = pos;
                lastPos = pos;
                pressStartedOverUI = IsOverUI(pos);
                wasMultiTouch = multi;
            }
            else if (pressed)                    // กดค้าง
            {
                lastPos = pos;
                if (multi) wasMultiTouch = true; // เคยเป็น 2 นิ้ว = pinch ทั้งท่า ไม่ใช่ tap
            }
            else if (wasPressed)                 // ปล่อยนิ้ว
            {
                var isTap = !pressStartedOverUI && !wasMultiTouch
                            && (lastPos - pressStartPos).magnitude <= tapMoveThreshold
                            && !IsOverUI(lastPos);
                if (isTap) HandleTap(lastPos);
            }

            wasPressed = pressed;
        }

        private void HandleTap(Vector2 screen)
        {
            if (buildManager == null || raycastCamera == null) return;
            var ray = raycastCamera.ScreenPointToRay(screen);

            // ไม่ได้กำลังวางชิ้นใหม่ → แตะโดนชิ้นที่วางแล้วให้ "เลือก" (ไว้ย้าย/ลบ)
            if (!buildManager.HasArmed && Physics.Raycast(ray, out var anyHit, float.MaxValue))
            {
                var existing = anyHit.collider.GetComponentInParent<BaseBuildPiece>();
                if (existing != null)
                {
                    buildManager.Select(existing);
                    return;
                }
            }

            // วาง (armed) หรือ ย้าย (มีตัวเลือก): หา "จุดบนระนาบ grid" (y=gridOrigin.y) แทน raycast พื้น
            // → กัน parallax ตอนกล้องเอียง (ไม่งั้นกดไม่ตรงช่อง)
            if (TryGetGroundPoint(screen, out var groundPoint))
                buildManager.OnGroundTapped(groundPoint);
        }

        // จุดที่ ray จากจอตัด "ระนาบ grid" (y = gridOrigin.y) — ใช้แทน raycast collider พื้น เพราะ collider พื้น
        // กับระนาบที่วาด grid อาจคนละความสูง ทำให้กดไม่ตรงช่องเมื่อกล้องเอียง (parallax)
        private bool TryGetGroundPoint(Vector2 screen, out Vector3 point)
        {
            point = default;
            if (raycastCamera == null || buildManager == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, buildManager.Grid.gridOrigin.y, 0f));
            var ray = raycastCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out var dist) || dist <= 0f) return false;
            point = ray.GetPoint(dist);
            return true;
        }

        // วงระยะที่ช่องซึ่งชี้อยู่ (ตามนิ้วขณะกด) ตอนกำลังวาง/มีตัวเลือก — เขียว = วางได้, แดง = วางไม่ได้
        private void UpdatePreview()
        {
            if (placementPreview == null || buildManager == null || raycastCamera == null) return;
            if (!buildManager.WantsPreview) { placementPreview.Hide(); return; }

            if (TryGetPointer(out var pointer) && TryGetGroundPoint(pointer, out var point))
            {
                var cell = buildManager.SnapPoint(point);
                placementPreview.Show(cell, buildManager.PreviewRange, buildManager.CanPlaceCell(cell) ? validColor : invalidColor);
                return;
            }

            placementPreview.Hide();
        }

        // ---------- input helpers ----------

        private static bool IsMultiTouch()
        {
            var touchscreen = Touchscreen.current;
            return touchscreen != null && touchscreen.touches[0].press.isPressed && touchscreen.touches[1].press.isPressed;
        }

        private bool TryGetPointer(out Vector2 screenPosition)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        // Fresh UI hit-test — stateless (กันค้าง "over UI") เหมือน CameraPanController
        private static bool IsOverUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
            uiHits.Clear();
            eventSystem.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }
    }
}
