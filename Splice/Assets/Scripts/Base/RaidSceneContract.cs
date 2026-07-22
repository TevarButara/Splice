using System;
using System.Collections.Generic;
using Splice.Characters;
using Splice.Network;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Splice.Base
{
    [Serializable]
    public sealed class RaidSceneContractReport
    {
        public string contractVersion;
        public bool valid;
        public bool coreFound;
        public bool attackerEntryFound;
        public bool navMeshSurfaceFound;
        public bool bakedNavMeshFound;
        public bool attackerEntryOnNavMesh;
        public bool coreOnNavMesh;
        public bool completePathFound;
        public int pathCornerCount;
        public float pathDistance;
        public Vector3 sampledAttackerEntry;
        public Vector3 sampledCore;
        public readonly List<string> errors = new();

        public string ErrorSummary => errors.Count == 0 ? string.Empty : string.Join(" ", errors);
    }

    // Runtime gate shared by the pre-raid offer and Editor diagnostics. A raid is not allowed to debit its
    // stake until the authored scene proves that an attacker can spawn and navigate to the objective.
    public static class RaidSceneContract
    {
        public const string Version = "prototype-b.6b.scene-contract.v1";

        public static RaidSceneContractReport Validate(FortCore core, RaidHeroSpawner heroSpawner,
            NavMeshSurface navMeshSurface, float sampleRadius = 8f)
        {
            var report = new RaidSceneContractReport { contractVersion = Version };
            report.coreFound = core != null;
            report.attackerEntryFound = heroSpawner != null && heroSpawner.SpawnPoint != null;
            report.navMeshSurfaceFound = navMeshSurface != null;
            report.bakedNavMeshFound = navMeshSurface != null && navMeshSurface.navMeshData != null;

            if (!report.coreFound) report.errors.Add("Fort Core is missing from the raid scene.");
            if (!report.attackerEntryFound) report.errors.Add("Raid Hero attacker entry is missing.");
            if (!report.navMeshSurfaceFound) report.errors.Add("NavMeshSurface is missing from the raid scene.");
            else if (!report.bakedNavMeshFound) report.errors.Add("NavMeshSurface has no baked NavMesh data.");

            if (!report.coreFound || !report.attackerEntryFound || !report.bakedNavMeshFound)
                return report;

            var radius = Mathf.Max(0.25f, sampleRadius);
            report.attackerEntryOnNavMesh = NavMesh.SamplePosition(heroSpawner.SpawnPoint.position,
                out var entryHit, radius, NavMesh.AllAreas);
            report.coreOnNavMesh = NavMesh.SamplePosition(core.transform.position,
                out var coreHit, radius, NavMesh.AllAreas);

            if (!report.attackerEntryOnNavMesh)
                report.errors.Add("Attacker entry cannot be sampled onto the baked NavMesh.");
            else
                report.sampledAttackerEntry = entryHit.position;

            if (!report.coreOnNavMesh)
                report.errors.Add("Fort Core cannot be sampled onto the baked NavMesh.");
            else
                report.sampledCore = coreHit.position;

            if (report.attackerEntryOnNavMesh && report.coreOnNavMesh)
            {
                var path = new NavMeshPath();
                var calculated = NavMesh.CalculatePath(entryHit.position, coreHit.position, NavMesh.AllAreas, path);
                report.completePathFound = calculated && path.status == NavMeshPathStatus.PathComplete;
                report.pathCornerCount = path.corners != null ? path.corners.Length : 0;
                if (path.corners != null)
                {
                    for (var i = 1; i < path.corners.Length; i++)
                        report.pathDistance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                }
                if (!report.completePathFound)
                    report.errors.Add($"No complete NavMesh path from attacker entry to Fort Core ({path.status}).");
            }

            report.valid = report.errors.Count == 0;
            return report;
        }
    }
}
