using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Base;
using Splice.Core;
using Splice.RaidWorker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.UI
{
    // UI-independent C4C2F presenter. Generated visual skins can bind cards/buttons later without
    // changing the authoritative history, replay, or revenge contracts.
    [DisallowMultipleComponent]
    public sealed class RaidHistoryController : MonoBehaviour
    {
        [Range(1, 50)] [SerializeField] private int pageSize = 20;
        [SerializeField] private string raidSceneName = "RaidArena";

        private readonly List<RaidDefenseHistoryItemDto> items = new();
        private CancellationTokenSource lifetimeCancellation;
        private string nextBeforeUtc = string.Empty;
        private string nextBeforeRaidId = string.Empty;

        public IReadOnlyList<RaidDefenseHistoryItemDto> Items => items;
        public bool IsBusy { get; private set; }
        public bool HasMore => !string.IsNullOrWhiteSpace(nextBeforeUtc) &&
                               !string.IsNullOrWhiteSpace(nextBeforeRaidId);
        public string LastError { get; private set; } = string.Empty;

        private void Awake() => lifetimeCancellation = new CancellationTokenSource();
        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        public void Refresh() => _ = RefreshAsync();
        public void LoadMore() => _ = LoadMoreAsync();
        public void Revenge(int index) => _ = PrepareRevengeAsync(index);

        public async Task RefreshAsync()
        {
            items.Clear();
            nextBeforeUtc = string.Empty;
            nextBeforeRaidId = string.Empty;
            await LoadPageAsync(false);
        }

        public async Task LoadMoreAsync()
        {
            if (!HasMore) return;
            await LoadPageAsync(true);
        }

        public bool PlayReplay(int index)
        {
            LastError = string.Empty;
            if (!TryItem(index, out var item)) return false;
            if (!item.replayAvailable)
            {
                LastError = "Verified replay is not available for this defense report.";
                return false;
            }
            if (!RaidReplayLaunchContext.TryPrepare(item.raidId))
            {
                LastError = "Defense history returned an invalid raid identity.";
                return false;
            }
            SceneManager.LoadScene(raidSceneName);
            return true;
        }

        public async Task<RaidPreparationResult> PrepareRevengeAsync(int index)
        {
            LastError = string.Empty;
            if (!TryItem(index, out var item))
                return Failed(LastError);
            if (!item.revengeAvailable)
                return Failed("Revenge is unavailable: " + (item.revengeState ?? "UNKNOWN"));
            if (lifetimeCancellation == null)
                return Failed("Raid history controller is shutting down.");

            IsBusy = true;
            try
            {
                var prepared = await SpliceServiceHub.RaidReports.PrepareRevengeAsync(
                    item.reportId, Guid.NewGuid().ToString("N"), lifetimeCancellation.Token);
                if (prepared?.success != true)
                    return Failed(prepared?.error ?? "Backend rejected the revenge request.");
                var snapshot = await SpliceServiceHub.TownSnapshots.GetByIdAsync(
                    prepared.targetSnapshotId, lifetimeCancellation.Token);
                if (!TryBuildRevengeTarget(prepared, snapshot, SpliceServiceHub.IsRemoteMeta,
                        out var target, out var error))
                    return Failed(error);
                if (!RaidContext.TrySelectTarget(target, PlayerProfile.ActiveFactionId,
                        PlayerProfile.AccountId, out error))
                    return Failed(error);
                SceneManager.LoadScene(raidSceneName);
                return new RaidPreparationResult
                {
                    success = true,
                    error = string.Empty,
                    target = target,
                };
            }
            catch (OperationCanceledException)
            {
                return Failed("Revenge preparation was cancelled.");
            }
            catch (Exception exception)
            {
                return Failed(exception.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public static bool TryBuildRevengeTarget(RaidRevengeTargetDto prepared,
            TownDefenseSnapshot snapshot, bool requireServerUuids,
            out RaidTarget target, out string error)
        {
            target = null;
            if (prepared?.success != true || snapshot == null ||
                string.IsNullOrWhiteSpace(prepared.requestId) ||
                prepared.targetSnapshotId != snapshot.snapshotId ||
                prepared.targetOwnerAccountId != snapshot.ownerAccountId)
            {
                error = "Revenge target identity does not match its immutable snapshot.";
                return false;
            }
            if (requireServerUuids &&
                (!Guid.TryParse(prepared.sourceRaidId, out _) ||
                 !Guid.TryParse(prepared.requestId, out _) ||
                 !Guid.TryParse(prepared.targetDeploymentId, out _) ||
                 !Guid.TryParse(prepared.targetSnapshotId, out _) ||
                 snapshot.deploymentId != prepared.targetDeploymentId))
            {
                error = "Server-issued revenge identities are invalid or mismatched.";
                return false;
            }

            target = RaidTarget.FromSnapshot(snapshot, prepared.targetDisplayName, false);
            if (target == null)
            {
                error = "Revenge target could not be created.";
                return false;
            }
            target.targetId = prepared.targetDeploymentId;
            target.deploymentId = prepared.targetDeploymentId;
            target.isRevenge = true;
            target.revengeReportId = prepared.sourceRaidId;
            target.revengeRequestId = prepared.requestId;
            error = string.Empty;
            return true;
        }

        private async Task LoadPageAsync(bool append)
        {
            if (IsBusy || lifetimeCancellation == null) return;
            IsBusy = true;
            LastError = string.Empty;
            try
            {
                var page = await SpliceServiceHub.RaidReports.GetDefenseHistoryAsync(
                    pageSize, append ? nextBeforeUtc : string.Empty,
                    append ? nextBeforeRaidId : string.Empty, lifetimeCancellation.Token);
                if (page?.items != null) items.AddRange(page.items);
                nextBeforeUtc = page?.nextBeforeUtc ?? string.Empty;
                nextBeforeRaidId = page?.nextBeforeRaidId ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                // Scene teardown owns cancellation.
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError("[RaidHistory] " + LastError, this);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool TryItem(int index, out RaidDefenseHistoryItemDto item)
        {
            item = index >= 0 && index < items.Count ? items[index] : null;
            if (item != null) return true;
            LastError = "Defense history selection is invalid.";
            return false;
        }

        private RaidPreparationResult Failed(string error)
        {
            LastError = error ?? "Raid history operation failed.";
            return new RaidPreparationResult { success = false, error = LastError };
        }
    }
}
