using PinePie.SimpleJoystick;
using Splice.Characters;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.Input
{
    // Runtime binding keeps the imported UI prefab presentation-only: exact button names become
    // owner intents, while RaidHeroCharacter repeats all gameplay validation on the server.
    [DisallowMultipleComponent]
    public sealed class HeroActionButtonController : MonoBehaviour
    {
        [SerializeField] private RaidHeroCharacter hero;
        [SerializeField] private HeroAbilityTargetingController targetingController;
        [SerializeField] private JoystickController movementJoystick;

        private Button blinkButton;
        private Button healButton;
        private Button attackButton;
        private Button skill1Button;
        private Button skill2Button;
        private Button skill3Button;
        private Button autoButton;
        private Button targetMonsterButton;
        private Button targetTowerButton;
        private GameObject attackPanel;
        private bool? lastControlAvailability;
        private HeroControlMode? lastControlMode;
        private JoystickController subscribedJoystick;
        private HeroAbilitySlot suppressedClickSlot;
        private int suppressClickThroughFrame = -1;
        private bool? lastResolvedFocusTarget;

        public JoystickController MovementJoystick => movementJoystick;
        public bool HasCompleteBinding =>
            movementJoystick != null && blinkButton != null && healButton != null && attackButton != null &&
            skill1Button != null && skill2Button != null && skill3Button != null &&
            autoButton != null && targetMonsterButton != null && targetTowerButton != null &&
            attackPanel != null;

        private void Awake()
        {
            EnsureBinding();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void Update()
        {
            ResolveHero();
            RefreshVisibleTargetLock();
            RefreshControlAvailability();
        }

        public void Blink() => UseAbility(HeroAbilitySlot.Blink);
        public void Heal() => UseAbility(HeroAbilitySlot.Heal);
        public void Skill1() => UseAbility(HeroAbilitySlot.Skill1);
        public void Skill2() => UseAbility(HeroAbilitySlot.Skill2);
        public void Skill3() => UseAbility(HeroAbilitySlot.Skill3);

        public void Attack()
        {
            ResolveHero();
            if (hero != null && hero.CanLocalPlayerControl) hero.RequestNormalAttackServerRpc();
        }

        public void Auto()
        {
            ResolveHero();
            if (hero != null && hero.CanLocalPlayerControl)
                hero.RequestSetControlModeServerRpc(HeroControlMode.Auto);
        }

        public void TargetMonster()
        {
            ResolveHero();
            RequestVisibleTarget(HeroTargetPreference.Monster);
        }

        public void TargetTower()
        {
            ResolveHero();
            RequestVisibleTarget(HeroTargetPreference.Tower);
        }

        public void EnterHeroMode()
        {
            ResolveHero();
            if (hero != null && hero.CanLocalPlayerControl &&
                hero.ControlMode != HeroControlMode.Manual)
                hero.RequestSetControlModeServerRpc(HeroControlMode.Manual);
        }

        public void EnsureBinding()
        {
            UnbindButtons();
            ResolveReferences();
            BindButtons();
            SubscribeJoystick();
        }

        private void UseAbility(HeroAbilitySlot slot)
        {
            if (slot == suppressedClickSlot && Time.frameCount <= suppressClickThroughFrame)
            {
                suppressClickThroughFrame = -1;
                return;
            }
            ResolveHero();
            if (hero == null || !hero.CanLocalPlayerControl) return;

            var ability = hero.GetAbility(slot);
            if (ability == null)
            {
                hero.RequestAbilityStatusFeedbackServerRpc(slot);
                return;
            }

            if (ability.castType == HeroAbilityCastType.DragArea)
            {
                if (targetingController == null)
                    targetingController = FindAnyObjectByType<HeroAbilityTargetingController>();
                if (targetingController != null)
                {
                    targetingController.BeginTargeting(slot);
                    return;
                }
            }

            hero.RequestCastAbilityServerRpc(slot, hero.GetSuggestedAbilityTargetPoint(slot));
        }

        private void ResolveReferences()
        {
            ResolveHero();
            if (targetingController == null)
                targetingController = FindAnyObjectByType<HeroAbilityTargetingController>();
            if (movementJoystick == null)
                movementJoystick = GetComponentInChildren<JoystickController>(true);
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                if (string.Equals(
                        transforms[i].name,
                        "Panel_Attack_Button",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    attackPanel = transforms[i].gameObject;
                    break;
                }
        }

        private void ResolveHero()
        {
            if (hero == null) hero = RaidHeroCharacter.Instance;
        }

        private void BindButtons()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                switch (button.name)
                {
                    case "bt-blink": blinkButton = button; break;
                    case "bt-heal": healButton = button; break;
                    case "bt-attack": attackButton = button; break;
                    case "bt-skill1": skill1Button = button; break;
                    case "bt-skill2": skill2Button = button; break;
                    case "bt-skill3": skill3Button = button; break;
                    case "bt-auto": autoButton = button; break;
                    case "bt-target-mon": targetMonsterButton = button; break;
                    case "bt-target-tower": targetTowerButton = button; break;
                }
            }

            blinkButton?.onClick.AddListener(Blink);
            healButton?.onClick.AddListener(Heal);
            attackButton?.onClick.AddListener(Attack);
            skill1Button?.onClick.AddListener(Skill1);
            skill2Button?.onClick.AddListener(Skill2);
            skill3Button?.onClick.AddListener(Skill3);
            autoButton?.onClick.AddListener(Auto);
            targetMonsterButton?.onClick.AddListener(TargetMonster);
            targetTowerButton?.onClick.AddListener(TargetTower);
            EnsureSkillDragHandler(skill1Button, HeroAbilitySlot.Skill1);
            EnsureSkillDragHandler(skill2Button, HeroAbilitySlot.Skill2);
            EnsureSkillDragHandler(skill3Button, HeroAbilitySlot.Skill3);
            lastControlAvailability = null;
            lastControlMode = null;
            RefreshControlAvailability();
        }

        private void UnbindButtons()
        {
            blinkButton?.onClick.RemoveListener(Blink);
            healButton?.onClick.RemoveListener(Heal);
            attackButton?.onClick.RemoveListener(Attack);
            skill1Button?.onClick.RemoveListener(Skill1);
            skill2Button?.onClick.RemoveListener(Skill2);
            skill3Button?.onClick.RemoveListener(Skill3);
            autoButton?.onClick.RemoveListener(Auto);
            targetMonsterButton?.onClick.RemoveListener(TargetMonster);
            targetTowerButton?.onClick.RemoveListener(TargetTower);
            if (subscribedJoystick != null)
            {
                subscribedJoystick.OnTouchPressed -= EnterHeroMode;
                subscribedJoystick = null;
            }
        }

        private void RefreshControlAvailability()
        {
            var available = hero != null && hero.CanLocalPlayerControl;
            var mode = hero != null ? hero.ControlMode : HeroControlMode.Auto;
            if (lastControlAvailability == available && lastControlMode == mode) return;
            lastControlAvailability = available;
            lastControlMode = mode;
            SetInteractable(blinkButton, available);
            SetInteractable(healButton, available);
            SetInteractable(attackButton, available);
            SetInteractable(skill1Button, available);
            SetInteractable(skill2Button, available);
            SetInteractable(skill3Button, available);
            SetInteractable(autoButton, available);
            SetInteractable(targetMonsterButton, available);
            SetInteractable(targetTowerButton, available);
            if (movementJoystick != null) movementJoystick.enabled = available;
            var showAttackPanel = available && hero.ControlMode == HeroControlMode.Manual;
            if (attackPanel != null && attackPanel.activeSelf != showAttackPanel)
                attackPanel.SetActive(showAttackPanel);
        }

        private void SubscribeJoystick()
        {
            if (subscribedJoystick == movementJoystick) return;
            if (subscribedJoystick != null) subscribedJoystick.OnTouchPressed -= EnterHeroMode;
            subscribedJoystick = movementJoystick;
            if (subscribedJoystick != null) subscribedJoystick.OnTouchPressed += EnterHeroMode;
        }

        public bool TryBeginAbilityDrag(HeroAbilitySlot slot, Vector2 screenPoint)
        {
            ResolveHero();
            if (hero == null || !hero.CanLocalPlayerControl) return false;
            var ability = hero.GetAbility(slot);
            if (ability == null || ability.castType != HeroAbilityCastType.DragArea) return false;
            if (targetingController == null)
                targetingController = FindAnyObjectByType<HeroAbilityTargetingController>();
            return targetingController != null &&
                   targetingController.BeginDragTargeting(slot, screenPoint);
        }

        public void UpdateAbilityDrag(HeroAbilitySlot slot, Vector2 screenPoint)
        {
            targetingController?.UpdateDragTargeting(slot, screenPoint);
        }

        public void ReleaseAbilityDrag(HeroAbilitySlot slot, Vector2 screenPoint)
        {
            if (targetingController == null ||
                !targetingController.ReleaseDragTargeting(slot, screenPoint))
                return;
            suppressedClickSlot = slot;
            suppressClickThroughFrame = Time.frameCount + 1;
        }

        public void CancelAbilityDrag(HeroAbilitySlot slot)
        {
            if (targetingController != null && targetingController.IsDragTargeting &&
                targetingController.SelectedSlot == slot)
                targetingController.CancelTargeting();
        }

        private void EnsureSkillDragHandler(Button button, HeroAbilitySlot slot)
        {
            if (button == null) return;
            var handler = button.GetComponent<HeroSkillDragButton>();
            if (handler == null) handler = button.gameObject.AddComponent<HeroSkillDragButton>();
            handler.Configure(this, slot);
        }

        private void RefreshVisibleTargetLock()
        {
            if (hero == null || !hero.CanLocalPlayerControl)
            {
                lastResolvedFocusTarget = null;
                return;
            }

            var hasResolvedTarget = hero.TryGetFocusTarget(out _);
            if (lastResolvedFocusTarget == true &&
                !hasResolvedTarget &&
                hero.TargetPreference != HeroTargetPreference.Default)
            {
                // The locked target died/despawned. Retarget only from what the player can see now;
                // RequestVisibleTarget sends an empty reference to cancel when the screen has no candidate.
                RequestVisibleTarget(hero.TargetPreference);
                hasResolvedTarget = hero.TryGetFocusTarget(out _);
            }
            lastResolvedFocusTarget = hasResolvedTarget;
        }

        private void RequestVisibleTarget(HeroTargetPreference preference)
        {
            if (hero == null || !hero.CanLocalPlayerControl) return;
            var visibleTarget = FindVisibleTarget(preference);
            var targetReference = visibleTarget != null &&
                                  visibleTarget.NetworkObject != null &&
                                  visibleTarget.IsSpawned
                ? new NetworkObjectReference(visibleTarget.NetworkObject)
                : default;
            hero.RequestSetTargetPreferenceServerRpc(preference, targetReference);
        }

        private CharacterBase FindVisibleTarget(HeroTargetPreference preference)
        {
            var camera = ResolveGameplayCamera();
            if (hero == null || camera == null) return null;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);

            // Preserve the combat rule: target-mon gives an on-screen enemy Hero priority,
            // then falls back to an on-screen monster. Distance never expands the camera scope.
            if (preference == HeroTargetPreference.Monster)
            {
                var visibleHero = FindNearestVisibleEnemyHero(camera, planes);
                if (visibleHero != null) return visibleHero;

                MonsterCharacter nearestMonster = null;
                var bestMonsterSqr = float.PositiveInfinity;
                var monsters = MonsterCharacter.Instances;
                for (var i = 0; i < monsters.Count; i++)
                {
                    var candidate = monsters[i];
                    if (candidate == null || candidate.IsDead || candidate.Side == hero.Side ||
                        !candidate.IsSpawned || !IsCandidateVisible(camera, planes, candidate))
                        continue;
                    var sqr = (candidate.transform.position - hero.transform.position).sqrMagnitude;
                    if (sqr >= bestMonsterSqr) continue;
                    bestMonsterSqr = sqr;
                    nearestMonster = candidate;
                }
                return nearestMonster;
            }

            if (preference == HeroTargetPreference.Tower)
            {
                TowerCharacter nearestTower = null;
                var bestTowerSqr = float.PositiveInfinity;
                var towers = TowerCharacter.Instances;
                for (var i = 0; i < towers.Count; i++)
                {
                    var candidate = towers[i];
                    if (candidate == null || candidate.IsDead || candidate is FortCore ||
                        !candidate.IsSpawned || !IsCandidateVisible(camera, planes, candidate))
                        continue;
                    var sqr = (candidate.transform.position - hero.transform.position).sqrMagnitude;
                    if (sqr >= bestTowerSqr) continue;
                    bestTowerSqr = sqr;
                    nearestTower = candidate;
                }
                return nearestTower;
            }
            return null;
        }

        private RaidHeroCharacter FindNearestVisibleEnemyHero(Camera camera, Plane[] planes)
        {
            RaidHeroCharacter best = null;
            var bestSqr = float.PositiveInfinity;
            var candidates = RaidHeroCharacter.Instances;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate == hero || candidate.IsDead ||
                    candidate.Side == hero.Side || !candidate.IsSpawned ||
                    !IsCandidateVisible(camera, planes, candidate))
                    continue;
                var sqr = (candidate.transform.position - hero.transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = candidate;
            }
            return best;
        }

        private static Camera ResolveGameplayCamera()
        {
            var panControllers = FindObjectsByType<CameraPanController>(FindObjectsSortMode.None);
            for (var i = 0; i < panControllers.Length; i++)
            {
                var gameplayCamera = panControllers[i].GetComponent<Camera>();
                if (gameplayCamera != null && gameplayCamera.isActiveAndEnabled &&
                    gameplayCamera.gameObject.activeInHierarchy)
                    return gameplayCamera;
            }

            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled && main.gameObject.activeInHierarchy)
                return main;

            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (var i = 0; i < cameras.Length; i++)
                if (cameras[i].isActiveAndEnabled && cameras[i].gameObject.activeInHierarchy)
                    return cameras[i];
            return null;
        }

        private static bool IsCandidateVisible(Camera camera, Plane[] planes, CharacterBase candidate)
        {
            var renderers = candidate.GetComponentsInChildren<Renderer>(false);
            var hasRenderer = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                hasRenderer = true;
                if (IsBoundsVisible(camera, planes, renderer.bounds)) return true;
            }

            if (hasRenderer) return false;
            var viewport = camera.WorldToViewportPoint(candidate.transform.position);
            return viewport.z > camera.nearClipPlane &&
                   viewport.x >= 0f && viewport.x <= 1f &&
                   viewport.y >= 0f && viewport.y <= 1f;
        }

        public static bool IsBoundsVisible(Camera camera, Bounds bounds)
        {
            if (camera == null || !camera.isActiveAndEnabled) return false;
            return IsBoundsVisible(camera, GeometryUtility.CalculateFrustumPlanes(camera), bounds);
        }

        private static bool IsBoundsVisible(Camera camera, Plane[] planes, Bounds bounds)
        {
            if (!GeometryUtility.TestPlanesAABB(planes, bounds)) return false;
            var viewport = camera.WorldToViewportPoint(bounds.ClosestPoint(camera.transform.position));
            return viewport.z > camera.nearClipPlane;
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null) selectable.interactable = interactable;
        }
    }
}
