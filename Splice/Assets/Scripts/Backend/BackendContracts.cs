using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splice.Base;
using Splice.Core;
using Splice.Combat;
using Splice.RaidWorker;

namespace Splice.Backend
{
    [Serializable]
    public sealed class WalletView
    {
        public int warGemBalance;
        public bool hasPendingRaid;
        public RaidStakeTransaction pendingRaid;
        public string latestTransactionSummary;
    }

    [Serializable]
    public sealed class RaidFundingRequest
    {
        // Amounts are deliberately absent: every implementation must resolve a server/local-authority quote.
        public string quoteId;
    }

    [Serializable]
    public sealed class RaidFundingResult
    {
        public bool success;
        public string error;
        public RaidStakeTransaction transaction;
        public WalletView wallet;
    }

    [Serializable]
    public sealed class DeployTownRequest
    {
        public BaseLayout checkedOutLayout;
        public int usedCapacity;
        public int maxCapacity;
    }

    [Serializable]
    public sealed class SnapshotCommitResult
    {
        public bool success;
        public string error;
        public TownDefenseSnapshot snapshot;
    }

    [Serializable]
    public sealed class TownDraftView
    {
        public bool exists;
        public BaseLayout checkedOutLayout;
    }

    [Serializable]
    public sealed class RaidReportWriteResult
    {
        public bool success;
        public string error;
        public LocalRaidReportRecord report;
    }

    [Serializable]
    public sealed class RevengeGateResult
    {
        public bool success;
        public string error;
    }

    [Serializable]
    public sealed class RaidSettlementResult
    {
        public bool success;
        public string error;
        public RaidStakeTransaction transaction;
        public RaidReportWriteResult report;
    }

    [Serializable]
    public sealed class LootCreditResult
    {
        public bool success;
        public string error;
        public int creditedGold;
        public int metaGoldBalance;
        public RaidReportWriteResult report;
    }

    [Serializable]
    public sealed class RaidPreparationResult
    {
        public bool success;
        public string error;
        public RaidTarget target;
    }

    [Serializable]
    public sealed class BackendOperationResult
    {
        public bool success;
        public string error;
    }

    [Serializable]
    public sealed class CreateRaidQuoteRequest
    {
        public string targetId;
        public string targetName;
        public string difficultyBand;
        public string attackerLoadoutId;
        public string revengeRequestId;
    }

    [Serializable]
    public sealed class AttackerLoadoutEntryDto
    {
        public string cardId;
        public int count;
    }

    [Serializable]
    public sealed class PutAttackerLoadoutRequest
    {
        public string factionId;
        public string heroId;
        public List<string> gearInstanceIds = new();
        public List<AttackerLoadoutEntryDto> entries = new();
    }

    [Serializable]
    public sealed class AttackerLoadoutDto
    {
        public bool success;
        public string error;
        public string loadoutId;
        public long revision;
        public string factionId;
        public string heroId;
        public List<AttackerLoadoutEntryDto> entries = new();
        public List<string> gearInstanceIds = new();
        public long armyPower;
        public long heroPower;
        public long gearPower;
        public long raidPower;
        public string contentVersion;
        public string payloadSha256;
        public string updatedUtc;
    }

    [Serializable]
    public sealed class RaidQuoteDto
    {
        public string quoteId;
        public string targetId;
        public string targetName;
        public string difficultyBand;
        public int entryStake;
        public int fullVictoryPayout;
        public int outerExtractionPayout;
        public int innerExtractionPayout;
        public int coreExtractionPayout;
        public string expiresUtc;
        public string revengeRequestId;
    }

    [Serializable]
    public sealed class RaidStartDto
    {
        public bool success;
        public string error;
        public string raidId;
        public string quoteId;
        public WalletView wallet;
    }

    [Serializable]
    public sealed class RaidAllocationDto
    {
        public bool success;
        public string error;
        public string raidId;
        public string allocationId;
        public string raidServerId;
        public string ticket;
        public string targetSnapshotId;
        public string sceneContractVersion;
        public string expiresUtc;
    }

    [Serializable]
    public sealed class RaidLifecycleDto
    {
        public string raidId;
        public string state;
        public string targetSnapshotId;
        public string allocationState;
        public string resultId;
        public string outcome;
        public int breachedRings;
        public int warGemPayout;
        public bool replayAvailable;
        public string simulationVersion;
        public string commandStreamHash;
        public string updatedUtc;
    }

    [Serializable]
    public sealed class RaidReplayDto
    {
        public string raidId;
        public string resultId;
        public FixedTickRaidSimulationInput input;
        public RaidSimulationResult result;
        public string createdUtc;
    }

