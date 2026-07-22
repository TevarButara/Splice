using System.Collections.Generic;
using Splice.Characters;
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

        [Header("Hero Follow (Raid)")]
        [Tooltip("เมื่อ Hero ของผู้เล่นอยู่ Manual ให้กล้องรักษา Hero ไว้กลางจอโดยอัตโนมัติ")]
        [SerializeField] private bool followOwnedHeroInManualMode = true;
        [Tooltip("เมื่อ Hero ของผู้เล่นอยู่ Auto ให้กล้องตาม Hero เช่นเดียวกับ Manual")]
        [SerializeField] private bool followOwnedHeroInAutoMode = true;
        [Tooltip("เปิดเมื่อจำเป็นต้องบังคับกล้องอยู่ใน Pan Bounds แม้จะทำให้ Hero ไม่อยู่กลางจอบริเวณขอบแมป")]
        [SerializeField] private bool clampHeroFollowToPanBounds;
        [Tooltip("ความเร็วที่กล้องเลื่อนไปหา Hero (world units/วินาที)")]
        [Min(0.1f)] [SerializeField] private float heroFollowSpeed = 30f;
        [Tooltip("หลังผู้เล่นลากกล้องเอง รอกี่วินาทีก่อนกลับไปตาม Hero")]
        [Min(0f)] [SerializeField] private float heroFollowResumeDelay = 1.25f;

        [Header("Zoom (pinch มือถือ / scroll ใน editor)")]
        [Tooltip("[Perspective] world units ที่ dolly เข้า/ออก ต่อ 1 หน่วย pinch/scroll")]
        [SerializeField] private float zoomSpeed = 0.01f;
        [Tooltip("[Perspective] ความสูงกล้องต่ำสุด (zoom เข้าใกล้สุด)")]
        [SerializeField] private float minHeight = 5f;
        [Tooltip("[Perspective] ความสูงกล้องสูงสุด (zoom ออกดูภาพรวมเมือง)")]
        [SerializeField] private float maxHeight = 30f;

        [Header("Zoom (Orthographic) — ใช้เมื่อกล้องตั้งเป็น Ortho")]
        [Tooltip("[Ortho] ซูมกี่ % ต่อ 1 คลิก scroll (0.2 = 20% ต่อคลิก) — ซูมแบบสัดส่วน ความรู้สึกคงที่ทุกระดับ. ช้าไปให้เพิ่ม")]
        [SerializeField] private float orthoZoomStep = 0.2f;
        [Tooltip("ลากนิ้ว pinch กี่ % ของความสูงจอ = 1 คลิก (0.15 = ลาก 15% ของจอ เท่ากับ scroll 1 คลิก). เล็ก = pinch ไวขึ้น")]
        [SerializeField] private float pinchScreenFraction = 0.15f;
        [Tooltip("[Ortho] size ต่ำสุด (ซูมเข้าใกล้สุด) — ยิ่งเล็กยิ่งซูมเข้า")]
        [SerializeField] private float minOrthoSize = 3f;
        [Tooltip("[Ortho] size สูงสุด (ซูมออกดูภาพรวม)")]
        [SerializeField] private float maxOrthoSize = 20f;
        [Tooltip("[Ortho] ตัวคูณความไวการลาก — 1 = ลากแล้วจุดใต้นิ้วอยู่กับที่พอดี (1:1 grab the map)")]
        [SerializeField] private float orthoPanMultiplier = 1f;

        [Header("Tilt (ปุ่มสลับมุมมอง บน ↔ เอียง)")]
        [Tooltip("มุมก้ม X ตอนกดปุ่มเอียง (องศา) เช่น 55")]
        [SerializeField] private float tiltAngle = 55f;
        [Tooltip("มุมมองบน (top-down) — ปกติ 90")]
        [SerializeField] private float topDownAngle = 90f;
        [Tooltip("ความเร็วหมุนตอนสลับมุม (องศา/วินาที) — มาก = เร็ว. คุมความ smooth")]
        [SerializeField] private float tiltSpeed = 180f;
        [Tooltip("ระดับพื้น (y) สำหรับหา 'จุดกลางจอ' ตอนยังไม่มี focus object")]
        [SerializeField] private float groundY = 0f;

        private Vector3 homePosition;
        private Vector2 lastPointer;
        private bool dragging;
        private bool returningHome;
        private bool wasPressed;
        private float lastPinchDistance;
        private bool pinching;
        private bool tilted;
        private float targetPitch;
        private Vector3 orbitFocus;
        private float orbitDistance;
        private bool isTilting;
        private Vector3 focusPoint;
        private bool hasFocus;
        private Camera cam;
        private float heroFollowSuspendedUntil;
        private bool heroFollowRuntimeEnabled = true;

        private static readonly List<RaycastResult> uiHits = new();

        // Fort placement guard reads this: true when the camera sits at (near) the base.
        public bool IsAtHome =>
            (transform.position - homePosition).sqrMagnitude <= homeThreshold * homeThreshold;

        private void Start()
        {
            // The camera's authored starting spot IS the base view.
            homePosition = transform.position;
            cam = GetComponent<Camera>();
        }

        // Wire to the Home button's OnClick.
        public void GoHome() => returningHome = true;

        // Optional UI hook: immediately re-center on the owned Hero in either raid control mode.
        public void FocusHeroNow()
        {
            heroFollowSuspendedUntil = 0f;
            TickHeroFollow(immediate: true);
        }

        // Defender spectator mode watches the town rather than centering the locally-owned simulated attacker.
        public void SetHeroFollowEnabled(bool enabled)
        {
            heroFollowRuntimeEnabled = enabled;
            if (!enabled) heroFollowSuspendedUntil = float.PositiveInfinity;
            else heroFollowSuspendedUntil = 0f;
        }

        // wire ปุ่ม → สลับมุมมอง บน (topDownAngle) ↔ เอียง (tiltAngle) แบบ smooth หมุนรอบ "จุด focus"
        // (object ล่าสุดที่ทำงานอยู่ — set ผ่าน SetFocusPoint; ถ้าไม่มีใช้จุดกลางจอบนพื้น) → กล้องไม่เด้งไปมั่ว
        public void ToggleTilt()
        {
            tilted = !tilted;
            targetPitch = tilted ? tiltAngle : topDownAngle;
            orbitFocus = GetOrbitFocus();
            orbitDistance = Vector3.Distance(transform.position, orbitFocus);
            isTilting = true;
        }

        // ตั้ง "จุดกึ่งกลาง" ที่กล้องหมุนรอบตอนสลับมุม — BaseBuildManager เรียกใส่ตำแหน่ง object ล่าสุด
        public void SetFocusPoint(Vector3 worldPoint)
        {
            focusPoint = worldPoint;
            hasFocus = true;
        }

        // ตั้งมุมก้ม (X) ทันที (ไม่ smooth) — เผื่อเรียกตรงๆ
        public void SetPitch(float xAngle)
        {
            var e = transform.eulerAngles;
            transform.eulerAngles = new Vector3(xAngle, e.y, e.z);
        }

        private Vector3 GetOrbitFocus()
        {
            if (hasFocus) return focusPoint;
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (plane.Raycast(ray, out var d) && d > 0f) return ray.GetPoint(d);
            }
            return transform.position + transform.forward * 20f;
        }

        // หมุนกล้องรอบ orbitFocus ไปหา targetPitch แบบ smooth — focus อยู่กลางจอเสมอ
        private void TickTilt()
        {
            var e = transform.eulerAngles;
            var newPitch = Mathf.MoveTowardsAngle(e.x, targetPitch, tiltSpeed * Time.deltaTime);
            var rot = Quaternion.Euler(newPitch, e.y, 0f);
            transform.rotation = rot;
            transform.position = orbitFocus - rot * Vector3.forward * orbitDistance;

            if (Mathf.Abs(Mathf.DeltaAngle(newPitch, targetPitch)) < 0.05f)
            {
                rot = Quaternion.Euler(targetPitch, e.y, 0f);
                transform.rotation = rot;
                transform.position = orbitFocus - rot * Vector3.forward * orbitDistance;
                isTilting = false;
            }
        }

        private void Update()
        {
            if (isTilting) { TickTilt(); return; } // กำลังสลับมุม → หมุน smooth ก่อน (ไม่ pan/zoom)
            if (HandleZoom()) return; // pinch/scroll กำลัง zoom → ข้าม pan เฟรมนี้

            var moved = TryGetDragDelta(out var delta);
            if (dragging) returningHome = false; // any active touch cancels the auto-return
            if (moved)
            {
                PanBy(delta);
                return;
            }
            if (returningHome) ReturnHomeStep();
        }

        private void LateUpdate()
        {
            // Run after the server/host Hero movement tick so the camera sees this frame's final position.
            TickHeroFollow(immediate: false);
        }

        private void TickHeroFollow(bool immediate)
        {
            if (!heroFollowRuntimeEnabled) return;
            var hero = RaidHeroCharacter.Instance;
            if (hero == null || !hero.IsOwner || hero.LifeState != HeroLifeState.Active || isTilting ||
                returningHome || Time.unscaledTime < heroFollowSuspendedUntil)
                return;

            var followThisMode = hero.ControlMode == HeroControlMode.Manual
                ? followOwnedHeroInManualMode
                : followOwnedHeroInAutoMode;
            if (!followThisMode) return;

            // Intersect the center ray with a horizontal plane through the Hero's transform, not the ground.
            // On a tilted camera, centering the ground below a capsule places the actual Hero above screen center.
            if (!TryGetViewCenterAtHeight(hero.transform.position.y, out var viewCenter)) return;

            var offset = hero.transform.position - viewCenter;
            offset.y = 0f;
            var desired = transform.position + offset;
            if (clampHeroFollowToPanBounds) desired = ClampPanPosition(desired);
            transform.position = immediate
                ? desired
                : Vector3.MoveTowards(transform.position, desired, heroFollowSpeed * Time.unscaledDeltaTime);

            // Tilt now or later should orbit around the Hero rather than an old build-piece focus.
            focusPoint = hero.transform.position;
            hasFocus = true;
        }

        private bool TryGetViewCenterAtHeight(float worldY, out Vector3 point)
        {
            point = default;
            if (Cam == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, worldY, 0f));
            var ray = Cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!plane.Raycast(ray, out var distance) || distance <= 0f) return false;
            point = ray.GetPoint(distance);
            return true;
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
            heroFollowSuspendedUntil = Time.unscaledTime + heroFollowResumeDelay;

            var right = transform.right;
            right.y = 0f; right.Normalize();

            // "ขึ้นบนจอ" บนพื้น: ปกติใช้ forward ปรับให้ราบ. แต่ถ้ากล้องมองดิ่ง 90° (forward ชี้ลง → flatten แล้วเป็น 0)
            // จะ pan แกน Z ไม่ได้ → ใช้ transform.up แทน (up ของกล้องชี้ไปทาง "บนจอ" เมื่อมองลง)
            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) { forward = transform.up; forward.y = 0f; }
            forward.Normalize();

            // Drag the finger right -> the map slides right -> the camera moves left (natural "grab the map" feel).
            // Ortho: world-units-ต่อ-pixel ขึ้นกับ orthographicSize ล้วนๆ → คำนวณจริงเพื่อให้จุดใต้นิ้วอยู่กับที่
            // ทุกระดับซูม (ไม่งั้นซูมเข้าแล้วลากไวเกิน / ซูมออกแล้วลากช้าเกิน)
            var unitsPerPixel = IsOrtho
                ? (2f * Cam.orthographicSize / Mathf.Max(1, Screen.height)) * orthoPanMultiplier
                : panSpeed;

            var move = (-dragDelta.x * right - dragDelta.y * forward) * unitsPerPixel;
            transform.position = ClampPanPosition(transform.position + move);
        }

        private Vector3 ClampPanPosition(Vector3 position)
        {
            if (panBounds == null) return position;
            var bounds = panBounds.bounds; // world-space AABB — accounts for position/scale
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            return position;
        }

        // Zoom = dolly กล้องตาม forward (มุมคงที่) clamp ด้วยความสูง. คืน true ถ้ากำลัง pinch (2 นิ้ว) → งด pan.
        private bool HandleZoom()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var t0 = touchscreen.touches[0];
                var t1 = touchscreen.touches[1];
                if (t0.press.isPressed && t1.press.isPressed)
                {
                    var dist = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
                    // pinch delta เป็น "พิกเซล" → แปลงเป็นหน่วยคลิก (เทียบสัดส่วนความสูงจอ) ให้เท่ากับ scroll
                    if (pinching)
                        Zoom((dist - lastPinchDistance) / Mathf.Max(1f, Screen.height * pinchScreenFraction));
                    pinching = true;
                    lastPinchDistance = dist;
                    dragging = false;
                    return true;
                }
            }
            pinching = false;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // scroll คืนค่าไม่เท่ากันแต่ละแพลตฟอร์ม (บางที ±1, บางที ±120 ต่อคลิก) → normalize เป็น "คลิก"
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(Mathf.Abs(scroll) >= 10f ? scroll / 120f : scroll);
            }
            return false;
        }

        // notches = จำนวน "คลิก" ที่ normalize มาแล้วจากทั้ง pinch และ scroll (บวก = ซูมเข้า)
        private void Zoom(float notches)
        {
            // Ortho: ขยับกล้องเข้า-ออกไม่มีผลกับขนาดภาพเลย → ย่อ/ขยาย orthographicSize แทน (size เล็ก = ซูมเข้า)
            // ใช้การคูณ (exponential) ไม่ใช่บวก → ซูมเข้า/ออกรู้สึกเร็วเท่ากันทุกระดับ + ไม่มีทางติดลบ
            if (IsOrtho)
            {
                var factor = Mathf.Pow(1f - Mathf.Clamp(orthoZoomStep, 0.01f, 0.9f), notches);
                Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize * factor, minOrthoSize, maxOrthoSize);
                return;
            }

            var target = transform.position + transform.forward * (notches * zoomSpeed);
            // clamp ความสูงแทนการ reject — ไม่งั้นถ้ากล้องเริ่มนอกช่วง min/max จะ zoom ไม่ได้เลย
            target.y = Mathf.Clamp(target.y, minHeight, maxHeight);
            transform.position = target;
        }

        private Camera Cam
        {
            get
            {
                if (cam == null) cam = GetComponent<Camera>();
                return cam;
            }
        }

        private bool IsOrtho => Cam != null && Cam.orthographic;

        // Pointer movement since last frame while a press is held. A drag that BEGINS over UI is ignored so
        // panning never fights the card/tower buttons; the first frame of a press only seeds lastPointer.
        // "New press" is tracked from our own isPressed edge (not wasPressedThisFrame, which is flaky for
        // touch), and the over-UI test is a fresh raycast (not EventSystem's cached per-pointer state, which
        // could stick "true" after tapping a button that flips interactable/active mid-press — that left
        // panning dead until a scene reload, sometimes even past it).
        private bool TryGetDragDelta(out Vector2 delta)
        {
            delta = Vector2.zero;

            var isPressed = TryGetPointer(out var position);
            var justPressed = isPressed && !wasPressed;
            wasPressed = isPressed;

            if (!isPressed)
            {
                dragging = false;
                return false;
            }

            if (justPressed)
            {
                if (IsOverUI(position)) { dragging = false; return false; } // started over UI -> not a pan
                dragging = true;
                lastPointer = position;
                return false;
            }

            if (!dragging) return false;

            delta = position - lastPointer;
            lastPointer = position;
            return delta != Vector2.zero;
        }

        private static bool TryGetPointer(out Vector2 position)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        // Fresh UI hit-test at a screen point — stateless, so it can never get stuck reporting "over UI".
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
