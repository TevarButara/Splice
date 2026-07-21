using System.Collections.Generic;
using Splice.Characters;
using Splice.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Owner-side targeting presentation. It chooses a world point and sends intent; RaidHeroCharacter repeats
    // every gameplay check on the server before applying damage or starting cooldown.
    // Run after CameraPanController's default LateUpdate so the ray matches the camera pose rendered this frame.
    [DefaultExecutionOrder(100)]
    public class HeroAbilityTargetingController : MonoBehaviour
    {
        [SerializeField] private RaidHeroCharacter hero;
        [SerializeField] private Camera targetCamera;
        [Tooltip("Layer ของพื้นสนามเท่านั้น เพื่อไม่ให้ ray ชน model/tower ก่อนพื้น")]
        [SerializeField] private LayerMask groundLayerMask = ~0;

        [Header("Greybox Indicators")]
        [SerializeField] private RangeIndicator effectRadiusIndicator;
        [SerializeField] private RangeIndicator castRangeIndicator;
        [SerializeField] private Color validColor = new(0.2f, 1f, 0.25f, 1f);
        [SerializeField] private Color invalidColor = new(1f, 0.2f, 0.15f, 1f);
        [SerializeField] private Color castRangeColor = new(1f, 0.55f, 0.1f, 1f);
        [Tooltip("Panel/ข้อความ 'เลือกจุดวาง Breach Charge' — เว้นว่างได้")]
        [SerializeField] private GameObject targetingUiRoot;

        private static readonly List<RaycastResult> uiHits = new();
        private Camera inspectorCamera;
        private bool targeting;
        private int armedFrame;

        public bool IsTargeting => targeting;

        private void Awake()
        {
            // Remember an explicit Inspector assignment. An automatically resolved camera may change when
            // SideSelectionController swaps the overview/Fort/Monster camera.
            inspectorCamera = targetCamera;
        }

        private void Update()
        {
            ResolveReferences();

            var keyboard = Keyboard.current;
            if (!targeting && keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                BeginTargeting();
                return;
            }

            if (!targeting) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }

            if (hero == null || !hero.IsOwner || !hero.CanAct || hero.TacticalAbility == null)
            {
                CancelTargeting();
            }
        }

        private void LateUpdate()
        {
            if (!targeting) return;

            ResolveReferences();
            if (hero == null || !hero.IsOwner || !hero.CanAct || hero.TacticalAbility == null)
            {
                CancelTargeting();
                return;
            }

            if (!TryGetPointerPosition(out var screenPoint) || !TryGetGroundPoint(screenPoint, out var targetPoint))
            {
                effectRadiusIndicator?.Hide();
                return;
            }

            var castCenter = hero.transform.position;
            castCenter.y = targetPoint.y;
            castRangeIndicator?.Show(castCenter, hero.TacticalAbility.castRange, castRangeColor);

            var inRange = HorizontalSqrDistance(hero.transform.position, targetPoint) <=
                          hero.TacticalAbility.castRange * hero.TacticalAbility.castRange;
            effectRadiusIndicator?.Show(
                targetPoint,
                hero.TacticalAbility.effectRadius,
                inRange ? validColor : invalidColor);

            if (Time.frameCount <= armedFrame || !WasPrimaryPressedThisFrame() || IsOverUI(screenPoint)) return;

            // Send even an out-of-range point so the authoritative server can reject it and publish feedback.
            hero.RequestCastTacticalAbilityServerRpc(targetPoint);
            CancelTargeting();
        }

        public void BeginTargeting()
        {
            ResolveReferences();
            if (hero == null || !hero.IsOwner || hero.TacticalAbility == null) return;

            // Only one pointer-targeting mode may own the next click.
            var orderTargeting = GetComponent<HeroTacticalOrderController>();
            if (orderTargeting == null) orderTargeting = FindAnyObjectByType<HeroTacticalOrderController>();
            orderTargeting?.CancelTargeting();

            if (!hero.CanAct || hero.TacticalAbilityCooldownRemaining > 0f)
            {
                hero.RequestTacticalAbilityStatusFeedbackServerRpc();
                return;
            }

            targeting = true;
            armedFrame = Time.frameCount;
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(true);
        }

        public void CancelTargeting()
        {
            targeting = false;
            effectRadiusIndicator?.Hide();
            castRangeIndicator?.Hide();
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(false);
        }

        private void OnDisable() => CancelTargeting();

        private void ResolveReferences()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;

            // If a camera was assigned in the Inspector, prefer it whenever that gameplay camera is active.
            if (IsUsableCamera(inspectorCamera))
            {
                targetCamera = inspectorCamera;
                return;
            }

            // Camera.main is the static overview camera in Bootstrap. The player actually sees MonCamera or
            // FortCamera, so resolve the active CameraPanController camera before falling back to Camera.main.
            if (IsUsableCamera(targetCamera) && targetCamera.GetComponent<CameraPanController>() != null) return;

            var panControllers = FindObjectsByType<CameraPanController>();
            for (var i = 0; i < panControllers.Length; i++)
            {
                var gameplayCamera = panControllers[i].GetComponent<Camera>();
                if (!IsUsableCamera(gameplayCamera)) continue;
                targetCamera = gameplayCamera;
                return;
            }

            var mainCamera = Camera.main;
            targetCamera = IsUsableCamera(mainCamera) ? mainCamera : null;
        }

        private static bool IsUsableCamera(Camera candidate) =>
            candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy;

        private bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (targetCamera == null) return false;
            var ray = targetCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(
                    ray,
                    out var hit,
                    float.MaxValue,
                    groundLayerMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            worldPoint = hit.point;
            return true;
        }

        private static bool TryGetPointerPosition(out Vector2 position)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        private static bool WasPrimaryPressedThisFrame()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private static bool IsOverUI(Vector2 screenPoint)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var eventData = new PointerEventData(eventSystem) { position = screenPoint };
            uiHits.Clear();
            eventSystem.RaycastAll(eventData, uiHits);
            return uiHits.Count > 0;
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
    }
}
