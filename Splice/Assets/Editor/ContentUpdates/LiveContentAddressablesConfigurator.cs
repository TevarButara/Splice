#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Splice.ContentUpdates;
using Splice.Data;
using Splice.Validation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Splice.Editor.ContentUpdates
{
    public static class LiveContentAddressablesConfigurator
    {
        public const string ConfigureMenu = "Splice/Live Content/1. Configure Local Addressables";
        public const string ProbeAssetPath = "Assets/LiveContent/Season/LiveContentProbe.asset";
        public const string ProbeAddress = "livecontent/probe";
        public const string ContentVersionVariable = "Splice.ContentVersion";
        public const string LocalRemoteBuildPath = "ServerData/[BuildTarget]";
        public const string LocalRemoteLoadPath = "http://127.0.0.1:8081/[BuildTarget]";

        public static readonly string[] BaseLabels =
        {
            "core-local", "shared-remote", "map/default", "hero/default", "season/default",
        };

        public static readonly string[] BaseGroups =
        {
            "core-local", "shared-remote", "map-default", "hero-default", "season-default",
        };

        [MenuItem(ConfigureMenu, priority = 1800)]
        public static void ConfigureFromMenu()
        {
            var settings = Configure();
            Debug.Log($"[LiveContent] Addressables configured. Profile={settings.activeProfileId}, " +
                      $"groups={settings.groups.Count}.");
        }

        public static AddressableAssetSettings Configure()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) throw new InvalidOperationException("Unable to create Addressables settings.");

            EnsureProfileValue(settings, ContentVersionVariable, LiveContentRuntime.EmbeddedContentVersion);
            settings.profileSettings.SetValue(settings.activeProfileId,
                AddressableAssetSettings.kRemoteBuildPath, LocalRemoteBuildPath);
            settings.profileSettings.SetValue(settings.activeProfileId,
                AddressableAssetSettings.kRemoteLoadPath, LocalRemoteLoadPath);
            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings,
                AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings,
                AddressableAssetSettings.kRemoteLoadPath);
            settings.DisableCatalogUpdateOnStartup = true;
            settings.EnableJsonCatalog = true;
            settings.BundleRetryCount = 3;
            settings.UniqueBundleIds = true;

            var labels = CollectLabels();
            PruneStaleEmptyManagedGroups(settings,
                labels.Select(GroupName).ToHashSet(StringComparer.Ordinal));

            foreach (var label in labels.OrderBy(value => value, StringComparer.Ordinal))
            {
                settings.AddLabel(label, false);
                EnsureGroup(settings, GroupName(label), label == "core-local");
            }

            var probe = EnsureProbeAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProbeAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var probeGuid = AssetDatabase.AssetPathToGUID(ProbeAssetPath);
            if (string.IsNullOrWhiteSpace(probeGuid))
                throw new InvalidOperationException($"Addressables probe GUID was not created: {ProbeAssetPath}");
            var seasonGroup = settings.FindGroup(GroupName("season/default"));
            var entry = settings.CreateOrMoveEntry(probeGuid, seasonGroup, false, false);
            if (entry == null) throw new InvalidOperationException("Unable to create Addressables probe entry.");
            entry.address = ProbeAddress;
            entry.SetLabel("season/default", true, false, false);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification,
                settings, true, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return settings;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings,
            string groupName, bool local)
        {
            var group = settings.FindGroup(groupName);
            if (group == null)
                group = settings.CreateGroup(groupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            if (!group.HasSchema<BundledAssetGroupSchema>()) group.AddSchema<BundledAssetGroupSchema>();
            if (!group.HasSchema<ContentUpdateGroupSchema>()) group.AddSchema<ContentUpdateGroupSchema>();

            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, local
                ? AddressableAssetSettings.kLocalBuildPath
                : AddressableAssetSettings.kRemoteBuildPath);
            bundle.LoadPath.SetVariableByName(settings, local
                ? AddressableAssetSettings.kLocalLoadPath
                : AddressableAssetSettings.kRemoteLoadPath);
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundle.UseAssetBundleCache = !local;
            bundle.UseAssetBundleCrc = true;
            bundle.RetryCount = 3;
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = local;
            EditorUtility.SetDirty(bundle);
            return group;
        }

        public static string GroupName(string label) => (label ?? string.Empty).Replace('/', '-');

        private static HashSet<string> CollectLabels()
        {
            var labels = new HashSet<string>(BaseLabels, StringComparer.Ordinal);
            foreach (var faction in LoadAll<FactionSO>())
                if (!string.IsNullOrWhiteSpace(faction.factionId)) labels.Add("faction/" + faction.factionId);
            foreach (var hero in LoadAll<HeroDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(hero.heroId)) labels.Add("hero/" + hero.heroId);
            return labels;
        }

        private static void PruneStaleEmptyManagedGroups(AddressableAssetSettings settings,
            HashSet<string> expectedGroupNames)
        {
            var staleGroups = settings.groups
                .Where(group => group != null && IsManagedGroupName(group.Name) &&
                                !expectedGroupNames.Contains(group.Name) && !group.entries.Any())
                .ToArray();
            foreach (var group in staleGroups)
            {
                var assetPath = AssetDatabase.GetAssetPath(group);
                settings.RemoveGroup(group);
                if (!string.IsNullOrWhiteSpace(assetPath) &&
                    AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static bool IsManagedGroupName(string groupName) =>
            groupName == "core-local" || groupName == "shared-remote" ||
            groupName.StartsWith("map-", StringComparison.Ordinal) ||
            groupName.StartsWith("hero-", StringComparison.Ordinal) ||
            groupName.StartsWith("faction-", StringComparison.Ordinal) ||
            groupName.StartsWith("season-", StringComparison.Ordinal);

        private static void EnsureProfileValue(AddressableAssetSettings settings,
            string variableName, string defaultValue)
        {
            if (settings.profileSettings.GetProfileDataByName(variableName) == null)
                settings.profileSettings.CreateValue(variableName, defaultValue);
            settings.profileSettings.SetValue(settings.activeProfileId, variableName, defaultValue);
        }

        private static LiveContentProbeSO EnsureProbeAsset()
        {
            var probe = AssetDatabase.LoadAssetAtPath<LiveContentProbeSO>(ProbeAssetPath);
            if (probe != null) return probe;
            EnsureAssetFolder("Assets/LiveContent/Season");
            probe = ScriptableObject.CreateInstance<LiveContentProbeSO>();
            AssetDatabase.CreateAsset(probe, ProbeAssetPath);
            return probe;
        }

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null).ToList();

        public static void Validate(ContentValidationReport report)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                report.Error("LIVE_CONTENT_SETTINGS_MISSING", "Addressables settings have not been configured.");
                return;
            }

            if (!settings.BuildRemoteCatalog)
                report.Error("LIVE_CONTENT_REMOTE_CATALOG_DISABLED", "Addressables remote catalog must be enabled.", settings);
            foreach (var groupName in BaseGroups)
            {
                var group = settings.FindGroup(groupName);
                if (group == null)
                {
                    report.Error("LIVE_CONTENT_GROUP_MISSING", $"Addressables group '{groupName}' is missing.", settings);
                    continue;
                }
                var bundle = group.GetSchema<BundledAssetGroupSchema>();
                if (bundle == null) report.Error("LIVE_CONTENT_SCHEMA_MISSING",
                    $"Addressables group '{groupName}' has no bundle schema.", group);
            }

            var expectedGroupNames = CollectLabels().Select(GroupName)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var group in settings.groups.Where(group => group != null &&
                         IsManagedGroupName(group.Name) && !expectedGroupNames.Contains(group.Name)))
                report.Error("LIVE_CONTENT_STALE_GROUP",
                    $"Managed Addressables group '{group.Name}' has no matching content definition.", group);

            var probeGuid = AssetDatabase.AssetPathToGUID(ProbeAssetPath);
            var probeEntry = settings.FindAssetEntry(probeGuid);
            if (probeEntry == null || probeEntry.address != ProbeAddress)
                report.Error("LIVE_CONTENT_PROBE_MISSING", "The live-content validation probe is not Addressable.", settings);
        }
    }
}
#endif
