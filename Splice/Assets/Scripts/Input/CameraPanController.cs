using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Client-side free-pan camera (tap + slide) for both sides (architecture 5.9). Portrait mobile with a ~50°
    // 3D view — the lane is longer than the screen, so the player drags to scroll the map along the ground plane
    // (height and angle stay fixed). Movement is clamped to the map bounds. GoHome() glides the view back to the
    // player's base; the Fort side may only place towers while at home (TowerPlacementInputController reads IsAtHome).
    //
    // Attach to the side's camera GameObject. Because SideSelectionController enables one camera at a time, only
    // the active side's pan controller runs.
    public class CameraPanController : MonoBehaviour
    {
        [Tooltip("world units ที่กล้องเลื่อน ต่อ 1 pixel ที่ลากนิ้ว")]
        [SerializeField] private float panSpeed = 0.02f;

        [Tooltip("กล่องขอบเขตแมป — สร้าง empty object ใส่ BoxCollider (Is Trigger) ลากคลุมพื้นที่ที่กล้อง 'ตำแหน่งกล้องเอง' " +
                 "เลื่อนไปได้ (clamp X/Z ตาม bounds). เว้นว่าง = เลื่อนได้ไม่จำกัด")]
        [SerializeField] private BoxCollider panBounds;

        [Header("Home")]
        [Tooltip("ระยะที่ถือว่ากล้อง 'อยู่ที่ฐาน' แล้ว — ฝั่ง Fort วางป้อมได้เฉพาะตอนอยู่ในระยะนี้")]
        [SerializeField] private float homeThreshold = 0.3f;
        [Tooltip("ความเร็ว (world units/วินาที) ที่กล้องเลื่อนกลับฐานตอนกด Home")]
        [SerializeField] private float homeReturnSpeed = 10f;

        private Vector3 homePosition;
        private Vector2 lastPointer;
        private bool dragging;
        private bool returningHome;

        // Fort placement guard reads this: true when the camera sits at (near) the base.
        public bool IsAtHome =>
            (transform.position - homePosition).sqrMagnitude <= homeThreshold * homeThreshold;

        private void Start()
        {
            // The camera's authored starting spot IS the base view.
            homePosition = transform.position;
        }

        // Wire to the Home button's OnClick.
        public void GoHome() => returningHome = true;

        private void Update()
        {
            var moved = TryGetDragDelta(out var delta);
            if (dragging) returningHome = false; // any active touch cancels the auto-return
            if (moved)
            {
                PanBy(delta);
                return;
            }
            if (returningHome) ReturnHomeStep();
        }

        private void ReturnHomeStep()
        {
            transform.position = Vector3.MoveTowards(transform.position, homePosition, homeReturnSpeed * Time.deltaTime);
            if ((transform.position - homePosition).sqrMagnitude <= 0.0001f)
            {
                transform.position = homePosition;
                returningHome = false;
            }
        }

        private void PanBy(Vector2 dragDelta)
        {
            var right = transform.right;
            var forward = transform.forward;
            right.y = 0f; right.Normalize();
            forward.y = 0f; forward.Normalize();

            // Drag the finger right -> the map slides right -> the camera moves left (natural "grab the map" feel).
            var move = (-dragDelta.x * right - dragDelta.y * forward) * panSpeed;
            var p = transform.position + move;
            if (panBounds != null)
            {
                var b = panBounds.bounds; // world-space AABB — accounts for the box's position/scale
                p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
                p.z = Mathf.Clamp(p.z, b.min.z, b.max.z);
            }
            transform.position = p;
        }

        // Pointer movement since last frame while a press is held. A drag that begins over UI is ignored so panning
        // never fights the card/tower buttons. The first frame of a press only seeds lastPointer (no movement).
        private bool TryGetDragDelta(out Vector2 delta)
        {
            delta = Vector2.zero;
            if (!TryGetPointer(out var position, out var pressedThisFrame))
            {
                dragging = false;
                return false;
            }

            if (pressedThisFrame)
            {
                if (IsPointerOverUI()) return false; // started over UI -> not a pan
                dragging = true;
                lastPointer = position;
                return false;
            }

            if (!dragging) return false;

            delta = position - lastPointer;
            lastPointer = position;
            return delta != Vector2.zero;
        }

        private static bool TryGetPointer(out Vector2 position, out bool pressedThisFrame)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                pressedThisFrame = touchscreen.primaryTouch.press.wasPressedThisFrame;
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                position = mouse.position.ReadValue();
                pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                return true;
            }

            position = default;
            pressedThisFrame = false;
            return false;
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                return EventSystem.current.IsPointerOverGameObject(touchscreen.primaryTouch.touchId.ReadValue());
            }
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
