using System.Collections.Generic;
using Splice.Combat;
using Splice.Core;
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
        Lose,
        Landing,   // เล่นตอน spawn (ก่อนเริ่มเดิน)
        Dance,     // เผื่อไว้ (ยังไม่ผูก trigger — เรียก SetAnim(Dance) ได้ในอนาคต)
        Spell      // supporter cast (มานาเต็ม) — ตัวไม่มี clip ก็ข้าม (SafeCrossFade)
    }

    // Invader unit (architecture 5.6/5.8). Walks a fixed, map-authored LanePath toward the fort. When a
    // defender (tower/Fort) is in range and the cooldown is ready it STOPS, turns to face the target, plays
    // its Attack clip, lands the hit, then resumes walking. Server drives movement + combat; clients see
    // position via NetworkTransform and the animation via the replicated animState.
    public class MonsterCharacter : CharacterBase
    {
        private static readonly List<MonsterCharacter> active = new();
        public static IReadOnlyList<MonsterCharacter> Active => active;

        public RaidSide Side => side;
        private bool IsGarrison => side == RaidSide.Defender;

        [SerializeField] private MonsterDefinitionSO definition;
        [Tooltip("บทบาทใน raid: Attacker = ยกทัพบุก (เดินเลน) / Defender = garrison เฝ้าเมือง (ยืนกับที่ ตื่นสู้เมื่อ Attacker เข้าระยะ)")]
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [Tooltip("ติ๊กเพื่อโชว์วงระยะโจมตีตลอดเวลา (ไม่ต้องเลือกก่อน) — ช่วยกะระยะ. เป็น Gizmo (Scene view เสมอ, Game view ต้องเปิดปุ่ม Gizmos)")]
        [SerializeField] private bool alwaysShowRange;

        [Header("Animation")]
        [Tooltip("Animator ของตัวละคร — ปิด Apply Root Motion (การเคลื่อนที่ขับด้วยโค้ด). ค่า tuning อื่น (ความเร็วหมุน/เวลา clip/separation) ย้ายไป MonsterDefinitionSO แล้ว")]
        [SerializeField] private Animator animator;
        [Tooltip("ชื่อ state ใน Animator ต่อสถานะ (กองกลาง — แก้ที่เดียว ไม่ hardcode). เว้น = ใช้ชื่อ default: Idle/Walk/Attack/Injured/Death/Victory/Lose")]
        [SerializeField] private MonsterAnimSetSO animSet;
        [Tooltip("จุดยิงกระสุน (ปลายปาก/มือ) — เฉพาะมอนยิงไกลที่ใส่ projectile ใน SO. เว้น = ใช้จุดกลางตัว")]
        [SerializeField] private Transform muzzle;

        private readonly NetworkVariable<MonsterAnim> animState = new(
            MonsterAnim.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Supporter mana (0..100). Charged by attacking; full → cast spell → reset. Replicated for the mana bar.
        private const float ManaMax = 100f;
        private readonly NetworkVariable<float> mana = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Timed buff (server-only) from a supporter's Buff spell — refresh + strongest (single slot, take max).
        private float buffAttackMult = 1f;
        private float buffMoveMult = 1f;
        private float buffAtkSpeedMult = 1f;
        private float buffExpiry;

        public bool IsSupporter => definition != null && definition.role == MonsterRole.Supporter && definition.spell != null;
        public float Mana => mana.Value;
        public float ManaMaxValue => ManaMax;

        // Effective combat stats = base × active buffs (attack speed shortens cooldown).
        private int EffectiveAttackDamage => definition == null ? 0 : Mathf.RoundToInt(definition.attackDamage * buffAttackMult);
        private float EffectiveAttackCooldown => definition == null ? 1f : definition.attackCooldown / Mathf.Max(0.01f, buffAtkSpeedMult);

        private LanePath path;
        private int waypointIndex;
        private float baseGroundY;   // fallback ground level (path start) — used only if no map point is available
        private float attackTimer;

        private bool attacking;
        private float attackPhaseTimer;
        private CharacterBase attackTarget;
        private Camera faceCamera;

        // Roadside aggro: Attacker แวะตี "เฉพาะป้อมที่ยิงมัน" (ไม่ใช่ทุกป้อมในระยะ) เป็นเวลา roadsideEngageSeconds แล้วเดินต่อ
        private TowerCharacter aggroTower;
        private float engageStartTime;
        private readonly HashSet<TowerCharacter> engagedTowers = new();  // ป้อมที่เคยแวะตีแล้ว — ไม่ aggro ซ้ำ (ไม่วิ่งย้อนกลับ)

        private bool dying;
        private float deathTimer;

        private bool spawnInitialized;   // init ครั้งเดียวตอน definition พร้อม (landing + ค่าสุ่มประจำตัว)
        private float landingTimer;
        private float laneOffset;        // เยื้องจากเส้นกลางเลน (สุ่มประจำตัว) → เดินเป็นแถบ ไม่ทับเส้นเดียว
        private float speedMult = 1f;    // สุ่มความเร็วประจำตัว → ไม่เดินพร้อมเพรียง
        private bool casting;          // supporter กำลังค้างท่า Spell (หลัง cast ตอนมานาเต็ม)
        private float castTimer;

        // Ranged: มอนยิงไกล → ดาเมจลงตอนกระสุนถึงเป้า (impact-time) เหมือนป้อม
        private struct PendingHit { public CharacterBase target; public float timeLeft; public int damage; }
        private readonly List<PendingHit> pendingHits = new();

        private static readonly List<ulong> castFxTargets = new();   // reuse — เป้าที่โดนสเปล ส่งให้ client เล่น FX

        private bool HasProjectile => definition != null && definition.projectile != null && definition.projectile.ProjectilePrefab != null;
        private Vector3 MuzzlePos => muzzle != null ? muzzle.position : transform.position;

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

        // Garrison (ฝ่ายตั้งรับ) — ยืนเฝ้าที่ homePosition ไม่เดินเลน, ตื่นสู้เมื่อ Attacker เข้าระยะ (architecture 5.10)
        public void InitializeGarrison(MonsterDefinitionSO monsterDefinition, RaidSide owningSide, Vector3 homePosition)
        {
            definition = monsterDefinition;
            side = owningSide;
            InitializeHealth(definition.maxHealth);
            path = null;
            transform.position = homePosition;
            baseGroundY = homePosition.y;
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

            // Timed buff / shield expiry + กระสุนที่ยิงไปแล้ว (ranged) ลงดาเมจตอนถึงเป้า — run ทุกเฟรมทุกสถานะ
            TickBuff();
            TickShield();
            TickPendingHits(Time.deltaTime);

            // init ครั้งแรกที่ definition พร้อม (ครอบทั้ง spawn runtime + scene-placed): landing + ค่าสุ่มประจำตัว
            if (!spawnInitialized)
            {
                spawnInitialized = true;
                landingTimer = Mathf.Max(0f, definition.landingSeconds);
                laneOffset = Random.Range(-definition.laneOffsetRange, definition.laneOffsetRange);
                speedMult = 1f + Random.Range(-definition.speedVariancePercent, definition.speedVariancePercent);
            }

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
                deathTimer = definition.deathAnimSeconds;
                SetAnim(MonsterAnim.Death);
                return;
            }

            // Game over → victory (invaders won) / lose pose, then freeze in place.
            if (IsMatchOver(out var invadersWon))
            {
                // มุมมองของตัวเอง: Attacker ชนะเมื่อ invaders ชนะ / Defender (garrison) ชนะเมื่อ invaders แพ้
                var thisSideWon = invadersWon == (side == RaidSide.Attacker);
                SetAnim(thisSideWon ? MonsterAnim.Victory : MonsterAnim.Lose);
                return;
            }

            // Spawn landing: เล่นท่า Landing ค้างที่จุดเกิด landingSeconds วิ แล้วค่อยเริ่มเดิน/ทำงาน
            if (landingTimer > 0f)
            {
                landingTimer -= Time.deltaTime;
                SetAnim(MonsterAnim.Landing);
                return;
            }

            // Supporter cast: มานาเต็มแล้ว cast → ค้างท่า Spell spellCastSeconds วิ แล้วค่อยทำงานต่อ
            if (casting)
            {
                castTimer -= Time.deltaTime;
                SetAnim(MonsterAnim.Spell);
                if (castTimer <= 0f) casting = false;
                return;
            }

            // Mid-attack: stop moving, face the target, swing; damage lands at the end, then resume.
            if (attacking)
            {
                TickAttack();
                return;
            }

            // Garrison: ยืนเฝ้าเมือง — สแกนหา Attacker ในระยะแล้วตี (ไม่เดินเลน)
            if (IsGarrison)
            {
                TickGarrison();
                return;
            }

            attackTimer += Time.deltaTime;

            // ป้อมข้างทางที่ "ยิงเรา" (aggro) → แวะวิ่งไปตีตามระยะตัวเอง ตีครบ roadsideEngageSeconds แล้วเดินต่อ.
            // Fort ในระยะมาก่อนเสมอ = objective → ทิ้ง aggro ป้อมข้างทาง
            UpdateAggro();
            if (aggroTower != null && !IsFortInRange())
            {
                EngageTarget(aggroTower);
                return;
            }

            // เป้า objective/blocker ในระยะ = Fort + garrison ศัตรู (ไม่รวมป้อมข้างทาง — ป้อมข้างทางใช้ aggro).
            // ปักหลักตี ไม่ดันเข้า core + กระจาย separation กันกองทับ (architecture 5.8)
            var target = FindEngageTarget(definition.attackRange);
            if (target != null)
            {
                HoldAndSpread();
                FaceTarget(target.transform.position);
                if (attackTimer >= EffectiveAttackCooldown) { StartAttack(target); return; }
                SetAnim(MonsterAnim.Idle);
                return;
            }

            // ไม่มีเป้าในระยะ → เดินเลนเข้าหา Fort ตามปกติ
            var before = transform.position;
            Advance();
            var moved = (transform.position - before).sqrMagnitude > 0.000001f;
            SetAnim(moved ? (IsInjured() ? MonsterAnim.InjuredWalk : MonsterAnim.Walk) : MonsterAnim.Idle);
        }

        private bool IsInjured() => MaxHealth > 0 && definition != null && (float)CurrentHealth / MaxHealth <= definition.injuredHealthFraction;

        // Walk slower once injured (if set); × active move-speed buff × ค่าสุ่มความเร็วประจำตัว
        private float CurrentMoveSpeed =>
            (IsInjured() && definition.injuredMoveSpeed > 0f ? definition.injuredMoveSpeed : definition.moveSpeed)
            * buffMoveMult * speedMult;

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

        private void StartAttack(CharacterBase target)
        {
            attacking = true;
            attackPhaseTimer = 0f;
            attackTarget = target;
            SetAnim(MonsterAnim.Attack);
        }

        // Garrison tick: ยืนกับที่, ตี Attacker ที่ใกล้สุดในระยะเมื่อ cooldown พร้อม, ไม่งั้นยืน Idle หันหน้าเข้ากล้อง
        private void TickGarrison()
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= EffectiveAttackCooldown)
            {
                var target = FindAttackTarget(definition.attackRange);
                if (target != null)
                {
                    StartAttack(target);
                    return;
                }
            }
            FaceCamera();
            SetAnim(MonsterAnim.Idle);
        }

        // garrison ยืนเฉยๆ ให้หันหน้าเข้ากล้อง (Y-only) — raid = local host จึงใช้ Camera.main ฝั่ง server ได้.
        // มองดิ่ง → หันเข้าหาผู้เล่น (ล่างจอ). (rotation replicate ผ่าน NetworkTransform)
        private void FaceCamera()
        {
            if (faceCamera == null) faceCamera = Camera.main;
            if (faceCamera == null) return;

            var dir = faceCamera.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { dir = -faceCamera.transform.up; dir.y = 0f; }
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ---------- Roadside aggro (แวะตีเฉพาะป้อมที่ยิงเรา) ----------

        // ถูกป้อมยิง → aggro แวะตีป้อมนั้น (เฉพาะ Attacker, ไม่ใช่ Fort, ไม่อยู่ช่วง immune, ไม่ได้ engage อยู่แล้ว)
        protected override void OnDamagedBy(CharacterBase source)
        {
            if (side != RaidSide.Attacker) return;
            if (aggroTower != null) return;                            // กำลังแวะตีป้อมอื่นอยู่ ไม่เปลี่ยนเป้า
            if (definition == null || definition.roadsideEngageSeconds <= 0f) return;
            // แวะตีเฉพาะป้อม (ไม่ใช่ Fort) ที่ "ยังไม่เคยตี" — กันวิ่งย้อนกลับไปตีป้อมเดิมที่ผ่านมาแล้ว
            if (source is TowerCharacter tower && !(tower is FortCore) && !engagedTowers.Contains(tower))
            {
                aggroTower = tower;
                engageStartTime = -1f;   // ยังไม่เริ่มนับ — เริ่มนับตอนตีครั้งแรก (ไม่รวมเวลาเดินไปหาป้อม)
            }
        }

        // หมดเวลา engage / ป้อมตาย → จำว่าเคยตีแล้ว, เลิก aggro, แล้วมุ่งไป waypoint ใกล้สุดข้างหน้า (ไม่ย้อนจุดเก่า)
        private void UpdateAggro()
        {
            if (aggroTower == null) return;
            // เลิกได้เมื่อป้อมตาย หรือ (เริ่มตีแล้ว และครบเวลา) — engageStartTime < 0 = ยังเดินไปหาป้อม ไม่นับ
            if (aggroTower.IsDead ||
                (engageStartTime >= 0f && Time.time - engageStartTime >= definition.roadsideEngageSeconds))
            {
                engagedTowers.Add(aggroTower);
                aggroTower = null;
                SnapToNearestForwardWaypoint();
            }
        }

        // ตั้ง waypoint เป้าหมายเป็นจุดใกล้สุดในบรรดาจุดที่ยังไม่ผ่าน (index ≥ ปัจจุบัน) — ไม่เดินย้อนไปเก็บจุดเก่าข้างหลัง
        private void SnapToNearestForwardWaypoint()
        {
            if (path == null || path.Count == 0) return;
            var pos = transform.position;
            var best = waypointIndex;
            var bestDist = float.MaxValue;
            for (var i = waypointIndex; i < path.Count; i++)
            {
                var d = HorizontalDistance(pos, WaypointAt(i));
                if (d < bestDist) { bestDist = d; best = i; }
            }
            waypointIndex = best;
        }

        // จุดทางเดินที่ "เยื้อง" ตาม laneOffset ประจำตัว (ตั้งฉากกับทิศเส้นทางช่วงนั้น) → มอนเดินเป็นแถบขนานเส้นกลาง
        // ใช้ทั้งตอนเช็ค 'ถึงจุด' และตอนตั้งเป้า ไม่งั้นตัวที่เยื้างจะเข้าไม่ถึง arriveRadius ของจุดกลางแล้วค้าง
        private Vector3 WaypointAt(int index)
        {
            var p = path.GetPoint(index);
            if (laneOffset == 0f || path.Count < 2) return p;

            var i = Mathf.Clamp(index, 0, path.Count - 2);
            var dir = path.GetPoint(i + 1) - path.GetPoint(i);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return p;

            return p + Vector3.Cross(dir.normalized, Vector3.up) * laneOffset;
        }

        // ---------- Ranged (projectile) — เหมือนป้อม: กระสุน cosmetic + ดาเมจลงตอนถึงเป้า ----------

        // server: ปล่อยกระสุน → ตั้งเวลาลงดาเมจตอนถึงเป้า + สั่งทุก client spawn ภาพกระสุน
        private void FireProjectile(CharacterBase target, int damage)
        {
            var dest = target.transform.position;
            pendingHits.Add(new PendingHit { target = target, timeLeft = TravelTimeTo(dest), damage = damage });
            FireProjectileClientRpc(NetId(target), dest);
        }

        [ClientRpc]
        private void FireProjectileClientRpc(ulong targetId, Vector3 fallback)
        {
            var tf = Resolve(targetId);
            var dest = tf != null ? tf.position : fallback;
            ProjectileVisual.Spawn(definition.projectile, MuzzlePos, tf, fallback, TravelTimeTo(dest));
        }

        // เวลาเดินทางถึงเป้า — server+client คำนวณเหมือนกัน (distance/avgSpeed, floor ด้วย minFlight) → ดาเมจ/ภาพตรงกัน
        private float TravelTimeTo(Vector3 dest)
        {
            var p = definition.projectile;
            if (p == null) return 0f;
            return Mathf.Max(p.MinFlightSeconds, Vector3.Distance(MuzzlePos, dest) / p.AverageSpeed);
        }

        private void TickPendingHits(float dt)
        {
            for (var i = pendingHits.Count - 1; i >= 0; i--)
            {
                var hit = pendingHits[i];
                hit.timeLeft -= dt;
                if (hit.timeLeft > 0f) { pendingHits[i] = hit; continue; }
                if (hit.target != null && !hit.target.IsDead) hit.target.ApplyDamage(hit.damage);   // เป้าตายก่อน = ยิงพลาด
                pendingHits.RemoveAt(i);
            }
        }

        private static ulong NetId(CharacterBase c) =>
            c != null && c.TryGetComponent<NetworkObject>(out var no) ? no.NetworkObjectId : 0;

        private static Transform Resolve(ulong netId)
        {
            if (netId == 0 || NetworkManager.Singleton == null) return null;
            var sm = NetworkManager.Singleton.SpawnManager;
            return sm != null && sm.SpawnedObjects.TryGetValue(netId, out var no) ? no.transform : null;
        }

        private bool IsFortInRange()
        {
            var fort = FortCore.Instance;
            return fort != null && !fort.IsDead &&
                   Vector3.Distance(transform.position, fort.transform.position) <= definition.attackRange;
        }

        // แวะตีป้อมที่ aggro: ยังไกล → วิ่งเข้าไป, เข้าระยะ (attackRange) แล้ว → ปักหลักตี
        private void EngageTarget(CharacterBase target)
        {
            if (Vector3.Distance(transform.position, target.transform.position) <= definition.attackRange)
            {
                if (engageStartTime < 0f) engageStartTime = Time.time;   // ถึงระยะ = เริ่มตีครั้งแรก → เริ่มนับเวลาตรงนี้
                HoldAndSpread();
                FaceTarget(target.transform.position);
                if (attackTimer >= EffectiveAttackCooldown) { StartAttack(target); return; }
                SetAnim(MonsterAnim.Idle);
            }
            else
            {
                MoveTowardPoint(target.transform.position);
                SetAnim(IsInjured() ? MonsterAnim.InjuredWalk : MonsterAnim.Walk);
            }
        }

        // เดินเข้าหาจุดตายตัว (ใช้ตอนวิ่งไปหาป้อมที่ aggro) — มี separation + Y ตามพื้น/บิน เหมือน Advance
        private void MoveTowardPoint(Vector3 targetPos)
        {
            var pos = transform.position;
            var step = CurrentMoveSpeed * Time.deltaTime;

            var flat = new Vector3(targetPos.x, pos.y, targetPos.z);
            var forwardDelta = Vector3.MoveTowards(pos, flat, step) - pos;
            var separation = SeparationOffset() * step;
            if (forwardDelta.sqrMagnitude > 0.0001f)
            {
                var fwd = forwardDelta.normalized;
                var opposing = Mathf.Min(0f, Vector3.Dot(separation, fwd));
                separation -= fwd * opposing;
            }

            var moved = pos + forwardDelta + separation;
            var heading = new Vector3(moved.x - pos.x, 0f, moved.z - pos.z);
            if (heading.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(heading);

            var groundY = targetPos.y;
            var desiredY = definition.movement == MonsterMovement.Flying ? groundY + definition.flightHeight : groundY;
            moved.y = Mathf.MoveTowards(pos.y, desiredY, step);

            transform.position = moved;
        }

        // เป้าที่ปักหลักตีตอนเดิน (Attacker): Fort (objective) + garrison ศัตรูที่ใกล้สุดในระยะ — ไม่รวมป้อมข้างทาง
        private CharacterBase FindEngageTarget(float range)
        {
            CharacterBase nearest = null;
            var nearestDist = range;
            var pos = transform.position;

            var fort = FortCore.Instance;
            if (fort != null) Consider(fort, pos, ref nearest, ref nearestDist);

            for (var i = 0; i < active.Count; i++)
            {
                var other = active[i];
                if (other == this || other.side == side) continue;
                Consider(other, pos, ref nearest, ref nearestDist);
            }
            return nearest;
        }

        // เป้าที่ตีได้ = ศัตรูที่ใกล้สุดในระยะ. Attacker → ป้อม/Fort (ฝ่าย Defender) + มอน garrison ฝ่ายตรงข้าม;
        // Defender (garrison) → มอน Attacker เท่านั้น (ป้อมทั้งหมดถือเป็นฝ่าย Defender จึงไม่ตีพวกเดียวกัน)
        private CharacterBase FindAttackTarget(float range)
        {
            CharacterBase nearest = null;
            var nearestDist = range;
            var pos = transform.position;

            if (side == RaidSide.Attacker)
            {
                var towers = TowerCharacter.Active;
                for (var i = 0; i < towers.Count; i++)
                    Consider(towers[i], pos, ref nearest, ref nearestDist);
            }

            for (var i = 0; i < active.Count; i++)
            {
                var other = active[i];
                if (other == this || other.side == side) continue;
                Consider(other, pos, ref nearest, ref nearestDist);
            }

            return nearest;
        }

        private static void Consider(CharacterBase candidate, Vector3 pos, ref CharacterBase nearest, ref float nearestDist)
        {
            if (candidate == null || candidate.IsDead) return;
            var distance = Vector3.Distance(pos, candidate.transform.position);
            if (distance > nearestDist) return;
            nearest = candidate;
            nearestDist = distance;
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
            if (attackPhaseTimer < definition.attackDurationSeconds) return;

            // Swing lands: ยิงไกล = ปล่อยกระสุน (ดาเมจลงตอนถึงเป้า) / ประชิด = ลงดาเมจทันที
            if (HasProjectile) FireProjectile(attackTarget, EffectiveAttackDamage);
            else attackTarget.ApplyDamage(EffectiveAttackDamage);
            if (IsSupporter) ChargeMana();   // supporter ชาร์จมานาทุกครั้งที่ตี — เต็มแล้ว cast
            attacking = false;
            attackTimer = 0f;
        }

        // ---------- Supporter: mana + spell ----------

        private void ChargeMana()
        {
            mana.Value = Mathf.Min(ManaMax, mana.Value + definition.spell.manaPerAttack);
            if (mana.Value >= ManaMax)
            {
                CastSpell();
                mana.Value = 0f;   // มานาหมด → ชาร์จใหม่จากการตี
                casting = true;    // เล่นท่า Spell ค้าง spellCastSeconds วิ
                castTimer = Mathf.Max(0f, definition.spellCastSeconds);
            }
        }

        // ลง spell ให้พวกฝ่ายเดียวกันในรัศมี (Heal/Shield/Buff × Single/Area). server-only.
        private void CastSpell()
        {
            var spell = definition.spell;
            if (spell == null) return;
            var pos = transform.position;

            // เก็บ id ของเป้าที่โดนสเปล → ส่งไปให้ทุก client เล่น FX (ดาเมจ/ผลจริงคิดที่ server เท่านั้น)
            castFxTargets.Clear();

            if (spell.targeting == SpellTargeting.SingleAlly)
            {
                var target = PickSingleAlly(spell, pos);
                if (target != null) { ApplySpell(spell, target); castFxTargets.Add(NetId(target)); }
            }
            else
            {
                for (var i = 0; i < active.Count; i++)
                {
                    var ally = active[i];
                    if (ally.IsDead || ally.side != side) continue;
                    if (Vector3.Distance(pos, ally.transform.position) > spell.radius) continue;
                    ApplySpell(spell, ally);
                    castFxTargets.Add(NetId(ally));
                }
            }

            CastFxClientRpc(castFxTargets.ToArray());
        }

        // FX ตอนร่ายเวท: ที่ตัวผู้ร่าย + ที่เป้าทุกตัวที่โดนสเปล (cosmetic ล้วน)
        [ClientRpc]
        private void CastFxClientRpc(ulong[] targetIds)
        {
            var spell = definition != null ? definition.spell : null;
            if (spell == null) return;

            if (spell.attachEffectToTarget) OneShotEffect.SpawnAttached(spell.castEffect, transform);
            else OneShotEffect.Spawn(spell.castEffect, transform.position, transform.rotation);

            if (spell.targetEffect == null) return;
            foreach (var id in targetIds)
            {
                var tf = Resolve(id);
                if (tf == null) continue;
                if (spell.attachEffectToTarget) OneShotEffect.SpawnAttached(spell.targetEffect, tf);
                else OneShotEffect.Spawn(spell.targetEffect, tf.position, tf.rotation);
            }
        }

        // เป้าเดี่ยว: Heal = ตัวเลือดน้อยสุดที่บาดเจ็บ / อื่นๆ = ตัวที่ใกล้สุด (รวมตัวเองได้)
        private MonsterCharacter PickSingleAlly(SpellDefinitionSO spell, Vector3 pos)
        {
            MonsterCharacter best = null;
            var bestScore = float.MaxValue;
            for (var i = 0; i < active.Count; i++)
            {
                var ally = active[i];
                if (ally.IsDead || ally.side != side) continue;
                if (Vector3.Distance(pos, ally.transform.position) > spell.radius) continue;

                float score;
                if (spell.effect == SpellEffect.Heal)
                {
                    if (ally.MaxHealth <= 0 || ally.CurrentHealth >= ally.MaxHealth) continue; // เต็มเลือดไม่ต้อง heal
                    score = (float)ally.CurrentHealth / ally.MaxHealth;    // เลือดน้อยสุด = score ต่ำสุด
                }
                else
                {
                    score = Vector3.Distance(pos, ally.transform.position); // ใกล้สุด
                }
                if (score < bestScore) { bestScore = score; best = ally; }
            }
            return best;
        }

        private static void ApplySpell(SpellDefinitionSO spell, MonsterCharacter ally)
        {
            switch (spell.effect)
            {
                case SpellEffect.Heal:
                    ally.Heal(spell.healAmount);
                    break;
                case SpellEffect.Shield:
                    ally.ApplyShield(spell.shieldAmount, spell.shieldDuration);
                    break;
                case SpellEffect.Buff:
                    ally.ApplyBuff(spell.attackMultiplier, spell.moveSpeedMultiplier, spell.attackSpeedMultiplier, spell.buffDuration);
                    break;
            }
        }

        // buff แบบ refresh + เอาแรงสุด (single slot). server-only.
        public void ApplyBuff(float atkMult, float moveMult, float atkSpdMult, float duration)
        {
            if (!IsServer) return;
            if (Time.time >= buffExpiry) { buffAttackMult = 1f; buffMoveMult = 1f; buffAtkSpeedMult = 1f; } // buff เก่าหมดแล้วเริ่มใหม่
            buffAttackMult = Mathf.Max(buffAttackMult, Mathf.Max(1f, atkMult));
            buffMoveMult = Mathf.Max(buffMoveMult, Mathf.Max(1f, moveMult));
            buffAtkSpeedMult = Mathf.Max(buffAtkSpeedMult, Mathf.Max(1f, atkSpdMult));
            buffExpiry = Time.time + duration;
        }

        private void TickBuff()
        {
            if (buffExpiry > 0f && Time.time >= buffExpiry)
            {
                buffAttackMult = 1f; buffMoveMult = 1f; buffAtkSpeedMult = 1f;
                buffExpiry = 0f;
            }
        }

        private void FaceTarget(Vector3 targetPos)
        {
            var dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, definition.turnSpeedDegPerSec * Time.deltaTime);
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
            AnimatorUtil.SafeCrossFade(animator, StateName(value), 0.1f);
        }

        // ชื่อ state ที่ส่งให้ Animator — อ่านจาก animSet (กองกลาง) ถ้ามี, ไม่งั้น fallback ชื่อ default.
        // ช่องใน SO ที่เว้นว่างก็ fallback ต่อ (กันสะกดผิด/ลืมกรอกแล้วท่าหาย)
        private string StateName(MonsterAnim value)
        {
            var fallback = value == MonsterAnim.InjuredWalk ? "Injured" : value.ToString();
            if (animSet == null) return fallback;
            var name = value switch
            {
                MonsterAnim.Walk => animSet.walk,
                MonsterAnim.Attack => animSet.attack,
                MonsterAnim.InjuredWalk => animSet.injured,
                MonsterAnim.Death => animSet.death,
                MonsterAnim.Victory => animSet.victory,
                MonsterAnim.Lose => animSet.lose,
                MonsterAnim.Landing => animSet.landing,
                MonsterAnim.Dance => animSet.dance,
                MonsterAnim.Spell => animSet.spell,
                _ => animSet.idle
            };
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

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
                          HorizontalDistance(pos, fort.transform.position) <= definition.fortHoldDistance;

            Vector3 targetXZ;
            if (holding)
            {
                targetXZ = pos; // reached the Fort — hold, don't push into the core
            }
            else
            {
                while (waypointIndex < path.Count && HorizontalDistance(pos, WaypointAt(waypointIndex)) <= definition.waypointArriveRadius)
                {
                    waypointIndex++;
                }

                if (waypointIndex < path.Count)
                    targetXZ = WaypointAt(waypointIndex);
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

        // ปักหลักตอนอยู่ในระยะตี: ไม่ดันเข้า core, ดันออกด้านข้าง (separation) ให้กระจายเป็นวง แล้ว "นิ่ง"
        // เมื่อ push ต่ำกว่า deadzone → หยุดสนิท (จบอาการเบียด/สั่น/กระพริบตรง Fort). ตัวที่นิ่งกลายเป็นสิ่งกีดขวาง
        // ให้ตัวมาใหม่หลบอ้อมเอง → เกิดวงแหวนที่เสถียร
        private void HoldAndSpread()
        {
            if (definition == null || definition.separationRadius <= 0f) return;
            var step = CurrentMoveSpeed * Time.deltaTime;
            var separation = SeparationOffset() * step;              // XZ push ออกจากมอนข้างเคียง (y=0 อยู่แล้ว)
            if (separation.magnitude < definition.settleDeadzone) return;   // กระจายพอแล้ว → freeze
            var pos = transform.position;
            transform.position = new Vector3(pos.x + separation.x, pos.y, pos.z + separation.z);
        }

        private Vector3 SeparationOffset()
        {
            if (definition == null || definition.separationRadius <= 0f) return Vector3.zero;

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
                if (distance < 0.0001f || distance > definition.separationRadius) continue;

                push -= offset / distance * (1f - distance / definition.separationRadius);
            }

            return Vector3.ClampMagnitude(push, 1f) * definition.separationStrength;
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
            if (ShowSpellAreaAlways) DrawSpellAreaGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!alwaysShowRange) DrawRangeGizmo();
            if (!ShowSpellAreaAlways) DrawSpellAreaGizmo();
        }

        private void DrawRangeGizmo()
        {
            if (definition != null) RangeGizmo.DrawFlatCircle(transform.position, definition.attackRange, new Color(1f, 0.5f, 0f));
        }

        // ติ๊ก showAreaGizmo ใน SpellDefinitionSO = โชว์ตลอด / ไม่ติ๊ก = โชว์ตอนเลือกตัวมอน
        private bool ShowSpellAreaAlways =>
            definition != null && definition.spell != null && definition.spell.showAreaGizmo;

        // วง area ของสเปล supporter (ม่วง) — เฉพาะ targeting = AreaAllies (เป้าเดี่ยวไม่มีวง)
        private void DrawSpellAreaGizmo()
        {
            var s = definition != null ? definition.spell : null;
            if (s == null || s.targeting != SpellTargeting.AreaAllies) return;
            RangeGizmo.DrawFlatCircle(transform.position, s.radius, new Color(0.65f, 0.25f, 0.95f));
        }
    }
}
