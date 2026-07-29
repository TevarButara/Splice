using Splice.Combat;
using Splice.FxStudio;
using UnityEngine;

namespace Splice.Data
{
    public enum HeroAbilitySlot
    {
        Blink,
        Heal,
        Skill1,
        Skill2,
        Skill3
    }

    public enum HeroAbilityEffect
    {
        AreaDamage,
        ForwardBlink,
        SelfHeal
    }

    public enum HeroAbilityTargeting
    {
        TargetPoint,
        Self,
        Forward
    }

    public enum HeroAbilityCastType
    {
        [InspectorName("AOE — Drag From Skill Button")] DragArea,
        [InspectorName("Self — Cast Around Hero Immediately")] SelfCast,
        [InspectorName("Target — Locked Target, Forward Fallback")] LockedTarget
    }

    public enum HeroAbilityDamageMode
    {
        Instant,
        [InspectorName("Damage Over Time")] DamageOverTime
    }

    public enum HeroAbilityEffectPlacement
    {
        GroundSurface,
        HeroRoot,
        HeroEffectAnchor,
        WorldPoint
    }

    // Server-authoritative data contract shared by universal actions and hero-specific skills.
    [CreateAssetMenu(fileName = "NewHeroAbility", menuName = "Splice/Hero Ability Definition")]
    public class HeroAbilityDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId = "breach_charge";
        public string displayName = "Breach Charge";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Targeting")]
        [Tooltip("Self casts instantly around the Hero. Target uses the locked target or fires forward. AOE is aimed by dragging from the skill button and releases on the ground.")]
        public HeroAbilityCastType castType = HeroAbilityCastType.DragArea;
        [Tooltip("Legacy low-level targeting used by universal Blink/Heal effects.")]
        public HeroAbilityTargeting targeting = HeroAbilityTargeting.TargetPoint;
        [Min(0.1f)] public float castRange = 7f;
        [Min(0.1f)] public float effectRadius = 2.5f;

        [Header("Effect")]
        public HeroAbilityEffect effect = HeroAbilityEffect.AreaDamage;
        public HeroAbilityDamageMode damageMode = HeroAbilityDamageMode.Instant;
        [Tooltip("Instant = damage per cast. DOT = TOTAL damage dealt to each target that remains affected for the full duration.")]
        [Min(1)] public int damage = 80;
        [Tooltip("Duration of a DOT zone/effect. Ignored by Instant damage.")]
        [Min(0.05f)] public float damageDurationSeconds = 3f;
        [Tooltip("Server tick interval for DOT. Total Damage is divided exactly across these ticks.")]
        [Min(0.05f)] public float dotTickIntervalSeconds = 0.5f;
        [Min(0)] public int healing;
        [Min(0f)] public float movementDistance;
        [Min(0f)] public float manaCost;
        [Min(0.1f)] public float cooldownSeconds = 8f;

        [Header("Optional Execution Strategy")]
        [Tooltip("Leave empty for the standard instant/DOT ability pipeline. Assign a strategy asset when this skill has unique server-authoritative rules such as Rowan's multi-dash Ultimate.")]
        public HeroAbilityExecutionSO execution;

        [Header("Presentation Hook")]
        [Tooltip("Animator state played after the server accepts this action. Missing states are skipped safely.")]
        public string animationState;
        [Tooltip("Local cosmetic spawned on every client after the server accepts the cast — NetworkObject not required")]
        public GameObject castEffectPrefab;
        [Min(0f)] public float castEffectLifetime = 2f;
        [Tooltip("Ground skills snap to the terrain. Self effects follow the Hero. Hero Effect Anchor uses the optional socket on RaidHeroCharacter.")]
        public HeroAbilityEffectPlacement effectPlacement = HeroAbilityEffectPlacement.GroundSurface;
        public Vector3 effectLocalOffset;
        [Header("Staged VFX Presentation")]
        [Tooltip("Optional Splice FX Studio package. Exported Studio stages take precedence over the legacy cue for the same stage.")]
        public SpliceFxSkillPackage fxStudioPackage;
        [Tooltip("Optional staged presentation. When any cue is assigned, the legacy single castEffectPrefab is skipped.")]
        public HeroAbilityVfxCue castVfx = new();
        public HeroAbilityVfxCue launchVfx = new();
        public HeroAbilityVfxCue travelVfx = new();
        public HeroAbilityVfxCue impactVfx = new();
        public HeroAbilityVfxCue persistentVfx = new();
        public HeroAbilityVfxCue endVfx = new();

        public bool HasStagedVfx =>
            fxStudioPackage?.HasConfiguredStage == true ||
            castVfx?.IsConfigured == true ||
            launchVfx?.IsConfigured == true ||
            travelVfx?.IsConfigured == true ||
            impactVfx?.IsConfigured == true ||
            persistentVfx?.IsConfigured == true ||
            endVfx?.IsConfigured == true;

        [Min(0f)] public float groundEffectOffset = 0.05f;

        public int DamageTickCount =>
            damageMode == HeroAbilityDamageMode.Instant
                ? 1
                : Mathf.Min(
                    Mathf.Max(1, damage),
                    Mathf.Max(1, Mathf.CeilToInt(
                        Mathf.Max(0.05f, damageDurationSeconds) /
                        Mathf.Max(0.05f, dotTickIntervalSeconds))));

        public int DamageAtTick(int zeroBasedTick)
        {
            if (zeroBasedTick < 0 || zeroBasedTick >= DamageTickCount) return 0;
            var total = Mathf.Max(0, damage);
            var baseDamage = total / DamageTickCount;
            var remainder = total % DamageTickCount;
            return baseDamage + (zeroBasedTick < remainder ? 1 : 0);
        }
    }
}
