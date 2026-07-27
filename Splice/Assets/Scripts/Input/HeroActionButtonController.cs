using PinePie.SimpleJoystick;
using Splice.Characters;
using Splice.Data;
using TMPro;
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
        private sealed class AbilityCooldownBinding
        {
            public HeroAbilitySlot Slot;
            public Button Button;
            public Image Overlay;
            public TMP_Text Label;
        }

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
        private Button rebornButton;
        private TMP_Text rebornCountLabel;
        private readonly AbilityCooldownBinding[] cooldownBindings = new AbilityCooldownBinding[5];
        private GameObject attackPanel;
        private bool? lastControlAvailability;
        private HeroControlMode? lastControlMode;
        private JoystickController subscribedJoystick;
        private HeroAbilitySlot suppressedClickSlot;
        private int suppressClickThroughFrame = -1;
        private bool? lastResolvedFocusTarget;
        private bool offscreenFocusClearPending;

        public JoystickController MovementJoystick => movementJoystick;
        public bool HasCompleteBinding =>
            movementJoystick != null && blinkButton != null && healButton != null && attackButton != null &&
            skill1Button != null && skill2Button != null && skill3Button != null &&
            autoButton != null && targetMonsterButton != null && targetTowerButton != null &&
            rebornButton != null && rebornCountLabel != null && attackPanel != null &&
            HasCompleteCooldownBinding();

        private void Awake()
        {
            EnsureBinding();
        }

        private void Start()
        {
            // Cross-Canvas HUD objects may finish their scene activation after this component's Awake.
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
            RefreshReborn();
            RefreshAbilityCooldowns();
        }

        public void Blink() => UseAbility(HeroAbilitySlot.Blink);
        public void Heal() => UseAbility(HeroAbilitySlot.Heal);
        public void Skill1() => UseAbility(HeroAbilitySlot.Skill1);
        public void Skill2() => UseAbility(HeroAbilitySlot.Skill2);
        public void Skill3() => UseAbility(HeroAbilitySlot.Skill3);

        public void Reborn()
        {
            ResolveHero();
            if (hero != null && hero.CanLocalPlayerControl) hero.RequestRebornServerRpc();
        }

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
            RefreshReborn();
            RefreshAbilityCooldowns();
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
            var preferred = RaidHeroCharacter.Instance;
            if (preferred == null) return;

            // The UI can wake before the owner Hero finishes spawning. In that case Instance initially
            // points at the first replicated/synthetic Hero and later changes to the locally owned one.
            // Never keep that stale binding: it hid Reborn and could route buttons to the wrong Hero.
            if (hero == null ||
                hero == preferred ||
                !hero.IsSpawned ||
                (!hero.IsOwner && preferred.IsOwner))
                hero = preferred;
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
                    case "bt-reborn": rebornButton = button; break;
                }
            }

            // Reborn belongs to the user's bottom HUD rather than CanvasJoyControl. Keep that Editor
            // hierarchy intact and resolve the exact button name from this controller's scene.
            if (rebornButton == null) rebornButton = FindSceneButton("bt-reborn");
            rebornCountLabel = FindNamedChild<TMP_Text>(rebornButton, "reborn_count");
            BindCooldown(0, blinkButton, HeroAbilitySlot.Blink);
            BindCooldown(1, healButton, HeroAbilitySlot.Heal);
            BindCooldown(2, skill1Button, HeroAbilitySlot.Skill1);
            BindCooldown(3, skill2Button, HeroAbilitySlot.Skill2);
            BindCooldown(4, skill3Button, HeroAbilitySlot.Skill3);

            blinkButton?.onClick.AddListener(Blink);
            healButton?.onClick.AddListener(Heal);
            attackButton?.onClick.AddListener(Attack);
            skill1Button?.onClick.AddListener(Skill1);
            skill2Button?.onClick.AddListener(Skill2);
            skill3Button?.onClick.AddListener(Skill3);
            autoButton?.onClick.AddListener(Auto);
            targetMonsterButton?.onClick.AddListener(TargetMonster);
            targetTowerButton?.onClick.AddListener(TargetTower);
            rebornButton?.onClick.AddListener(Reborn);
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
            rebornButton?.onClick.RemoveListener(Reborn);
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

        private void RefreshReborn()
        {
            if (rebornButton == null) return;
            var remaining = hero != null ? hero.RevivesRemaining : 0;
            if (rebornCountLabel != null) rebornCountLabel.text = remaining.ToString();

            var show = hero != null &&
                       hero.CanLocalPlayerControl &&
                       remaining > 0 &&
                       hero.LifeState != HeroLifeState.Active;
            if (rebornButton.gameObject.activeSelf != show)
                rebornButton.gameObject.SetActive(show);
            rebornButton.interactable = show && hero.CanReborn;
        }

        private void RefreshAbilityCooldowns()
        {
            for (var i = 0; i < cooldownBindings.Length; i++)
            {
                var binding = cooldownBindings[i];
                if (binding == null || binding.Button == null) continue;

                var ability = hero != null ? hero.GetAbility(binding.Slot) : null;
                var remaining = hero != null
                    ? Mathf.Max(0f, hero.GetAbilityCooldownRemaining(binding.Slot))
                    : 0f;
                var total = ability != null ? Mathf.Max(0f, ability.cooldownSeconds) : 0f;
                var coolingDown = remaining > 0.001f;

                if (binding.Overlay != null)
                {
                    binding.Overlay.fillAmount = CalculateCooldownFill(remaining, total);
                    if (binding.Overlay.gameObject.activeSelf != coolingDown)
                        binding.Overlay.gameObject.SetActive(coolingDown);
                }

                if (binding.Label != null)
                {
                    binding.Label.text = coolingDown ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
                    if (binding.Label.gameObject.activeSelf != coolingDown)
                        binding.Label.gameObject.SetActive(coolingDown);
                }

                binding.Button.interactable =
                    hero != null &&
                    hero.CanLocalPlayerControl &&
                    hero.CanAct &&
                    ability != null &&
                    hero.HasManaForAbility(binding.Slot) &&
                    !coolingDown;
            }
        }

        public static float CalculateCooldownFill(float remaining, float total)
        {
            return total > 0.001f ? Mathf.Clamp01(remaining / total) : 0f;
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

        private void BindCooldown(int index, Button button, HeroAbilitySlot slot)
        {
            if (index < 0 || index >= cooldownBindings.Length) return;
            var overlay = FindNamedChild<Image>(button, "cool-overlay");
            var label = FindNamedChild<TMP_Text>(button, "cooldown");
            if (overlay != null)
            {
                overlay.type = Image.Type.Filled;
                overlay.fillMethod = Image.FillMethod.Radial360;
                overlay.fillClockwise = true;
                overlay.raycastTarget = false;
            }
            if (label != null) label.raycastTarget = false;
            cooldownBindings[index] = new AbilityCooldownBinding
            {
                Slot = slot,
                Button = button,
                Overlay = overlay,
                Label = label
            };
        }

        private bool HasCompleteCooldownBinding()
        {
            for (var i = 0; i < cooldownBindings.Length; i++)
            {
                var binding = cooldownBindings[i];
                if (binding == null || binding.Button == null ||
                    binding.Overlay == null || binding.Label == null)
                    return false;
            }
            return true;
        }

        private static T FindNamedChild<T>(Button button, string childName) where T : Component
        {
            if (button == null) return null;
            var components = button.GetComponentsInChildren<T>(true);
            for (var i = 0; i < components.Length; i++)
                if (string.Equals(
                        components[i].name,
                        childName,
                        System.StringComparison.OrdinalIgnoreCase))
                    return components[i];
            return null;
        }

        private Button FindSceneButton(string buttonName)
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var buttons = roots[rootIndex].GetComponentsInChildren<Button>(true);
                for (var buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
                    if (string.Equals(
                            buttons[buttonIndex].name,
                            buttonName,
                            System.StringComparison.OrdinalIgnoreCase))
                        return buttons[buttonIndex];
            }
            return null;
        }

        private void RefreshVisibleTargetLock()
        {
            if (hero == null || !hero.CanLocalPlayerControl)
            {
                lastResolvedFocusTarget = null;
                offscreenFocusClearPending = false;
                return;
            }

            var hasResolvedTarget = hero.TryGetFocusTarget(out var resolvedTarget);
            if (hasResolvedTarget)
            {
                var gameplayCamera = ResolveGameplayCamera();
                if (gameplayCamera != null &&
                    !IsCharacterVisible(gameplayCamera, resolvedTarget))
                {
                    // Leaving the player's current screen is an explicit disengage signal. Clear this
                    // exact lock and never search for a replacement, even if another candidate is visible.
                    if (!offscreenFocusClearPending)
                        hero.RequestClearFocusTargetServerRpc();
                    offscreenFocusClearPending = true;
                    lastResolvedFocusTarget = false;
                    return;
                }
                offscreenFocusClearPending = false;
            }
            else
            {
                offscreenFocusClearPending = false;
            }

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
            offscreenFocusClearPending = false;
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

        public static bool IsCharacterVisible(Camera camera, CharacterBase candidate)
        {
            if (camera == null || candidate == null || !camera.isActiveAndEnabled) return false;
            return IsCandidateVisible(
                camera,
                GeometryUtility.CalculateFrustumPlanes(camera),
                candidate);
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
