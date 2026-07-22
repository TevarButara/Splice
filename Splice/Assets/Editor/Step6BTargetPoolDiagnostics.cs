using System;
using System.Collections.Generic;
using System.Threading;
using Splice.Backend;
using Splice.Base;
using Splice.Data;
using UnityEditor;
using UnityEngine;

public static class Step6BTargetPoolDiagnostics
{
    private const string RemoteFaction = "__step6b_menu_remote__";
    private const string OwnFaction = "__step6b_menu_own__";

    [MenuItem("Splice/Diagnostics/Run Step 6B-A Target Pool Test")]
    public static void Run()
    {
        TownSnapshotStore.DeleteFactionSnapshotsForTests(RemoteFaction);
        TownSnapshotStore.DeleteFactionSnapshotsForTests(OwnFaction);
        GameObject providerObject = null;
        try
        {
            var registry = FindRegistry();
            providerObject = new GameObject("__Step6BTargetPoolDiagnostics__")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var provider = providerObject.AddComponent<RaidTargetProvider>();
            var serialized = new SerializedObject(provider);
            serialized.FindProperty("registry").objectReferenceValue = registry;
            serialized.FindProperty("targetCount").intValue = 5;
            serialized.FindProperty("includeOwnSnapshotForInspection").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var registryTargets = provider.GenerateTargets(62001);
            Require(provider.LastBuildResult != null, "Provider did not return a build report.");
            Require(provider.LastBuildResult.RaidableCount == 5,
                $"Bot fallback produced {provider.LastBuildResult.RaidableCount}/5 raidable targets.");
            Require(registryTargets.Exists(target => !target.inspectionOnly), "No raidable target was generated.");

            var remoteLayout = ValidLayout("remote_step6b_account", RemoteFaction, 200, 1f);
            var remoteV1 = TownSnapshotStore.Commit(remoteLayout, 1, 10);
            var ownV1 = TownSnapshotStore.Commit(
                ValidLayout("attacker_step6b_account", OwnFaction, 300, 2f), 1, 10);
            var bots = BuildDiagnosticBots(3);
            var pool = RaidTargetPool.Compose(new[] { remoteV1, ownV1 }, bots,
                "attacker_step6b_account", 3, true);

            Require(pool.RaidableCount == 3, $"Composed pool has {pool.RaidableCount}/3 raidable targets.");
            Require(pool.playerSnapshotTargets == 1 && pool.botTargets == 2 && pool.inspectionTargets == 1,
                "Expected one remote snapshot, two bot fallbacks and one self inspection target.");

            var remoteTarget = pool.targets.Find(target => target.snapshotId == remoteV1.snapshotId);
            var ownTarget = pool.targets.Find(target => target.snapshotId == ownV1.snapshotId);
            Require(remoteTarget != null && remoteTarget.layout == null,
                "Snapshot target must carry identity only, not a mutable layout reference.");
            Require(ownTarget != null && !ownTarget.CanRaid("attacker_step6b_account", out _),
                "Self-owned deployed town became raidable.");
            Require(RaidContext.TrySelectTarget(remoteTarget, "attacker_faction", "attacker_step6b_account", out var error),
                "Remote snapshot selection failed: " + error);

            remoteLayout.storedGold = 999;
            remoteLayout.towers[0].position = new Vector3(9f, 0f, 9f);
            var remoteV2 = TownSnapshotStore.Commit(remoteLayout, 1, 10);
            var locked = SpliceServiceHub.TownSnapshots.GetByIdAsync(
                remoteTarget.snapshotId, CancellationToken.None).GetAwaiter().GetResult()?.layout;
            Require(remoteV2.revision == 2, "Second snapshot revision was not created.");
            Require(locked != null && locked.storedGold == 200 && Mathf.Approximately(locked.towers[0].position.x, 1f),
                "Selected V1 changed after committing V2.");

            Debug.Log($"[Step 6B-A] PASS — registry targets 5; composed pool 3 raidable + 1 inspection; " +
                      $"immutable V1 {remoteV1.snapshotId[..8]} remained locked after V2 {remoteV2.snapshotId[..8]}.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[Step 6B-A] FAIL — " + exception.Message);
            throw;
        }
        finally
        {
            RaidContext.Clear();
            TownSnapshotStore.DeleteFactionSnapshotsForTests(RemoteFaction);
            TownSnapshotStore.DeleteFactionSnapshotsForTests(OwnFaction);
            if (providerObject != null) UnityEngine.Object.DestroyImmediate(providerObject);
        }
    }

    private static FactionRegistrySO FindRegistry()
    {
        var guids = AssetDatabase.FindAssets("t:FactionRegistrySO");
        if (guids.Length == 0) throw new InvalidOperationException("FactionRegistrySO asset was not found.");
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<FactionRegistrySO>(path) ??
               throw new InvalidOperationException("FactionRegistrySO could not be loaded from " + path);
    }

    private static BaseLayout ValidLayout(string owner, string faction, int gold, float x)
    {
        var layout = new BaseLayout { ownerAccountId = owner, factionId = faction, storedGold = gold };
        layout.towers.Add(new PlacedTowerData
        {
            towerId = "diagnostic/tower",
            position = new Vector3(x, 0f, x),
        });
        return layout;
    }

    private static List<RaidTarget> BuildDiagnosticBots(int count)
    {
        var result = new List<RaidTarget>();
        for (var i = 0; i < count; i++)
        {
            var layout = ValidLayout("diagnostic_bot_" + i, "diagnostic_bot", 100 + i, i + 3f);
            result.Add(new RaidTarget
            {
                targetId = "bot:diagnostic:" + i,
                displayName = "Diagnostic Bot " + i,
                source = RaidTargetSource.Bot,
                ownerAccountId = layout.ownerAccountId,
                factionId = layout.factionId,
                basePowerRating = 100,
                towerCount = 1,
                storedGoldPreview = layout.storedGold,
                matchmakingEligible = true,
                layout = layout,
            });
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
