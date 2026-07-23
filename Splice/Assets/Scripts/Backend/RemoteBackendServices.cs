using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splice.Base;
using Splice.Combat;
using Splice.Core;

namespace Splice.Backend
{
    public static class BackendRoutes
    {
        public const string Wallet = "/v1/wallet";
        public const string RaidQuotes = "/v1/raid-quotes";
        public const string Raids = "/v1/raids";
        public const string SnapshotBatch = "/v1/town-snapshots/latest/query";

        public static string TownDraft(string factionId) =>
            "/v1/towns/" + Segment(factionId) + "/draft";
        public static string TownDeployments(string factionId) =>
            "/v1/towns/" + Segment(factionId) + "/deployments";
        public static string LatestTownSnapshot(string factionId) =>
            "/v1/towns/" + Segment(factionId) + "/snapshots/latest";
        public static string SnapshotById(string snapshotId) =>
            "/v1/town-snapshots/" + Segment(snapshotId);
        public static string StartupRefund(string raidId) =>
            "/v1/raids/" + Segment(raidId) + "/startup-refund";
        public static string RaidAllocation(string raidId) =>
            "/v1/raids/" + Segment(raidId) + "/allocation";
        public static string RaidLifecycle(string raidId) =>
            "/v1/raids/" + Segment(raidId);
        public static string RaidReplay(string raidId) =>
            "/v1/raids/" + Segment(raidId) + "/replay";
        public static string DefenseHistory(int limit, string beforeUtc, string beforeRaidId)
        {
            var path = "/v1/raid-history/defense?limit=" + Math.Clamp(limit, 1, 50);
            if (string.IsNullOrWhiteSpace(beforeUtc) || string.IsNullOrWhiteSpace(beforeRaidId))
                return path;
            return path + "&beforeUtc=" + Uri.EscapeDataString(beforeUtc) +
                   "&beforeRaidId=" + Uri.EscapeDataString(beforeRaidId);
        }
        public static string PrepareRevenge(string sourceRaidId) =>
            "/v1/raid-history/" + Segment(sourceRaidId) + "/revenge";
        public static string AttackerLoadout(string loadoutId) =>
            "/v1/attacker-loadouts/" + Segment(loadoutId);

