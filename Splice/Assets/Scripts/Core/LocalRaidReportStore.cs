using System;
using System.Collections.Generic;
using System.Text;
using Splice.Base;
using Splice.Combat;
using UnityEngine;

namespace Splice.Core
{
    [Serializable]
    public sealed class LocalRaidReportRecord
    {
        public string reportId;
        public string sourceRaidId;
        public string attackerAccountId;
        public string defenderAccountId;
        public string targetId;
        public string targetDisplayName;
        public string factionId;
        public string defenderSnapshotId;
        public int defenderSnapshotRevision;
        public string attackerSnapshotId;
        public int attackerSnapshotRevision;
        public RaidOutcome outcome;
        public int breachedRings;
        public int entryStake;
        public int payout;
        public int netWarGems;
        public int goldLoot;
        public string completedUtc;
        public string pendingRevengeRequestId;
        public string pendingRevengeCreatedUtc;
        public string lastRevengeRequestId;
        public string lastRevengeRaidId;
        public string lastRevengeStartedUtc;

        public bool HasRevengeTarget => !string.IsNullOrWhiteSpace(attackerSnapshotId);
    }

    // Local persistence proof for Prototype B Step 6D. Production replaces this PlayerPrefs repository with
    // server report/outbox records while preserving reportId, sourceRaidId and revenge idempotency contracts.
    public static class LocalRaidReportStore
    {
        private const string StoreKey = "Splice.LocalRaidReports.v1";
        private const int MaxReports = 40;
        public static readonly TimeSpan RevengeCooldown = TimeSpan.FromHours(4);

        [Serializable]
        private sealed class ReportCollection
        {
            public List<LocalRaidReportRecord> reports = new();
        }

#if UNITY_EDITOR
        private static string editorTestScope = string.Empty;
#endif

        public static bool TryRecordCompletedRaid(RaidSessionIdentity session, RaidStakeTransaction transaction,
            int goldLoot, out LocalRaidReportRecord record, out string error)
        {
            record = null;
            error = string.Empty;
            if (session == null || session.phase != RaidSessionPhase.Completed)
            {
                error = "Raid session is not completed.";
                return false;
            }
            if (transaction == null || !transaction.settled || string.IsNullOrWhiteSpace(transaction.raidId))
            {
                error = "Settled economy transaction is missing.";
                return false;
            }
            if (session.raidId != transaction.raidId)
            {
                error = "Session and economy raid IDs do not match.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(session.attackerAccountId) ||
                string.IsNullOrWhiteSpace(session.defenderAccountId))
            {
                error = "Raid ownership identity is incomplete.";
                return false;
            }
            if (session.attackerAccountId == session.defenderAccountId)
            {
                error = "Self-target raid reports are forbidden.";
                return false;
            }
            if (session.outcome == RaidOutcome.InProgress || transaction.outcome != session.outcome)
            {
                error = "Session and economy outcomes do not match.";
                return false;
            }
            if (Mathf.Clamp(transaction.breachedRings, 0, 3) !=
                Mathf.Clamp(session.breachedRings, 0, 3))
            {
                error = "Session and economy breached-ring results do not match.";
                return false;
            }

            var collection = Load();
            var reportId = ReportIdFor(transaction.raidId);
            var existing = Find(collection, reportId);
            if (existing != null)
            {
                if (!SameImmutableResult(existing, session, transaction))
                {
                    error = "A conflicting result already exists for this raid ID.";
                    return false;
                }

                var mergedLoot = Math.Max(existing.goldLoot, Math.Max(0, goldLoot));
                if (mergedLoot != existing.goldLoot)
                {
                    existing.goldLoot = mergedLoot;
                    Save(collection);
                }
                record = existing;
                return true;
            }

            var stake = transaction.offer != null ? Math.Max(0, transaction.offer.entryStake) : 0;
            record = new LocalRaidReportRecord
            {
                reportId = reportId,
                sourceRaidId = transaction.raidId,
                attackerAccountId = session.attackerAccountId,
                defenderAccountId = session.defenderAccountId,
                targetId = session.targetId,
                targetDisplayName = session.targetDisplayName,
                factionId = session.targetFactionId,
                defenderSnapshotId = session.snapshotId,
                defenderSnapshotRevision = session.snapshotRevision,
                attackerSnapshotId = session.attackerTownSnapshotId,
                attackerSnapshotRevision = session.attackerTownSnapshotRevision,
                outcome = session.outcome,
                breachedRings = Mathf.Clamp(session.breachedRings, 0, 3),
                entryStake = stake,
                payout = Math.Max(0, transaction.payout),
                netWarGems = Math.Max(0, transaction.payout) - stake,
                goldLoot = Math.Max(0, goldLoot),
                completedUtc = !string.IsNullOrWhiteSpace(session.completedUtc)
                    ? session.completedUtc
                    : DateTime.UtcNow.ToString("O"),
            };
            collection.reports.Add(record);
            while (collection.reports.Count > MaxReports) collection.reports.RemoveAt(0);
            Save(collection);
            Debug.Log($"[RaidReport] recorded {record.reportId}: {record.attackerAccountId} -> " +
                      $"{record.defenderAccountId}, {record.outcome}, payout {record.payout}.");
            return true;
        }

