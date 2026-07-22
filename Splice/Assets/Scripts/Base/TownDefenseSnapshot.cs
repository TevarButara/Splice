using System;
using System.Collections.Generic;
using System.Text;
using Splice.Core;
using UnityEngine;

namespace Splice.Base
{
    [Serializable]
    public class TownDefenseSnapshot
    {
        public int schemaVersion = 1;
        public string snapshotId;
        public string deploymentId;
        public int revision;
        public string committedUtc;
        public string ownerAccountId;
        public string factionId;
        public int baseLevel;
        public int basePowerRating;
        public int usedCapacity;
        public int maxCapacity;
        public bool matchmakingEligible;
        public string validationVersion = "prototype-b.6a.v1";
        public List<string> validationWarnings = new();
        public BaseLayout layout;

        // Reserved public-profile fields. Step 6A persists them now so the snapshot format does not need
        // to be broken when the army showcase and hero appearance are connected in later Prototype B steps.
        public string armyShowcasePresetName;
        public string heroAppearanceId;
    }

    public sealed class TownSnapshotValidationReport
    {
        public readonly List<string> errors = new();
        public readonly List<string> warnings = new();
        public bool IsValid => errors.Count == 0;
    }

    public static class TownSnapshotValidator
    {
        public const string RulesVersion = "prototype-b.6a.v1";

        public static TownSnapshotValidationReport Validate(BaseLayout layout, int usedCapacity, int maxCapacity)
        {
            var report = new TownSnapshotValidationReport();
            if (layout == null)
            {
                report.errors.Add("No checked-out town layout was found.");
                return report;
            }

            layout.towers ??= new List<PlacedTowerData>();
            layout.garrison ??= new List<GarrisonMonsterData>();
            layout.minerCardIds ??= new List<string>();

            if (string.IsNullOrWhiteSpace(layout.ownerAccountId)) report.errors.Add("Town owner is missing.");
            if (string.IsNullOrWhiteSpace(layout.factionId)) report.errors.Add("Town faction is missing.");
            if (layout.towers.Count + layout.garrison.Count == 0)
                report.errors.Add("Place at least one tower or garrison unit before deployment.");
            if (usedCapacity < 0 || maxCapacity < 0 || usedCapacity > maxCapacity)
                report.errors.Add($"Defense capacity is invalid ({usedCapacity}/{maxCapacity}).");
            if (layout.storedGold < 0) report.errors.Add("Stored Gold cannot be negative.");

            var occupied = new HashSet<string>();
            for (var i = 0; i < layout.towers.Count; i++)
            {
                var tower = layout.towers[i];
                if (tower == null || string.IsNullOrWhiteSpace(tower.towerId))
                    report.errors.Add($"Tower slot {i + 1} has no content ID.");
                else if (!occupied.Add(PositionKey(tower.position)))
                    report.errors.Add($"Tower slot {i + 1} overlaps another saved defense position.");
            }

            for (var i = 0; i < layout.garrison.Count; i++)
            {
                var unit = layout.garrison[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.cardId))
                    report.errors.Add($"Garrison slot {i + 1} has no content ID.");
                else if (!occupied.Add(PositionKey(unit.position)))
                    report.errors.Add($"Garrison slot {i + 1} overlaps another saved defense position.");
            }

            if (layout.towers.Count == 0) report.warnings.Add("No tower deployed: fast raiders may rush the Core.");
            if (layout.garrison.Count == 0) report.warnings.Add("No garrison deployed: the town has no mobile defense.");
            report.warnings.Add("Core, breach entries and path reachability remain scene contracts until Step 6B server validation.");
            return report;
        }

        public static int CalculateBasePower(BaseLayout layout, int usedCapacity, int baseLevel)
        {
            if (layout == null) return 0;
            var towerPower = 0;
            if (layout.towers != null)
            {
                foreach (var tower in layout.towers)
                {
                    if (tower == null) continue;
                    var upgrades = Mathf.Max(0, tower.attackLevel) + Mathf.Max(0, tower.healthLevel) +
                                   Mathf.Max(0, tower.armorLevel) + Mathf.Max(0, tower.rangeLevel) +
                                   Mathf.Max(0, tower.targetsLevel);
                    towerPower += 100 + upgrades * 20;
                }
            }

            var garrisonPower = (layout.garrison?.Count ?? 0) * 80;
            var capacityPower = Mathf.Max(0, usedCapacity) * 25;
            var progressionPower = Mathf.Max(1, baseLevel) * 100;
            var economyPower = Mathf.Max(0, layout.storedGold) / 100;
            return towerPower + garrisonPower + capacityPower + progressionPower + economyPower;
        }

