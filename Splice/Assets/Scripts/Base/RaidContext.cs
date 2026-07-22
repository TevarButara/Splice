namespace Splice.Base
{
    public enum RaidTargetSource
    {
        Bot = 0,
        PlayerSnapshot = 1,
    }

    // Target card model. Bot targets carry an in-memory layout; player targets carry only immutable snapshot
    // identity + display metadata. Snapshot-backed layouts are resolved only through ITownSnapshotService.
    [System.Serializable]
    public class RaidTarget
    {
        public string targetId;
        public string displayName;
        public RaidTargetSource source;
        public int baseLevel = 1;
        public int basePowerRating;
        public int usedCapacity;
        public int maxCapacity;
        public int towerCount;
        public int garrisonCount;
        public int storedGoldPreview;
        public string ownerAccountId;
        public string factionId;
        public string snapshotId;
        public int snapshotRevision;
        public string committedUtc;
        public string validationVersion;
        public bool matchmakingEligible = true;
        public bool inspectionOnly;
        public bool isRevenge;
        public string revengeReportId;
        public string revengeRequestId;
        public bool isIncomingDefense;
        public string simulatedAttackerSnapshotId;
        public int simulatedAttackerSnapshotRevision;
        public BaseLayout layout;
        public bool Looted;   // ปล้นไปแล้ว (กัน replay ตีซ้ำเป้าเดิมเพื่อ farm loot — stand-in ของ cooldown rule 3)

        public bool IsSnapshotBacked => source == RaidTargetSource.PlayerSnapshot && !string.IsNullOrWhiteSpace(snapshotId);
        public int StoredGold => IsSnapshotBacked
            ? System.Math.Max(0, storedGoldPreview)
            : layout != null ? System.Math.Max(0, layout.storedGold) : System.Math.Max(0, storedGoldPreview);

        public bool CanRaid(string attackerAccountId, out string reason)
        {
            if (inspectionOnly)
            {
                reason = "This deployed town belongs to the current account and is inspection-only.";
                return false;
            }
            if (!matchmakingEligible)
            {
                reason = "Target is no longer eligible for matchmaking.";
                return false;
            }
            if (Looted)
            {
                reason = "Target was already looted in this local session.";
                return false;
            }

            if (!IsSnapshotBacked && layout == null)
            {
                reason = "Target layout is unavailable.";
                return false;
            }

            var defender = !string.IsNullOrWhiteSpace(ownerAccountId) ? ownerAccountId : layout?.ownerAccountId;
            if (!string.IsNullOrWhiteSpace(attackerAccountId) && defender == attackerAccountId)
            {
                reason = "Attacker and defender accounts must be different.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static RaidTarget FromSnapshot(TownDefenseSnapshot snapshot, string name, bool inspectionOnly)
        {
            if (snapshot == null) return null;
            return new RaidTarget
            {
                targetId = "snapshot:" + snapshot.snapshotId,
                displayName = string.IsNullOrWhiteSpace(name) ? $"{snapshot.factionId} Town" : name,
                source = RaidTargetSource.PlayerSnapshot,
                baseLevel = snapshot.baseLevel,
                basePowerRating = snapshot.basePowerRating,
                usedCapacity = snapshot.usedCapacity,
                maxCapacity = snapshot.maxCapacity,
                towerCount = snapshot.layout?.towers?.Count ?? 0,
                garrisonCount = snapshot.layout?.garrison?.Count ?? 0,
                storedGoldPreview = snapshot.layout?.storedGold ?? 0,
                ownerAccountId = snapshot.ownerAccountId,
                factionId = snapshot.factionId,
                snapshotId = snapshot.snapshotId,
                snapshotRevision = snapshot.revision,
                committedUtc = snapshot.committedUtc,
                validationVersion = snapshot.validationVersion,
                matchmakingEligible = snapshot.matchmakingEligible,
                inspectionOnly = inspectionOnly,
                // Intentionally null. Runtime must resolve the immutable record by snapshotId.
                layout = null,
            };
        }
    }

    // ตัวส่งต่อ "เป้าหมายที่กำลังบุก" ข้ามซีน (จอเลือกเป้า → ซีน raid) — static คงอยู่ข้ามการโหลดซีน (architecture §5.10).
    // ต่อไป (server) จะแทนด้วยการส่ง snapshot ผู้เล่นจริงมาที่นี่.
    public static class RaidContext
    {
        public static RaidTarget Target;          // เป้าหมายรอบนี้
        public static string AttackerFactionId;   // เผ่าที่ผู้เล่นใช้บุก (loadout)
        public static int LastLootGained;         // loot รอบล่าสุด (จอผลอ่าน)
        public static bool HasLastWarGemSettlement;
        public static int LastWarGemStake;
        public static int LastWarGemPayout;
        public static int LastWarGemNet;
        public static int LastWarGemBalance;
        public static string LastWarGemSettlementNote;

        public static bool HasTarget => Target != null && (Target.IsSnapshotBacked || Target.layout != null);
        public static string TargetSnapshotId => Target?.snapshotId ?? string.Empty;
        public static int TargetSnapshotRevision => Target?.snapshotRevision ?? 0;

        public static bool TrySelectTarget(RaidTarget target, string attackerFactionId, string attackerAccountId,
            out string error)
        {
            if (target == null)
            {
                error = "Target is missing.";
                return false;
            }
            if (!target.CanRaid(attackerAccountId, out error)) return false;

            Target = target;
            AttackerFactionId = attackerFactionId;
            ClearLastRaidResults();
            return true;
        }

        // Bot layouts are an in-memory cache. Immutable player snapshots must be loaded through ITownSnapshotService.
        public static BaseLayout ResolveTargetLayout() => Target != null && !Target.IsSnapshotBacked
            ? Target.layout
            : null;

        public static void SetLastWarGemSettlement(int stake, int payout, int balance, string note)
        {
            HasLastWarGemSettlement = true;
            LastWarGemStake = System.Math.Max(0, stake);
            LastWarGemPayout = System.Math.Max(0, payout);
            LastWarGemNet = LastWarGemPayout - LastWarGemStake;
            LastWarGemBalance = System.Math.Max(0, balance);
            LastWarGemSettlementNote = note ?? string.Empty;
        }

        public static void ClearLastRaidResults()
        {
            LastLootGained = 0;
            HasLastWarGemSettlement = false;
            LastWarGemStake = 0;
            LastWarGemPayout = 0;
            LastWarGemNet = 0;
            LastWarGemBalance = 0;
            LastWarGemSettlementNote = string.Empty;
        }

        public static void Clear()
        {
            Target = null;
            ClearLastRaidResults();
        }
    }
}
