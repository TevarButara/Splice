using Splice.Characters;
using Splice.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // หลอดมานาของ supporter — อ่าน Mana/ManaMax จาก MonsterCharacter (networked, read-only เหมือน HealthBar).
    // ซ่อนอัตโนมัติถ้าไม่ใช่ supporter (warrior ไม่มีมานา).
    public class ManaBarDisplay : MonoBehaviour
    {
        [SerializeField] private MonsterCharacter monster;
        [SerializeField] private Image fillImage;
        [Tooltip("ราก UI ของหลอด (จะปิดถ้าไม่ใช่ supporter). เว้น = ใช้ gameObject นี้")]
        [SerializeField] private GameObject root;
        [Tooltip("สีมานาตามสัดส่วน (SO) — เว้น = ใช้สีคงที่ของ Image")]
        [SerializeField] private BarColorSO manaColor;
        [SerializeField] private Camera billboardCamera;

        private void Awake()
        {
            if (monster == null) monster = GetComponentInParent<MonsterCharacter>();
            if (root == null) root = gameObject;
            if (billboardCamera == null) billboardCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (monster == null || fillImage == null) return;

            var show = monster.IsSupporter;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;

            var f = monster.ManaMaxValue > 0f ? monster.Mana / monster.ManaMaxValue : 0f;
            fillImage.fillAmount = f;
            if (manaColor != null) fillImage.color = manaColor.Evaluate(f);   // สีตามสัดส่วนมานา

            // หันเข้าหากล้องตลอด (billboard). re-resolve ถ้ากล้องยังไม่พร้อมตอน Awake — ไม่ cache null ค้าง
            if (billboardCamera == null)
                billboardCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (billboardCamera != null) transform.rotation = billboardCamera.transform.rotation;
        }
    }
}
