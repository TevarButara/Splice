using System.Collections.Generic;
using Splice.Data;
using UnityEngine;

namespace Splice.Characters
{
    // Invader unit (architecture 5.6/5.8). Walks a fixed, map-authored LanePath toward the fort and
    // NEVER stops — it attacks any defender TowerCharacter (the Fort included) that falls within range
    // while passing, in parallel with movement. That "advance-and-shoot" model keeps constant pressure
    // (no stalemate) while still letting monsters chip down towers, so the Fort must repair/replace them.
    // Server drives movement + combat; clients replicate via NetworkTransform (no local NavMesh).
    public class MonsterCharacter : CharacterBase
    {
        private static readonly List<MonsterCharacter> active = new();
        public static IReadOnlyList<MonsterCharacter> Active => active;

        [SerializeField] private MonsterDefinitionSO definition;
        [Tooltip("ระยะ (XZ) ที่ถือว่า 'ถึง' waypoint แล้วเลื่อนไปจุดถัดไป")]
        [SerializeField] private float waypointArriveRadius = 0.15f;

        private LanePath path;
        private int waypointIndex;
        private TowerCharacter currentTarget;
        private float attackTimer;

        public void Initialize(MonsterDefinitionSO monsterDefinition, LanePath lanePath)
        {
            definition = monsterDefinition;
            InitializeHealth(definition.maxHealth);
            SetPath(lanePath);
        }

        public void SetPath(LanePath lanePath)
        {
            path = lanePath;
            waypointIndex = 0;
            if (path != null && path.Count > 0) transform.position = path.Start;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            active.Add(this);
            // Monsters placed directly in a scene skip DeploymentManager's runtime Initialize() call,
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

            Advance();
            TryAttack();
        }

        // Move along the lane at moveSpeed, advancing to the next waypoint once close enough.
        // Past the final waypoint the monster steers straight at the Fort core until it's within its own
        // attackRange — so units with different reach stop at the right distance and always land hits.
        private void Advance()
        {
            if (path == null) return;

            // Consume every waypoint we're already on top of (XZ only, so the monster's pivot height
            // and any parent-transform float error can't stop it from registering arrival — the exact
            // Vector3 == check used to leave it stuck pressing into a waypoint forever).
            while (waypointIndex < path.Count && HorizontalDistance(transform.position, path.GetPoint(waypointIndex)) <= waypointArriveRadius)
            {
                waypointIndex++;
            }

            if (waypointIndex >= path.Count)
            {
                StepTowardFort();
                return;
            }

            StepToward(path.GetPoint(waypointIndex));
        }

        // Close the last gap onto the Fort. Uses the exact same InRange check TryAttack does, so the
        // monster stops precisely where it can attack — no walking-past, no stopping just short.
        private void StepTowardFort()
        {
            var fort = FortCore.Instance;
            if (fort == null || fort.IsDead || InRange(fort)) return;
            StepToward(fort.transform.position);
        }

        private void StepToward(Vector3 target)
        {
            var step = definition.moveSpeed * Time.deltaTime;
            var next = Vector3.MoveTowards(transform.position, target, step);

            var heading = next - transform.position;
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading);

            transform.position = next;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // Runs independently of Advance — attacking never halts the march. Picks the nearest defender
        // in range (Fort included, since FortCore is a TowerCharacter) and chips it on cooldown.
        private void TryAttack()
        {
            if (currentTarget == null || currentTarget.IsDead || !InRange(currentTarget))
            {
                currentTarget = TargetingUtility.FindNearest(TowerCharacter.Active, transform.position, definition.attackRange);
            }

            if (currentTarget == null) return;

            attackTimer += Time.deltaTime;
            if (attackTimer < definition.attackCooldown) return;

            attackTimer = 0f;
            currentTarget.ApplyDamage(definition.attackDamage);
        }

        // No NavMesh here, so raw pivot distance is reliable (nothing carves the world for on-rails movers).
        private bool InRange(TowerCharacter target)
        {
            return Vector3.Distance(transform.position, target.transform.position) <= definition.attackRange;
        }
    }
}
