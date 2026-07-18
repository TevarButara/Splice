using System.Collections.Generic;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Splice.Characters
{
    // Animation states of a miner — replicated so every client plays the right clip (parallel to MonsterAnim).
    public enum MinerAnim
    {
        Idle,
        Walk,
        Farming,   // ขุด/เก็บเกี่ยว
        Death,
        Landing,   // เล่นตอน spawn
        Dance      // เผื่อไว้
    }

    // Economy unit (architecture 5.7): walks to the nearest gold node, mines a full load over time,
    // carries it back to the team's MinerBase, and only there deposits into GoldController — so the
    // round-trip distance directly gates income (far nodes = slower gold).
    // Server-authoritative like every other character; clients see movement via NetworkTransform.
    // Extends CharacterBase so it has health and can be killed — enabling economic warfare later.
    [RequireComponent(typeof(NavMeshAgent))]
    public class MinerCharacter : CharacterBase
    {
        private enum MinerState { SeekingNode, Mining, Returning }

        private static readonly List<MinerCharacter> active = new();
        public static IReadOnlyList<MinerCharacter> Active => active;

        [SerializeField] private MinerDefinitionSO definition;
        [FormerlySerializedAs("team")]
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [Tooltip("จำนวน miner สูงสุดต่อบ่อ ก่อนตัวถัดไปจะเด้งไปบ่อใกล้สุด 'ที่ยังมีที่ว่าง'. 1 = บ่อละ 1 ตัว (กระจายสุด). บ่อไกลจะถูกเลือกก็ต่อเมื่อบ่อใกล้กว่าหมด (depleted) เท่านั้น")]
        [SerializeField] private int minersPerNode = 1;
        [Tooltip("รัศมี snap ตำแหน่งบ่อลง NavMesh ก่อนเดินไป — pivot บ่อมักลอยเหนือพื้น. กว้างพอให้ครอบความสูง/ระยะห่างจากพื้นของบ่อ. snap ไม่ได้ในรัศมีนี้ = บ่ออยู่ไกล mesh เกิน จะถูกข้าม")]
        [SerializeField] private float nodeSnapRadius = 5f;

        [Header("Animation")]
        [Tooltip("Animator ของ miner — clip ชื่อตาม anim set. auto หา child ถ้าเว้น")]
        [SerializeField] private Animator animator;
        [Tooltip("ชื่อ state ใน Animator (กองกลาง — แก้ที่เดียว). เว้น = ใช้ชื่อ default (Idle/Walk/Farming/Death/Landing/Dance)")]
        [SerializeField] private MinerAnimSetSO animSet;

        private readonly NetworkVariable<MinerAnim> animState = new(
            MinerAnim.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool landingStarted;
        private float landingTimer;
        private bool dying;
        private float deathTimer;

        private NavMeshAgent agent;
        private MinerState state = MinerState.SeekingNode;
        private GoldNode targetNode;
        private bool baseRouteIssued;
        private int carrying;
        private float mineTimer;
        private float stuckTimer;
        private Vector3 nodeDestination;

        public RaidSide Side => side;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public void Initialize(MinerDefinitionSO minerDefinition, RaidSide owningSide)
        {
            definition = minerDefinition;
            side = owningSide;
            InitializeHealth(definition.maxHealth);
            ApplyDefinitionToAgent();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // ทุก instance (server + clients) เล่น animation ตามที่ server ตั้ง
            animState.OnValueChanged += HandleAnimChanged;
            PlayAnim(animState.Value);

            agent.enabled = IsServer;
            if (!IsServer) return;

            active.Add(this);
            if (definition != null && CurrentHealth <= 0) InitializeHealth(definition.maxHealth);
            ApplyDefinitionToAgent();
        }

        public override void OnNetworkDespawn()
        {
            animState.OnValueChanged -= HandleAnimChanged;
            active.Remove(this);
            base.OnNetworkDespawn();
        }

        // เก็บซากไว้ให้ Death clip เล่นก่อน — Update จัดการ despawn เอง (เหมือน MonsterCharacter)
        protected override void HandleDeath()
        {
        }

        private void ApplyDefinitionToAgent()
        {
            if (agent == null || !agent.enabled || definition == null) return;
            agent.speed = definition.moveSpeed;
            agent.stoppingDistance = definition.arrivalRadius;
            agent.autoBraking = true;
            // Brake hard enough to STOP at the target instead of overshooting and orbiting it — at high
            // speeds the default acceleration can't decelerate within the stopping distance, so miners
            // circled far nodes forever without ever arriving/mining.
            agent.acceleration = Mathf.Max(agent.acceleration, definition.moveSpeed * 8f);
        }

        private void Update()
        {
            if (!IsServer || definition == null) return;

            // Death sequence: หยุด agent, เล่นท่า Death, ค้าง deathAnimSeconds แล้ว despawn
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
                deathTimer = definition.deathAnimSeconds;
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                SetAnim(MinerAnim.Death);
                return;
            }

            // Spawn landing: เล่นท่า Landing ค้างที่จุดเกิด landingSeconds วิ ก่อนเริ่มขุด (agent ยังไม่มีปลายทาง = อยู่นิ่ง)
            if (!landingStarted)
            {
                landingStarted = true;
                landingTimer = Mathf.Max(0f, definition.landingSeconds);
            }
            if (landingTimer > 0f)
            {
                landingTimer -= Time.deltaTime;
                SetAnim(MinerAnim.Landing);
                return;
            }

            if (!EnsureOnNavMesh()) return;

            switch (state)
            {
                case MinerState.SeekingNode: TickSeeking(); break;
                case MinerState.Mining: TickMining(); break;
                case MinerState.Returning: TickReturning(); break;
            }

            UpdateAnim();
        }

        // ---------- Animation ----------

        // เลือกท่าตามสถานะงาน: ขุด = Farming / กำลังเดิน = Walk / อยู่นิ่ง = Idle
        private void UpdateAnim()
        {
            if (state == MinerState.Mining) { SetAnim(MinerAnim.Farming); return; }
            var moving = agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.02f;
            SetAnim(moving ? MinerAnim.Walk : MinerAnim.Idle);
        }

        private void SetAnim(MinerAnim value)
        {
            if (!IsServer || animState.Value == value) return;
            animState.Value = value;
            PlayAnim(value);               // server เล่นทันที, clients ตามผ่าน OnValueChanged
        }

        private void HandleAnimChanged(MinerAnim previous, MinerAnim current)
        {
            if (!IsServer) PlayAnim(current);
        }

        private void PlayAnim(MinerAnim value)
        {
            AnimatorUtil.SafeCrossFade(animator, StateName(value), 0.1f);
        }

        // ชื่อ state จากกองกลาง animSet (fallback ชื่อ default ถ้าไม่ assign / ช่องว่าง)
        private string StateName(MinerAnim value)
        {
            var fallback = value.ToString();   // Idle/Walk/Farming/Death/Landing/Dance ตรงชื่อ enum
            if (animSet == null) return fallback;
            var name = value switch
            {
                MinerAnim.Walk => animSet.walk,
                MinerAnim.Farming => animSet.farming,
                MinerAnim.Death => animSet.death,
                MinerAnim.Landing => animSet.landing,
                MinerAnim.Dance => animSet.dance,
                _ => animSet.idle
            };
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        // NavMeshAgent APIs (remainingDistance / SetDestination / velocity) throw if the agent isn't on the
        // NavMesh — which happens when a miner spawns or gets nudged slightly off it. Snap back onto the
        // nearest mesh point; if we're too far to recover this frame, skip the tick until we are.
        private bool EnsureOnNavMesh()
        {
            if (agent == null || !agent.enabled) return false;
            if (agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            return agent.isOnNavMesh;
        }

        private void TickSeeking()
        {
            if (targetNode == null || targetNode.IsDepleted)
            {
                targetNode = FindBestNode();
                if (targetNode == null) return; // nothing mineable right now — idle

                // Head for the node, snapped onto the NavMesh (pivots usually sit a bit off it). If the snap
                // fails we still SetDestination — the agent maps the target internally too.
                var dest = targetNode.transform.position;
                if (NavMesh.SamplePosition(dest, out var snapped, nodeSnapRadius, NavMesh.AllAreas))
                    dest = snapped.position;
                nodeDestination = dest;
                agent.SetDestination(dest);
                stuckTimer = 0f;
                return;
            }

            if (agent.pathPending) return;

            // Straight-line distance (XZ) to the exact point we're pathing to — immune to the agent
            // orbiting/overshooting the target, which made remainingDistance & velocity unreliable and
            // stranded miners circling far nodes.
            var flatDest = nodeDestination;
            flatDest.y = transform.position.y;
            var atDestination = Vector3.Distance(transform.position, flatDest) <= agent.stoppingDistance + 0.2f;

            // Also arrive if touching the node, if the agent reached its path end (it may stop short — a node
            // that carves the NavMesh keeps the miner an agent-radius away), or if it simply stopped moving.
            var touchingNode = targetNode.DistanceToSurface(transform.position) <= definition.arrivalRadius;
            var reachedPathEnd = agent.remainingDistance <= agent.stoppingDistance;

            if (agent.velocity.sqrMagnitude < 0.02f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;

            if (!atDestination && !touchingNode && !reachedPathEnd && stuckTimer < 1.5f) return;

            stuckTimer = 0f;
            mineTimer = 0f;
            state = MinerState.Mining;
        }

        private void TickMining()
        {
            if (targetNode == null || targetNode.IsDepleted)
            {
                state = MinerState.SeekingNode;
                return;
            }

            mineTimer += Time.deltaTime;
            if (mineTimer < definition.mineDurationSeconds) return;

            carrying = targetNode.Mine(definition.carryCapacity);
            if (carrying <= 0)
            {
                state = MinerState.SeekingNode;
                return;
            }

            state = MinerState.Returning;
            baseRouteIssued = false;
        }

        private void TickReturning()
        {
            var home = MinerBase.For(side);
            if (home == null) return; // base not wired — hold cargo until one exists

            if (!baseRouteIssued)
            {
                agent.SetDestination(home.transform.position);
                baseRouteIssued = true;
            }

            // Deposit as soon as we're within the base's deposit radius (XZ only, so a base placed above the
            // floor still works). Miners stop at the edge of the ring instead of all cramming onto the exact
            // base point, which is what made drop-offs look like a scramble.
            var toHome = transform.position - home.transform.position;
            toHome.y = 0f;
            if (toHome.magnitude > home.DepositRadius) return;

            var bank = GoldController.For(side);
            if (bank != null) bank.Add(carrying);
            carrying = 0;
            targetNode = null;
            state = MinerState.SeekingNode;
        }

        // Hard rule (matches the design):
        //   1. Go to the NEAREST non-depleted node this team may mine that still has an open slot
        //      (< minersPerNode) — so a batch of miners fills nearby nodes one slot each, not one pile.
        //   2. A farther node is only ever chosen once the nearer ones are DEPLETED (skipped here).
        // Fallback: if every non-depleted node is full, double up on the nearest — never run far while a
        // nearer node still has gold.
        private GoldNode FindBestNode()
        {
            GoldNode nearestWithRoom = null;
            var nearestWithRoomDist = float.MaxValue;
            GoldNode nearestAny = null;
            var nearestAnyDist = float.MaxValue;

            var nodes = GoldNode.Active;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.IsDepleted || !node.CanBeMinedBy(side)) continue;

                var distance = Vector3.Distance(transform.position, node.transform.position);

                if (distance < nearestAnyDist)
                {
                    nearestAny = node;
                    nearestAnyDist = distance;
                }

                if (distance < nearestWithRoomDist && CountMinersTargeting(node) < minersPerNode)
                {
                    nearestWithRoom = node;
                    nearestWithRoomDist = distance;
                }
            }

            return nearestWithRoom != null ? nearestWithRoom : nearestAny;
        }

        // Miners of the SAME team actively heading to / mining this node. Same-team only, so the spread cap
        // is per team — a Neutral node can still draw both sides (that's the contest). Returning miners are
        // excluded: they're already leaving with their load, so the slot is free for someone else.
        private int CountMinersTargeting(GoldNode node)
        {
            var count = 0;
            for (var i = 0; i < active.Count; i++)
            {
                var other = active[i];
                if (other == this || other.IsDead || other.side != side || other.state == MinerState.Returning) continue;
                if (other.targetNode == node) count++;
            }
            return count;
        }
    }
}
