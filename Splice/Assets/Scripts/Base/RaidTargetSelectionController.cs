using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
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
        private CancellationTokenSource lifetimeCancellation;
        public IReadOnlyList<RaidTarget> Targets => targets;

        private void Awake() => lifetimeCancellation = new CancellationTokenSource();
        private void Start() => Refresh();
        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        public void Refresh() => _ = RefreshAsync();

        public async Task RefreshAsync()
        {
            if (provider == null || lifetimeCancellation == null)
            {
                targets = new List<RaidTarget>();
                return;
            }
            try
            {
                targets = await provider.GenerateTargetsAsync(lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Scene teardown owns cancellation.
            }
        }

        // wire ปุ่มเป้าหมาย → Raid(index)
        public void Raid(int index)
        {
            if (index < 0 || index >= targets.Count) return;
            var target = targets[index];
            if (!RaidContext.TrySelectTarget(target, PlayerProfile.ActiveFactionId, PlayerProfile.AccountId,
                    out var error))
            {
                Debug.LogWarning($"[Raid] target rejected: {error}");
                return;
            }

            Debug.Log(target.IsSnapshotBacked
                ? $"[Raid] locked immutable snapshot {target.snapshotId} v{target.snapshotRevision} before scene load."
                : $"[Raid] locked bot target {target.targetId} before scene load.");
            SceneManager.LoadScene(raidSceneName);
        }
    }
}