        // Called by both settlement and loot listeners. Event order does not matter: the first complete call
        // creates the report and a later call only merges a larger gold-loot value into the same report ID.
        public static bool TryRecordCurrentCompletedRaid(int goldLoot, out LocalRaidReportRecord record)
        {
            return TryRecordCompletedRaid(RaidSessionContext.Current, LocalWarGemEconomy.ActiveRaid,
                goldLoot, out record, out _);
        }

        public static IReadOnlyList<LocalRaidReportRecord> LoadAll()
        {
            var source = Load().reports;
            var result = new List<LocalRaidReportRecord>(source.Count);
            for (var i = source.Count - 1; i >= 0; i--) result.Add(source[i]);
            return result;
        }

        public static IReadOnlyList<LocalRaidReportRecord> LoadDefenseHistory(string defenderAccountId)
        {
            var result = new List<LocalRaidReportRecord>();
            if (string.IsNullOrWhiteSpace(defenderAccountId)) return result;
            var source = Load().reports;
            for (var i = source.Count - 1; i >= 0; i--)
                if (source[i] != null && source[i].defenderAccountId == defenderAccountId) result.Add(source[i]);
            return result;
        }

        public static bool TrySelectRevenge(string reportId, string requestingAccountId, string attackerFactionId,
            DateTime utcNow, out RaidTarget target, out string error)
        {
            if (!TryPrepareRevenge(reportId, requestingAccountId, utcNow, out target, out error)) return false;
            if (RaidContext.TrySelectTarget(target, attackerFactionId, requestingAccountId, out error)) return true;
            ClearPendingRequest(reportId, target.revengeRequestId);
            target = null;
            return false;
        }

        public static bool TryPrepareRevenge(string reportId, string requestingAccountId, DateTime utcNow,
            out RaidTarget target, out string error)
        {
            target = null;
            if (!TryGetRevengeReadyReport(reportId, requestingAccountId, utcNow, out var collection,
                    out var report, out var snapshot, out error)) return false;

            var requestId = Guid.NewGuid().ToString("N");
            target = RaidTarget.FromSnapshot(snapshot,
                $"Revenge • {ShortAccount(report.attackerAccountId)}", false);
            if (target == null)
            {
                error = "Revenge target could not be created from the attacker snapshot.";
                return false;
            }
            target.targetId = $"revenge:{report.reportId}:{snapshot.snapshotId}";
            target.isRevenge = true;
            target.revengeReportId = report.reportId;
            target.revengeRequestId = requestId;
            report.pendingRevengeRequestId = requestId;
            report.pendingRevengeCreatedUtc = utcNow.ToUniversalTime().ToString("O");
            Save(collection);
            return true;
        }

        public static bool CanStartRevenge(string reportId, string requestId, string requestingAccountId,
            DateTime utcNow, out string error)
        {
            if (!TryGetRevengeReadyReport(reportId, requestingAccountId, utcNow, out _, out var report,
                    out _, out error)) return false;
            if (string.IsNullOrWhiteSpace(requestId) || report.pendingRevengeRequestId != requestId)
            {
                error = "Revenge request is stale or was replaced.";
                return false;
            }
            return true;
        }

