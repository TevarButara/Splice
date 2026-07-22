using System;
using System.Collections.Generic;
using Splice.Base;
using Splice.Combat;
using UnityEngine;

namespace Splice.Core
{
    [Serializable]
    public class RaidStakeOffer
    {
        public string targetName = "Blackstone Keep";
        public string difficultyBand = "FAIR";
        public int entryStake = 100;
        public int fullVictoryPayout = 180;
        public int outerExtractionPayout = 60;
        public int innerExtractionPayout = 90;
        public int coreExtractionPayout = 120;
    }

    [Serializable]
    public class RaidStakeTransaction
    {
        public string raidId;
        public RaidStakeOffer offer;
        public bool settled;
        public RaidOutcome outcome;
        public int breachedRings;
        public int payout;
        public int balanceAfter;
        public string startedUtc;
        public string settledUtc;
        public string settlementNote;
    }

    [Serializable]
    public class WarGemLedgerEntry
    {
        public string transactionId;
        public string raidId;
        public int delta;
        public int balanceAfter;
        public string category;
        public string note;
        public string utc;
    }

    // Step 5B local/offline economy proof. War Gems are raid stake currency, deliberately separate from
    // Premium Diamonds and Meta Gold. PlayerPrefs is acceptable only for this greybox; a real release must
    // move the same transaction IDs and idempotency rules to an authoritative server ledger.
    public static class LocalWarGemEconomy
    {
        private const string WalletKey = "Splice.LocalWarGem.Wallet.v2";
        private const string ActiveRaidKey = "Splice.LocalWarGem.ActiveRaid.v2";
        private const string LegacyWalletKey = "Splice.LocalWarGem.Wallet.v1";
        private const string LegacyActiveRaidKey = "Splice.LocalWarGem.ActiveRaid.v1";
        private const int LegacyScaleMultiplier = 10;
        private const int DefaultBalance = 1000;
        private const int MaxLedgerEntries = 80;

        [Serializable]
        private class WalletState
        {
            public int balance = DefaultBalance;
            public List<WarGemLedgerEntry> entries = new();
        }

        public static int Balance => LoadWallet().balance;
        public static int TransactionCount => LoadWallet().entries.Count;
        public static RaidStakeTransaction ActiveRaid => LoadActiveRaid();
        public static bool HasPendingRaid
        {
            get
            {
                var active = LoadActiveRaid();
                return active != null && !active.settled;
            }
        }

        public static string LatestTransactionSummary
        {
            get
            {
                var wallet = LoadWallet();
                if (wallet.entries.Count == 0) return "NO TRANSACTIONS";
                var entry = wallet.entries[wallet.entries.Count - 1];
                return $"{entry.category} {FormatSigned(entry.delta)} => {entry.balanceAfter} ({entry.transactionId})";
            }
        }

        public static bool TryBeginRaid(RaidStakeOffer rawOffer, out RaidStakeTransaction transaction, out string error)
        {
            transaction = null;
            error = string.Empty;

            var existing = LoadActiveRaid();
            if (existing != null && !existing.settled)
            {
                error = "Another raid stake is still pending.";
                return false;
            }

            var offer = Sanitize(rawOffer);
            if (Balance < offer.entryStake)
            {
                error = $"Not enough War Gems. Need {offer.entryStake}, have {Balance}.";
                return false;
            }

            var raidId = Guid.NewGuid().ToString("N");
            var debitId = $"raid.{raidId}.stake";
            if (!TryApply(debitId, raidId, -offer.entryStake, "STAKE", $"Raid stake: {offer.targetName}", out var balanceAfter))
            {
                error = "Stake transaction was rejected.";
                return false;
            }

            transaction = new RaidStakeTransaction
            {
                raidId = raidId,
                offer = offer,
                settled = false,
                outcome = RaidOutcome.InProgress,
                breachedRings = 0,
                payout = 0,
                balanceAfter = balanceAfter,
                startedUtc = DateTime.UtcNow.ToString("O"),
                settledUtc = string.Empty,
                settlementNote = string.Empty,
            };
            SaveActiveRaid(transaction);
            RaidContext.ClearLastRaidResults();
            Debug.Log($"[WarGem] raid {raidId} confirmed: stake -{offer.entryStake}, balance {balanceAfter}");
            return true;
        }

