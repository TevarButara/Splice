using UnityEngine;

namespace Splice.Combat
{
    // Server-side extraction checkpoint. Step 3 will call TryExtract after validating that the active
    // Hero is inside this point; step 2 provides the result/loot contract and an Editor smoke test.
    public class ExtractionPoint : MonoBehaviour
    {
        [SerializeField] private RaidManager raidManager;
        [SerializeField] private RaidLootController lootController;
        [Tooltip("เมื่อถอนที่ checkpoint ให้นำของที่กำลังแบกเข้า Secured ก่อนจบ raid")]
        [SerializeField] private bool secureCarriedOnExtract = true;
        [Tooltip("ป้องกันการถอนเปล่าใน prototype")]
        [SerializeField] private bool requireSecuredLoot = true;

        public bool TryExtract()
        {
            ResolveReferences();
            if (raidManager == null || lootController == null || !raidManager.IsServer || raidManager.IsOver)
                return false;

            if (secureCarriedOnExtract) lootController.SecureCarried();
            if (requireSecuredLoot && lootController.Secured <= 0) return false;

            return raidManager.TryCompleteExtraction();
        }

        private void ResolveReferences()
        {
            if (raidManager == null) raidManager = FindFirstObjectByType<RaidManager>();
            if (lootController == null) lootController = FindFirstObjectByType<RaidLootController>();
        }

        [ContextMenu("Debug/Extract Now")]
        private void DebugExtract()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Extraction] Debug command works only in Play Mode.", this);
                return;
            }

            if (!TryExtract())
                Debug.LogWarning("[Extraction] Rejected — carry/secure loot first and run on the host.", this);
        }
    }
}
