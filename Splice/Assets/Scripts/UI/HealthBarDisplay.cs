using Splice.Characters;
using Splice.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Realtime HP readout for any CharacterBase (monster, tower, fort); reads the networked
    // CurrentHealth/MaxHealth values only — no local prediction, matches the read-only NetworkVariable pattern.
    // Optional shield overlay: a Shield fill layered ON TOP of the HP fill (same bar). โล่หักก่อน HP อยู่แล้ว
    // ฝั่ง server (CharacterBase.ApplyDamage) — อันนี้แค่โชว์: โล่แตก (shrink) แล้วค่อยเห็น HP ลด.
    public class HealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private CharacterBase character;
        [SerializeField] private Image fillImage;
        [Tooltip("หลอดโล่ (Image แบบ Filled Horizontal) ซ้อนทับบนหลอดเลือด — เว้นได้ถ้าไม่ใช้โล่")]
        [SerializeField] private Image shieldFillImage;
        [Tooltip("สี HP ตามสัดส่วนที่เหลือ (SO) — เว้น = ใช้สีคงที่ของ Image")]
        [SerializeField] private BarColorSO hpColor;
        [Tooltip("สีโล่ตามสัดส่วน (SO) — เว้น = ใช้สีคงที่ของ Image")]
        [SerializeField] private BarColorSO shieldColor;
        [SerializeField] private Camera billboardCamera;

        private void Awake()
        {
            if (character == null) character = GetComponentInParent<CharacterBase>();
            if (billboardCamera == null) billboardCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (character == null) return;

            var max = character.MaxHealth;

            if (fillImage != null)
            {
                var f = max > 0 ? (float)character.CurrentHealth / max : 0f;
                fillImage.fillAmount = f;
                if (hpColor != null) fillImage.color = hpColor.Evaluate(f);   // สีตามสัดส่วนที่เหลือ
            }

            // โล่: วัดเป็นสัดส่วนของ MaxHealth (เต็มบาร์เดียวกัน) ซ้อนบน HP — ซ่อนเมื่อโล่ = 0
            if (shieldFillImage != null)
            {
                var hasShield = character.Shield > 0 && max > 0;
                if (shieldFillImage.enabled != hasShield) shieldFillImage.enabled = hasShield;
                if (hasShield)
                {
                    var sf = Mathf.Clamp01((float)character.Shield / max);
                    shieldFillImage.fillAmount = sf;
                    if (shieldColor != null) shieldFillImage.color = shieldColor.Evaluate(sf);
                }
            }

            FaceCamera();
        }

        // หันเข้าหากล้องตลอด (billboard). re-resolve ถ้ากล้องยังไม่พร้อมตอน Awake — ไม่ cache null ค้าง
        private void FaceCamera()
        {
            if (billboardCamera == null)
                billboardCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (billboardCamera != null) transform.rotation = billboardCamera.transform.rotation;
        }
    }
}
