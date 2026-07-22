using Splice.Characters;
using UnityEngine;

namespace Splice.Combat
{
    public enum BreachRing
    {
        Outer = 0,
        Inner = 1,
        Core = 2
    }

    // Authored metadata for one required defense in the three-ring breach. Keeping the ring assignment on
    // the spawned defense lets a future base snapshot serialize the same contract without coordinate rules.
    [DisallowMultipleComponent]
    public sealed class BreachRingObjective : MonoBehaviour
    {
        [SerializeField] private BreachRing ring;
        [SerializeField] private bool required = true;
        [SerializeField] private CharacterBase target;

        public BreachRing Ring => ring;
        public bool Required => required;
        public CharacterBase Target => target;

        private void Awake() => ResolveTarget();

        private void OnValidate() => ResolveTarget();

        private void ResolveTarget()
        {
            if (target == null) target = GetComponent<CharacterBase>();
        }
    }
}