    [Serializable]
    public sealed class RaidDefenseHistoryItemDto
    {
        public string raidId;
        public string reportId;
        public string attackerPlayerId;
        public string attackerDisplayName;
        public string defenderPlayerId;
        public string targetSnapshotId;
        public string state;
        public string outcome;
        public int breachedRings;
        public long entryStake;
        public long attackerPayout;
        public long defenderWarGemDelta;
        public bool replayAvailable;
        public string completedUtc;
        public bool revengeAvailable;
        public string revengeState;
        public string revengeCooldownUntilUtc;
    }

    [Serializable]
    public sealed class RaidDefenseHistoryPageDto
    {
        public List<RaidDefenseHistoryItemDto> items = new();
        public string nextBeforeUtc;
        public string nextBeforeRaidId;
    }

    [Serializable]
    public sealed class PrepareRaidRevengeRequest
    {
        public string sourceRaidId;
    }

    [Serializable]
    public sealed class RaidRevengeTargetDto
    {
        public bool success;
        public string error;
        public string sourceRaidId;
        public string requestId;
        public string targetDeploymentId;
        public string targetSnapshotId;
        public string targetOwnerAccountId;
        public string targetDisplayName;
        public string targetFactionId;
        public long targetPower;
        public string expiresUtc;
    }

    public interface IWalletService
    {
        Task<WalletView> GetWalletAsync(CancellationToken cancellationToken);
        Task<RaidFundingResult> FundRaidAsync(RaidFundingRequest request, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<RaidFundingResult> CancelRaidBeforeStartAsync(string raidId, string reasonCode,
            string idempotencyKey, CancellationToken cancellationToken);
        Task<RaidFundingResult> ResolveAbandonedRaidAsync(CancellationToken cancellationToken);
    }

    public interface ITownSnapshotService
    {
        Task<TownDraftView> GetCheckedOutDraftAsync(string factionId, CancellationToken cancellationToken);
        Task SaveCheckedOutDraftAsync(BaseLayout layout, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<SnapshotCommitResult> DeployAsync(DeployTownRequest request, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<TownDefenseSnapshot> GetLatestAsync(string factionId, CancellationToken cancellationToken);
        Task<IReadOnlyList<TownDefenseSnapshot>> GetLatestManyAsync(
            IReadOnlyList<string> factionIds, CancellationToken cancellationToken);
        Task<TownDefenseSnapshot> GetByIdAsync(string snapshotId, CancellationToken cancellationToken);
    }

    public interface IRaidContractService
    {
        Task<AttackerLoadoutDto> SaveAttackerLoadoutAsync(string loadoutId,
            PutAttackerLoadoutRequest request, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<RaidQuoteDto> CreateQuoteAsync(CreateRaidQuoteRequest request, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<RaidStartDto> ConfirmAsync(string quoteId, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<RaidAllocationDto> AllocateAsync(string raidId, string idempotencyKey,
            CancellationToken cancellationToken);
        Task<RaidLifecycleDto> GetLifecycleAsync(string raidId, CancellationToken cancellationToken);
        Task<RaidReplayDto> GetReplayAsync(string raidId, CancellationToken cancellationToken);
    }

    public interface IRaidReportService
    {
        Task<RaidDefenseHistoryPageDto> GetDefenseHistoryAsync(int limit,
            string beforeUtc, string beforeRaidId, CancellationToken cancellationToken);
        Task<RaidRevengeTargetDto> PrepareRevengeAsync(string sourceRaidId,
            string idempotencyKey, CancellationToken cancellationToken);
        Task<RaidReportWriteResult> RecordCompletedAsync(RaidSessionIdentity session,
            RaidStakeTransaction transaction, int goldLoot, CancellationToken cancellationToken);
        Task<RaidReportWriteResult> RecordCurrentCompletedAsync(int goldLoot,
            CancellationToken cancellationToken);
        Task<RevengeGateResult> CanStartRevengeAsync(string reportId, string requestId,
            string requestingAccountId, DateTime utcNow, CancellationToken cancellationToken);
        Task<RevengeGateResult> MarkRevengeStartedAsync(string reportId, string requestId,
            string requestingAccountId, string raidId, DateTime utcNow, CancellationToken cancellationToken);
    }

    public interface IRaidSettlementService
    {
        Task<RaidSettlementResult> SettleActiveRaidAsync(RaidOutcome outcome, int breachedRings,
            CancellationToken cancellationToken);
        Task<LootCreditResult> CreditLootAsync(int goldLoot, CancellationToken cancellationToken);
    }
}
