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

        [Header("Squad Command UX")]
        [Tooltip("วงรัศมีรับคำสั่งรอบ Hero — เว้นว่างได้")]
        [SerializeField] private RangeIndicator commandRadiusIndicator;
        [Tooltip("วงต้นแบบสำหรับ clone เป็น marker ใต้ยูนิต Squad — object ควร active แต่ LineRenderer ซ่อนไว้")]
        [SerializeField] private RangeIndicator squadUnitIndicatorTemplate;
        [Min(0.1f)] [SerializeField] private float squadUnitMarkerRadius = 0.75f;
        [SerializeField] private Color commandRadiusColor = new(0.2f, 0.75f, 1f, 0.8f);
        [SerializeField] private Color squadPreviewColor = new(1f, 0.8f, 0.1f, 1f);
        [SerializeField] private Color squadActiveColor = new(0.15f, 0.85f, 1f, 1f);

        private static readonly List<RaycastResult> uiHits = new();
        private readonly List<RangeIndicator> squadUnitIndicators = new();
        private readonly List<MonsterCharacter> visibleSquadUnits = new();
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
                HideAllIndicators();
                return;
            }

            if (!targeting)
            {
                commandRadiusIndicator?.Hide();
                ShowCurrentFocusTarget();
                ShowSquadUnitMarkers(previewCandidates: false);
                return;
            }

            commandRadiusIndicator?.Show(
                hero.transform.position,
                hero.SquadCommandRadius,
                commandRadiusColor);
            ShowSquadUnitMarkers(previewCandidates: true);

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
            else if (TryGetPointerWorldPoint(screenPoint, out var hoverPoint))
                focusTargetIndicator?.Show(hoverPoint, markerRadius, invalidHoverColor);
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
            commandRadiusIndicator?.Hide();
            HideSquadUnitMarkers();
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(false);
        }

        private void OnDisable()
        {
            targeting = false;
            HideAllIndicators();
            if (targetingUiRoot != null && targetingUiRoot != gameObject) targetingUiRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < squadUnitIndicators.Count; i++)
            {
                var indicator = squadUnitIndicators[i];
                if (indicator != null && indicator != squadUnitIndicatorTemplate)
                    Destroy(indicator.gameObject);
            }
            squadUnitIndicators.Clear();
        }

        private void ShowCurrentFocusTarget()
        {
            if (hero.TryGetFocusTarget(out var currentTarget))
                focusTargetIndicator?.Show(currentTarget.transform.position, markerRadius, selectedColor);
            else
                focusTargetIndicator?.Hide();
        }

        private void ShowSquadUnitMarkers(bool previewCandidates)
        {
            visibleSquadUnits.Clear();
            var monsters = MonsterCharacter.Instances;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.IsDead || monster.Side != hero.Side) continue;
                var visible = previewCandidates
                    ? hero.IsSquadCommandCandidate(monster)
                    : monster.HasTacticalFocusOrder;
                if (visible) visibleSquadUnits.Add(monster);
            }

            EnsureSquadIndicatorPool(visibleSquadUnits.Count);
            var color = previewCandidates ? squadPreviewColor : squadActiveColor;
            for (var i = 0; i < squadUnitIndicators.Count; i++)
            {
                var indicator = squadUnitIndicators[i];
                if (indicator == null) continue;
                if (i < visibleSquadUnits.Count)
                    indicator.Show(visibleSquadUnits[i].transform.position, squadUnitMarkerRadius, color);
                else
                    indicator.Hide();
            }
        }

        private void EnsureSquadIndicatorPool(int required)
        {
            if (required <= 0 || squadUnitIndicatorTemplate == null) return;
            if (squadUnitIndicators.Count == 0) squadUnitIndicators.Add(squadUnitIndicatorTemplate);
            while (squadUnitIndicators.Count < required)
            {
                var clone = Instantiate(
                    squadUnitIndicatorTemplate,
                    squadUnitIndicatorTemplate.transform.parent);
                clone.name = $"HeroSquadUnitIndicator_{squadUnitIndicators.Count + 1}";
                squadUnitIndicators.Add(clone);
            }
        }

        private void HideSquadUnitMarkers()
        {
            squadUnitIndicatorTemplate?.Hide();
            for (var i = 0; i < squadUnitIndicators.Count; i++) squadUnitIndicators[i]?.Hide();
            visibleSquadUnits.Clear();
        }

        private void HideAllIndicators()
        {
            focusTargetIndicator?.Hide();
            commandRadiusIndicator?.Hide();
            HideSquadUnitMarkers();
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

            var pointerRay = targetCamera.ScreenPointToRay(screenPoint);
            var hits = Physics.RaycastAll(
                pointerRay,
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

            // Several existing tower prefabs intentionally have no Collider. They still need to be
            // selectable, so fall back to their visible renderer bounds after checking physics hits.
            // This also keeps the order input independent from combat/placement collider setup.
            var characters = FindObjectsByType<CharacterBase>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var characterIndex = 0; characterIndex < characters.Length; characterIndex++)
            {
                var candidate = characters[characterIndex];
                if (candidate == null) continue;

                var renderers = candidate.GetComponentsInChildren<Renderer>();
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var candidateRenderer = renderers[rendererIndex];
                    if (candidateRenderer == null || !candidateRenderer.enabled ||
                        (targetLayerMask.value & (1 << candidateRenderer.gameObject.layer)) == 0 ||
                        !candidateRenderer.bounds.IntersectRay(pointerRay, out var distance) ||
                        distance >= nearestDistance)
                        continue;

                    character = candidate;
                    nearestDistance = distance;
                }
            }
            return character != null;
        }

        private bool TryGetPointerWorldPoint(Vector2 screenPoint, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (targetCamera == null) return false;

            if (!Physics.Raycast(
                    targetCamera.ScreenPointToRay(screenPoint),
                    out var hit,
                    float.MaxValue,
                    targetLayerMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            worldPoint = hit.point;
            return true;
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
