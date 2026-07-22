#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Splice.ContentUpdates;
using Splice.Editor.ContentUpdates;
using Splice.Validation;
using UnityEditor.AddressableAssets;

namespace Splice.Tests.EditMode
{
    public sealed class LiveContentUpdateEditModeTests
    {
        [Test]
        public void Manifest_RejectsUnsupportedSchemaAndOldClient()
        {
            var manifest = Manifest("1.2.0");
            manifest.schemaVersion = 99;
            Assert.That(LiveContentManifestValidator.Validate(manifest, "1.0.0", false).IsValid, Is.False);

            manifest.schemaVersion = 1;
            manifest.minimumClientVersion = "2.0.0";
            var validation = LiveContentManifestValidator.Validate(manifest, "1.0.0", false);
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.RequiresStoreUpdate, Is.True);
        }

        [Test]
        public async Task Coordinator_DownloadsMandatoryPacksRetriesAndCommits()
        {
            var operations = new FakeOperations { LoadFailuresRemaining = 1 };
            var store = new FakeStore();
            var coordinator = new LiveContentUpdateCoordinator(operations, store, 3);

            var result = await coordinator.RunAsync(Manifest("1.1.0"), "1.0.0", false,
                CancellationToken.None);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Updated, Is.True);
            Assert.That(operations.LoadAttempts, Is.EqualTo(2));
            Assert.That(operations.DownloadedLabels, Is.EquivalentTo(new[] { "season/default" }));
            Assert.That(store.ActiveVersion, Is.EqualTo("1.1.0"));
        }

        [Test]
        public async Task Coordinator_ValidationFailureRollsBackAndKeepsLastKnownGood()
        {
            var operations = new FakeOperations { FailValidation = true };
            var store = new FakeStore();
            var coordinator = new LiveContentUpdateCoordinator(operations, store, 1);

            var result = await coordinator.RunAsync(Manifest("1.1.0"), "1.0.0", false,
                CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(operations.RollbackCount, Is.EqualTo(1));
            Assert.That(store.ActiveVersion, Is.EqualTo("1.0.0"));
            Assert.That(store.PendingVersion, Is.Empty);
        }

        [Test]
        public void AddressablesConfiguration_HasRequiredGroupsAndProbe()
        {
            var settings = LiveContentAddressablesConfigurator.Configure();
            foreach (var groupName in LiveContentAddressablesConfigurator.BaseGroups)
                Assert.That(settings.FindGroup(groupName), Is.Not.Null, groupName);
            var entry = settings.FindAssetEntry(
                UnityEditor.AssetDatabase.AssetPathToGUID(LiveContentAddressablesConfigurator.ProbeAssetPath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo(LiveContentAddressablesConfigurator.ProbeAddress));
        }

        [Test]
        public void AddressablesConfiguration_IsIdempotentAndLeavesNoStaleManagedGroups()
        {
            var first = LiveContentAddressablesConfigurator.Configure();
            var firstNames = first.groups.Where(group => group != null)
                .Select(group => group.Name).OrderBy(name => name).ToArray();
            var second = LiveContentAddressablesConfigurator.Configure();
            var secondNames = second.groups.Where(group => group != null)
                .Select(group => group.Name).OrderBy(name => name).ToArray();

            Assert.That(secondNames, Is.EqualTo(firstNames));
            Assert.That(secondNames.Distinct().Count(), Is.EqualTo(secondNames.Length));
            Assert.That(second.FindGroup("map-default1"), Is.Null);
            Assert.That(second.FindGroup("season-default1"), Is.Null);
        }

        [Test]
        public void BackendCatalogExport_IsDeterministicAndCurrent()
        {
            var first = SpliceContentCatalogExporter.CurrentJson();
            var second = SpliceContentCatalogExporter.CurrentJson();
            Assert.That(second, Is.EqualTo(first));

            var report = new ContentValidationReport();
            SpliceContentCatalogExporter.ValidateGenerated(report);
            Assert.That(report.ErrorCount, Is.Zero, report.DetailedSummary());
        }

        [Test]
        public void ContentOnlyProofReport_ConfirmsCatalogChangedWithoutPlayerBuild()
        {
            var repositoryRoot = Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName;
            var path = Path.Combine(repositoryRoot,
                "Backend/content/generated/live-content-update-proof.json");
            Assert.That(File.Exists(path), Is.True, "Run Splice/Live Content/4 before release tests.");
            var json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\"result\": \"PASS\""));
            Assert.That(json, Does.Contain("\"playerRebuildInvoked\": false"));
            Assert.That(json, Does.Contain("\"addressablesContentUpdateBuild\": true"));
        }

        private static LiveContentManifest Manifest(string version) => new()
        {
            contentVersion = version,
            minimumClientVersion = "0.0.0",
            packs = new[]
            {
                new LiveContentPackManifest { label = "season/default", mandatory = true },
                new LiveContentPackManifest { label = "hero/default", mandatory = false },
            },
        };

        private sealed class FakeStore : ILiveContentVersionStore
        {
            private string pendingCatalog;
            public string ActiveVersion { get; private set; } = "1.0.0";
            public string ActiveCatalogUrl { get; private set; } = "catalog-v1.json";
            public string PendingVersion { get; private set; } = string.Empty;
            public string PreviousCatalogUrl { get; private set; } = string.Empty;

            public void BeginActivation(string version, string catalogUrl)
            {
                PreviousCatalogUrl = ActiveCatalogUrl;
                PendingVersion = version;
                pendingCatalog = catalogUrl;
            }

            public void CommitActivation()
            {
                ActiveVersion = PendingVersion;
                ActiveCatalogUrl = pendingCatalog;
                AbortActivation();
            }

            public void AbortActivation()
            {
                PendingVersion = string.Empty;
                PreviousCatalogUrl = string.Empty;
                pendingCatalog = string.Empty;
            }
        }

        private sealed class FakeOperations : ILiveContentOperations
        {
            public int LoadFailuresRemaining;
            public int LoadAttempts;
            public bool FailValidation;
            public int RollbackCount;
            public readonly List<string> DownloadedLabels = new();

            public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task LoadCatalogAsync(LiveContentManifest manifest, CancellationToken cancellationToken)
            {
                LoadAttempts++;
                if (LoadFailuresRemaining-- > 0) throw new IOException("transient");
                return Task.CompletedTask;
            }

            public Task<long> GetDownloadSizeAsync(string label, CancellationToken cancellationToken) =>
                Task.FromResult(1024L);

            public Task DownloadAsync(string label, IProgress<float> progress,
                CancellationToken cancellationToken)
            {
                DownloadedLabels.Add(label);
                progress?.Report(1f);
                return Task.CompletedTask;
            }

            public Task ValidateAsync(LiveContentManifest manifest, CancellationToken cancellationToken)
            {
                if (FailValidation) throw new InvalidDataException("broken bundle");
                return Task.CompletedTask;
            }

            public Task RollbackAsync(string catalogUrl, CancellationToken cancellationToken)
            {
                RollbackCount++;
                return Task.CompletedTask;
            }
        }
    }
}
#endif