        private static string Segment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Backend route identity is required.", nameof(value));
            return Uri.EscapeDataString(value.Trim());
        }
    }

    [Serializable]
    public sealed class ConfirmRaidRequest
    {
        public string quoteId;
    }

    [Serializable]
    public sealed class StartupRefundRequest
    {
        public string raidId;
        public string reasonCode;
    }

    [Serializable]
    public sealed class AllocateRaidRequest
    {
        public string raidId;
    }

    [Serializable]
    public sealed class SnapshotBatchRequest
    {
        public List<string> factionIds = new();
    }

    [Serializable]
    public sealed class SnapshotBatchResponse
    {
        public List<TownDefenseSnapshot> snapshots = new();
    }

    public sealed class RemoteWalletService : IWalletService
    {
        private readonly BackendApiClient client;

        public RemoteWalletService(BackendApiClient client) =>
            this.client = client ?? throw new ArgumentNullException(nameof(client));

        public Task<WalletView> GetWalletAsync(CancellationToken cancellationToken) =>
            client.GetAsync<WalletView>(BackendRoutes.Wallet, cancellationToken);

        public Task<RaidFundingResult> FundRaidAsync(RaidFundingRequest request, string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult(AuthorityDeniedFunding(
                "Unity clients confirm a server quote through IRaidContractService; direct funding is forbidden."));

        public async Task<RaidFundingResult> CancelRaidBeforeStartAsync(string raidId, string reasonCode,
            string idempotencyKey, CancellationToken cancellationToken)
        {
            try
            {
                return await client.SendAsync<StartupRefundRequest, RaidFundingResult>(
                    BackendHttpMethods.Post, BackendRoutes.StartupRefund(raidId),
                    new StartupRefundRequest { raidId = raidId, reasonCode = reasonCode },
                    idempotencyKey, true, cancellationToken);
            }
            catch (BackendServiceException exception)
            {
                return new RaidFundingResult { success = false, error = Format(exception) };
            }
        }

        public async Task<RaidFundingResult> ResolveAbandonedRaidAsync(
            CancellationToken cancellationToken)
        {
            // Production reconciliation belongs to the server. Client startup only refreshes the wallet view.
            var wallet = await GetWalletAsync(cancellationToken);
            return new RaidFundingResult { success = false, error = string.Empty, wallet = wallet };
        }

        private static RaidFundingResult AuthorityDeniedFunding(string message) => new()
        {
            success = false,
            error = BackendErrorCodes.ClientAuthorityForbidden + ": " + message,
        };

        internal static string Format(BackendServiceException exception) =>
            exception.Code + ": " + exception.Message +
            (string.IsNullOrWhiteSpace(exception.RequestId) ? string.Empty : " [" + exception.RequestId + "]");
    }

    public sealed class RemoteTownSnapshotService : ITownSnapshotService
    {
        private readonly BackendApiClient client;

        public RemoteTownSnapshotService(BackendApiClient client) =>
            this.client = client ?? throw new ArgumentNullException(nameof(client));

        public Task<TownDraftView> GetCheckedOutDraftAsync(string factionId,
            CancellationToken cancellationToken) =>
            client.GetAsync<TownDraftView>(BackendRoutes.TownDraft(factionId), cancellationToken);

        public async Task SaveCheckedOutDraftAsync(BaseLayout layout, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            await client.SendAsync<BaseLayout, BackendAck>(BackendHttpMethods.Put,
                BackendRoutes.TownDraft(layout.factionId), layout, idempotencyKey, true,
                cancellationToken);
        }

        public async Task<SnapshotCommitResult> DeployAsync(DeployTownRequest request,
            string idempotencyKey, CancellationToken cancellationToken)
        {
            if (request?.checkedOutLayout == null)
                return new SnapshotCommitResult { success = false, error = "Town layout is required." };
            try
            {
                return await client.SendAsync<DeployTownRequest, SnapshotCommitResult>(
                    BackendHttpMethods.Post,
                    BackendRoutes.TownDeployments(request.checkedOutLayout.factionId), request,
                    idempotencyKey, true, cancellationToken);
            }
            catch (BackendServiceException exception)
            {
                return new SnapshotCommitResult { success = false, error = RemoteWalletService.Format(exception) };
            }
        }

        public Task<TownDefenseSnapshot> GetLatestAsync(string factionId,
            CancellationToken cancellationToken) =>
            client.GetAsync<TownDefenseSnapshot>(BackendRoutes.LatestTownSnapshot(factionId),
                cancellationToken);

        public async Task<IReadOnlyList<TownDefenseSnapshot>> GetLatestManyAsync(
            IReadOnlyList<string> factionIds, CancellationToken cancellationToken)
        {
            var request = new SnapshotBatchRequest();
            if (factionIds != null)
                for (var i = 0; i < factionIds.Count; i++) request.factionIds.Add(factionIds[i]);
            var response = await client.SendAsync<SnapshotBatchRequest, SnapshotBatchResponse>(
                BackendHttpMethods.Post, BackendRoutes.SnapshotBatch, request,
                string.Empty, false, cancellationToken);
            return response.snapshots ?? new List<TownDefenseSnapshot>();
        }

        public Task<TownDefenseSnapshot> GetByIdAsync(string snapshotId,
            CancellationToken cancellationToken) =>
            client.GetAsync<TownDefenseSnapshot>(BackendRoutes.SnapshotById(snapshotId),
                cancellationToken);
    }

    public sealed class RemoteRaidContractService : IRaidContractService
    {
        private readonly BackendApiClient client;

        public RemoteRaidContractService(BackendApiClient client) =>
            this.client = client ?? throw new ArgumentNullException(nameof(client));

        public Task<AttackerLoadoutDto> SaveAttackerLoadoutAsync(string loadoutId,
            PutAttackerLoadoutRequest request, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(loadoutId, out _) || request == null ||
                string.IsNullOrWhiteSpace(request.factionId) || request.entries == null ||
                request.entries.Count == 0)
                throw new BackendServiceException(0, BackendErrorCodes.InvalidTransportRequest,
                    "Remote raid requires a selected non-empty attacker army.", string.Empty, false);
            return client.SendAsync<PutAttackerLoadoutRequest, AttackerLoadoutDto>(
                BackendHttpMethods.Put, BackendRoutes.AttackerLoadout(loadoutId), request,
                idempotencyKey, true, cancellationToken);
        }

        public Task<RaidQuoteDto> CreateQuoteAsync(CreateRaidQuoteRequest request,
            string idempotencyKey, CancellationToken cancellationToken)
        {
            if (request == null || !Guid.TryParse(request.targetId, out _) ||
                !Guid.TryParse(request.attackerLoadoutId, out _))
                throw new BackendServiceException(0, BackendErrorCodes.InvalidTransportRequest,
                    "Remote raid quote requires server-issued deployment and attacker loadout UUIDs.",
                    string.Empty, false);
            return client.SendAsync<CreateRaidQuoteRequest, RaidQuoteDto>(BackendHttpMethods.Post,
                BackendRoutes.RaidQuotes, request, idempotencyKey, true, cancellationToken);
        }

        public async Task<RaidStartDto> ConfirmAsync(string quoteId, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            try
            {
                return await client.SendAsync<ConfirmRaidRequest, RaidStartDto>(BackendHttpMethods.Post,
                    BackendRoutes.Raids, new ConfirmRaidRequest { quoteId = quoteId },
                    idempotencyKey, true, cancellationToken);
            }
            catch (BackendServiceException exception)
            {
                return new RaidStartDto
                {
                    success = false,
                    quoteId = quoteId,
                    error = RemoteWalletService.Format(exception),
                };
            }
        }

        public async Task<RaidAllocationDto> AllocateAsync(string raidId, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            try
            {
                return await client.SendAsync<AllocateRaidRequest, RaidAllocationDto>(
                    BackendHttpMethods.Post, BackendRoutes.RaidAllocation(raidId),
                    new AllocateRaidRequest { raidId = raidId }, idempotencyKey, true, cancellationToken);
            }
            catch (BackendServiceException exception)
            {
                return new RaidAllocationDto
                {
                    success = false,
                    raidId = raidId,
                    error = RemoteWalletService.Format(exception),
                };
            }
        }

        public Task<RaidLifecycleDto> GetLifecycleAsync(string raidId,
            CancellationToken cancellationToken) =>
            client.GetAsync<RaidLifecycleDto>(BackendRoutes.RaidLifecycle(raidId), cancellationToken);

        public Task<RaidReplayDto> GetReplayAsync(string raidId,
            CancellationToken cancellationToken) =>
            client.GetAsync<RaidReplayDto>(BackendRoutes.RaidReplay(raidId), cancellationToken);
    }

    // Deliberately retained after C4A: only the trusted /internal result route may settle shared economy.
    // The player client can allocate/read lifecycle state but cannot write reports or settlement results.
    public sealed class RemoteRaidReportService : IRaidReportService
    {
        private const string Error = BackendErrorCodes.ClientAuthorityForbidden +
                                     ": raid reports must be written by trusted server settlement.";
        private readonly BackendApiClient client;

        public RemoteRaidReportService(BackendApiClient client) =>
            this.client = client ?? throw new ArgumentNullException(nameof(client));

        public Task<RaidDefenseHistoryPageDto> GetDefenseHistoryAsync(int limit,
            string beforeUtc, string beforeRaidId, CancellationToken cancellationToken) =>
            client.GetAsync<RaidDefenseHistoryPageDto>(
                BackendRoutes.DefenseHistory(limit, beforeUtc, beforeRaidId), cancellationToken);

        public Task<RaidRevengeTargetDto> PrepareRevengeAsync(string sourceRaidId,
            string idempotencyKey, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(sourceRaidId, out _))
                throw new BackendServiceException(0, BackendErrorCodes.InvalidTransportRequest,
                    "Remote revenge requires a server-issued raid UUID.", string.Empty, false);
            return client.SendAsync<PrepareRaidRevengeRequest, RaidRevengeTargetDto>(
                BackendHttpMethods.Post, BackendRoutes.PrepareRevenge(sourceRaidId),
                new PrepareRaidRevengeRequest { sourceRaidId = sourceRaidId },
                idempotencyKey, true, cancellationToken);
        }

        public Task<RaidReportWriteResult> RecordCompletedAsync(RaidSessionIdentity session,
            RaidStakeTransaction transaction, int goldLoot, CancellationToken cancellationToken) =>
            Task.FromResult(new RaidReportWriteResult { success = false, error = Error });

        public Task<RaidReportWriteResult> RecordCurrentCompletedAsync(int goldLoot,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RaidReportWriteResult { success = false, error = Error });

        public Task<RevengeGateResult> CanStartRevengeAsync(string reportId, string requestId,
            string requestingAccountId, DateTime utcNow, CancellationToken cancellationToken) =>
            Task.FromResult(new RevengeGateResult { success = false, error = Error });

        public Task<RevengeGateResult> MarkRevengeStartedAsync(string reportId, string requestId,
            string requestingAccountId, string raidId, DateTime utcNow,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RevengeGateResult { success = false, error = Error });
    }

    public sealed class ClientAuthorityGuardRaidSettlementService : IRaidSettlementService
    {
        private const string Error = BackendErrorCodes.ClientAuthorityForbidden +
                                     ": raid settlement must be produced by a trusted Raid Server result.";

        public Task<RaidSettlementResult> SettleActiveRaidAsync(RaidOutcome outcome, int breachedRings,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RaidSettlementResult { success = false, error = Error });

        public Task<LootCreditResult> CreditLootAsync(int goldLoot,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LootCreditResult { success = false, error = Error });
    }
}
