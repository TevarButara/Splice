using Splice.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Splice.Input
{
    // Keeps normal Button clicks for Self/Target skills, while AOE skills own the complete
    // pointer-down → drag → release gesture that begins on the skill button.
    [DisallowMultipleComponent]
    public sealed class HeroSkillDragButton : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        private HeroActionButtonController owner;
        private HeroAbilitySlot slot;
        private bool draggingAbility;

        public void Configure(HeroActionButtonController actionOwner, HeroAbilitySlot abilitySlot)
        {
            owner = actionOwner;
            slot = abilitySlot;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            draggingAbility = owner != null &&
                              owner.TryBeginAbilityDrag(slot, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (draggingAbility) owner.UpdateAbilityDrag(slot, eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!draggingAbility) return;
            owner.ReleaseAbilityDrag(slot, eventData.position);
            draggingAbility = false;
        }

        private void OnDisable()
        {
            if (draggingAbility && owner != null) owner.CancelAbilityDrag(slot);
            draggingAbility = false;
        }
    }
}
