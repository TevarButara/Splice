using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Splice.ContentUpdates
{
    public enum LiveContentUpdateState
    {
        Idle,
        Checking,
        StoreUpdateRequired,
        CalculatingDownload,
        Downloading,
        Validating,
        Activating,
        Ready,
        RollingBack,
        Failed,
    }

    public readonly struct LiveContentProgress
    {
        public LiveContentUpdateState State { get; }
        public float Normalized { get; }
        public long DownloadedBytes { get; }
        public long TotalBytes { get; }
        public string Message { get; }

        public LiveContentProgress(LiveContentUpdateState state, float normalized,
            long downloadedBytes, long totalBytes, string message)
        {
            State = state;
            Normalized = Math.Clamp(normalized, 0f, 1f);
            DownloadedBytes = Math.Max(0, downloadedBytes);
            TotalBytes = Math.Max(0, totalBytes);
            Message = message ?? string.Empty;
        }
    }

    public readonly struct LiveContentUpdateResult
    {
        public bool Success { get; }
        public bool RequiresStoreUpdate { get; }
        public bool Updated { get; }
        public string Error { get; }

        public LiveContentUpdateResult(bool success, bool requiresStoreUpdate, bool updated, string error)
        {
            Success = success;
            RequiresStoreUpdate = requiresStoreUpdate;
            Updated = updated;
            Error = error ?? string.Empty;
        }
    }

    public interface ILiveContentOperations
    {
        Task InitializeAsync(CancellationToken cancellationToken);
        Task LoadCatalogAsync(LiveContentManifest manifest, CancellationToken cancellationToken);
        Task<long> GetDownloadSizeAsync(string label, CancellationToken cancellationToken);
        Task DownloadAsync(string label, IProgress<float> progress, CancellationToken cancellationToken);
        Task ValidateAsync(LiveContentManifest manifest, CancellationToken cancellationToken);
        Task RollbackAsync(string catalogUrl, CancellationToken cancellationToken);
    }

    public interface ILiveContentVersionStore
    {
        string ActiveVersion { get; }
        string ActiveCatalogUrl { get; }
        string PendingVersion { get; }
        string PreviousCatalogUrl { get; }
        void BeginActivation(string version, string catalogUrl);
        void CommitActivation();
        void AbortActivation();
    }

    public sealed class LiveContentUpdateCoordinator
    {
        private readonly ILiveContentOperations operations;
        private readonly ILiveContentVersionStore versionStore;
        private readonly int maxAttempts;

        public event Action<LiveContentProgress> ProgressChanged;

        public LiveContentUpdateCoordinator(ILiveContentOperations operations,
            ILiveContentVersionStore versionStore, int maxAttempts = 3)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
            this.maxAttempts = Math.Max(1, maxAttempts);
        }

        public async Task RecoverInterruptedActivationAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(versionStore.PendingVersion)) return;
            Report(LiveContentUpdateState.RollingBack, 0f, 0, 0,
                "Recovering the last known-good content catalog...");
            await operations.InitializeAsync(cancellationToken);
            await operations.RollbackAsync(versionStore.PreviousCatalogUrl, cancellationToken);
            versionStore.AbortActivation();
            LiveContentRuntime.ActiveContentVersion = versionStore.ActiveVersion;
        }

        public async Task<LiveContentUpdateResult> RunAsync(LiveContentManifest manifest,
            string clientVersion, bool requireProductionSignature, CancellationToken cancellationToken)
        {
            var validation = LiveContentManifestValidator.Validate(
                manifest, clientVersion, requireProductionSignature);
            if (!validation.IsValid) return Fail(validation.Error);
            if (validation.RequiresStoreUpdate)
            {
                Report(LiveContentUpdateState.StoreUpdateRequired, 0f, 0, 0,
                    "A newer app version is required before this content can be used.");
                return new LiveContentUpdateResult(false, true, false, string.Empty);
            }

            LiveContentRuntime.ActiveContentVersion = string.IsNullOrWhiteSpace(versionStore.ActiveVersion)
                ? LiveContentRuntime.EmbeddedContentVersion
                : versionStore.ActiveVersion;
            if (SemanticVersion.Compare(manifest.contentVersion, LiveContentRuntime.ActiveContentVersion) <= 0)
            {
                Report(LiveContentUpdateState.Ready, 1f, 0, 0, "Content is up to date.");
                return new LiveContentUpdateResult(true, false, false, string.Empty);
            }

            try
            {
                Report(LiveContentUpdateState.Checking, 0f, 0, 0, "Checking content catalog...");
                await operations.InitializeAsync(cancellationToken);
                await RetryAsync(() => operations.LoadCatalogAsync(manifest, cancellationToken), cancellationToken);

                var labels = manifest.MandatoryLabels().Distinct(StringComparer.Ordinal).ToArray();
                var sizes = new long[labels.Length];
                long totalBytes = 0;
                Report(LiveContentUpdateState.CalculatingDownload, 0f, 0, 0, "Calculating download size...");
                for (var i = 0; i < labels.Length; i++)
                {
                    sizes[i] = Math.Max(0, await operations.GetDownloadSizeAsync(labels[i], cancellationToken));
                    totalBytes += sizes[i];
                }

                long completedBytes = 0;
                for (var i = 0; i < labels.Length; i++)
                {
                    var index = i;
                    var progress = new Progress<float>(value =>
                    {
                        var currentBytes = (long)(sizes[index] * Math.Clamp(value, 0f, 1f));
                        var downloaded = completedBytes + currentBytes;
                        var normalized = totalBytes <= 0 ? 1f : downloaded / (float)totalBytes;
                        Report(LiveContentUpdateState.Downloading, normalized, downloaded, totalBytes,
                            $"Downloading {labels[index]}...");
                    });
                    await RetryAsync(() => operations.DownloadAsync(labels[index], progress, cancellationToken),
                        cancellationToken);
                    completedBytes += sizes[index];
                }

                Report(LiveContentUpdateState.Validating, 1f, totalBytes, totalBytes,
                    "Validating downloaded content...");
                await operations.ValidateAsync(manifest, cancellationToken);

                Report(LiveContentUpdateState.Activating, 1f, totalBytes, totalBytes,
                    "Activating content...");
                versionStore.BeginActivation(manifest.contentVersion, manifest.catalogUrl);
                versionStore.CommitActivation();
                LiveContentRuntime.ActiveContentVersion = manifest.contentVersion;
                Report(LiveContentUpdateState.Ready, 1f, totalBytes, totalBytes, "Content is ready.");
                return new LiveContentUpdateResult(true, false, true, string.Empty);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                try
                {
                    Report(LiveContentUpdateState.RollingBack, 0f, 0, 0,
                        "Update failed. Restoring the last known-good catalog...");
                    await operations.RollbackAsync(versionStore.ActiveCatalogUrl, cancellationToken);
                }
                catch
                {
                    // Preserve the original failure. Embedded content remains the final fallback.
                }
                versionStore.AbortActivation();
                LiveContentRuntime.ActiveContentVersion = versionStore.ActiveVersion;
                return Fail(exception.Message);
            }
        }

        private async Task RetryAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            Exception last = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await action();
                    return;
                }
                catch (Exception exception) when (attempt < maxAttempts &&
                                                  exception is not OperationCanceledException)
                {
                    last = exception;
                    await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
                }
            }
            throw last ?? new InvalidOperationException("Live-content operation failed.");
        }

        private LiveContentUpdateResult Fail(string error)
        {
            Report(LiveContentUpdateState.Failed, 0f, 0, 0, error);
            return new LiveContentUpdateResult(false, false, false, error);
        }

        private void Report(LiveContentUpdateState state, float normalized,
            long downloadedBytes, long totalBytes, string message) =>
            ProgressChanged?.Invoke(new LiveContentProgress(state, normalized,
                downloadedBytes, totalBytes, message));
    }
}
