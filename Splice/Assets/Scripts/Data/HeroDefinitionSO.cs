using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "NewHero", menuName = "Splice/Hero Definition")]
    public class HeroDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string heroId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public GameObject prefab;

        [Header("Presentation")]
        public HeroAnimSetSO animSet;
        [Tooltip("One-shot effect attached to the Hero whenever a normal attack is performed.")]
        public GameObject normalAttackEffectPrefab;
        [Min(0.05f)] public float normalAttackEffectLifetime = 2f;

        [Header("Combat")]
        [Min(1)] public int maxHealth = 300;
        [Min(0)] public int armor = 10;
        [Min(1)] public int attackDamage = 25;
        [Min(0.05f)] public float attackCooldown = 0.8f;
        [Min(0.1f)] public float attackRange = 1.8f;

        [Header("Movement & Auto")]
        [Min(0.1f)] public float moveSpeed = 4f;
        [Min(0.1f)] public float autoAggroRange = 8f;
        [Min(1f)] public float turnSpeedDegPerSec = 720f;
        public bool startsInAutoMode = true;

        [Header("Interaction")]
        [Min(0.1f)] public float interactionRange = 2.5f;

        [Header("Tactical Ability")]
        [Tooltip("Legacy Skill 1 reference. New content should also assign Skill 1 below.")]
        public HeroAbilityDefinitionSO tacticalAbility;

        [Header("Universal Actions")]
        public HeroAbilityDefinitionSO blinkAbility;
        public HeroAbilityDefinitionSO healAbility;

        [Header("Active Skills")]
        public HeroAbilityDefinitionSO skill1;
        public HeroAbilityDefinitionSO skill2;
        public HeroAbilityDefinitionSO skill3;

        [Header("Downed")]
        [Min(1f)] public float downedWindowSeconds = 10f;
        [Range(0.05f, 1f)] public float reviveHealthPercent = 0.4f;
        [Min(0)] public int maxRevivesPerRaid = 1;

        public HeroAbilityDefinitionSO GetAbility(HeroAbilitySlot slot)
        {
            return slot switch
            {
                HeroAbilitySlot.Blink => blinkAbility,
                HeroAbilitySlot.Heal => healAbility,
                HeroAbilitySlot.Skill1 => skill1 != null ? skill1 : tacticalAbility,
                HeroAbilitySlot.Skill2 => skill2,
                HeroAbilitySlot.Skill3 => skill3,
                _ => null
            };
        }
    }
}
