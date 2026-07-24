using PinePie.SimpleJoystick;
using Splice.Characters;
using Splice.Data;
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
        private bool? lastControlAvailability;

        public JoystickController MovementJoystick => movementJoystick;
        public bool HasCompleteBinding =>
            movementJoystick != null && blinkButton != null && healButton != null && attackButton != null &&
            skill1Button != null && skill2Button != null && skill3Button != null;

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

        public void EnsureBinding()
        {
            UnbindButtons();
            ResolveReferences();
            BindButtons();
        }

        private void UseAbility(HeroAbilitySlot slot)
        {
            ResolveHero();
            if (hero == null || !hero.CanLocalPlayerControl) return;

            var ability = hero.GetAbility(slot);
            if (ability == null)
            {
                hero.RequestAbilityStatusFeedbackServerRpc(slot);
                return;
            }

            if (ability.targeting == HeroAbilityTargeting.TargetPoint &&
                ability.effect == HeroAbilityEffect.AreaDamage)
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
                }
            }

            blinkButton?.onClick.AddListener(Blink);
            healButton?.onClick.AddListener(Heal);
            attackButton?.onClick.AddListener(Attack);
            skill1Button?.onClick.AddListener(Skill1);
            skill2Button?.onClick.AddListener(Skill2);
            skill3Button?.onClick.AddListener(Skill3);
            lastControlAvailability = null;
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
        }

        private void RefreshControlAvailability()
        {
            var available = hero != null && hero.CanLocalPlayerControl;
            if (lastControlAvailability == available) return;
            lastControlAvailability = available;
            SetInteractable(blinkButton, available);
            SetInteractable(healButton, available);
            SetInteractable(attackButton, available);
            SetInteractable(skill1Button, available);
            SetInteractable(skill2Button, available);
            SetInteractable(skill3Button, available);
            if (movementJoystick != null) movementJoystick.enabled = available;
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null) selectable.interactable = interactable;
        }
    }
}