        public static bool TryMarkRevengeStarted(string reportId, string requestId, string requestingAccountId,
            string economyRaidId, DateTime utcNow, out string error)
        {
            error = string.Empty;
            var collection = Load();
            var report = Find(collection, reportId);
            if (report == null)
            {
                error = "Defense report is unavailable.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(requestingAccountId) || report.defenderAccountId != requestingAccountId)
            {
                error = "Only the original defender can start this revenge raid.";
                return false;
            }
            if (report.attackerAccountId == requestingAccountId)
            {
                error = "Revenge cannot target the same account.";
                return false;
            }

            // Server retry of the same accepted start is a successful no-op.
            if (report.lastRevengeRequestId == requestId && report.lastRevengeRaidId == economyRaidId)
                return true;
            if (!CanStartRevenge(reportId, requestId, requestingAccountId, utcNow, out error)) return false;
            if (string.IsNullOrWhiteSpace(economyRaidId) || economyRaidId == report.sourceRaidId)
            {
                error = "Revenge must use a fresh economy raid transaction ID.";
                return false;
            }

            // Reload after CanStartRevenge so we update the latest persisted instance.
            collection = Load();
            report = Find(collection, reportId);
            report.lastRevengeRequestId = requestId;
            report.lastRevengeRaidId = economyRaidId;
            report.lastRevengeStartedUtc = utcNow.ToUniversalTime().ToString("O");
            report.pendingRevengeRequestId = string.Empty;
            report.pendingRevengeCreatedUtc = string.Empty;
            Save(collection);
            Debug.Log($"[RaidReport] revenge {economyRaidId} started from {report.reportId}; " +
                      $"source transaction {report.sourceRaidId} remains immutable.");
            return true;
        }

        private static bool TryGetRevengeReadyReport(string reportId, string requestingAccountId, DateTime utcNow,
            out ReportCollection collection, out LocalRaidReportRecord report, out TownDefenseSnapshot snapshot,
            out string error)
        {
            collection = Load();
            report = Find(collection, reportId);
            snapshot = null;
            error = string.Empty;
            if (report == null)
            {
                error = "Defense report is unavailable.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(requestingAccountId) || report.defenderAccountId != requestingAccountId)
            {
                error = "Only the original defender can start this revenge raid.";
                return false;
            }
            if (report.attackerAccountId == requestingAccountId)
            {
                error = "Revenge cannot target the same account.";
                return false;
            }
            if (IsOnCooldown(report, utcNow, out var remaining))
            {
                error = $"Revenge cooldown is active for {Math.Ceiling(remaining.TotalMinutes)} more minute(s).";
                return false;
            }
            if (string.IsNullOrWhiteSpace(report.attackerSnapshotId))
            {
                error = "The attacker's deployed snapshot was not included in this report.";
                return false;
            }

            snapshot = TownSnapshotStore.LoadById(report.attackerSnapshotId);
            if (snapshot == null)
            {
                error = "The attacker's immutable snapshot is no longer available.";
                return false;
            }
            if (snapshot.ownerAccountId != report.attackerAccountId)
            {
                error = "Revenge snapshot ownership does not match the original attacker.";
                return false;
            }
            if (snapshot.revision != report.attackerSnapshotRevision)
            {
                error = "Revenge snapshot revision changed from the recorded attack.";
                return false;
            }
            if (!RaidTargetPool.IsPoolEligible(snapshot, out error)) return false;
            return true;
        }

        private static bool IsOnCooldown(LocalRaidReportRecord report, DateTime utcNow, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(report.lastRevengeStartedUtc) ||
                !DateTime.TryParse(report.lastRevengeStartedUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var started)) return false;
            var ends = started.ToUniversalTime() + RevengeCooldown;
            remaining = ends - utcNow.ToUniversalTime();
            return remaining > TimeSpan.Zero;
        }

        private static bool SameImmutableResult(LocalRaidReportRecord existing, RaidSessionIdentity session,
            RaidStakeTransaction transaction)
        {
            var stake = transaction.offer != null ? Math.Max(0, transaction.offer.entryStake) : 0;
            return existing.sourceRaidId == transaction.raidId &&
                   existing.attackerAccountId == session.attackerAccountId &&
                   existing.defenderAccountId == session.defenderAccountId &&
                   existing.targetId == session.targetId &&
                   existing.defenderSnapshotId == session.snapshotId &&
                   existing.defenderSnapshotRevision == session.snapshotRevision &&
                   existing.outcome == session.outcome &&
                   existing.breachedRings == Mathf.Clamp(session.breachedRings, 0, 3) &&
                   existing.entryStake == stake && existing.payout == Math.Max(0, transaction.payout);
        }

        private static void ClearPendingRequest(string reportId, string requestId)
        {
            var collection = Load();
            var report = Find(collection, reportId);
            if (report == null || report.pendingRevengeRequestId != requestId) return;
            report.pendingRevengeRequestId = string.Empty;
            report.pendingRevengeCreatedUtc = string.Empty;
            Save(collection);
        }

        private static LocalRaidReportRecord Find(ReportCollection collection, string reportId)
        {
            if (collection?.reports == null || string.IsNullOrWhiteSpace(reportId)) return null;
            for (var i = 0; i < collection.reports.Count; i++)
                if (collection.reports[i] != null && collection.reports[i].reportId == reportId)
                    return collection.reports[i];
            return null;
        }

        private static ReportCollection Load()
        {
            if (!PlayerPrefs.HasKey(ActiveStoreKey)) return new ReportCollection();
            var collection = JsonUtility.FromJson<ReportCollection>(PlayerPrefs.GetString(ActiveStoreKey)) ??
                             new ReportCollection();
            collection.reports ??= new List<LocalRaidReportRecord>();
            return collection;
        }

        private static void Save(ReportCollection collection)
        {
            PlayerPrefs.SetString(ActiveStoreKey, JsonUtility.ToJson(collection));
            PlayerPrefs.Save();
        }

        private static string ReportIdFor(string raidId) => "report:" + raidId;
        private static string ShortAccount(string accountId) =>
            string.IsNullOrWhiteSpace(accountId) ? "Unknown Raider" :
            accountId.Length <= 8 ? accountId : accountId.Substring(0, 8);

        private static string ActiveStoreKey
        {
            get
            {
#if UNITY_EDITOR
                return string.IsNullOrEmpty(editorTestScope) ? StoreKey : StoreKey + ".test." + SafeKey(editorTestScope);
#else
                return StoreKey;
#endif
            }
        }

        private static string SafeKey(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

#if UNITY_EDITOR
        public static void UseIsolatedStorageForTests(string scope)
        {
            editorTestScope = scope ?? string.Empty;
        }

        public static void DeleteIsolatedStorageForTests()
        {
            if (string.IsNullOrEmpty(editorTestScope)) return;
            PlayerPrefs.DeleteKey(ActiveStoreKey);
            PlayerPrefs.Save();
            editorTestScope = string.Empty;
        }
#endif
    }
}
