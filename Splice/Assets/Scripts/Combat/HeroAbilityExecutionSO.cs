using System;
using System.Collections;
using System.Collections.Generic;
using Splice.Characters;
using Splice.Data;
using UnityEngine;

namespace Splice.Combat
{
    // Pluggable server-authoritative skill rule. HeroAbilityDefinitionSO remains the shared
    // cost/cooldown/targeting/VFX contract while unique heroes supply only their execution strategy.
    public abstract class HeroAbilityExecutionSO : ScriptableObject
    {
        public abstract bool TryStart(HeroAbilityExecutionContext context);

        public virtual void Validate(HeroAbilityDefinitionSO ability,
            Action<string, string> reportError)
        {
        }
    }

    public sealed class HeroAbilityExecutionContext
    {
        public MonoBehaviour CoroutineHost { get; set; }
        public Transform HeroTransform { get; set; }
        public HeroAbilityDefinitionSO Ability { get; set; }
        public HeroAbilitySlot Slot { get; set; }
        public Vector3 CastOrigin { get; set; }
        public float WorldScaleFactor { get; set; } = 1f;
        public CharacterBase PreferredTarget { get; set; }
        public Func<List<CharacterBase>> ResolveTargets { get; set; }
        public Func<CharacterBase, bool> IsValidTarget { get; set; }
        public Func<bool> CanContinue { get; set; }
        public Func<Vector3, Vector3> ResolveGroundedDestination { get; set; }
        public Action<Vector3> Face { get; set; }
        public Action<CharacterBase, int> ApplyDamage { get; set; }
        public Action<HeroAbilityVfxStage, Vector3, Vector3, float> Present { get; set; }
        public Action<int> Completed { get; set; }

        public Coroutine StartCoroutine(IEnumerator routine) =>
            CoroutineHost != null && routine != null
                ? CoroutineHost.StartCoroutine(routine)
                : null;
    }
}
