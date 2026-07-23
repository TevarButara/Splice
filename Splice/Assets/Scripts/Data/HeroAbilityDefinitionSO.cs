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
        public HeroAbilityTargeting targeting = HeroAbilityTargeting.TargetPoint;
        [Min(0.1f)] public float castRange = 7f;
        [Min(0.1f)] public float effectRadius = 2.5f;

        [Header("Effect")]
        public HeroAbilityEffect effect = HeroAbilityEffect.AreaDamage;
        [Min(1)] public int damage = 80;
        [Min(0)] public int healing;
        [Min(0f)] public float movementDistance;
        [Min(0.1f)] public float cooldownSeconds = 8f;

        [Header("Presentation Hook")]
        [Tooltip("Animator state played after the server accepts this action. Missing states are skipped safely.")]
        public string animationState;
        [Tooltip("Local cosmetic spawned on every client after the server accepts the cast — NetworkObject not required")]
        public GameObject castEffectPrefab;
        [Min(0f)] public float castEffectLifetime = 2f;
    }
}
