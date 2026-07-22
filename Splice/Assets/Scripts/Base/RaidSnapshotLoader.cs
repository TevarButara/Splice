using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Characters;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Base
{
    // ฝั่ง server ตอนเริ่ม raid: อ่าน BaseLayout snapshot ของฐานเป้าหมาย แล้ว spawn ฝั่งตั้งรับทั้งชุด —
    // ป้อม (พร้อม upgrade levels, ข้ามเวลาก่อสร้าง), miner, ทองในคลัง (architecture 5.10)
    // Raid ฐานคนอื่น = PvE local host ที่โหลด snapshot — ไม่ต้องมี dedicated server
    // Garrison monster spawn มาในขั้น 5.3 (ตอนนี้เก็บใน data เฉยๆ)
    public class RaidSnapshotLoader : NetworkBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [SerializeField] private RaidSide defendSide = RaidSide.Defender;
        [Tooltip("faction ของเมืองที่จะโหลดมาตั้งรับ (local test) — เว้นว่าง = ActiveFactionId. raid จริง (5.4) ส่ง layout เป้าหมายผ่าน LoadIntoMatch แทน")]
        [SerializeField] private string targetFactionId;
        [Tooltip("จุดเกิด miner ฝั่งตั้งรับ — เว้นว่าง = เกิดที่ตำแหน่ง loader นี้")]
        [SerializeField] private Transform minerSpawnPoint;
        [Tooltip("โหลดผังจาก local save อัตโนมัติตอน server เริ่ม — ไว้เทส loop 'ปล้นฐานตัวเอง' ก่อนมีระบบเลือกเป้าหมาย (ขั้น 5.4)")]
        [SerializeField] private bool loadLocalSaveOnSpawn;

        private bool loadQueued;
        public FactionRegistrySO Registry => registry;
        public bool HasStartedLoading { get; private set; }
        public bool HasCompletedLoading { get; private set; }
        public string LastLoadedSnapshotId { get; private set; } = string.Empty;
        public int LastLoadedTowerCount { get; private set; }
        public int LastLoadedGarrisonCount { get; private set; }
        public int LastLoadedMinerCount { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            // A scene with RaidSceneAdapter waits for pre-flight validation + stake confirmation. Legacy scenes
            // keep the old automatic target load path; explicit local-save testing remains available everywhere.
            if (loadLocalSaveOnSpawn)
                QueueLoad();
            else if (RaidContext.HasTarget && FindFirstObjectByType<RaidSceneAdapter>() == null)
                QueueLoad();
        }

        // faction ของเมืองที่โหลด (local test) — Inspector override หรือเผ่าที่เลือกอยู่
        private string CityFactionId => string.IsNullOrEmpty(targetFactionId) ? PlayerProfile.ActiveFactionId : targetFactionId;

        private IEnumerator LoadNextFrame()
        {
            yield return null;
            HasStartedLoading = true;

            // เป้าหมายจากจอเลือก raid (5.4) — โหลด snapshot ของเป้าหมายนั้น
            if (RaidContext.HasTarget)
            {
                BaseLayout selectedLayout;
                if (RaidContext.Target.IsSnapshotBacked)
                {
                    var snapshotTask = SpliceServiceHub.TownSnapshots.GetByIdAsync(
                        RaidContext.TargetSnapshotId, CancellationToken.None);
                    while (!snapshotTask.IsCompleted) yield return null;
                    if (snapshotTask.IsFaulted)
                    {
                        Debug.LogError("[RaidSnapshotLoader] snapshot service failed: " +
                                       snapshotTask.Exception?.GetBaseException().Message);
                        loadQueued = false;
                        yield break;
                    }
                    selectedLayout = snapshotTask.Result?.layout;
                }
                else
                {
                    selectedLayout = RaidContext.Target.layout;
                }
                if (selectedLayout == null)
                {
                    Debug.LogError("[RaidSnapshotLoader] selected target could not resolve an immutable layout.");
                    loadQueued = false;
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(RaidContext.TargetSnapshotId))
                    Debug.Log($"[RaidSnapshotLoader] resolving immutable snapshot {RaidContext.TargetSnapshotId} " +
                              $"v{RaidContext.TargetSnapshotRevision}; Draft storage is not consulted.");
                LastLoadedSnapshotId = RaidContext.TargetSnapshotId;
                LoadIntoMatch(selectedLayout);
                yield break;
            }

            // local test: โหลดผังเมืองตัวเองจาก save (เทส loop ก่อนมีจอเลือกเป้า)
            var draftTask = SpliceServiceHub.TownSnapshots.GetCheckedOutDraftAsync(
                CityFactionId, CancellationToken.None);
            while (!draftTask.IsCompleted) yield return null;
            if (draftTask.IsFaulted)
            {
                Debug.LogError("[RaidSnapshotLoader] draft service failed: " +
                               draftTask.Exception?.GetBaseException().Message);
                loadQueued = false;
                yield break;
            }
            var layout = draftTask.Result?.checkedOutLayout;
            if (layout != null) LoadIntoMatch(layout);
            else Debug.LogWarning($"[RaidSnapshotLoader] ไม่มี BaseLayout ของ faction '{CityFactionId}' ใน local save — ข้าม (จัดเมือง/capture ก่อน)");
            loadQueued = false;
        }

        public bool CanLoadSelectedTarget(out string error)
        {
            if (!IsSpawned || !IsServer)
            {
                error = "Raid snapshot server is not ready.";
                return false;
            }
            if (registry == null)
            {
                error = "Faction registry is missing from RaidSnapshotLoader.";
                return false;
            }
            if (loadQueued || HasStartedLoading || HasCompletedLoading)
            {
                error = "Raid snapshot loading has already started.";
                return false;
            }
            if (!RaidContext.HasTarget)
            {
                error = "Selected raid target has no resolvable immutable layout.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryLoadSelectedTarget(out string error)
        {
            if (!CanLoadSelectedTarget(out error)) return false;
            QueueLoad();
            return true;
        }

        private void QueueLoad()
        {
            if (loadQueued || HasStartedLoading || HasCompletedLoading) return;
            loadQueued = true;
            StartCoroutine(LoadNextFrame()); // wait for in-scene NetworkObjects to finish spawning
        }

        // จุดเข้าเดียวของการโหลด snapshot เข้าแมตช์ — ขั้น 5.4 (raid flow) จะส่ง layout ของเป้าหมายจริงมาที่นี่
        public void LoadIntoMatch(BaseLayout layout)
        {
            if (!IsServer || layout == null || registry == null)
            {
                loadQueued = false;
                return;
            }

            var towers = 0;
            foreach (var data in layout.towers)
            {
                if (SpawnTower(data)) towers++;
            }

            var miners = 0;
            foreach (var cardId in layout.minerCardIds)
            {
                if (SpawnMiner(cardId)) miners++;
            }

            // ทองในคลังฐาน = แทนที่ startingGold ของทีมตั้งรับ (เป้าโดนปล้น — loot % คิดในขั้น 5.4)
            GoldController.For(defendSide)?.SetBalance(layout.storedGold);

            var garrison = 0;
            foreach (var data in layout.garrison)
                if (SpawnGarrison(data)) garrison++;

            LastLoadedTowerCount = towers;
            LastLoadedGarrisonCount = garrison;
            LastLoadedMinerCount = miners;
            HasCompletedLoading = true;
            loadQueued = false;

            Debug.Log($"[RaidSnapshotLoader] โหลดเมืองตั้งรับ: ป้อม {towers}, garrison {garrison}, miner {miners}, ทองคลัง {layout.storedGold}");
        }

        private bool SpawnTower(PlacedTowerData data)
        {
            var definition = registry.ResolveTower(data.towerId);
            if (definition == null || definition.prefab == null)
            {
                Debug.LogWarning($"[RaidSnapshotLoader] resolve tower '{data.towerId}' ไม่ได้ — ข้าม (content ถูกลบ/เปลี่ยน id?)");
                return false;
            }

            var instance = Instantiate(definition.prefab, data.position, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn();
            var tower = instance.GetComponent<TowerCharacter>();
            tower.Initialize(definition);
            tower.SkipConstruction(); // ฐานที่จัดไว้แล้ว = สร้างเสร็จแล้ว ต้องพร้อมยิงทันทีตอนถูก raid

            ApplyLevels(tower, TowerStat.Attack, data.attackLevel);
            ApplyLevels(tower, TowerStat.Health, data.healthLevel);
            ApplyLevels(tower, TowerStat.Armor, data.armorLevel);
            ApplyLevels(tower, TowerStat.Range, data.rangeLevel);
            ApplyLevels(tower, TowerStat.Targets, data.targetsLevel);
            return true;
        }

        // เดินซ้ำ ApplyStatUpgrade ทีละระดับ (เส้นทางเดียวกับการอัพจริง — side effect HP/armor ถูกต้องเอง)
        // clamp ที่ maxLevel กัน save เก่าที่อัพไว้เกินเพดานใหม่หลัง balance patch
        private void ApplyLevels(TowerCharacter tower, TowerStat stat, int level)
        {
            var max = tower.Definition.UpgradeFor(stat).maxLevel;
            var target = Mathf.Min(level, max);
            for (var i = 0; i < target; i++) tower.ApplyStatUpgrade(stat);
        }

        // มอนเฝ้าเมือง (garrison) ฝ่ายตั้งรับ — spawn แบบ hold-position (MonsterCharacter.InitializeGarrison)
        private bool SpawnGarrison(GarrisonMonsterData data)
        {
            var card = registry.ResolveCard(data.cardId);
            if (card == null || card.linkedMonster == null || card.linkedMonster.prefab == null)
            {
                Debug.LogWarning($"[RaidSnapshotLoader] resolve garrison card '{data.cardId}' ไม่ได้ — ข้าม");
                return false;
            }

            var instance = Instantiate(card.linkedMonster.prefab, data.position, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn();
            instance.GetComponent<MonsterCharacter>().InitializeGarrison(card.linkedMonster, defendSide, data.position);
            return true;
        }

        private bool SpawnMiner(string cardId)
        {
            var card = registry.ResolveCard(cardId);
            if (card == null || card.linkedMiner == null || card.linkedMiner.prefab == null)
            {
                Debug.LogWarning($"[RaidSnapshotLoader] resolve miner card '{cardId}' ไม่ได้ — ข้าม");
                return false;
            }

            var pos = minerSpawnPoint != null ? minerSpawnPoint.position : transform.position;
            var instance = Instantiate(card.linkedMiner.prefab, pos, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn();
            instance.GetComponent<MinerCharacter>().Initialize(card.linkedMiner, defendSide);
            return true;
        }

        // ---------- Debug (ชั่วคราว จนกว่า Build Mode ขั้น 5.2 จะมาแทน) ----------
        // เก็บป้อมทุกตัวที่ยืนอยู่ในซีน "ตอนนี้" เป็น BaseLayout ลง local save — ให้เทส loop snapshot ได้ทันที:
        // เล่นฝั่ง Fort วางป้อมตามใจ → คลิกขวาที่ component นี้ (ตอน Play, เครื่อง host) → เลือกเมนูนี้
        // → รันใหม่โดยติ๊ก loadLocalSaveOnSpawn = ป้อมชุดเดิมโผล่เอง
        [ContextMenu("Debug/Capture Scene Towers -> Save Layout")]
        private void DebugCaptureSceneToSave() => _ = DebugCaptureSceneToSaveAsync();

        private async Task DebugCaptureSceneToSaveAsync()
        {
            if (!IsServer || registry == null)
            {
                Debug.LogWarning("[RaidSnapshotLoader] capture ได้เฉพาะตอน Play ฝั่ง server/host");
                return;
            }

            var fid = CityFactionId;
            if (string.IsNullOrEmpty(fid))
            {
                Debug.LogWarning("[RaidSnapshotLoader] ไม่มี faction (ตั้ง targetFactionId หรือเลือก faction ก่อน) — ไม่ capture");
                return;
            }

            var draft = await SpliceServiceHub.TownSnapshots.GetCheckedOutDraftAsync(
                fid, CancellationToken.None);
            var layout = draft?.checkedOutLayout ?? new BaseLayout();
            layout.factionId = fid;
            layout.ownerAccountId = PlayerProfile.AccountId;
            layout.towers.Clear();

            foreach (var tower in TowerCharacter.Active)
            {
                if (tower == null || tower.IsDead || tower.Definition == null) continue;
                if (tower is FortCore) continue; // Fort เป็นของแมปฐาน ไม่ใช่ของ layout

                var id = registry.IdOf(tower.Definition);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[RaidSnapshotLoader] ป้อม '{tower.Definition.displayName}' ไม่อยู่ใน registry — ข้าม");
                    continue;
                }

                layout.towers.Add(new PlacedTowerData
                {
                    towerId = id,
                    position = tower.transform.position,
                    attackLevel = tower.UpgradeLevel(TowerStat.Attack),
                    healthLevel = tower.UpgradeLevel(TowerStat.Health),
                    armorLevel = tower.UpgradeLevel(TowerStat.Armor),
                    rangeLevel = tower.UpgradeLevel(TowerStat.Range),
                    targetsLevel = tower.UpgradeLevel(TowerStat.Targets),
                });
            }

            var bank = GoldController.For(defendSide);
            if (bank != null) layout.storedGold = bank.CurrentGold;

            await SpliceServiceHub.TownSnapshots.SaveCheckedOutDraftAsync(layout,
                Guid.NewGuid().ToString("N"), CancellationToken.None);
            Debug.Log($"[RaidSnapshotLoader] capture แล้ว: ป้อม {layout.towers.Count}, ทองคลัง {layout.storedGold} → local save");
        }
    }
}
