using Splice.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Realtime HP readout for any CharacterBase (monster, tower, fort); reads the networked
    // CurrentHealth/MaxHealth values only — no local prediction, matches the read-only NetworkVariable pattern.
    public class HealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private CharacterBase character;
        [SerializeField] private Image fillImage;
        [SerializeField] private Camera billboardCamera;

        private void Awake()
        {
            if (character == null) character = GetComponentInParent<CharacterBase>();
            if (billboardCamera == null) billboardCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (character == null || fillImage == null) return;

            fillImage.fillAmount = character.MaxHealth > 0
                ? (float)character.CurrentHealth / character.MaxHealth
                : 0f;

            if (billboardCamera != null)
            {
                transform.rotation = billboardCamera.transform.rotation;
            }
        }
    }
}
