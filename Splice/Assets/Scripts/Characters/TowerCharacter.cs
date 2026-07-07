using System.Collections.Generic;
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

    // Static defender that attacks the nearest MonsterCharacter(s) in range (architecture 5.1/5.6). Stats are
    // base (TowerDefinitionSO) + per-stat upgrade levels; HP/armor upgrades are pushed into CharacterBase,
    // attack/range/targets are computed on the fly. Placed towers first spend buildTimeSeconds under
    // construction (can't fire, but are attackable). FortCore extends this.
    public class TowerCharacter : CharacterBase
    {
        private static readonly List<TowerCharacter> active = new();
        public static IReadOnlyList<TowerCharacter> Active => active;

        // Reused per fire (server is single-threaded, so sharing is safe).
        private static readonly List<MonsterCharacter> fireBuffer = new();

        [SerializeField] private TowerDefinitionSO definition;
        [Tooltip("ติ๊กเพื่อโชว์วงระยะโจมตีตลอดเวลา (ไม่ต้องเลือกก่อน) — ช่วยกะระยะตอนจัดวาง. เป็น Gizmo (Scene view เสมอ, Game view ต้องเปิดปุ่ม Gizmos)")]
        [SerializeField] private bool alwaysShowRange;

        // Exposed so the server-side interaction flow (TowerDeploymentManager) can read cost/tier data.
        public TowerDefinitionSO Definition => definition;

        private float attackTimer;

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

            // Still under construction: tick the build timer down and hold fire until it's finished.
            if (buildRemaining.Value > 0f)
            {
                buildRemaining.Value = Mathf.Max(0f, buildRemaining.Value - Time.deltaTime);
                return;
            }

            attackTimer += Time.deltaTime;
            if (attackTimer < definition.attackCooldown) return;

            // Fire at up to EffectiveMaxTargets nearest monsters in range; only spend the cooldown when we
            // actually hit something (otherwise stay primed so a monster entering range is shot at once).
            if (FireAtNearest() > 0) attackTimer = 0f;
            else attackTimer = definition.attackCooldown;
        }

        private int FireAtNearest()
        {
            var range = EffectiveRange;
            var pos = transform.position;

            fireBuffer.Clear();
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster.IsDead) continue;
                if (Vector3.Distance(pos, monster.transform.position) <= range) fireBuffer.Add(monster);
            }
            if (fireBuffer.Count == 0) return 0;

            fireBuffer.Sort((a, b) =>
                (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));

            var damage = EffectiveDamage;
            var hits = Mathf.Min(EffectiveMaxTargets, fireBuffer.Count);
            for (var i = 0; i < hits; i++) fireBuffer[i].ApplyDamage(damage);
            return hits;
        }

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
