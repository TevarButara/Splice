using System.Collections.Generic;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Characters
{
    // Per-tower upgrade levels for the five stats. Replicated so clients can display upgraded values.
    public struct TowerUpgradeLevels : INetworkSerializable, System.IEquatable<TowerUpgradeLevels>
    {
        public int Attack;
        public int Health;
        public int Armor;
        public int Range;
        public int Targets;

        public int Get(TowerStat stat) => stat switch
        {
            TowerStat.Attack => Attack,
            TowerStat.Health => Health,
            TowerStat.Armor => Armor,
            TowerStat.Range => Range,
            TowerStat.Targets => Targets,
            _ => 0
        };

        public void Increment(TowerStat stat)
        {
            switch (stat)
            {
                case TowerStat.Attack: Attack++; break;
                case TowerStat.Health: Health++; break;
                case TowerStat.Armor: Armor++; break;
                case TowerStat.Range: Range++; break;
                case TowerStat.Targets: Targets++; break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Attack);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref Armor);
            serializer.SerializeValue(ref Range);
            serializer.SerializeValue(ref Targets);
        }

        public bool Equals(TowerUpgradeLevels other) =>
            Attack == other.Attack && Health == other.Health && Armor == other.Armor &&
            Range == other.Range && Targets == other.Targets;
    }

    // Static defender that attacks the nearest attacker units/Hero in range (architecture 5.1/5.6). Stats are
    // base (TowerDefinitionSO) + per-stat upgrade levels; HP/armor upgrades are pushed into CharacterBase,
    // attack/range/targets are computed on the fly. Placed towers first spend buildTimeSeconds under
    // construction (can't fire, but are attackable). FortCore extends this.
    public class TowerCharacter : CharacterBase
    {
        private static readonly List<TowerCharacter> active = new();
        public static IReadOnlyList<TowerCharacter> Active => active;

        // Reused per fire (server is single-threaded, so sharing is safe).
        private static readonly List<CharacterBase> fireBuffer = new();

        [SerializeField] private TowerDefinitionSO definition;
        [Tooltip("ติ๊กเพื่อโชว์วงระยะโจมตีตลอดเวลา (ไม่ต้องเลือกก่อน) — ช่วยกะระยะตอนจัดวาง. เป็น Gizmo (Scene view เสมอ, Game view ต้องเปิดปุ่ม Gizmos)")]
        [SerializeField] private bool alwaysShowRange;
        [Tooltip("ป้อมปืน (เล็ง+อนิเมชันยิง+ภาพกระสุน) — เว้นได้: ถ้าไม่มีจะลงดาเมจทันทีแบบไม่มีภาพ (auto หาใน object เดียวกัน)")]
        [SerializeField] private TurretController turret;

        // Exposed so the server-side interaction flow (TowerDeploymentManager) can read cost/tier data.
        public TowerDefinitionSO Definition => definition;

        private float attackTimer;

        // โหมด Projectile: ดาเมจไม่ลงตอนยิง แต่ตั้งเวลาไว้ให้ตรงกับตอนกระสุนถึงเป้า (server-only).
        // เป้าตาย/หายก่อนถึง = ยิงพลาด (whiff) ไม่ลงดาเมจ.
        private struct PendingHit { public CharacterBase target; public float timeLeft; public int damage; }
        private readonly List<PendingHit> pendingHits = new();

        // เป้าที่จะยิงในชอตนี้ (reuse; server single-thread)
        private static readonly List<CharacterBase> volleyTargets = new();

        // Counts down after placement; the tower is placed & attackable but can't fire until it hits 0.
        private readonly NetworkVariable<float> buildRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<TowerUpgradeLevels> upgradeLevels = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsConstructing => buildRemaining.Value > 0f;
        public float BuildProgress01 => definition != null && definition.buildTimeSeconds > 0f
            ? 1f - Mathf.Clamp01(buildRemaining.Value / definition.buildTimeSeconds)
            : 1f;

        // Effective stats = base + level × amountPerLevel. (HP & armor live in CharacterBase, applied on upgrade.)
        public int EffectiveDamage => definition == null ? 0
            : definition.attackDamage + Mathf.RoundToInt(upgradeLevels.Value.Attack * definition.attackUpgrade.amountPerLevel);
        public float EffectiveRange => definition == null ? 0f
            : definition.attackRange + upgradeLevels.Value.Range * definition.rangeUpgrade.amountPerLevel;
        public int EffectiveMaxTargets => definition == null ? 1
            : Mathf.Max(1, definition.maxTargets + Mathf.RoundToInt(upgradeLevels.Value.Targets * definition.targetsUpgrade.amountPerLevel));

        public int UpgradeLevel(TowerStat stat) => upgradeLevels.Value.Get(stat);

        private void Awake()
        {
            // Auto-wire the turret if it wasn't dragged in (usually sits on the same tower object).
            if (turret == null) turret = GetComponent<TurretController>();
        }

        public void Initialize(TowerDefinitionSO towerDefinition)
        {
            definition = towerDefinition;
            InitializeHealth(definition.maxHealth);
            if (IsServer)
            {
                buildRemaining.Value = Mathf.Max(0f, definition.buildTimeSeconds);
                upgradeLevels.Value = default;
                SetArmor(definition.armor);
            }
        }

        // Server-only: ข้ามเวลาก่อสร้าง — ใช้ตอน spawn จาก snapshot ฐาน (RaidSnapshotLoader, architecture 5.10)
        // ที่ป้อมซึ่งจัดไว้แล้วต้องพร้อมยิงทันที ไม่ใช่เริ่มก่อสร้างใหม่
        public void SkipConstruction()
        {
            if (IsServer) buildRemaining.Value = 0f;
        }

        // Server-only: bump a stat's level and apply the ones that live in CharacterBase (HP, armor).
        public void ApplyStatUpgrade(TowerStat stat)
        {
            if (!IsServer || definition == null) return;

            var levels = upgradeLevels.Value;
            levels.Increment(stat);
            upgradeLevels.Value = levels;

            switch (stat)
            {
                case TowerStat.Health:
                    RaiseMaxHealth(Mathf.RoundToInt(definition.healthUpgrade.amountPerLevel));
                    break;
                case TowerStat.Armor:
                    SetArmor(definition.armor + Mathf.RoundToInt(levels.Armor * definition.armorUpgrade.amountPerLevel));
                    break;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            active.Add(this);
            // Towers/forts placed directly in a scene skip Initialize(), so seed health + armor here from the
            // Inspector definition.
            if (definition != null && CurrentHealth <= 0)
            {
                InitializeHealth(definition.maxHealth);
                SetArmor(definition.armor);
            }
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || IsDead || definition == null) return;

            // Projectile-mode damage lands on impact — run the scheduled hits regardless of build/cooldown
            // so shots already in flight still resolve.
            TickPendingHits(Time.deltaTime);

            // จบแมตช์แล้ว → หยุดยิง + หยุดเล็ง (ป้อมค้างท่า ไม่โจมตีต่อ) เหมือนที่ monster หยุดสู้
            if (IsMatchOver())
            {
                turret?.SetAimTarget(null);
                return;
            }

            // Still under construction: tick the build timer down and hold fire until it's finished.
            if (buildRemaining.Value > 0f)
            {
                buildRemaining.Value = Mathf.Max(0f, buildRemaining.Value - Time.deltaTime);
                return;
            }

            attackTimer += Time.deltaTime;

            // Nearest-first list of monsters in range (used for both aiming and the volley).
            CollectTargetsInRange(fireBuffer);
            var primary = fireBuffer.Count > 0 ? fireBuffer[0] : null;

            // Keep the turret tracking the primary target every frame (even during cooldown).
            if (turret != null) turret.SetAimTarget(primary);

            if (attackTimer < definition.attackCooldown) return;
            if (primary == null) { attackTimer = definition.attackCooldown; return; } // stay primed for a new arrival
            if (turret != null && !turret.ReadyToFire(primary)) return;                // hold only if Hold-Fire is on

            FireVolley();
            attackTimer = 0f;
        }

        // Fill `buffer` with living attacker units/Hero within EffectiveRange, nearest first.
        private void CollectTargetsInRange(List<CharacterBase> buffer)
        {
            var range = EffectiveRange;
            var pos = transform.position;

            buffer.Clear();
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster.IsDead || monster.Side != RaidSide.Attacker) continue;
                if (Vector3.Distance(pos, monster.transform.position) <= range) buffer.Add(monster);
            }

            var hero = RaidHeroCharacter.Instance;
            if (hero != null && !hero.IsDead && hero.Side == RaidSide.Attacker &&
                Vector3.Distance(pos, hero.transform.position) <= range)
                buffer.Add(hero);

            buffer.Sort((a, b) =>
                (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
        }

        // Fire at up to EffectiveMaxTargets nearest attacker targets (already collected in fireBuffer).
        //  - No turret / Direct mode: damage lands immediately (turret shows the beam).
        //  - Projectile mode: damage is scheduled for when the shot arrives (travel time), so a monster can
        //    dodge death by dying first (whiff). The turret spawns the cosmetic projectiles on every client.
        private void FireVolley()
        {
            var damage = EffectiveDamage;
            var hits = Mathf.Min(EffectiveMaxTargets, fireBuffer.Count);
            if (hits <= 0) return;

            var projectileMode = turret != null && turret.Mode == TurretFireMode.Projectile;

            volleyTargets.Clear();
            for (var i = 0; i < hits; i++)
            {
                var target = fireBuffer[i];
                volleyTargets.Add(target);

                if (projectileMode)
                    pendingHits.Add(new PendingHit
                    {
                        target = target,
                        timeLeft = turret.TravelTimeTo(target.transform.position),
                        damage = damage
                    });
                else
                    target.ApplyDamage(damage, this); // no turret / Direct = instant (this = ผู้ตี → มอน aggro ได้)
            }

            if (turret != null) turret.Fire(volleyTargets);
        }

        private void TickPendingHits(float dt)
        {
            for (var i = pendingHits.Count - 1; i >= 0; i--)
            {
                var hit = pendingHits[i];
                hit.timeLeft -= dt;
                if (hit.timeLeft > 0f) { pendingHits[i] = hit; continue; }

                if (hit.target != null && !hit.target.IsDead) hit.target.ApplyDamage(hit.damage, this);
                pendingHits.RemoveAt(i);
            }
        }

        // แมตช์จบเมื่อ RaidManager ประกาศจบ หรือ Fort แตก — ป้อมหยุดยิงเหมือนที่ monster หยุดสู้ (สอดคล้อง IsMatchOver ใน MonsterCharacter)
        private static bool IsMatchOver() =>
            (RaidManager.Instance != null && RaidManager.Instance.IsOver) ||
            (FortCore.Instance != null && FortCore.Instance.IsDead);

        // Scene-view range ring (also inherited by FortCore). Red = defender range; reflects upgrades at runtime.
        // Shown when selected, or all the time when alwaysShowRange is ticked.
        private void OnDrawGizmos()
        {
            if (alwaysShowRange) DrawRangeGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!alwaysShowRange) DrawRangeGizmo();
        }

        private void DrawRangeGizmo()
        {
            if (definition != null) RangeGizmo.DrawFlatCircle(transform.position, EffectiveRange, Color.red);
        }
    }
}
