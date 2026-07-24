using Splice.Characters;
using Splice.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Armor is a non-consumable defensive stat. The fill uses the same mitigation curve as damage:
    // armor / (100 + armor), so the bar communicates effective reduction instead of an arbitrary cap.
    public sealed class ArmorBarDisplay : MonoBehaviour
    {
        [SerializeField] private CharacterBase character;
        [SerializeField] private Image fillImage;
        [SerializeField] private BarColorSO armorColor;
        [SerializeField] private Camera billboardCamera;

        private void Awake()
        {
            if (character == null) character = GetComponentInParent<CharacterBase>();
            if (billboardCamera == null) billboardCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (character == null || fillImage == null) return;
            var armor = Mathf.Max(0, character.Armor);
            var fill = armor > 0 ? armor / (100f + armor) : 0f;
            fillImage.fillAmount = fill;
            fillImage.enabled = armor > 0;
            if (armorColor != null) fillImage.color = armorColor.Evaluate(fill);

            if (billboardCamera == null)
                billboardCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (billboardCamera != null) transform.rotation = billboardCamera.transform.rotation;
        }
    }
}