        private static string PositionKey(Vector3 position) =>
            $"{Mathf.RoundToInt(position.x * 100f)}:{Mathf.RoundToInt(position.z * 100f)}";
    }

    // Local/offline repository for Prototype B. Each committed record has a unique immutable key; publishing
    // revision N+1 only moves the latest pointer and never mutates revision N that an active raid may reference.
    public static class TownSnapshotStore
    {
        private const string IndexPrefix = "Splice.TownSnapshot.Index.v1.";
        private const string SnapshotPrefix = "Splice.TownSnapshot.Record.v1.";
        private const int MaxHistory = 5;

        [Serializable]
        private class SnapshotIndex
        {
            public string factionId;
            public string latestSnapshotId;
            public int latestRevision;
            public List<string> snapshotIds = new();
        }

        public static TownDefenseSnapshot Commit(BaseLayout source, int usedCapacity, int maxCapacity)
        {
            var report = TownSnapshotValidator.Validate(source, usedCapacity, maxCapacity);
            if (!report.IsValid) throw new InvalidOperationException(string.Join(" ", report.errors));

            var layout = DeepCopy(source);
            var index = LoadIndex(layout.factionId);
            var revision = index.latestRevision + 1;
            var snapshot = new TownDefenseSnapshot
            {
                snapshotId = Guid.NewGuid().ToString("N"),
                revision = revision,
                committedUtc = DateTime.UtcNow.ToString("O"),
                ownerAccountId = layout.ownerAccountId,
                factionId = layout.factionId,
                baseLevel = PlayerProfile.BaseLevel(layout.factionId),
                usedCapacity = Mathf.Max(0, usedCapacity),
                maxCapacity = Mathf.Max(0, maxCapacity),
                matchmakingEligible = true,
                validationVersion = TownSnapshotValidator.RulesVersion,
                validationWarnings = new List<string>(report.warnings),
                layout = layout,
            };
            snapshot.basePowerRating = TownSnapshotValidator.CalculateBasePower(
                layout, snapshot.usedCapacity, snapshot.baseLevel);

            PlayerPrefs.SetString(SnapshotKey(snapshot.snapshotId), JsonUtility.ToJson(snapshot));
            index.latestSnapshotId = snapshot.snapshotId;
            index.latestRevision = revision;
            index.snapshotIds ??= new List<string>();
            index.snapshotIds.Add(snapshot.snapshotId);
            while (index.snapshotIds.Count > MaxHistory)
            {
                index.snapshotIds.RemoveAt(0);
            }
            SaveIndex(index);
            PlayerPrefs.Save();
            Debug.Log($"[TownSnapshot] committed {layout.factionId} v{revision} " +
                      $"({snapshot.snapshotId}), power {snapshot.basePowerRating}, capacity {usedCapacity}/{maxCapacity}");
            return snapshot;
        }

        public static TownDefenseSnapshot LoadLatest(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return null;
            var index = LoadIndex(factionId);
            return LoadById(index.latestSnapshotId);
        }

        public static TownDefenseSnapshot LoadById(string snapshotId)
        {
            if (string.IsNullOrWhiteSpace(snapshotId) || !PlayerPrefs.HasKey(SnapshotKey(snapshotId))) return null;
            return JsonUtility.FromJson<TownDefenseSnapshot>(PlayerPrefs.GetString(SnapshotKey(snapshotId)));
        }

        public static IReadOnlyList<TownDefenseSnapshot> LoadHistory(string factionId)
        {
            var result = new List<TownDefenseSnapshot>();
            if (string.IsNullOrWhiteSpace(factionId)) return result;
            var index = LoadIndex(factionId);
            for (var i = index.snapshotIds.Count - 1; i >= 0; i--)
            {
                var snapshot = LoadById(index.snapshotIds[i]);
                if (snapshot != null) result.Add(snapshot);
            }
            return result;
        }

        // Exact-scope cleanup used by editor smoke tests. Never call with a player's real faction.
        public static void DeleteFactionSnapshotsForTests(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return;
            var index = LoadIndex(factionId);
            foreach (var id in index.snapshotIds) PlayerPrefs.DeleteKey(SnapshotKey(id));
            PlayerPrefs.DeleteKey(IndexKey(factionId));
            PlayerPrefs.Save();
        }

        private static SnapshotIndex LoadIndex(string factionId)
        {
            var key = IndexKey(factionId);
            if (!PlayerPrefs.HasKey(key)) return new SnapshotIndex { factionId = factionId };
            var index = JsonUtility.FromJson<SnapshotIndex>(PlayerPrefs.GetString(key)) ??
                        new SnapshotIndex { factionId = factionId };
            index.factionId = factionId;
            index.snapshotIds ??= new List<string>();
            return index;
        }

        private static void SaveIndex(SnapshotIndex index) =>
            PlayerPrefs.SetString(IndexKey(index.factionId), JsonUtility.ToJson(index));

        private static BaseLayout DeepCopy(BaseLayout source) =>
            JsonUtility.FromJson<BaseLayout>(JsonUtility.ToJson(source));

        private static string IndexKey(string factionId) => IndexPrefix + SafeKey(factionId);
        private static string SnapshotKey(string snapshotId) => SnapshotPrefix + snapshotId;

        private static string SafeKey(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