        public static bool TrySettleActiveRaid(RaidOutcome outcome, int breachedRings,
            out RaidStakeTransaction transaction)
        {
            transaction = LoadActiveRaid();
            if (transaction == null) return false;

            // Replaying the same result is a successful no-op. This lets server retries remain idempotent.
            if (transaction.settled)
            {
                PublishResult(transaction);
                return true;
            }

            var rings = Mathf.Clamp(breachedRings, 0, 3);
            var payout = CalculatePayout(transaction.offer, outcome, rings);
            var settlementId = $"raid.{transaction.raidId}.settlement";
            var note = SettlementNote(outcome, rings);
            if (!TryApply(settlementId, transaction.raidId, payout, "SETTLEMENT", note, out var balanceAfter))
                return false;

            transaction.settled = true;
            transaction.outcome = outcome;
            transaction.breachedRings = rings;
            transaction.payout = payout;
            transaction.balanceAfter = balanceAfter;
            transaction.settledUtc = DateTime.UtcNow.ToString("O");
            transaction.settlementNote = note;
            SaveActiveRaid(transaction);
            PublishResult(transaction);
            Debug.Log($"[WarGem] raid {transaction.raidId} settled: {outcome}, payout +{payout}, " +
                      $"net {FormatSigned(payout - transaction.offer.entryStake)}, balance {balanceAfter}");
            return true;
        }

        // Technical startup failure is different from a played defeat: no defender snapshot/controls were
        // committed, so the exact stake is returned. The deterministic transaction ID makes retries safe.
        public static bool TryCancelActiveRaidBeforeStart(string reason, out RaidStakeTransaction transaction)
        {
            transaction = LoadActiveRaid();
            if (transaction == null || transaction.settled || transaction.offer == null) return false;

            var refund = Mathf.Max(0, transaction.offer.entryStake);
            var refundId = $"raid.{transaction.raidId}.startup_refund";
            var note = "Technical raid startup cancellation; stake refunded. " + (reason ?? string.Empty);
            if (!TryApply(refundId, transaction.raidId, refund, "REFUND", note, out var balanceAfter))
                return false;

            transaction.settled = true;
            transaction.outcome = RaidOutcome.Defeat;
            transaction.breachedRings = 0;
            transaction.payout = refund;
            transaction.balanceAfter = balanceAfter;
            transaction.settledUtc = DateTime.UtcNow.ToString("O");
            transaction.settlementNote = note;
            SaveActiveRaid(transaction);
            PublishResult(transaction);
            Debug.LogWarning($"[WarGem] raid {transaction.raidId} did not start; stake {refund} refunded " +
                             $"idempotently, balance {balanceAfter}.");
            return true;
        }

        // A pending local transaction after a scene/domain restart represents an abandoned raid. We retain
        // the already-debited stake and close it as defeat instead of refunding or allowing a second payout.
        public static bool ResolveAbandonedRaidIfNeeded(out RaidStakeTransaction transaction)
        {
            transaction = LoadActiveRaid();
            if (transaction == null || transaction.settled) return false;

            if (!TrySettleActiveRaid(RaidOutcome.Defeat, 0, out transaction)) return false;
            transaction.settlementNote = "Interrupted raid recovered as defeat; stake retained.";
            SaveActiveRaid(transaction);
            Debug.LogWarning($"[WarGem] recovered interrupted raid {transaction.raidId} as defeat; " +
                             $"balance {transaction.balanceAfter}");
            return true;
        }

        private static int CalculatePayout(RaidStakeOffer offer, RaidOutcome outcome, int breachedRings)
        {
            if (outcome == RaidOutcome.FullVictory) return offer.fullVictoryPayout;
            if (outcome != RaidOutcome.Extracted) return 0;

            return breachedRings switch
            {
                >= 3 => offer.coreExtractionPayout,
                2 => offer.innerExtractionPayout,
                1 => offer.outerExtractionPayout,
                _ => 0,
            };
        }

        private static string SettlementNote(RaidOutcome outcome, int breachedRings)
        {
            return outcome switch
            {
                RaidOutcome.FullVictory => "Full Victory payout",
                RaidOutcome.Extracted => $"Extraction payout after {breachedRings} breached ring(s)",
                _ => "Raid defeat; stake lost",
            };
        }

        private static void PublishResult(RaidStakeTransaction transaction)
        {
            RaidContext.SetLastWarGemSettlement(
                transaction.offer != null ? transaction.offer.entryStake : 0,
                transaction.payout,
                transaction.balanceAfter,
                transaction.settlementNote);
        }

        private static RaidStakeOffer Sanitize(RaidStakeOffer source)
        {
            source ??= new RaidStakeOffer();
            return new RaidStakeOffer
            {
                targetName = string.IsNullOrWhiteSpace(source.targetName) ? "Unknown Fortress" : source.targetName.Trim(),
                difficultyBand = string.IsNullOrWhiteSpace(source.difficultyBand) ? "FAIR" : source.difficultyBand.Trim().ToUpperInvariant(),
                entryStake = Mathf.Max(0, source.entryStake),
                fullVictoryPayout = Mathf.Max(0, source.fullVictoryPayout),
                outerExtractionPayout = Mathf.Max(0, source.outerExtractionPayout),
                innerExtractionPayout = Mathf.Max(0, source.innerExtractionPayout),
                coreExtractionPayout = Mathf.Max(0, source.coreExtractionPayout),
            };
        }

