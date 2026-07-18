using UnityEngine;

namespace Splice.UI
{
    // ⚠️ DEPRECATED (v0.2): โมเดลใหม่ (architecture §1.1) ไม่มีฝั่งถาวร Fort/Monster แล้ว — ผู้เล่นเลือก "faction"
    //    แล้วเข้าเมืองตัวเอง (ดู FactionSelectionController). เก็บคลาสนี้ไว้เป็น "เครื่องมือ dev/test" สลับดูสองฝั่ง
    //    ใน Editor เท่านั้น (Attacker/Defender เป็นบทบาทต่อ raid ไม่ใช่ตัวเลือกของผู้เล่น). อย่าใช้เป็น entry ผู้เล่นจริง.
    //
    // Start-of-match "which side are you?" picker (architecture 5.6/5.9). The map is built reverse — the two
    // sides sit at opposite ends, each with its OWN camera — so choosing a side both routes the player's input
    // to that side AND switches to that side's camera. Purely client-side/presentation: in PvE the host drives
    // both sides, so this only decides what THIS client controls and looks at (PvP assigns sides server-side later).
    //
    // Camera switching enables exactly ONE camera GameObject at a time, which also disables the others'
    // AudioListeners — that's what clears the "There are N audio listeners in the scene" warning when you
    // have separate Main/Fort/Monster cameras.
    public class SideSelectionController : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;

        [Header("Cameras — เปิดทีละตัว (แก้ปัญหา multiple audio listeners ด้วย)")]
        [Tooltip("กล้องภาพรวมตอนกำลังเลือกฝั่ง (เช่น Main) — เว้นว่างได้ ถ้าเว้นจะใช้กล้อง Fort เป็น backdrop")]
        [SerializeField] private Camera overviewCamera;
        [SerializeField] private Camera fortCamera;
        [SerializeField] private Camera monsterCamera;

        [Header("Enable only the chosen side's controllers + UI")]
        [Tooltip("Fort/Defender: TowerPlacementInputController, TowerInteractionController, ปุ่มวางป้อม ฯลฯ")]
        [SerializeField] private GameObject[] fortObjects;
        [Tooltip("Monster/Invader: DeployInputController, การ์ด ฯลฯ")]
        [SerializeField] private GameObject[] monsterObjects;

        private void Start()
        {
            // Nothing is controllable until a side is chosen.
            SetActive(fortObjects, false);
            SetActive(monsterObjects, false);

            // While choosing, show the overview camera (or the Fort camera as a fallback backdrop).
            ActivateOnly(overviewCamera != null ? overviewCamera : fortCamera);

            if (selectionPanel == null)
            {
                Debug.LogError("SideSelectionController: 'Selection Panel' is not assigned in the Inspector — " +
                               "the Fort/Monster picker cannot show. Assign the panel GameObject.", this);
                return;
            }
            selectionPanel.SetActive(true);
            Debug.Log($"[SideSelection] Start ran บน GameObject '{gameObject.name}' (id={GetEntityId()}) " +
                      $"— เปิด panel '{selectionPanel.name}'", this);
        }

        // Wire to the "Fort" button.
        public void ChooseFort() => Choose(isFort: true);

        // Wire to the "Monster" button.
        public void ChooseMonster() => Choose(isFort: false);

        private void Choose(bool isFort)
        {
            ActivateOnly(isFort ? fortCamera : monsterCamera);
            SetActive(fortObjects, isFort);
            SetActive(monsterObjects, !isFort);
            if (selectionPanel != null) selectionPanel.SetActive(false);
        }

        // Enable the given camera's GameObject and disable the other two — so exactly one camera (and one
        // AudioListener) is live at a time.
        private void ActivateOnly(Camera target)
        {
            ToggleCamera(overviewCamera, overviewCamera == target);
            ToggleCamera(fortCamera, fortCamera == target);
            ToggleCamera(monsterCamera, monsterCamera == target);
        }

        private static void ToggleCamera(Camera cam, bool on)
        {
            if (cam != null) cam.gameObject.SetActive(on);
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
                if (obj != null) obj.SetActive(active);
            }
        }
    }
}
