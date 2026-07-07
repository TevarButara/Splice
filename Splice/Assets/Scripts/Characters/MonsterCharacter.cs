using System.Collections.Generic;
using Splice.Combat;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Characters
{
    // Animation states the monster can be in — replicated so every client plays the right clip.
    public enum MonsterAnim
    {
        Idle,
        Walk,
        Attack,
        InjuredWalk,
        Death,
        Victory,
        Lose
    }

    // Invader unit (architecture 5.6/5.8). Walks a fixed, map-authored LanePath toward the fort. When a
    // defender (tower/Fort) is in range and the cooldown is ready it STOPS, turns to face the target, plays
    // its Attack clip, lands the hit, then resumes walking. Server drives movement + combat; clients see
    // position via NetworkTransform and the animation via the replicated animState.
    public class MonsterCharacter : CharacterBase
    {
        private static readonly List<MonsterCharacter> active = new();
        public static IReadOnlyList<MonsterCharacter> Active => active;

        [SerializeField] private MonsterDefinitionSO definition;
        [Tooltip("ระยะ (XZ) ที่ถือว่า 'ถึง' waypoint แล้วเลื่อนไปจุดถัดไป")]
        [SerializeField] private float waypointArriveRadius = 0.15f;
        [Tooltip("ระยะที่ถือว่า 'ถึงป้อมแล้ว' → หยุดรุกเข้า core (แยกจาก attackRange — ตั้งเล็กๆ). กันมอนหยุดค้างกลางทางถ้า attackRange กว้าง; มอนจะเดินเข้าใกล้ป้อมก่อนหยุด (ยิงระหว่างเข้าได้)")]
        [SerializeField] private float fortHoldDistance = 1.5f;
        [Tooltip("ติ๊กเพื่อโชว์วงระยะโจมตีตลอดเวลา (ไม่ต้องเลือกก่อน) — ช่วยกะระยะ. เป็น Gizmo (Scene view เสมอ, Game view ต้องเปิดปุ่ม Gizmos)")]
        [SerializeField] private bool alwaysShowRange;

        [Header("Separation (กันมอนกองทับกันตรง Fort)")]
        [Tooltip("รัศมี (XZ) ที่เริ่มดันออกจากมอนชนิดเดียวกัน — ~2x รัศมีตัวมอน. 0 = ปิด separation")]
        [SerializeField] private float separationRadius = 1f;
        [Tooltip("แรงดันแยก เทียบกับความเร็วเดินหน้า (<1 = เดินหน้าชนะแต่ยังกระจายตัว)")]
        [SerializeField] private float separationStrength = 0.6f;

        [Header("Animation")]
        [Tooltip("Animator ของตัวละคร — ปิด Apply Root Motion (การเคลื่อนที่ขับด้วยโค้ด). clip ต้องชื่อ: Idle/Walk/Attack/Injured Walk/Death/Victory/Lose")]
        [SerializeField] private Animator animator;
        [Tooltip("เวลาหยุดหันหน้า+ตี 1 ครั้ง (วินาที) — ตั้งให้พอดีความยาว Attack clip. ดาเมจลงตอนจบ swing")]
        [SerializeField] private float attackDurationSeconds = 0.6f;
        [Tooltip("ความเร็วหันหน้าเข้าหาเป้า (องศา/วินาที)")]
        [SerializeField] private float turnSpeedDegPerSec = 720f;
        [Tooltip("HP ต่ำกว่าสัดส่วนนี้ (0-1) → เดินท่า Injured Walk")]
        [SerializeField] private float injuredHealthFraction = 0.3f;
        [Tooltip("เวลาโชว์ท่า Death ก่อนหายไป (วินาที)")]
        [SerializeField] private float deathAnimSeconds = 1.5f;

        private readonly NetworkVariable<MonsterAnim> animState = new(
            MonsterAnim.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private LanePath path;
        private int waypointIndex;
        private float baseGroundY;   // fallback ground level (path start) — used only if no map point is available
        private float attackTimer;

        private bool attacking;
        private float attackPhaseTimer;
        private TowerCharacter attackTarget;

        private bool dying;
        private float deathTimer;

        private void Awake()
        {
            // Auto-wire the Animator if it wasn't dragged into the field (it usually sits on a child model).
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

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

            // Every instance (server + clients) plays whatever animation the server has set.
            animState.OnValueChanged += HandleAnimChanged;
            PlayAnim(animState.Value);

            if (!IsServer) return;

            active.Add(this);
            // Monsters placed directly in a scene skip DeploymentManager's runtime Initialize() call,
            // so seed health here from whatever definition is wired in the Inspector.
            if (definition != null && CurrentHealth <= 0) InitializeHealth(definition.maxHealth);
        }

        public override void OnNetworkDespawn()
        {
            animState.OnValueChanged -= HandleAnimChanged;
            active.Remove(this);
            base.OnNetworkDespawn();
        }

        // Keep the corpse for a moment so the Death clip can play; Update's dying sequence despawns it.
        protected override void HandleDeath()
        {
        }

        private void Update()
        {
            if (!IsServer || definition == null) return;

            // Death sequence: hold, let the Death clip play, then despawn.
            if (dying)
            {
                deathTimer -= Time.deltaTime;
                if (deathTimer <= 0f && NetworkObject.IsSpawned)
                    NetworkObject.Despawn(destroy: NetworkObject.IsSceneObject != true);
                return;
            }
            if (IsDead)
            {
                dying = true;
                deathTimer = deathAnimSeconds;
                SetAnim(MonsterAnim.Death);
                return;
            }

            // Game over → victory (invaders won) / lose pose, then freeze in place.
            if (IsMatchOver(out var invadersWon))
            {
                SetAnim(invadersWon ? MonsterAnim.Victory : MonsterAnim.Lose);
                return;
            }

            // Mid-attack: stop moving, face the target, swing; damage lands at the end, then resume.
            if (attacking)
            {
                TickAttack();
                return;
            }

            // Walk the lane.
            var before = transform.position;
            Advance();
            var moved = (transform.position - before).sqrMagnitude > 0.000001f;

            // Cooldown ready + a defender in range → stop and start an attack.
            attackTimer += Time.deltaTime;
            if (attackTimer >= definition.attackCooldown)
            {
                var target = TargetingUtility.FindNearest(TowerCharacter.Active, transform.position, definition.attackRange);
                if (target != null)
                {
                    StartAttack(target);
                    return;
                }
            }

            SetAnim(moved ? (IsInjured() ? MonsterAnim.InjuredWalk : MonsterAnim.Walk) : MonsterAnim.Idle);
        }

        private bool IsInjured() => MaxHealth > 0 && (float)CurrentHealth / MaxHealth <= injuredHealthFraction;

        // Walk slower once injured (if the definition sets a separate injured speed).
        private float CurrentMoveSpeed =>
            IsInjured() && definition.injuredMoveSpeed > 0f ? definition.injuredMoveSpeed : definition.moveSpeed;

        private static bool IsMatchOver(out bool invadersWon)
        {
            if (RaidManager.Instance != null && RaidManager.Instance.IsOver)
            {
                invadersWon = RaidManager.Instance.Outcome == RaidOutcome.MonstersWin;
                return true;
            }
            if (FortCore.Instance != null && FortCore.Instance.IsDead)
            {
                invadersWon = true;
                return true;
            }
            invadersWon = false;
            return false;
        }

        private void StartAttack(TowerCharacter target)
        {
            attacking = true;
            attackPhaseTimer = 0f;
            attackTarget = target;
            SetAnim(MonsterAnim.Attack);
        }

        private void TickAttack()
        {
            // Target gone/dead mid-swing → abort and resume walking.
            if (attackTarget == null || attackTarget.IsDead)
            {
                attacking = false;
                attackTimer = 0f;
                return;
            }

            FaceTarget(attackTarget.transform.position);

            attackPhaseTimer += Time.deltaTime;
            if (attackPhaseTimer < attackDurationSeconds) return;

            // Swing lands, then the cooldown restarts and the monster walks on.
            attackTarget.ApplyDamage(definition.attackDamage);
            attacking = false;
            attackTimer = 0f;
        }

        private void FaceTarget(Vector3 targetPos)
        {
            var dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeedDegPerSec * Time.deltaTime);
        }

        // ---------- Animation plumbing ----------

        private void SetAnim(MonsterAnim value)
        {
            if (!IsServer || animState.Value == value) return;
            animState.Value = value;
            PlayAnim(value);              // server plays now; clients react via OnValueChanged
        }

        private void HandleAnimChanged(MonsterAnim previous, MonsterAnim current)
        {
            if (!IsServer) PlayAnim(current);
        }

        private void PlayAnim(MonsterAnim value)
        {
            if (animator != null) animator.CrossFade(StateName(value), 0.1f);
        }

        private static string StateName(MonsterAnim value) => value switch
        {
            MonsterAnim.InjuredWalk => "Injured Walk",
            _ => value.ToString() // Idle / Walk / Attack / Death / Victory / Lose match the enum names
        };

        // ---------- Movement (unchanged) ----------

        // Advance along the lane, with two behaviours layered on the waypoint march:
        //  - Separation: nudge away from nearby same-type monsters so a crowd spreads into a ring around
        //    the Fort instead of stacking on its centre.
        //  - Height: Ground types hug the ground (the target point's y); Flying types hover flightHeight
        //    above it. The ground reference is always a map point (waypoint/Fort), never our own y.
        // Once the Fort is within attackRange the monster stops advancing (holds) — separation still runs.
        private void Advance()
        {
            if (path == null) return;

            var fort = FortCore.Instance;
            var pos = transform.position;
            // "Arrived at the Fort" is a small physical distance, NOT attackRange — otherwise a big
            // attackRange would make the monster freeze mid-lane the moment the Fort came into range.
            var holding = fort != null && !fort.IsDead &&
                          HorizontalDistance(pos, fort.transform.position) <= fortHoldDistance;

            Vector3 targetXZ;
            if (holding)
            {
                targetXZ = pos; // reached the Fort — hold, don't push into the core
            }
            else
            {
                while (waypointIndex < path.Count && HorizontalDistance(pos, path.GetPoint(waypointIndex)) <= waypointArriveRadius)
                {
                    waypointIndex++;
                }

                if (waypointIndex < path.Count)
                    targetXZ = path.GetPoint(waypointIndex);
                else if (fort != null && !fort.IsDead)
                    targetXZ = fort.transform.position;      // waypoints done → close in on the Fort
                else
                    targetXZ = pos + LaneEndDirection();     // no Fort → keep marching instead of freezing
            }

            var step = CurrentMoveSpeed * Time.deltaTime;

            var flatTarget = new Vector3(targetXZ.x, pos.y, targetXZ.z);
            var forwardDelta = Vector3.MoveTowards(pos, flatTarget, step) - pos;
            var separation = SeparationOffset() * step;

            // Keep only separation's SIDEWAYS component while advancing, so it can never cancel forward
            // progress — that tug-of-war is what left monsters standing at crowds / broken towers until
            // another one bumped them. When holding (no forward), separation applies fully to spread out.
            if (forwardDelta.sqrMagnitude > 0.0001f)
            {
                var forwardDir = forwardDelta.normalized;
                var opposing = Mathf.Min(0f, Vector3.Dot(separation, forwardDir));
                separation -= forwardDir * opposing;
            }

            var moved = pos + forwardDelta + separation;

            var heading = new Vector3(moved.x - pos.x, 0f, moved.z - pos.z);
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading);

            var groundY = GroundReferenceY(fort, holding);
            var desiredY = definition.movement == MonsterMovement.Flying ? groundY + definition.flightHeight : groundY;
            moved.y = Mathf.MoveTowards(pos.y, desiredY, step);

            transform.position = moved;
        }

        private float GroundReferenceY(FortCore fort, bool holding)
        {
            if (!holding && waypointIndex < path.Count) return path.GetPoint(waypointIndex).y;
            if (fort != null && !fort.IsDead) return fort.transform.position.y;
            if (path.Count > 0) return path.GetPoint(path.Count - 1).y;
            return baseGroundY;
        }

        // Where to keep walking once waypoints are exhausted and there's no Fort to head for — the final
        // lane direction (a point a few units ahead), so a monster marches on instead of freezing at the end.
        private Vector3 LaneEndDirection()
        {
            if (path != null && path.Count >= 2)
            {
                var dir = path.GetPoint(path.Count - 1) - path.GetPoint(path.Count - 2);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) return dir.normalized * 5f;
            }

            var forward = transform.forward;
            forward.y = 0f;
            return (forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward) * 5f;
        }

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

        // Scene-view range ring. Orange = invader range. Shown when selected, or always if alwaysShowRange.
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