        private static bool TryApply(string transactionId, string raidId, int delta, string category, string note,
            out int balanceAfter)
        {
            var wallet = LoadWallet();
            for (var i = 0; i < wallet.entries.Count; i++)
            {
                var existing = wallet.entries[i];
                if (existing.transactionId != transactionId) continue;
                balanceAfter = existing.balanceAfter;
                return existing.delta == delta;
            }

            if (wallet.balance + delta < 0)
            {
                balanceAfter = wallet.balance;
                return false;
            }

            wallet.balance += delta;
            balanceAfter = wallet.balance;
            wallet.entries.Add(new WarGemLedgerEntry
            {
                transactionId = transactionId,
                raidId = raidId,
                delta = delta,
                balanceAfter = balanceAfter,
                category = category,
                note = note,
                utc = DateTime.UtcNow.ToString("O"),
            });
            while (wallet.entries.Count > MaxLedgerEntries) wallet.entries.RemoveAt(0);
            SaveWallet(wallet);
            return true;
        }

        private static WalletState LoadWallet()
        {
            if (!PlayerPrefs.HasKey(WalletKey))
            {
                var migrated = MigrateLegacyWallet();
                if (migrated != null) return migrated;
                return new WalletState();
            }
            var wallet = JsonUtility.FromJson<WalletState>(PlayerPrefs.GetString(WalletKey));
            if (wallet == null) return new WalletState();
            wallet.entries ??= new List<WarGemLedgerEntry>();
            wallet.balance = Mathf.Max(0, wallet.balance);
            return wallet;
        }

        private static void SaveWallet(WalletState wallet)
        {
            PlayerPrefs.SetString(WalletKey, JsonUtility.ToJson(wallet));
            PlayerPrefs.Save();
        }

        private static RaidStakeTransaction LoadActiveRaid()
        {
            if (!PlayerPrefs.HasKey(ActiveRaidKey))
            {
                var migrated = MigrateLegacyActiveRaid();
                if (migrated != null) return migrated;
                return null;
            }
            return JsonUtility.FromJson<RaidStakeTransaction>(PlayerPrefs.GetString(ActiveRaidKey));
        }

        private static void SaveActiveRaid(RaidStakeTransaction transaction)
        {
            PlayerPrefs.SetString(ActiveRaidKey, JsonUtility.ToJson(transaction));
            PlayerPrefs.Save();
        }

        private static WalletState MigrateLegacyWallet()
        {
            if (!PlayerPrefs.HasKey(LegacyWalletKey)) return null;
            var wallet = JsonUtility.FromJson<WalletState>(PlayerPrefs.GetString(LegacyWalletKey));
            if (wallet == null) return null;
            wallet.entries ??= new List<WarGemLedgerEntry>();
            wallet.balance = Mathf.Max(0, wallet.balance) * LegacyScaleMultiplier;
            for (var i = 0; i < wallet.entries.Count; i++)
            {
                var entry = wallet.entries[i];
                if (entry == null) continue;
                entry.delta *= LegacyScaleMultiplier;
                entry.balanceAfter *= LegacyScaleMultiplier;
                entry.note = $"[x{LegacyScaleMultiplier} denomination migration] {entry.note}";
            }
            SaveWallet(wallet);
            Debug.Log($"[WarGem] migrated v1 wallet to v2 denomination: balance {wallet.balance}");
            return wallet;
        }

        private static RaidStakeTransaction MigrateLegacyActiveRaid()
        {
            if (!PlayerPrefs.HasKey(LegacyActiveRaidKey)) return null;
            var transaction = JsonUtility.FromJson<RaidStakeTransaction>(PlayerPrefs.GetString(LegacyActiveRaidKey));
            if (transaction == null) return null;
            if (transaction.offer != null)
            {
                transaction.offer.entryStake *= LegacyScaleMultiplier;
                transaction.offer.fullVictoryPayout *= LegacyScaleMultiplier;
                transaction.offer.outerExtractionPayout *= LegacyScaleMultiplier;
                transaction.offer.innerExtractionPayout *= LegacyScaleMultiplier;
                transaction.offer.coreExtractionPayout *= LegacyScaleMultiplier;
            }
            transaction.payout *= LegacyScaleMultiplier;
            transaction.balanceAfter *= LegacyScaleMultiplier;
            transaction.settlementNote = $"[x{LegacyScaleMultiplier} denomination migration] {transaction.settlementNote}";
            SaveActiveRaid(transaction);
            return transaction;
        }

        private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();
    }
}
