using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Splice.ContentUpdates
{
    public sealed class AddressablesLiveContentOperations : ILiveContentOperations
    {
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.InitializeAsync(false);
            await handle.Task;
            try
            {
                EnsureSucceeded(handle, "Addressables initialization");
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        public async Task LoadCatalogAsync(LiveContentManifest manifest,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(manifest.catalogUrl))
            {
                var check = Addressables.CheckForCatalogUpdates(false);
                await check.Task;
                EnsureSucceeded(check, "Catalog update check");
                var catalogs = check.Result;
                Addressables.Release(check);
                if (catalogs == null || catalogs.Count == 0) return;

                var update = Addressables.UpdateCatalogs(false, catalogs, false);
                await update.Task;
                try
                {
                    EnsureSucceeded(update, "Catalog update");
                }
                finally
                {
                    Addressables.Release(update);
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(manifest.catalogSha256))
            {
                var catalogBytes = await DownloadBytesAsync(manifest.catalogUrl, cancellationToken);
                if (!LiveContentManifestValidator.CatalogMatches(catalogBytes, manifest.catalogSha256))
                    throw new InvalidOperationException("Downloaded catalog SHA-256 does not match the manifest.");
            }

            var load = Addressables.LoadContentCatalogAsync(manifest.catalogUrl, false);
            await load.Task;
            try
            {
                EnsureSucceeded(load, "Remote catalog load");
            }
            finally
            {
                Addressables.Release(load);
            }
        }

        public async Task<long> GetDownloadSizeAsync(string label, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.GetDownloadSizeAsync(label);
            await handle.Task;
            try
            {
                EnsureSucceeded(handle, $"Download-size query for {label}");
                return handle.Result;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        public async Task DownloadAsync(string label, IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var handle = Addressables.DownloadDependenciesAsync(label, false);
            try
            {
                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(handle.GetDownloadStatus().Percent);
                    await Task.Yield();
                }
                await handle.Task;
                EnsureSucceeded(handle, $"Download for {label}");
                progress?.Report(1f);
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        public async Task ValidateAsync(LiveContentManifest manifest,
            CancellationToken cancellationToken)
        {
            foreach (var label in manifest.MandatoryLabels())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var locations = Addressables.LoadResourceLocationsAsync(label, typeof(UnityEngine.Object));
                await locations.Task;
                try
                {
                    EnsureSucceeded(locations, $"Location validation for {label}");
                    if (locations.Result == null || locations.Result.Count == 0)
                        throw new InvalidOperationException($"Mandatory pack '{label}' contains no loadable assets.");
                }
                finally
                {
                    Addressables.Release(locations);
                }
            }

            if (string.IsNullOrWhiteSpace(manifest.validationAddress)) return;
            var probe = Addressables.LoadAssetAsync<UnityEngine.Object>(manifest.validationAddress);
            await probe.Task;
            try
            {
                EnsureSucceeded(probe, $"Smoke-load {manifest.validationAddress}");
                if (probe.Result == null) throw new InvalidOperationException("Content validation asset is null.");
            }
            finally
            {
                Addressables.Release(probe);
            }
        }

        public async Task RollbackAsync(string catalogUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(catalogUrl)) return;
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.LoadContentCatalogAsync(catalogUrl, false, "rollback");
            await handle.Task;
            try
            {
                EnsureSucceeded(handle, "Catalog rollback");
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private static async Task<byte[]> DownloadBytesAsync(string url,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = 15;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"Catalog download failed: {request.error}");
            return request.downloadHandler.data;
        }

        private static void EnsureSucceeded<T>(AsyncOperationHandle<T> handle, string operation)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"{operation} failed: {handle.OperationException?.Message}");
        }

        private static void EnsureSucceeded(AsyncOperationHandle handle, string operation)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"{operation} failed: {handle.OperationException?.Message}");
        }
    }
}
