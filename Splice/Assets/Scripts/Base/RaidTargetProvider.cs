using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Data;
using Splice.Core;
using UnityEngine;

namespace Splice.Base
{
    // สร้างรายชื่อเป้าหมาย raid (roadmap 5.4). greybox: **ฐาน bot generate** จาก registry → ไม่มี cold start
    // (มีเป้าให้ตีตลอดแม้ยังไม่มีผู้เล่นจริง). ต่อไปแทนด้วย snapshot ผู้เล่นจริงจาก server.
    // owner = "bot_x" (≠ PlayerProfile.AccountId) → กติกากัน self-farming (attacker≠defender) ผ่านเอง.
    public class RaidTargetProvider : MonoBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [SerializeField] private int targetCount = 5;
        [SerializeField] private int towersPerBase = 3;
        [SerializeField] private int garrisonPerBase = 2;
        [SerializeField] private int minGold = 100;
        [SerializeField] private int maxGold = 500;
        [Tooltip("จุดเริ่มวางฐาน bot (world) — ให้ตรงกับโซนฝ่ายตั้งรับในซีน raid")]
        [SerializeField] private Vector3 baseOrigin = Vector3.zero;
        [SerializeField] private float spacing = 3f;
        [SerializeField] private int perRow = 4;
        [Tooltip("Append the current account's deployed town after all raidable targets for inspection/testing.")]
        [SerializeField] private bool includeOwnSnapshotForInspection = true;

        private int generation;
        public FactionRegistrySO Registry => registry;
        public RaidTargetPoolResult LastBuildResult { get; private set; }

        public void ConfigureRegistry(FactionRegistrySO value)
        {
            if (value != null) registry = value;
        }

        public List<RaidTarget> GenerateTargets()
        {
            var seed = unchecked(System.Environment.TickCount + (++generation * 397));
            return GenerateTargets(seed);
        }

        // Legacy synchronous entry point retained for EditMode diagnostics. Gameplay uses GenerateTargetsAsync.
        public List<RaidTarget> GenerateTargets(int seed)
            => GenerateTargetsAsync(seed, CancellationToken.None).GetAwaiter().GetResult();

        public Task<List<RaidTarget>> GenerateTargetsAsync(CancellationToken cancellationToken)
        {
            var seed = unchecked(System.Environment.TickCount + (++generation * 397));
            return GenerateTargetsAsync(seed, cancellationToken);
        }

        public async Task<List<RaidTarget>> GenerateTargetsAsync(int seed, CancellationToken cancellationToken)
        {
            var snapshots = await LoadLatestDeployedSnapshotsAsync(cancellationToken);
            // A player client must never invent raidable targets while connected to the authoritative economy.
            // Production bot towns must be server deployments too, so C2 can lock their snapshot and stake.
            var bots = SpliceServiceHub.IsRemoteMeta
                ? new List<RaidTarget>()
                : GenerateBotTargets(Mathf.Max(0, targetCount), seed);
            LastBuildResult = RaidTargetPool.Compose(snapshots, bots, PlayerProfile.AccountId,
                Mathf.Max(0, targetCount), includeOwnSnapshotForInspection);

            foreach (var target in LastBuildResult.targets)
            {
                if (!target.IsSnapshotBacked || registry == null) continue;
                var faction = registry.GetFaction(target.factionId);
                var factionName = faction != null ? faction.displayName : target.factionId;
                target.displayName = target.inspectionOnly
                    ? $"Your {factionName} Town • V{target.snapshotRevision}"
                    : $"{factionName} Town • V{target.snapshotRevision}";
            }

            Debug.Log($"[RaidTargetPool] built {LastBuildResult.RaidableCount} raidable targets " +
                      $"({LastBuildResult.playerSnapshotTargets} snapshot, {LastBuildResult.botTargets} bot) + " +
                      $"{LastBuildResult.inspectionTargets} inspection target; rejected {LastBuildResult.rejectedSnapshots}.");
            return new List<RaidTarget>(LastBuildResult.targets);
        }

        private async Task<IReadOnlyList<TownDefenseSnapshot>> LoadLatestDeployedSnapshotsAsync(
            CancellationToken cancellationToken)
        {
            var factionIds = new List<string>();
            if (registry == null) return new List<TownDefenseSnapshot>();
            foreach (var faction in registry.Factions)
            {
                if (faction == null || string.IsNullOrWhiteSpace(faction.factionId)) continue;
                factionIds.Add(faction.factionId);
            }
            return await SpliceServiceHub.TownSnapshots.GetLatestManyAsync(factionIds, cancellationToken);
        }

        private List<RaidTarget> GenerateBotTargets(int count, int seed)
        {
            var list = new List<RaidTarget>();
            if (registry == null || registry.Factions.Count == 0 || count <= 0) return list;
            var random = new System.Random(seed);

            for (var i = 0; i < count; i++)
            {
                var faction = registry.Factions[random.Next(0, registry.Factions.Count)];
                if (faction == null) continue;

                var layout = new BaseLayout
                {
                    ownerAccountId = $"bot_{seed}_{i}",
                    factionId = faction.factionId,
                    storedGold = random.Next(Mathf.Min(minGold, maxGold), Mathf.Max(minGold, maxGold) + 1),
                };

                var slot = 0;
                for (var t = 0; t < towersPerBase && faction.towers.Count > 0; t++)
                {
                    var tower = faction.towers[random.Next(0, faction.towers.Count)];
                    var id = registry.IdOf(tower);
                    if (tower == null || string.IsNullOrEmpty(id)) continue;
                    layout.towers.Add(new PlacedTowerData { towerId = id, position = SlotPos(slot++) });
                }
                for (var g = 0; g < garrisonPerBase && faction.cards.Count > 0; g++)
                {
                    var card = faction.cards[random.Next(0, faction.cards.Count)];
                    var id = registry.IdOf(card);
                    if (card == null || card.linkedMonster == null || string.IsNullOrEmpty(id)) continue;
                    layout.garrison.Add(new GarrisonMonsterData { cardId = id, position = SlotPos(slot++) });
                }

                var usedCapacity = layout.towers.Count + layout.garrison.Count;
                list.Add(new RaidTarget
                {
                    targetId = $"bot:{seed}:{i}:{faction.factionId}",
                    displayName = $"Bot {faction.displayName} #{i + 1}",
                    source = RaidTargetSource.Bot,
                    baseLevel = 1,
                    basePowerRating = TownSnapshotValidator.CalculateBasePower(layout, usedCapacity, 1),
                    usedCapacity = usedCapacity,
                    maxCapacity = Mathf.Max(usedCapacity, towersPerBase + garrisonPerBase),
                    towerCount = layout.towers.Count,
                    garrisonCount = layout.garrison.Count,
                    storedGoldPreview = layout.storedGold,
                    ownerAccountId = layout.ownerAccountId,
                    factionId = layout.factionId,
                    matchmakingEligible = true,
                    validationVersion = RaidTargetPool.ValidationVersion,
                    layout = layout,
                });
            }
            return list;
        }

        private Vector3 SlotPos(int slot)
        {
            var col = slot % Mathf.Max(1, perRow);
            var row = slot / Mathf.Max(1, perRow);
            return baseOrigin + new Vector3(col * spacing, 0f, row * spacing);
        }
    }
}
