#if UNITY_EDITOR
using Splice.Base;
using Splice.Combat;
using Splice.Core;
using UnityEditor;
using UnityEngine;

namespace Splice.EditorTools
{
    public static class Step6CRaidSceneContractDiagnostics
    {
        [MenuItem("Splice/Diagnostics/Run Step 6B-C Raid Scene Contract Test")]
        public static void Run()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[Step 6B-C] FAIL — run this diagnostic in Edit Mode.");
                return;
            }

            var adapter = Object.FindFirstObjectByType<RaidSceneAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[Step 6B-C] FAIL — RaidSceneAdapter is missing from the active scene.");
                return;
            }

            try
            {
                RaidContext.Clear();
                RaidSessionContext.Clear();
                if (!adapter.TryPrepareRaid(out var target, out var error))
                    throw new System.InvalidOperationException("Preparation failed: " + error);

                var report = adapter.LastContractReport;
                if (report == null || !report.valid || !report.completePathFound)
                    throw new System.InvalidOperationException("Scene contract did not prove a complete path.");
                if (!RaidSessionContext.IsPrepared || RaidSessionContext.Current.targetId != target.targetId)
                    throw new System.InvalidOperationException("Prepared session did not lock the target identity.");

                var diagnosticRaidId = "diagnostic_" + System.Guid.NewGuid().ToString("N");
                if (!RaidSessionContext.TryBindAndStart(diagnosticRaidId, target, report, out error))
                    throw new System.InvalidOperationException("Raid ID binding failed: " + error);
                if (!RaidSessionContext.IsStarted || RaidSessionContext.Current.raidId != diagnosticRaidId)
                    throw new System.InvalidOperationException("Economy raid ID was not retained by the session.");

                var lockedTargetId = RaidSessionContext.Current.targetId;
                var lockedSnapshotId = RaidSessionContext.Current.snapshotId;
                var lockedSnapshotRevision = RaidSessionContext.Current.snapshotRevision;
                RaidSessionContext.MarkCompleted(RaidOutcome.FullVictory, 3);
                if (RaidSessionContext.Current.phase != RaidSessionPhase.Completed ||
                    RaidSessionContext.Current.targetId != lockedTargetId ||
                    RaidSessionContext.Current.snapshotId != lockedSnapshotId ||
                    RaidSessionContext.Current.snapshotRevision != lockedSnapshotRevision)
                    throw new System.InvalidOperationException("Completion changed immutable raid identity.");

                Debug.Log($"[Step 6B-C] PASS — {report.contractVersion}; PathComplete " +
                          $"({report.pathCornerCount} corners, {report.pathDistance:F1}m); target {lockedTargetId}; " +
                          $"raidId linked; immutable snapshot {lockedSnapshotId} v{lockedSnapshotRevision}; result linked.");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Step 6B-C] FAIL — " + exception.Message);
            }
            finally
            {
                RaidContext.Clear();
                RaidSessionContext.Clear();
            }
        }
    }
}
#endif
