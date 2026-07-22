using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splice.Base;
using Splice.Core;
using Splice.Combat;

namespace Splice.Backend
{
    internal interface ILocalQuoteFundingSink
    {
        void RegisterLocalQuote(RaidQuoteDto quote);
    }

    // Local-only adapters preserve the current prototype while moving PlayerPrefs behind a replaceable boundary.
    // No caller outside this folder should need to know which local store backs these contracts.
    public sealed class LocalWalletService : IWalletService, ILocalQuoteFundingSink
    {
        private readonly Dictionary<string, RaidStakeOffer> localQuotes = new();
        private readonly Dictionary<string, LocalRefundReplay> startupRefunds = new();

        private sealed class LocalRefundReplay
        {
            public string raidId;
            public RaidFundingResult result;
        }

        public Task<WalletView> GetWalletAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ReadWallet());
        }

        public Task<RaidFundingResult> FundRaidAsync(RaidFundingRequest request, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null || string.IsNullOrWhiteSpace(request.quoteId) ||
                !localQuotes.TryGetValue(request.quoteId, out var offer))
                return Task.FromResult(Failed("A server-issued raid quote is required."));
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Task.FromResult(Failed("Idempotency key is required."));

            var success = LocalWarGemEconomy.TryBeginRaid(offer,
                out var transaction, out var error);
            return Task.FromResult(new RaidFundingResult
            {
                success = success,
                error = error,
                transaction = transaction,
                wallet = ReadWallet(),
            });
        }

        public Task<RaidFundingResult> CancelRaidBeforeStartAsync(string raidId, string reasonCode,
            string idempotencyKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(raidId)) return Task.FromResult(Failed("Raid ID is required."));
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Task.FromResult(Failed("Idempotency key is required."));
            if (startupRefunds.TryGetValue(idempotencyKey, out var replay))
                return Task.FromResult(replay.raidId == raidId
                    ? replay.result
                    : Failed("Idempotency key was already used for another raid."));
            var active = LocalWarGemEconomy.ActiveRaid;
            if (active == null || active.raidId != raidId)
                return Task.FromResult(Failed("Funded raid identity does not match the startup refund request."));

            var success = LocalWarGemEconomy.TryCancelActiveRaidBeforeStart(reasonCode, out var transaction);
            var result = new RaidFundingResult
            {
                success = success,
                error = success ? string.Empty : "No funded raid was available for startup refund.",
                transaction = transaction,
                wallet = ReadWallet(),
            };
            if (success) startupRefunds.Add(idempotencyKey, new LocalRefundReplay
            {
                raidId = raidId,
                result = result,
            });
            return Task.FromResult(result);
        }

        public Task<RaidFundingResult> ResolveAbandonedRaidAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recovered = LocalWarGemEconomy.ResolveAbandonedRaidIfNeeded(out var transaction);
            return Task.FromResult(new RaidFundingResult
            {
                success = recovered,
                error = string.Empty,
                transaction = transaction,
                wallet = ReadWallet(),
            });
        }

        void ILocalQuoteFundingSink.RegisterLocalQuote(RaidQuoteDto quote)
        {
            if (quote == null || string.IsNullOrWhiteSpace(quote.quoteId)) return;
            localQuotes[quote.quoteId] = ToOffer(quote);
        }

        private static RaidFundingResult Failed(string error) => new()
        {
            success = false,
            error = error,
            wallet = ReadWallet(),
        };

        private static WalletView ReadWallet() => new()
        {
            warGemBalance = LocalWarGemEconomy.Balance,
            hasPendingRaid = LocalWarGemEconomy.HasPendingRaid,
            pendingRaid = LocalWarGemEconomy.ActiveRaid,
            latestTransactionSummary = LocalWarGemEconomy.LatestTransactionSummary,
        };

        private static RaidStakeOffer ToOffer(RaidQuoteDto quote) => new()
        {
            targetName = quote.targetName,
            difficultyBand = quote.difficultyBand,
            entryStake = quote.entryStake,
            fullVictoryPayout = quote.fullVictoryPayout,
            outerExtractionPayout = quote.outerExtractionPayout,
            innerExtractionPayout = quote.innerExtractionPayout,
            coreExtractionPayout = quote.coreExtractionPayout,
        };
    }

    public sealed class LocalTownSnapshotService : ITownSnapshotService
    {
        private readonly Dictionary<string, LocalSnapshotReplay> deployments = new();
        private readonly Dictionary<string, string> draftWrites = new();

        private sealed class LocalSnapshotReplay
        {
            public string requestHash;
            public SnapshotCommitResult result;
        }

        public Task<TownDraftView> GetCheckedOutDraftAsync(string factionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var layout = PlayerBaseStore.LoadLayout(factionId);
            return Task.FromResult(new TownDraftView
            {
                exists = layout != null,
                checkedOutLayout = layout,
            });
        }

        public Task SaveCheckedOutDraftAsync(BaseLayout layout, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
            var requestHash = BackendPayloadHash.ComputeObject(layout);
            if (draftWrites.TryGetValue(idempotencyKey, out var existingHash))
            {
                if (existingHash != requestHash)
                    throw new BackendServiceException(409, BackendErrorCodes.IdempotencyKeyReused,
                        "Idempotency key was reused with a different draft.", string.Empty, false);
                return Task.CompletedTask;
            }
            PlayerBaseStore.SaveLayout(layout);
            draftWrites.Add(idempotencyKey, requestHash);
            return Task.CompletedTask;
        }

        public Task<SnapshotCommitResult> DeployAsync(DeployTownRequest request, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request?.checkedOutLayout == null)
                return Task.FromResult(Failed("No checked-out town layout was provided."));
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Task.FromResult(Failed("Idempotency key is required."));

            var requestHash = BackendPayloadHash.ComputeObject(request);
            if (deployments.TryGetValue(idempotencyKey, out var replay))
                return Task.FromResult(replay.requestHash == requestHash
                    ? replay.result
                    : Failed(BackendErrorCodes.IdempotencyKeyReused));

            try
            {
                var snapshot = TownSnapshotStore.Commit(request.checkedOutLayout,
                    request.usedCapacity, request.maxCapacity);
                var result = new SnapshotCommitResult
                {
                    success = true,
                    error = string.Empty,
                    snapshot = snapshot,
                };
                deployments.Add(idempotencyKey, new LocalSnapshotReplay
                {
                    requestHash = requestHash,
                    result = result,
                });
                return Task.FromResult(result);
            }
            catch (Exception exception)
            {
                return Task.FromResult(Failed(exception.Message));
            }
        }

        public Task<TownDefenseSnapshot> GetLatestAsync(string factionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TownSnapshotStore.LoadLatest(factionId));
        }

        public Task<TownDefenseSnapshot> GetByIdAsync(string snapshotId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TownSnapshotStore.LoadById(snapshotId));
        }

        public Task<IReadOnlyList<TownDefenseSnapshot>> GetLatestManyAsync(
            IReadOnlyList<string> factionIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new List<TownDefenseSnapshot>();
            if (factionIds != null)
            {
                for (var i = 0; i < factionIds.Count; i++)
                {
                    var snapshot = TownSnapshotStore.LoadLatest(factionIds[i]);
                    if (snapshot != null) result.Add(snapshot);
                }
            }
            return Task.FromResult<IReadOnlyList<TownDefenseSnapshot>>(result);
        }

        private static SnapshotCommitResult Failed(string error) => new()
        {
            success = false,
            error = error,
        };
    }

    public sealed class LocalRaidContractService : IRaidContractService
    {
        private static readonly TimeSpan QuoteLifetime = TimeSpan.FromMinutes(2);
        private readonly IWalletService walletService;
        private readonly Dictionary<string, RaidQuoteDto> quotes = new();
        private readonly Dictionary<string, RaidStartDto> confirmedQuotes = new();
        private readonly Dictionary<string, string> idempotencyQuotes = new();
        private readonly Dictionary<string, string> quoteRequestHashes = new();
        private readonly Dictionary<string, RaidQuoteDto> idempotentQuoteResults = new();

        public LocalRaidContractService(IWalletService walletService)
        {
            this.walletService = walletService ?? throw new ArgumentNullException(nameof(walletService));
        }

        public Task<RaidQuoteDto> CreateQuoteAsync(CreateRaidQuoteRequest request, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null || string.IsNullOrWhiteSpace(request.targetId))
                throw new ArgumentException("A prepared raid target is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
            var requestHash = BackendPayloadHash.ComputeObject(request);
            if (quoteRequestHashes.TryGetValue(idempotencyKey, out var existingHash))
            {
                if (existingHash != requestHash)
                    throw new BackendServiceException(409, BackendErrorCodes.IdempotencyKeyReused,
                        "Idempotency key was reused with a different quote request.", string.Empty, false);
                return Task.FromResult(idempotentQuoteResults[idempotencyKey]);
            }

            var quote = new RaidQuoteDto
            {
                quoteId = Guid.NewGuid().ToString("N"),
                targetId = request.targetId,
                targetName = string.IsNullOrWhiteSpace(request.targetName) ? "Unknown Fortress" : request.targetName.Trim(),
                difficultyBand = string.IsNullOrWhiteSpace(request.difficultyBand) ? "FAIR" : request.difficultyBand.Trim().ToUpperInvariant(),
                entryStake = 100,
                fullVictoryPayout = 180,
                outerExtractionPayout = 60,
                innerExtractionPayout = 90,
                coreExtractionPayout = 120,
                expiresUtc = DateTime.UtcNow.Add(QuoteLifetime).ToString("O"),
            };
            quotes.Add(quote.quoteId, quote);
            quoteRequestHashes.Add(idempotencyKey, requestHash);
            idempotentQuoteResults.Add(idempotencyKey, quote);
            if (walletService is ILocalQuoteFundingSink localFunding)
                localFunding.RegisterLocalQuote(quote);
            return Task.FromResult(quote);
        }

        public async Task<RaidStartDto> ConfirmAsync(string quoteId, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(quoteId)) return Failed(quoteId, "Raid quote is required.");
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return Failed(quoteId, "Idempotency key is required.");

            if (idempotencyQuotes.TryGetValue(idempotencyKey, out var boundQuoteId))
            {
                if (!string.Equals(boundQuoteId, quoteId, StringComparison.Ordinal))
                    return Failed(quoteId, "Idempotency key was already used for another quote.");
                return confirmedQuotes.TryGetValue(quoteId, out var replay)
                    ? replay
                    : Failed(quoteId, "The previous confirmation is still incomplete.");
            }

            if (confirmedQuotes.TryGetValue(quoteId, out var existing)) return existing;
            if (!quotes.TryGetValue(quoteId, out var quote)) return Failed(quoteId, "Raid quote was not found.");
            if (!DateTime.TryParse(quote.expiresUtc, out var expires) || expires.ToUniversalTime() <= DateTime.UtcNow)
                return Failed(quoteId, "Raid quote has expired.");

            idempotencyQuotes.Add(idempotencyKey, quoteId);
            var funding = await walletService.FundRaidAsync(new RaidFundingRequest
            {
                quoteId = quote.quoteId,
            }, idempotencyKey, cancellationToken);
            var result = new RaidStartDto
            {
                success = funding.success,
                error = funding.error,
                raidId = funding.transaction?.raidId,
                quoteId = quoteId,
                wallet = funding.wallet,
            };
            if (result.success) confirmedQuotes.Add(quoteId, result);
            else idempotencyQuotes.Remove(idempotencyKey);
            return result;
        }

        private static RaidStartDto Failed(string quoteId, string error) => new()
        {
            success = false,
            quoteId = quoteId,
            error = error,
        };
    }

    public sealed class LocalRaidReportService : IRaidReportService
    {
        public Task<RaidReportWriteResult> RecordCompletedAsync(RaidSessionIdentity session,
            RaidStakeTransaction transaction, int goldLoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = LocalRaidReportStore.TryRecordCompletedRaid(session, transaction, goldLoot,
                out var report, out var error);
            return Task.FromResult(new RaidReportWriteResult
            {
                success = success,
                error = error,
                report = report,
            });
        }

        public Task<RaidReportWriteResult> RecordCurrentCompletedAsync(int goldLoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = LocalRaidReportStore.TryRecordCurrentCompletedRaid(goldLoot, out var report);
            return Task.FromResult(new RaidReportWriteResult
            {
                success = success,
                error = success ? string.Empty : "Raid report is waiting for a completed session and settlement.",
                report = report,
            });
        }

        public Task<RevengeGateResult> CanStartRevengeAsync(string reportId, string requestId,
            string requestingAccountId, DateTime utcNow, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = LocalRaidReportStore.CanStartRevenge(reportId, requestId,
                requestingAccountId, utcNow, out var error);
            return Task.FromResult(new RevengeGateResult { success = success, error = error });
        }

        public Task<RevengeGateResult> MarkRevengeStartedAsync(string reportId, string requestId,
            string requestingAccountId, string raidId, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var success = LocalRaidReportStore.TryMarkRevengeStarted(reportId, requestId,
                requestingAccountId, raidId, utcNow, out var error);
            return Task.FromResult(new RevengeGateResult { success = success, error = error });
        }
    }

    public sealed class LocalRaidSettlementService : IRaidSettlementService
    {
        private readonly IRaidReportService reportService;

        public LocalRaidSettlementService(IRaidReportService reportService)
        {
            this.reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        }

        public async Task<RaidSettlementResult> SettleActiveRaidAsync(RaidOutcome outcome,
            int breachedRings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LocalWarGemEconomy.HasPendingRaid)
                return FailedSettlement("No funded raid is pending settlement.");
            if (!LocalWarGemEconomy.TrySettleActiveRaid(outcome, breachedRings, out var transaction))
                return FailedSettlement("Active raid settlement failed and remains pending for retry.");

            RaidSessionContext.MarkCompleted(outcome, breachedRings);
            var report = RaidSessionContext.Current != null
                ? await reportService.RecordCompletedAsync(RaidSessionContext.Current, transaction,
                    RaidContext.LastLootGained, cancellationToken)
                : new RaidReportWriteResult { success = false, error = "Raid session identity is missing." };
            return new RaidSettlementResult
            {
                success = true,
                error = string.Empty,
                transaction = transaction,
                report = report,
            };
        }

        public async Task<LootCreditResult> CreditLootAsync(int goldLoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var credit = Math.Max(0, goldLoot);
            if (credit > 0) PlayerWallet.Add(credit);
            RaidContext.LastLootGained = credit;
            var report = await reportService.RecordCurrentCompletedAsync(credit, cancellationToken);
            return new LootCreditResult
            {
                success = true,
                error = string.Empty,
                creditedGold = credit,
                metaGoldBalance = PlayerWallet.MetaGold,
                report = report,
            };
        }

        private static RaidSettlementResult FailedSettlement(string error) => new()
        {
            success = false,
            error = error,
        };
    }

    public static class SpliceServiceHub
    {
        private static IWalletService wallet;
        private static ITownSnapshotService townSnapshots;
        private static IRaidContractService raidContracts;
        private static IRaidReportService raidReports;
        private static IRaidSettlementService raidSettlement;

        public static IWalletService Wallet => wallet ??= new LocalWalletService();
        public static ITownSnapshotService TownSnapshots => townSnapshots ??= new LocalTownSnapshotService();
        public static IRaidContractService RaidContracts => raidContracts ??= new LocalRaidContractService(Wallet);
        public static IRaidReportService RaidReports => raidReports ??= new LocalRaidReportService();
        public static IRaidSettlementService RaidSettlement =>
            raidSettlement ??= new LocalRaidSettlementService(RaidReports);

        public static void Configure(IWalletService walletService, ITownSnapshotService townSnapshotService,
            IRaidContractService raidContractService, IRaidReportService raidReportService = null,
            IRaidSettlementService raidSettlementService = null)
        {
            wallet = walletService ?? throw new ArgumentNullException(nameof(walletService));
            townSnapshots = townSnapshotService ?? throw new ArgumentNullException(nameof(townSnapshotService));
            raidContracts = raidContractService ?? throw new ArgumentNullException(nameof(raidContractService));
            raidReports = raidReportService ?? new LocalRaidReportService();
            raidSettlement = raidSettlementService ?? new LocalRaidSettlementService(raidReports);
        }

        public static void ConfigureRemoteMeta(IBackendTransport transport,
            IBackendJsonSerializer serializer = null)
        {
            var client = new BackendApiClient(transport, serializer);
            wallet = new RemoteWalletService(client);
            townSnapshots = new RemoteTownSnapshotService(client);
            raidContracts = new RemoteRaidContractService(client);
            raidReports = new ClientAuthorityGuardRaidReportService();
            raidSettlement = new ClientAuthorityGuardRaidSettlementService();
        }

        public static void ResetToLocalDefaults()
        {
            wallet = null;
            townSnapshots = null;
            raidContracts = null;
            raidReports = null;
            raidSettlement = null;
        }
    }
}
