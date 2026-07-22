using System.IO;
using Splice.Validation;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Validation
{
    public static class BackendBoundaryValidator
    {
        private static readonly string[] RuntimeBoundaryPaths =
        {
            "Assets/Scripts/UI/LocalRaidStakeController.cs",
            "Assets/Scripts/UI/TownSnapshotCommitController.cs",
            "Assets/Scripts/Base/RaidContext.cs",
            "Assets/Scripts/Base/RaidTargetProvider.cs",
            "Assets/Scripts/Base/RaidTargetSelectionController.cs",
            "Assets/Scripts/Base/RaidSnapshotLoader.cs",
            "Assets/Scripts/Base/RaidSceneAdapter.cs",
            "Assets/Scripts/Base/IncomingRaidScenarioController.cs",
            "Assets/Scripts/Core/RaidSessionContext.cs",
            "Assets/Scripts/Combat/RaidStakeSettlementController.cs",
            "Assets/Scripts/Base/RaidRewardController.cs",
        };

        private static readonly string[] ForbiddenStoreNames =
        {
            "LocalWarGemEconomy",
            "TownSnapshotStore",
            "PlayerBaseStore",
            "LocalRaidReportStore",
            "PlayerWallet",
        };

        public static void Validate(ContentValidationReport report)
        {
            foreach (var assetPath in RuntimeBoundaryPaths)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (script == null)
                {
                    report.Error("BACKEND_BOUNDARY_SCRIPT_MISSING",
                        $"Required boundary consumer is missing: {assetPath}");
                    continue;
                }

                var text = File.ReadAllText(assetPath);
                foreach (var forbidden in ForbiddenStoreNames)
                {
                    if (!text.Contains(forbidden)) continue;
                    report.Error("RUNTIME_BYPASSES_BACKEND_BOUNDARY",
                        $"{assetPath} references {forbidden} directly. Route it through SpliceServiceHub.", script);
                }
            }

            ValidateRemoteClientAuthority(report);
        }

        private static void ValidateRemoteClientAuthority(ContentValidationReport report)
        {
            const string assetPath = "Assets/Scripts/Backend/RemoteBackendServices.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script == null)
            {
                report.Error("BACKEND_TRANSPORT_SCRIPT_MISSING",
                    $"Remote-ready backend boundary is missing: {assetPath}");
                return;
            }

            var text = File.ReadAllText(assetPath);
            if (text.Contains("/internal/"))
                report.Error("UNITY_CLIENT_EXPOSES_INTERNAL_RAID_ROUTE",
                    "Unity player-client services must not contain internal raid result/start routes.", script);
            if (text.Contains("LocalWarGemEconomy") || text.Contains("LocalRaidReportStore") ||
                text.Contains("TownSnapshotStore") || text.Contains("PlayerBaseStore"))
                report.Error("REMOTE_SERVICE_FALLS_BACK_TO_LOCAL_AUTHORITY",
                    "Remote services must not fall back to a local persistence authority.", script);
        }
    }
}
