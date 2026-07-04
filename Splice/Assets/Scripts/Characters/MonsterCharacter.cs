using System.Collections.Generic;
using Splice.Combat;
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
        [Tooltip("ติ๊กเพื่อโชว์วงระยะโจมตีตลอดเวลา (ไม่ต้องเลือกก่อน) — ช่วยกะระยะ. เป็น Gizmo (Scene view เสมอ, Game view ต้องเปิดปุ่ม Gizmos)")]
        [SerializeField] private bool alwaysShowRange;

        [Header("Separation (กันมอนกองทับกันตรง Fort)")]
        [Tooltip("รัศมี (XZ) ที่เริ่มดันออกจากมอนชนิดเดียวกัน — ~2x รัศมีตัวมอน. 0 = ปิด separation")]
        [SerializeField] private float separationRadius = 1f;
        [Tooltip("แรงดันแยก เทียบกับความเร็วเดินหน้า (<1 = เดินหน้าชนะแต่ยังกระจายตัว)")]
        [SerializeField] private float separationStrength = 0.6f;

        private LanePath path;
        private int waypointIndex;
        private float baseGroundY;   // fallback ground level (path start) — used only if no map point is available
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
            if (path != null && path.Count > 0)
            {
                transform.position = path.Start;
                baseGroundY = path.Start.y;
            }
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

            // Game over → freeze everything (this monster and any still marching in): no moving, no damage.
            // Two independent signals so a mis-wired RaidManager can't leave monsters drifting/floating:
            //  - the raid was decided by any path (timer / elimination / fort), and
            //  - the Fort core is dead (checked directly — the husk stays in the scene, so IsDead is stable).
            if (RaidManager.Instance != null && RaidManager.Instance.IsOver) return;
            if (FortCore.Instance != null && FortCore.Instance.IsDead) return;

            Advance();
            TryAttack();
        }

        // Advance along the lane, with two behaviours layered on the waypoint march:
        //  - Separation: nudge away from nearby same-type monsters so a crowd spreads into a ring around
        //    the Fort instead of stacking on its centre.
        //  - Height: Ground types hug the ground (the target point's y); Flying types hover flightHeight
        //    above it. The ground reference is always a map point (waypoint/Fort), never our own y — so a
        //    flyer can't ratchet its own altitude upward every frame.
        // Once the Fort is within attackRange the monster stops advancing (holds + shoots) — but separation
        // still runs, so even a stalled crowd keeps spacing out.
        private void Advance()
        {
            if (path == null) return;

            var fort = FortCore.Instance;
            var holding = fort != null && !fort.IsDead && InRange(fort);
            var pos = transform.position;

            // Horizontal destination (XZ) — identical for Ground and Flying: both walk the same waypoints.
            Vector3 targetXZ;
            if (holding)
            {
                targetXZ = pos; // hold horizontal position, just keep shooting
            }
            else
            {
                // Consume every waypoint we're already on top of (XZ only, so pivot height / float error
                // can't stop arrival from registering — the old exact-Vector3 check left units stuck).
                while (waypointIndex < path.Count && HorizontalDistance(pos, path.GetPoint(waypointIndex)) <= waypointArriveRadius)
                {
                    waypointIndex++;
                }

                if (waypointIndex >= path.Count)
                    targetXZ = fort != null && !fort.IsDead ? fort.transform.position : pos;
                else
                    targetXZ = path.GetPoint(waypointIndex);
            }

            var step = definition.moveSpeed * Time.deltaTime;

            // Horizontal move toward the target (XZ) plus a separation nudge from neighbours.
            var flatTarget = new Vector3(targetXZ.x, pos.y, targetXZ.z);
            var moved = Vector3.MoveTowards(pos, flatTarget, step) + SeparationOffset() * step;

            var heading = new Vector3(moved.x - pos.x, 0f, moved.z - pos.z);
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading);

            // Height = a stable GROUND reference + a fixed flight offset. The reference is always a map
            // point (waypoint/Fort/path end), never our own y — otherwise a flyer would add flightHeight
            // on top of its already-raised position every frame and climb forever.
            var groundY = GroundReferenceY(fort, holding);
            var desiredY = definition.movement == MonsterMovement.Flying ? groundY + definition.flightHeight : groundY;
            moved.y = Mathf.MoveTowards(pos.y, desiredY, step);

            transform.position = moved;
        }

        // Ground level to sit on / hover above this frame — taken from map points only (never our altitude):
        // the waypoint we're heading to, else the Fort, else the path's end, else the spawn ground.
        private float GroundReferenceY(FortCore fort, bool holding)
        {
            if (!holding && waypointIndex < path.Count) return path.GetPoint(waypointIndex).y;
            if (fort != null && !fort.IsDead) return fort.transform.position.y;
            if (path.Count > 0) return path.GetPoint(path.Count - 1).y;
            return baseGroundY;
        }

        // Sum of pushes away from nearby monsters of the SAME movement type (XZ only), clamped so a big
        // crowd can't fling anyone. This is what keeps units from overlapping on the Fort.
        private Vector3 SeparationOffset()
        {
            if (separationRadius <= 0f || definition == null) return Vector3.zero;

            var push = Vector3.zero;
            var pos = transform.position;
            for (var i = 0; i < active.Count; i++)
            {
                var other = active[i];
                if (other == this || other.IsDead || other.definition == null) continue;
                if (other.definition.movement != definition.movement) continue;

                var offset = other.transform.position - pos;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance < 0.0001f || distance > separationRadius) continue;

                push -= offset / distance * (1f - distance / separationRadius);
            }

            return Vector3.ClampMagnitude(push, 1f) * separationStrength;
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

        // Scene-view range ring. Orange = invader range (distinct from towers' red). Shown when selected,
        // or all the time when alwaysShowRange is ticked.
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
            if (definition != null) RangeGizmo.DrawFlatCircle(transform.position, definition.attackRange, new Color(1f, 0.5f, 0f));
        }
    }
}
