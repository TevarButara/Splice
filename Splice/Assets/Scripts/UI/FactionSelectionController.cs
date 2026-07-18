using Splice.Core;
using UnityEngine;

namespace Splice.UI
{
    // จอเริ่มเซสชันแบบใหม่ (architecture §1.1): เลือก "faction" ที่จะเล่น แล้วเข้าเมืองของตัวเอง —
    // แทนที่การเลือกฝั่ง Fort/Monster เดิม (โมเดลใหม่ไม่มีฝั่งถาวร; ทุกคนมีเมือง+ทัพของตัวเอง).
    // เก็บ faction ที่เลือกผ่าน PlayerProfile (PlayerPrefs) ให้ระบบอื่นอ่านต่อ.
    // Presentation/local ล้วน เหมือน SideSelectionController เดิม (เปิดกล้อง/AudioListener ทีละตัว
    // เพื่อเลี่ยง warning "There are N audio listeners in the scene").
    public class FactionSelectionController : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;
        [Tooltip("เปิดหลังเลือก faction — เมือง/controller/UI ของผู้เล่น")]
        [SerializeField] private GameObject[] cityObjects;

        [Header("Cameras — เปิดทีละตัว (แก้ multiple audio listeners)")]
        [Tooltip("กล้องตอนกำลังเลือก faction — เว้นว่าง = ใช้ cityCamera เป็น backdrop")]
        [SerializeField] private Camera overviewCamera;
        [SerializeField] private Camera cityCamera;

        private void Start()
        {
            SetActive(cityObjects, false);
            ActivateOnly(overviewCamera != null ? overviewCamera : cityCamera);

            // เคยเลือก faction ไว้แล้ว (กลับเข้าเกม) → ข้ามหน้าเลือกเข้าเมืองเลย
            if (PlayerProfile.HasActiveFaction)
            {
                EnterCity();
                return;
            }

            if (selectionPanel != null) selectionPanel.SetActive(true);
        }

        // Wire จากปุ่ม faction (FactionSelectionButton) — เลือก/สลับ loadout แล้วเข้าเมืองของเผ่านั้น.
        // greybox: ถ้ายังไม่ปลดล็อกและมี slot ว่าง จะปลดล็อกให้อัตโนมัติ (ระบบซื้อ/gate จริงมาทีหลัง)
        public void ChooseFaction(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return;

            if (!PlayerProfile.Owns(factionId))
            {
                if (!PlayerProfile.UnlockFaction(factionId))
                {
                    Debug.LogWarning($"[FactionSelection] ปลดล็อก '{factionId}' ไม่ได้ — เต็มเพดาน {PlayerProfile.MaxCitySlots} เมือง");
                    return;
                }
            }

            PlayerProfile.ActiveFactionId = factionId;
            EnterCity();
        }

        private void EnterCity()
        {
            ActivateOnly(cityCamera);
            SetActive(cityObjects, true);
            if (selectionPanel != null) selectionPanel.SetActive(false);
        }

        private void ActivateOnly(Camera target)
        {
            ToggleCamera(overviewCamera, overviewCamera == target);
            ToggleCamera(cityCamera, cityCamera == target);
        }

        private static void ToggleCamera(Camera cam, bool on)
        {
            if (cam != null) cam.gameObject.SetActive(on);
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
                if (obj != null) obj.SetActive(active);
        }
    }
}
