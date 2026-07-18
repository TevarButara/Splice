using System.Collections.Generic;
using Splice.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Base
{
    // จอเลือกเป้าหมาย raid (roadmap 5.4) — สร้างรายการจาก RaidTargetProvider → เลือก 1 อัน → โหลดซีน raid.
    // ตั้ง RaidContext.Target ให้ RaidSnapshotLoader ในซีน raid อ่านไป spawn ฝ่ายตั้งรับ.
    public class RaidTargetSelectionController : MonoBehaviour
    {
        [SerializeField] private RaidTargetProvider provider;
        [Tooltip("ชื่อซีน raid ที่จะโหลดตอนกดบุก (ต้องอยู่ใน Build Settings)")]
        [SerializeField] private string raidSceneName = "Raid_Greybox";

        private List<RaidTarget> targets = new();
        public IReadOnlyList<RaidTarget> Targets => targets;

        private void Start() => Refresh();

        public void Refresh()
        {
            targets = provider != null ? provider.GenerateTargets() : new List<RaidTarget>();
        }

        // wire ปุ่มเป้าหมาย → Raid(index)
        public void Raid(int index)
        {
            if (index < 0 || index >= targets.Count) return;
            var target = targets[index];
            if (target.layout == null) return;

            // กติกากัน exploit ข้อ 2 (architecture §5.10): บุกเมืองของบัญชีตัวเองไม่ได้
            if (target.layout.ownerAccountId == PlayerProfile.AccountId)
            {
                Debug.LogWarning("[Raid] บุกเมืองของตัวเองไม่ได้ (attacker ≠ defender)");
                return;
            }

            RaidContext.Target = target;
            RaidContext.AttackerFactionId = PlayerProfile.ActiveFactionId;
            RaidContext.LastLootGained = 0;
            SceneManager.LoadScene(raidSceneName);
        }
    }
}
