using UnityEngine;

namespace Splice.Data
{
    // Data contract for a Hero's tactical ability. Step 4B implements the first behavior (Breach Charge)
    // while keeping tuning, HUD identity and effect hooks out of the runtime character code.
    [CreateAssetMenu(fileName = "NewHeroAbility", menuName = "Splice/Hero Ability Definition")]
    public class HeroAbilityDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId = "breach_charge";
        public string displayName = "Breach Charge";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Targeting")]
        [Min(0.1f)] public float castRange = 7f;
        [Min(0.1f)] public float effectRadius = 2.5f;

        [Header("Effect")]
        [Min(1)] public int damage = 80;
        [Min(0.1f)] public float cooldownSeconds = 8f;

        [Header("Presentation Hook")]
        [Tooltip("Local cosmetic spawned on every client after the server accepts the cast — NetworkObject not required")]
        public GameObject castEffectPrefab;
        [Min(0f)] public float castEffectLifetime = 2f;
    }
}
