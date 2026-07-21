using System.Collections.Generic;
using Splice.Characters;
using Splice.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Splice.Input
{
    // Owner-side pointer selection for the server-authoritative Hero Focus Target order.
    // R/button arms selection, click/tap chooses a defensive unit, C clears the current order.
    [DefaultExecutionOrder(110)]
    public class HeroTacticalOrderController : MonoBehaviour
    {
        [SerializeField] private RaidHeroCharacter hero;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask targetLayerMask = ~0;

        [Header("Greybox Feedback")]
        [SerializeField] private RangeIndicator focusTargetIndicator;
        [Min(0.1f)] [SerializeField] private float markerRadius = 1.75f;
        [SerializeField] private Color selectedColor = new(0.1f, 0.85f, 1f, 1f);
        [SerializeField] private Color validHoverColor = new(0.2f, 1f, 0.25f, 1f);
        [SerializeField] private Color invalidHoverColor = new(1f, 0.2f, 0.15f, 1f);
        [Tooltip("Panel/ข้อความ 'เลือก Tower หรือ Garrison' — เว้นว่างได้")]
        [SerializeField] private GameObject targetingUiRoot;

        private static readonly List<RaycastResult> uiHits = new();
        private Camera inspectorCamera;
        private bool targeting;
        private int armedFrame;

        public bool IsTargeting => targeting;

        private void Awake()
        {
            inspectorCamera = targetCamera;
        }

        private void Update()
        {
            ResolveReferences();
            if (hero == null || !hero.IsOwner) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                BeginFocusTargeting();
                return;
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                ClearFocusTarget();
                return;
            }

            if (!targeting) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelTargeting();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) CancelTargeting();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (hero == null || !hero.IsOwner || !hero.IsSpawned)
            {
                focusTargetIndicator?.Hide();
                return;
            }

            if (!targeting)
            {
                ShowCurrentFocusTarget();
                return;
            }

            if (!hero.CanAct)
            {
                CancelTargeting();
                return;
            }

            if (!TryGetPointerPosition(out var screenPoint))
            {
                focusTargetIndicator?.Hide();
                return;
            }

            var foundCharacter = TryGetCharacterUnderPointer(screenPoint, out var candidate);
            var selectable = foundCharacter && IsSelectableTarget(candidate);
            if (foundCharacter)
                focusTargetIndicator?.Show(
                    candidate.transform.position,
                    markerRadius,
                    selectable ? validHoverColor : invalidHoverColor);
            else
                focusTargetIndicator?.Hide();

            if (Time.frameCount <= armedFrame || !WasPrimaryPressedThisFrame() || IsOverUI(screenPoint)) return;

            var targetReference = selectable
                ? new NetworkObjectReference(candidate.NetworkObject)
                : default;
            // The server repeats ownership, life-state and target-type validation.
            hero.RequestSetFocusTargetServerRpc(targetReference);
            CancelTargeting();
        }

        public void BeginFocusTargeting()
        {
            ResolveReferences();
            if (hero == null || !hero.IsOwner) return;

            var abilityTargeting = GetComponent<HeroAbilityTargetingController>();
            if (abilityTargeting == null) abilityTargeting = FindAnyObjectByType<HeroAbilityTargetingController>();
            abilityTargeting?.CancelTargeting();

            if (!hero.CanAct)
            {
                hero.RequestSetFocusTargetServerRpc(default);
                return;
            }

            targeting = true;
            armedFrame = Time.frameCount;
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(true);
        }

        public void ClearFocusTarget()
        {
            ResolveReferences();
            CancelTargeting();
            if (hero != null && hero.IsOwner) hero.RequestClearFocusTargetServerRpc();
        }

        public void CancelTargeting()
        {
            targeting = false;
            focusTargetIndicator?.Hide();
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(false);
        }

        private void OnDisable()
        {
            targeting = false;
            focusTargetIndicator?.Hide();
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(false);
        }

        private void ShowCurrentFocusTarget()
        {
            if (hero.TryGetFocusTarget(out var currentTarget))
                focusTargetIndicator?.Show(currentTarget.transform.position, markerRadius, selectedColor);
            else
                focusTargetIndicator?.Hide();
        }

        private bool IsSelectableTarget(CharacterBase candidate)
        {
            if (candidate == null || candidate.IsDead) return false;
            if (candidate is TowerCharacter tower) return tower is not FortCore;
            return candidate is MonsterCharacter monster && monster.Side != hero.Side;
        }

        private bool TryGetCharacterUnderPointer(Vector2 screenPoint, out CharacterBase character)
        {
            character = null;
            if (targetCamera == null) return false;

            var hits = Physics.RaycastAll(
                targetCamera.ScreenPointToRay(screenPoint),
                float.MaxValue,
                targetLayerMask,
                QueryTriggerInteraction.Ignore);
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var candidate = hits[i].collider.GetComponentInParent<CharacterBase>();
                if (candidate == null || hits[i].distance >= nearestDistance) continue;
                character = candidate;
                nearestDistance = hits[i].distance;
            }
            return character != null;
        }

        private void ResolveReferences()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;

            if (IsUsableCamera(inspectorCamera))
            {
                targetCamera = inspectorCamera;
                return;
            }

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
    }
}
