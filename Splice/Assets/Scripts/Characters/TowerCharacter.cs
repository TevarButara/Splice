using System.Collections.Generic;
using Splice.Data;
using UnityEngine;

namespace Splice.Characters
{
    // Static defender that attacks the nearest MonsterCharacter within range (architecture 5.1).
    public class TowerCharacter : CharacterBase
    {
        private static readonly List<TowerCharacter> active = new();
        public static IReadOnlyList<TowerCharacter> Active => active;

        [SerializeField] private TowerDefinitionSO definition;
        [Tooltip("ติ๊กเพื่อโชว์วงระยะโจมตีตลอดเวลา (ไม่ต้องเลือกก่อน) — ช่วยกะระยะตอนจัดวาง. เป็น Gizmo (Scene view เสมอ, Game view ต้องเปิดปุ่ม Gizmos)")]
        [SerializeField] private bool alwaysShowRange;

        // Exposed so the server-side interaction flow (TowerDeploymentManager) can read cost/tier data
        // when computing repair/demolish/upgrade gold.
        public TowerDefinitionSO Definition => definition;

        private MonsterCharacter currentTarget;
        private float attackTimer;

        public void Initialize(TowerDefinitionSO towerDefinition)
        {
            definition = towerDefinition;
            InitializeHealth(definition.maxHealth);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            active.Add(this);
            // Towers/forts placed directly in a scene skip DeploymentManager's runtime Initialize() call,
            // so seed health here from whatever definition is wired in the Inspector.
            if (definition != null && CurrentHealth <= 0) InitializeHealth(definition.maxHealth);
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || IsDead || definition == null) return;

            if (currentTarget == null || currentTarget.IsDead || !InRange(currentTarget))
            {
                currentTarget = TargetingUtility.FindNearest(MonsterCharacter.Active, transform.position, definition.attackRange);
                return;
            }

            attackTimer += Time.deltaTime;
            if (attackTimer < definition.attackCooldown) return;

            attackTimer = 0f;
            currentTarget.ApplyDamage(definition.attackDamage);
        }

        private bool InRange(MonsterCharacter target)
        {
            return Vector3.Distance(transform.position, target.transform.position) <= definition.attackRange;
        }

        // Scene-view range ring (also inherited by FortCore). Red = defender range. Shown when selected,
        // or all the time when alwaysShowRange is ticked — so you can gauge reach while placing.
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
            if (definition != null) RangeGizmo.DrawFlatCircle(transform.position, definition.attackRange, Color.red);
        }
    }
}
