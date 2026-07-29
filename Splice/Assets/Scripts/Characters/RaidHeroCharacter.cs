using Splice.Base;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Characters
{
    public enum HeroControlMode
    {
        Auto,
        Manual
    }

    public enum HeroLifeState
    {
        Active,
        Downed,
        Defeated
    }

    public enum HeroTargetPreference
    {
        Default,
        Monster,
        Tower
    }

    public enum HeroInteractionKind
    {
        None,
        Loot,
        Extraction
    }

    public enum HeroFeedback
    {
        None,
        LootCollected,
        ExtractionCompleted,
        ExtractionRejected,
        NothingNearby,
        InteractionUnavailable,
        Revived,
        ReviveRejected,
        AbilityCast,
        AbilityCooldown,
        AbilityNoMana,
        AbilityOutOfRange,
        AbilityNoTargets,
        AbilityUnavailable,
        AbilityHealed,
        AbilityBlinked,
        NormalAttackHit,
        NormalAttackNoTarget,
        NormalAttackCooldown,
        FocusTargetSet,
        FocusTargetCleared,
        FocusTargetRejected,
        FocusTargetCompleted
    }

    // The player's field avatar. Movement, combat, interaction and life state are decided by the server;
    // the owning client sends only movement/control/interact intents.
    [RequireComponent(typeof(NetworkObject))]
    public class RaidHeroCharacter : CharacterBase
    {
        private enum HeroAnimationState
        {
            Idle,
            Walk,
            Death
        }

        public static RaidHeroCharacter Instance { get; private set; }
        private static readonly List<RaidHeroCharacter> instances = new();
        public static IReadOnlyList<RaidHeroCharacter> Instances => instances;

        [SerializeField] private HeroDefinitionSO definition;
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [Tooltip("Hero model Animator. Auto-resolved from children when left empty.")]
        [SerializeField] private Animator animator;
        [Tooltip("ขอบเขตตำแหน่ง Hero — เว้นว่าง = ไม่ clamp")]
        [SerializeField] private BoxCollider movementBounds;
        [Tooltip("Optional socket for Heal/Blink/Skill FX. Falls back to the Hero root.")]
        [SerializeField] private Transform abilityEffectAnchor;

        [Header("Squad Focus Order")]
        [Tooltip("ยูนิตฝ่ายบุกที่อยู่ไม่เกินรัศมีนี้จาก Hero ตอนยืนยันเป้าหมายจะรับคำสั่งช่วยโจมตี")]
        [Min(1f)] [SerializeField] private float squadCommandRadius = 16f;
        [Tooltip("เวลาสูงสุดที่กองทัพทำตามคำสั่ง ก่อนกลับ lane AI เดิม")]
        [Min(0.5f)] [SerializeField] private float squadCommandDuration = 10f;
        [Tooltip("ระยะสูงสุดที่ยูนิตหนึ่งตัวออกจากจุดรับคำสั่ง เพื่อป้องกันการลากข้ามทั้งแผนที่")]
        [Min(1f)] [SerializeField] private float squadMaxTravelDistance = 24f;
        [Tooltip("ยกเลิกคำสั่งเมื่อยูนิตอยู่นอกระยะโจมตีและไม่เข้าใกล้เป้าหมายต่อเนื่องนานเท่านี้")]
        [Min(0.5f)] [SerializeField] private float squadStalledSeconds = 2.5f;

        private readonly NetworkVariable<HeroControlMode> controlMode = new(
            HeroControlMode.Auto, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<HeroLifeState> lifeState = new(
            HeroLifeState.Active, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> mana = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<HeroTargetPreference> targetPreference = new(
            HeroTargetPreference.Default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> downedRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> revivesRemaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> tacticalAbilityCooldownRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> blinkCooldownRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> healCooldownRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> skill2CooldownRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> skill3CooldownRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<HeroAbilitySlot> lastAbilitySlot = new(
            HeroAbilitySlot.Skill1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> hasFocusTarget = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkObjectReference> focusTarget = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Lightweight replicated feedback channel. Payload values are written before the sequence changes,
        // so every peer can turn the server decision into presentation without deciding gameplay locally.
        private readonly NetworkVariable<HeroFeedback> lastFeedback = new(
            HeroFeedback.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> lastFeedbackValue = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> feedbackSequence = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Vector2 manualInput;
        private float lastManualInputTime;
        private float attackTimer;
        private CharacterBase pendingNormalAttackTarget;
        private float pendingNormalAttackImpactAt;
        private int pendingNormalAttackDamage;
        private bool normalAttackPending;
        private float nextAutoCombatActionAt;
        private int nextAutoSkillIndex;
        private Vector3 lastPresentationPosition;
        private float lastPresentationMovementTime;
        private float actionAnimationUntil;
        private string currentPresentationState;
        private bool presentationInitialized;
        private const float MovementSampleThreshold = 0.001f;
        private const float MovementHoldSeconds = 0.18f;
        private static readonly System.Collections.Generic.List<CharacterBase> abilityTargets = new();
        private sealed class PendingAbilityDot
        {
            public HeroAbilityDefinitionSO ability;
            public HeroAbilityCastType castType;
            public Vector3 center;
            public CharacterBase lockedTarget;
            public int tickIndex;
            public float tickSpacing;
            public float nextTickAt;
        }
        private readonly List<PendingAbilityDot> pendingAbilityDots = new();
        private static readonly HeroAbilitySlot[] AutoSkillSlots =
        {
            HeroAbilitySlot.Skill1,
            HeroAbilitySlot.Skill2,
            HeroAbilitySlot.Skill3
        };

        public event System.Action<HeroFeedback, int> FeedbackReceived;

        public HeroDefinitionSO Definition => definition;
        public RaidSide Side => side;
        public HeroControlMode ControlMode => controlMode.Value;
        public HeroLifeState LifeState => lifeState.Value;
        public float Mana => mana.Value;
        public float ManaMaxValue => definition != null ? Mathf.Max(1f, definition.maxMana) : 1f;
        public float Mana01 => Mathf.Clamp01(Mana / ManaMaxValue);
        public float ManaGenerationPercentPerSecond =>
            definition != null ? Mathf.Max(0f, definition.manaGenerationPercentPerSecond) : 0f;
        public HeroTargetPreference TargetPreference => targetPreference.Value;
        public static HeroControlMode TargetAssistControlMode => HeroControlMode.Manual;
        public float DownedRemaining => downedRemaining.Value;
        public int RevivesRemaining => revivesRemaining.Value;
        public HeroAbilityDefinitionSO TacticalAbility =>
            definition != null ? definition.GetAbility(HeroAbilitySlot.Skill1) : null;
        public float TacticalAbilityCooldownRemaining => tacticalAbilityCooldownRemaining.Value;
        public bool IsTacticalAbilityReady => TacticalAbility != null && tacticalAbilityCooldownRemaining.Value <= 0f;
        public bool HasFocusTarget => hasFocusTarget.Value;
        public bool CanAct => lifeState.Value == HeroLifeState.Active && !IsDead;
        public bool CanReborn =>
            definition != null &&
            revivesRemaining.Value > 0 &&
            lifeState.Value != HeroLifeState.Active &&
            IsDead &&
            (RaidManager.Instance == null || !RaidManager.Instance.IsOver);
        public float SquadCommandRadius => squadCommandRadius;
        public HeroAbilitySlot LastAbilitySlot => lastAbilitySlot.Value;
        public static bool IsLocalControlSuppressed =>
            RaidContext.Target?.isIncomingDefense == true ||
            RaidSessionContext.Current?.isIncomingDefense == true;
        public bool CanLocalPlayerControl => CanAcceptControlIntent(IsOwner);

        // Network ownership is not gameplay-role authority. In the local incoming-defense simulation the
        // host owns the synthetic attacker for replication, but the local player is only the defender viewer.
        public static bool CanAcceptControlIntent(bool senderHasAuthority) =>
            senderHasAuthority && !IsLocalControlSuppressed;

        public HeroAbilityDefinitionSO GetAbility(HeroAbilitySlot slot) =>
            definition != null ? definition.GetAbility(slot) : null;

        public bool HasManaForAbility(HeroAbilitySlot slot)
        {
            var ability = GetAbility(slot);
            return ability != null && HasSufficientMana(mana.Value, ability.manaCost);
        }

        public static bool HasSufficientMana(float currentMana, float manaCost) =>
            currentMana + 0.001f >= Mathf.Max(0f, manaCost);

        public static bool ShouldAutoHeal(int currentHealth, int maxHealth, int healing)
        {
            if (currentHealth <= 0 || maxHealth <= 0 || healing <= 0 || currentHealth >= maxHealth)
                return false;
            var missingHealth = maxHealth - currentHealth;
            var usefulHealThreshold = Mathf.Max(1, Mathf.CeilToInt(
                Mathf.Min(maxHealth, healing) * 0.5f));
            return missingHealth >= usefulHealThreshold || currentHealth * 2 <= maxHealth;
        }

        public float GetAbilityCooldownRemaining(HeroAbilitySlot slot)
        {
            return slot switch
            {
                HeroAbilitySlot.Blink => blinkCooldownRemaining.Value,
                HeroAbilitySlot.Heal => healCooldownRemaining.Value,
                HeroAbilitySlot.Skill1 => tacticalAbilityCooldownRemaining.Value,
                HeroAbilitySlot.Skill2 => skill2CooldownRemaining.Value,
                HeroAbilitySlot.Skill3 => skill3CooldownRemaining.Value,
                _ => 0f
            };
        }

        public Vector3 GetSuggestedAbilityTargetPoint(HeroAbilitySlot slot)
        {
            var ability = GetAbility(slot);
            if (ability == null || ability.castType == HeroAbilityCastType.SelfCast)
                return transform.position;
            if (ability.castType == HeroAbilityCastType.LockedTarget &&
                TryGetFocusTarget(out var lockedTarget))
                return lockedTarget.transform.position;
            var distance = Mathf.Min(ability.castRange, Mathf.Max(1f, ability.effectRadius * 0.6f));
            return transform.position + transform.forward * distance;
        }

        // Shared client/server preview predicate. The server still repeats every check when assigning the
        // actual command, but presentation can highlight the same recruitment snapshot before confirmation.
        public bool IsSquadCommandCandidate(MonsterCharacter monster)
        {
            if (monster == null || monster.IsDead || monster.Side != side || monster.Side != RaidSide.Attacker)
                return false;
            return HorizontalSqrDistance(monster.transform.position, transform.position) <=
                   squadCommandRadius * squadCommandRadius;
        }

        public void Initialize(HeroDefinitionSO heroDefinition, RaidSide owningSide = RaidSide.Attacker)
        {
            if (!IsServer || heroDefinition == null) return;
            definition = heroDefinition;
            side = owningSide;
            InitializeFromDefinition();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!instances.Contains(this)) instances.Add(this);
            if (Instance == null || IsOwner) Instance = this;
            ResolveAnimator();
            InitializePresentation();
            feedbackSequence.OnValueChanged += HandleFeedbackSequenceChanged;
            if (IsServer && definition != null && CurrentHealth <= 0) InitializeFromDefinition();
            if (definition != null && definition.animSet != null)
                PlayActionState(definition.animSet.landing);
        }

        public override void OnNetworkDespawn()
        {
            feedbackSequence.OnValueChanged -= HandleFeedbackSequenceChanged;
            instances.Remove(this);
            if (Instance == this)
            {
                Instance = null;
                for (var i = 0; i < instances.Count; i++)
                    if (instances[i] != null && instances[i].IsOwner)
                    {
                        Instance = instances[i];
                        break;
                    }
                if (Instance == null && instances.Count > 0) Instance = instances[0];
            }
            base.OnNetworkDespawn();
        }

        private void InitializeFromDefinition()
        {
            InitializeHealth(definition.maxHealth);
            SetArmor(definition.armor);
            mana.Value = Mathf.Clamp(definition.startingMana, 0f, Mathf.Max(1f, definition.maxMana));
            controlMode.Value = definition.startsInAutoMode ? HeroControlMode.Auto : HeroControlMode.Manual;
            targetPreference.Value = HeroTargetPreference.Default;
            lifeState.Value = HeroLifeState.Active;
            downedRemaining.Value = 0f;
            revivesRemaining.Value = Mathf.Max(0, definition.maxRevivesPerRaid);
            tacticalAbilityCooldownRemaining.Value = 0f;
            blinkCooldownRemaining.Value = 0f;
            healCooldownRemaining.Value = 0f;
            skill2CooldownRemaining.Value = 0f;
            skill3CooldownRemaining.Value = 0f;
            lastAbilitySlot.Value = HeroAbilitySlot.Skill1;
            hasFocusTarget.Value = false;
            focusTarget.Value = default;
            lastFeedback.Value = HeroFeedback.None;
            lastFeedbackValue.Value = 0;
            manualInput = Vector2.zero;
            attackTimer = definition.attackCooldown;
            normalAttackPending = false;
            pendingNormalAttackTarget = null;
            nextAutoCombatActionAt = 0f;
            nextAutoSkillIndex = 0;
            pendingAbilityDots.Clear();
        }

        private void Update()
        {
            if (!IsServer || definition == null) return;
            TickShield();
            if (tacticalAbilityCooldownRemaining.Value > 0f)
                tacticalAbilityCooldownRemaining.Value = Mathf.Max(
                    0f, tacticalAbilityCooldownRemaining.Value - Time.deltaTime);
            TickAbilityCooldown(blinkCooldownRemaining);
            TickAbilityCooldown(healCooldownRemaining);
            TickAbilityCooldown(skill2CooldownRemaining);
            TickAbilityCooldown(skill3CooldownRemaining);
            TickManaRegeneration();
            TickPendingNormalAttack();
            TickPendingAbilityDots();

            if (hasFocusTarget.Value && !TryResolveFocusTarget(out _))
            {
                ClearFocusTarget(HeroFeedback.FocusTargetCompleted);
            }

            if (lifeState.Value == HeroLifeState.Downed)
            {
                downedRemaining.Value = Mathf.Max(0f, downedRemaining.Value - Time.deltaTime);
                if (downedRemaining.Value <= 0f) lifeState.Value = HeroLifeState.Defeated;
                return;
            }

            if (!CanAct || (RaidManager.Instance != null && RaidManager.Instance.IsOver)) return;

            attackTimer += Time.deltaTime;
            if (controlMode.Value == HeroControlMode.Auto) TickCombat();

            if (controlMode.Value == HeroControlMode.Manual) TickManualMovement();
            else TickAutoMovement();
        }

        // Presentation is sampled on every peer from replicated transform movement. This keeps the dedicated
        // client and host visually identical without turning Animator state into authoritative gameplay data.
        private void LateUpdate()
        {
            if (!IsSpawned || definition == null) return;
            UpdatePresentationFromReplicatedTransform();
        }

        private void UpdatePresentationFromReplicatedTransform()
        {
            ResolveAnimator();
            if (animator == null) return;
            if (!presentationInitialized) InitializePresentation();

            var position = transform.position;
            var delta = position - lastPresentationPosition;
            delta.y = 0f;
            lastPresentationPosition = position;
            if (delta.sqrMagnitude > MovementSampleThreshold * MovementSampleThreshold)
                lastPresentationMovementTime = Time.unscaledTime;

            if (Time.unscaledTime < actionAnimationUntil) return;
            if (lifeState.Value == HeroLifeState.Defeated ||
                lifeState.Value == HeroLifeState.Downed)
            {
                SetPresentationState(AnimationStateName(HeroAnimationState.Death));
                return;
            }

            var moving = Time.unscaledTime - lastPresentationMovementTime <= MovementHoldSeconds;
            SetPresentationState(AnimationStateName(moving ? HeroAnimationState.Walk : HeroAnimationState.Idle));
        }

        private void TickCombat()
        {
            if (Time.time < nextAutoCombatActionAt) return;
            var target = ResolveAutoTarget();

            if (TryUseAutoHeal()) return;
            if (target != null && TryUseAutoSkill(target)) return;
            if (attackTimer < definition.attackCooldown || normalAttackPending ||
                target == null || !IsWithinHorizontalRange(target, definition.attackRange))
                return;

            BeginNormalAttack(target);
        }

        private CharacterBase ResolveAutoTarget()
        {
            if (TryResolveFocusTarget(out var orderedTarget)) return orderedTarget;
            var target = FindNearestEnemy(definition.autoAggroRange);
            if (target == null && side == RaidSide.Attacker) target = FortCore.Instance;
            return target != null && !target.IsDead ? target : null;
        }

        private bool TryUseAutoHeal()
        {
            var ability = GetAbility(HeroAbilitySlot.Heal);
            if (!IsAutoAbilityResourceReady(HeroAbilitySlot.Heal, ability) ||
                !ShouldAutoHeal(CurrentHealth, MaxHealth, ability.healing))
                return false;

            ScheduleNextAutoAction(HeroAbilitySlot.Heal, ability);
            TryCastAbility(HeroAbilitySlot.Heal, transform.position);
            return true;
        }

        private bool TryUseAutoSkill(CharacterBase target)
        {
            if (target == null || target.IsDead || target is FortCore) return false;
            for (var offset = 0; offset < AutoSkillSlots.Length; offset++)
            {
                var index = (nextAutoSkillIndex + offset) % AutoSkillSlots.Length;
                var slot = AutoSkillSlots[index];
                var ability = GetAbility(slot);
                if (!IsAutoAbilityResourceReady(slot, ability) ||
                    !TryGetAutoAbilityTargetPoint(ability, target, out var targetPoint))
                    continue;

                Face(target.transform.position - transform.position);
                ScheduleNextAutoAction(slot, ability);
                nextAutoSkillIndex = (index + 1) % AutoSkillSlots.Length;
                TryCastAbility(slot, targetPoint, target);
                return true;
            }
            return false;
        }

        private bool IsAutoAbilityResourceReady(
            HeroAbilitySlot slot,
            HeroAbilityDefinitionSO ability)
        {
            return ability != null &&
                   ability.effect != HeroAbilityEffect.ForwardBlink &&
                   GetAbilityCooldownRemaining(slot) <= 0.001f &&
                   HasSufficientMana(mana.Value, ability.manaCost);
        }

        private bool TryGetAutoAbilityTargetPoint(
            HeroAbilityDefinitionSO ability,
            CharacterBase target,
            out Vector3 targetPoint)
        {
            targetPoint = transform.position;
            if (ability == null) return false;
            if (ability.effect == HeroAbilityEffect.SelfHeal)
                return ShouldAutoHeal(CurrentHealth, MaxHealth, ability.healing);
            if (target == null || target.IsDead) return false;

            targetPoint = target.transform.position;
            var requiredRange = ability.castType == HeroAbilityCastType.SelfCast
                ? ability.effectRadius
                : ability.castRange;
            return IsWithinHorizontalRange(target, Mathf.Max(0.1f, requiredRange));
        }

        private void ScheduleNextAutoAction(
            HeroAbilitySlot slot,
            HeroAbilityDefinitionSO ability)
        {
            var animationState = ResolveAbilityAnimationState(slot, ability);
            nextAutoCombatActionAt = Time.time + Mathf.Max(
                0.25f,
                GetAnimationDuration(animationState));
        }

        private void TickManualMovement()
        {
            if (Time.time - lastManualInputTime > 0.35f) manualInput = Vector2.zero;
            var direction = new Vector3(manualInput.x, 0f, manualInput.y);
            var hasManualInput = direction.sqrMagnitude > 0.0001f;
            var hasLockedTarget = TryResolveFocusTarget(out var lockedTarget);

            if (hasManualInput)
                Move(direction, !hasLockedTarget);
            else if (hasLockedTarget)
            {
                var delta = lockedTarget.transform.position - transform.position;
                delta.y = 0f;
                if (delta.magnitude > definition.attackRange * 0.9f)
                    Move(delta.normalized, false);
            }

            // Target assist is strafing-friendly: joystick remains active, while facing stays locked.
            if (hasLockedTarget)
                Face(lockedTarget.transform.position - transform.position);
        }

        private void TickAutoMovement()
        {
            if (Time.time < nextAutoCombatActionAt) return;
            var target = ResolveAutoTarget();
            if (target == null) return;

            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude <= definition.attackRange * 0.9f) return;
            Move(delta.normalized);
        }

        private void Move(Vector3 direction, bool faceMovement = true)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            if (direction.sqrMagnitude < 0.0001f) return;

            var position = transform.position + direction * (definition.moveSpeed * Time.deltaTime);
            if (movementBounds != null)
            {
                var bounds = movementBounds.bounds;
                position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
                position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            }

            if (GroundSurfaceResolver.TrySnap(
                    position,
                    transform,
                    out var snappedPosition,
                    definition.groundOffset))
                position = snappedPosition;
            transform.position = position;
            if (faceMovement) Face(direction);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, definition.turnSpeedDegPerSec * Time.deltaTime);
        }

        private CharacterBase FindNearestEnemy(float range)
        {
            CharacterBase nearest = null;
            var bestSqr = range * range;
            var position = transform.position;

            var heroes = instances;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (hero == null || hero == this || hero.side == side) continue;
                Consider(hero, position, ref nearest, ref bestSqr);
            }

            var towers = TowerCharacter.Active;
            for (var i = 0; i < towers.Count; i++)
                Consider(towers[i], position, ref nearest, ref bestSqr);

            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster.Side == side) continue;
                Consider(monster, position, ref nearest, ref bestSqr);
            }
            return nearest;
        }

        private void TickManaRegeneration()
        {
            if (!CanAct || definition.maxMana <= 0f || definition.manaGenerationPercentPerSecond <= 0f) return;
            var perSecond = definition.maxMana * definition.manaGenerationPercentPerSecond * 0.01f;
            mana.Value = Mathf.Min(definition.maxMana, mana.Value + perSecond * Time.deltaTime);
        }

        private void BeginNormalAttack(CharacterBase target)
        {
            if (definition == null || normalAttackPending) return;
            if (target != null) Face(target.transform.position - transform.position);

            var useAttack2 = Random.value >= 0.5f;
            attackTimer = 0f;
            if (controlMode.Value == HeroControlMode.Auto)
                nextAutoCombatActionAt = Time.time + Mathf.Max(
                    0.25f,
                    definition.normalAttackImpactDelay);
            if (target != null)
            {
                pendingNormalAttackTarget = target;
                pendingNormalAttackDamage = definition.attackDamage;
                pendingNormalAttackImpactAt = Time.time + Mathf.Max(0.01f, definition.normalAttackImpactDelay);
                normalAttackPending = true;
            }
            PlayNormalAttackPresentationClientRpc(useAttack2);
        }

        private void TickPendingNormalAttack()
        {
            if (!normalAttackPending || Time.time < pendingNormalAttackImpactAt) return;
            normalAttackPending = false;
            var target = pendingNormalAttackTarget;
            pendingNormalAttackTarget = null;
            if (!CanAct || target == null || target.IsDead) return;

            target.ApplyDamage(pendingNormalAttackDamage, this);
            PublishFeedback(HeroFeedback.NormalAttackHit, pendingNormalAttackDamage);
        }

        private static void Consider(CharacterBase candidate, Vector3 position, ref CharacterBase nearest, ref float bestSqr)
        {
            if (candidate == null || candidate.IsDead) return;
            var sqr = HorizontalSqrDistance(candidate.transform.position, position);
            if (sqr > bestSqr) return;
            bestSqr = sqr;
            nearest = candidate;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSetControlModeServerRpc(HeroControlMode requested, ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId) || lifeState.Value != HeroLifeState.Active) return;
            controlMode.Value = requested;
            manualInput = Vector2.zero;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSetTargetPreferenceServerRpc(
            HeroTargetPreference requested,
            NetworkObjectReference requestedVisibleTarget = default,
            ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId) || !CanAct) return;
            controlMode.Value = TargetAssistControlMode;
            ClearFocusTarget(HeroFeedback.FocusTargetCleared);

            // Visibility is chosen from the active gameplay camera on the owning client. The server never
            // falls back to a nearest world target: it only validates and accepts that explicit candidate.
            if (!TryResolveRequestedPreferenceTarget(
                    requested,
                    requestedVisibleTarget,
                    out var visibleTarget))
            {
                targetPreference.Value = HeroTargetPreference.Default;
                return;
            }

            targetPreference.Value = requested;
            SetFocusTarget(visibleTarget, false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestMoveServerRpc(Vector2 worldDirectionXZ, ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId) || controlMode.Value != HeroControlMode.Manual || !CanAct)
                return;
            manualInput = Vector2.ClampMagnitude(worldDirectionXZ, 1f);
            lastManualInputTime = Time.time;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSetFocusTargetServerRpc(
            NetworkObjectReference requestedTarget,
            ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            if (!CanAct || (RaidManager.Instance != null && RaidManager.Instance.IsOver) ||
                !TryResolveRequestedFocusTarget(requestedTarget, out var target))
            {
                PublishFeedback(HeroFeedback.FocusTargetRejected);
                return;
            }

            // Write the reference before the flag so clients never observe "has target" with an old ref.
            targetPreference.Value = HeroTargetPreference.Default;
            SetFocusTarget(target, true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestClearFocusTargetServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            targetPreference.Value = HeroTargetPreference.Default;
            ClearFocusTarget(HeroFeedback.FocusTargetCleared);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRebornServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId))
            {
                PublishFeedback(HeroFeedback.ReviveRejected);
                return;
            }
            TryRevive();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            if (!CanAct)
            {
                PublishFeedback(HeroFeedback.InteractionUnavailable);
                return;
            }
            TryInteractNearby();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestCastTacticalAbilityServerRpc(
            Vector3 requestedTargetPoint,
            ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            TryCastAbility(HeroAbilitySlot.Skill1, requestedTargetPoint);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestCastAbilityServerRpc(
            HeroAbilitySlot slot,
            Vector3 requestedTargetPoint,
            ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            TryCastAbility(slot, requestedTargetPoint);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestNormalAttackServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            if (!CanAct || definition == null || (RaidManager.Instance != null && RaidManager.Instance.IsOver))
            {
                PublishFeedback(HeroFeedback.AbilityUnavailable);
                return;
            }

            if (attackTimer < definition.attackCooldown || normalAttackPending)
            {
                PublishFeedback(
                    HeroFeedback.NormalAttackCooldown,
                    Mathf.Max(1, Mathf.CeilToInt(definition.attackCooldown - attackTimer)));
                return;
            }

            var hasLockedTarget = TryResolveFocusTarget(out var lockedTarget);
            var target = hasLockedTarget
                ? IsWithinHorizontalRange(lockedTarget, definition.attackRange) ? lockedTarget : null
                : FindNearestEnemy(definition.attackRange);
            if (target != null)
            {
                BeginNormalAttack(target);
            }
            else
            {
                PublishFeedback(HeroFeedback.NormalAttackNoTarget);
                BeginNormalAttack(null);
            }
        }

        private void TryCastAbility(
            HeroAbilitySlot slot,
            Vector3 requestedTargetPoint,
            CharacterBase autoTarget = null)
        {
            lastAbilitySlot.Value = slot;
            var ability = GetAbility(slot);
            if (ability == null || !CanAct || (RaidManager.Instance != null && RaidManager.Instance.IsOver))
            {
                PublishFeedback(HeroFeedback.AbilityUnavailable);
                return;
            }

            var remaining = GetAbilityCooldownRemaining(slot);
            if (remaining > 0f)
            {
                PublishFeedback(
                    HeroFeedback.AbilityCooldown,
                    Mathf.CeilToInt(remaining));
                return;
            }

            if (!HasSufficientMana(mana.Value, ability.manaCost))
            {
                PublishFeedback(HeroFeedback.AbilityNoMana, Mathf.CeilToInt(ability.manaCost - mana.Value));
                return;
            }

            var hasLockedTarget = autoTarget != null && IsValidFocusTarget(autoTarget);
            var lockedTarget = hasLockedTarget ? autoTarget : null;
            if (!hasLockedTarget) hasLockedTarget = TryResolveFocusTarget(out lockedTarget);
            if (ability.castType == HeroAbilityCastType.LockedTarget && hasLockedTarget)
                requestedTargetPoint = lockedTarget.transform.position;

            if (ability.effect == HeroAbilityEffect.ForwardBlink)
            {
                CastBlink(slot, ability);
                return;
            }

            if (ability.effect == HeroAbilityEffect.SelfHeal)
            {
                CastHeal(slot, ability);
                return;
            }

            if (!TryResolveAbilityTargetPoint(
                    ability,
                    requestedTargetPoint,
                    out var targetPoint,
                    hasLockedTarget ? lockedTarget : null))
                return;

            // Preserve the ground hit height for presentation, but cap vertical input so a client cannot
            // spawn the cosmetic effect at an arbitrary altitude. Damage/range always use XZ distance.
            targetPoint.y = Mathf.Clamp(targetPoint.y, transform.position.y - 3f, transform.position.y + 3f);
            if (ability.castType != HeroAbilityCastType.SelfCast)
                Face(targetPoint - transform.position);

            var directTarget = ability.castType == HeroAbilityCastType.LockedTarget && hasLockedTarget
                ? lockedTarget
                : null;
            var hitCount = ability.damageMode == HeroAbilityDamageMode.DamageOverTime
                ? StartAbilityDot(ability, targetPoint, directTarget)
                : ApplyAbilityDamage(
                    ability.castType,
                    targetPoint,
                    directTarget,
                    ability.effectRadius,
                    ability.damage);

            ConsumeMana(ability.manaCost);
            SetAbilityCooldown(slot, ability.cooldownSeconds);
            PublishFeedback(
                hitCount > 0 ? HeroFeedback.AbilityCast : HeroFeedback.AbilityNoTargets,
                hitCount);
            PlayAbilityPresentationClientRpc(slot, targetPoint, transform.position);
        }

        // Lets the target-mode button produce server-confirmed feedback without entering targeting while the
        // Hero is downed or the ability is cooling down.
        [ServerRpc(RequireOwnership = false)]
        public void RequestTacticalAbilityStatusFeedbackServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            PublishAbilityStatus(HeroAbilitySlot.Skill1);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestAbilityStatusFeedbackServerRpc(
            HeroAbilitySlot slot,
            ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            PublishAbilityStatus(slot);
        }

        private void PublishAbilityStatus(HeroAbilitySlot slot)
        {
            lastAbilitySlot.Value = slot;
            if (GetAbility(slot) == null || !CanAct)
            {
                PublishFeedback(HeroFeedback.AbilityUnavailable);
                return;
            }

            var remaining = GetAbilityCooldownRemaining(slot);
            if (remaining > 0f)
                PublishFeedback(
                    HeroFeedback.AbilityCooldown,
                    Mathf.CeilToInt(remaining));
            else
            {
                var ability = GetAbility(slot);
                if (ability != null && !HasSufficientMana(mana.Value, ability.manaCost))
                    PublishFeedback(
                        HeroFeedback.AbilityNoMana,
                        Mathf.CeilToInt(ability.manaCost - mana.Value));
            }
        }

        private void CastBlink(HeroAbilitySlot slot, HeroAbilityDefinitionSO ability)
        {
            var distance = Mathf.Max(0f, ability.movementDistance);
            if (distance <= 0f)
            {
                PublishFeedback(HeroFeedback.AbilityUnavailable);
                return;
            }

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            var origin = transform.position;
            var destination = origin + forward.normalized * distance;
            destination = ClampToMovementBounds(destination);
            if (GroundSurfaceResolver.TrySnap(
                    destination,
                    transform,
                    out var snappedDestination,
                    definition.groundOffset))
                destination = snappedDestination;
            var movedDistance = Vector3.Distance(transform.position, destination);
            if (movedDistance < 0.01f)
            {
                PublishFeedback(HeroFeedback.AbilityOutOfRange);
                return;
            }

            transform.position = destination;
            manualInput = Vector2.zero;
            ConsumeMana(ability.manaCost);
            SetAbilityCooldown(slot, ability.cooldownSeconds);
            PublishFeedback(HeroFeedback.AbilityBlinked, Mathf.RoundToInt(movedDistance * 10f));
            PlayAbilityPresentationClientRpc(slot, destination, origin);
        }

        private void CastHeal(HeroAbilitySlot slot, HeroAbilityDefinitionSO ability)
        {
            if (ability.healing <= 0 || CurrentHealth >= MaxHealth)
            {
                PublishFeedback(HeroFeedback.AbilityNoTargets);
                return;
            }

            var previousHealth = CurrentHealth;
            Heal(ability.healing);
            var restored = CurrentHealth - previousHealth;
            if (restored <= 0)
            {
                PublishFeedback(HeroFeedback.AbilityNoTargets);
                return;
            }

            ConsumeMana(ability.manaCost);
            SetAbilityCooldown(slot, ability.cooldownSeconds);
            PublishFeedback(HeroFeedback.AbilityHealed, restored);
            PlayAbilityPresentationClientRpc(slot, transform.position, transform.position);
        }

        private bool TryResolveAbilityTargetPoint(
            HeroAbilityDefinitionSO ability,
            Vector3 requestedTargetPoint,
            out Vector3 targetPoint,
            CharacterBase lockedTargetOverride = null)
        {
            targetPoint = transform.position;
            if (ability.castType == HeroAbilityCastType.SelfCast) return true;
            if (ability.castType == HeroAbilityCastType.LockedTarget)
            {
                var hasTarget = lockedTargetOverride != null &&
                                IsValidFocusTarget(lockedTargetOverride);
                var lockedTarget = hasTarget ? lockedTargetOverride : null;
                if (!hasTarget) hasTarget = TryResolveFocusTarget(out lockedTarget);
                if (hasTarget)
                {
                    targetPoint = lockedTarget.transform.position;
                    if (HorizontalSqrDistance(transform.position, targetPoint) <=
                        ability.castRange * ability.castRange)
                        return true;
                    PublishFeedback(HeroFeedback.AbilityOutOfRange);
                    return false;
                }

                var distance = Mathf.Min(ability.castRange, Mathf.Max(1f, ability.effectRadius * 0.6f));
                targetPoint += transform.forward * distance;
                return true;
            }

            if (!IsFinite(requestedTargetPoint))
            {
                PublishFeedback(HeroFeedback.AbilityOutOfRange);
                return false;
            }

            targetPoint = requestedTargetPoint;
            if (HorizontalSqrDistance(transform.position, targetPoint) <= ability.castRange * ability.castRange)
                return true;

            PublishFeedback(HeroFeedback.AbilityOutOfRange);
            return false;
        }

        private void SetAbilityCooldown(HeroAbilitySlot slot, float seconds)
        {
            var value = Mathf.Max(0f, seconds);
            switch (slot)
            {
                case HeroAbilitySlot.Blink: blinkCooldownRemaining.Value = value; break;
                case HeroAbilitySlot.Heal: healCooldownRemaining.Value = value; break;
                case HeroAbilitySlot.Skill1: tacticalAbilityCooldownRemaining.Value = value; break;
                case HeroAbilitySlot.Skill2: skill2CooldownRemaining.Value = value; break;
                case HeroAbilitySlot.Skill3: skill3CooldownRemaining.Value = value; break;
            }
        }

        private static void TickAbilityCooldown(NetworkVariable<float> cooldown)
        {
            if (cooldown.Value > 0f)
                cooldown.Value = Mathf.Max(0f, cooldown.Value - Time.deltaTime);
        }

        private void ConsumeMana(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            mana.Value = Mathf.Max(0f, mana.Value - amount);
        }

        private Vector3 ClampToMovementBounds(Vector3 position)
        {
            if (movementBounds != null)
            {
                var bounds = movementBounds.bounds;
                position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
                position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            }
            return position;
        }

        private bool CanControl(ulong senderClientId) =>
            CanAcceptControlIntent(
                senderClientId == OwnerClientId || senderClientId == NetworkManager.ServerClientId);

        // Replicated presentation query. Server AI uses the same resolution plus target validation below.
        public bool TryGetFocusTarget(out CharacterBase target)
        {
            target = null;
            if (!hasFocusTarget.Value || !focusTarget.Value.TryGet(out var networkObject)) return false;
            return networkObject.TryGetComponent(out target) && target != null && !target.IsDead;
        }

        private bool TryResolveFocusTarget(out CharacterBase target)
        {
            if (!TryGetFocusTarget(out target)) return false;
            return IsValidFocusTarget(target);
        }

        private bool TryResolveRequestedFocusTarget(
            NetworkObjectReference requestedTarget,
            out CharacterBase target)
        {
            target = null;
            if (!requestedTarget.TryGet(out var networkObject) || !networkObject.TryGetComponent(out target))
                return false;
            return IsValidFocusTarget(target);
        }

        private bool TryResolveRequestedPreferenceTarget(
            HeroTargetPreference requested,
            NetworkObjectReference requestedTarget,
            out CharacterBase target)
        {
            target = null;
            if (requested == HeroTargetPreference.Default ||
                !TryResolveRequestedFocusTarget(requestedTarget, out var resolved))
                return false;

            var matchesPreference = requested switch
            {
                HeroTargetPreference.Monster =>
                    resolved is MonsterCharacter || resolved is RaidHeroCharacter,
                HeroTargetPreference.Tower =>
                    resolved is TowerCharacter && resolved is not FortCore,
                _ => false
            };
            if (!matchesPreference) return false;
            target = resolved;
            return true;
        }

        private bool IsValidFocusTarget(CharacterBase target)
        {
            if (target == null || target.IsDead) return false;
            if (target is TowerCharacter tower) return tower is not FortCore;
            if (target is MonsterCharacter monster) return monster.Side != side;
            return target is RaidHeroCharacter hero && hero != this && hero.side != side;
        }

        private void SetFocusTarget(CharacterBase target, bool issueSquadOrder)
        {
            if (!IsServer || target == null || target.IsDead || target.NetworkObject == null) return;
            focusTarget.Value = new NetworkObjectReference(target.NetworkObject);
            hasFocusTarget.Value = true;
            var assignedUnitCount = issueSquadOrder && target is not RaidHeroCharacter
                ? IssueSquadFocusOrder(target)
                : 0;
            PublishFeedback(HeroFeedback.FocusTargetSet, assignedUnitCount);
        }

        private void ClearFocusTarget(HeroFeedback feedback)
        {
            if (!IsServer) return;
            var hadTarget = hasFocusTarget.Value;
            if (hadTarget) ClearSquadFocusOrders();
            hasFocusTarget.Value = false;
            focusTarget.Value = default;
            if (hadTarget || feedback == HeroFeedback.FocusTargetRejected) PublishFeedback(feedback);
        }

        private int IssueSquadFocusOrder(CharacterBase target)
        {
            ClearSquadFocusOrders();
            var assigned = 0;
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (!IsSquadCommandCandidate(monster)) continue;

                if (monster.TryAssignTacticalFocusTarget(
                        target,
                        squadCommandDuration,
                        squadMaxTravelDistance,
                        squadStalledSeconds))
                    assigned++;
            }
            return assigned;
        }

        private void ClearSquadFocusOrders()
        {
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster != null && monster.Side == side) monster.ClearTacticalFocusTarget();
            }
        }

        private bool IsWithinHorizontalRange(CharacterBase target, float range) =>
            target != null && HorizontalSqrDistance(transform.position, target.transform.position) <= range * range;

        private int StartAbilityDot(
            HeroAbilityDefinitionSO ability,
            Vector3 center,
            CharacterBase directTarget)
        {
            var previewCount = directTarget != null && !directTarget.IsDead
                ? 1
                : CollectAreaTargets(center, ability.effectRadius);
            var tickCount = ability.DamageTickCount;
            pendingAbilityDots.Add(new PendingAbilityDot
            {
                ability = ability,
                castType = ability.castType,
                center = center,
                lockedTarget = directTarget,
                tickIndex = 0,
                tickSpacing = Mathf.Max(0.01f, ability.damageDurationSeconds / tickCount),
                nextTickAt = Time.time + Mathf.Max(0.01f, ability.damageDurationSeconds / tickCount)
            });
            return previewCount;
        }

        private void TickPendingAbilityDots()
        {
            for (var i = pendingAbilityDots.Count - 1; i >= 0; i--)
            {
                var dot = pendingAbilityDots[i];
                if (dot == null || dot.ability == null)
                {
                    pendingAbilityDots.RemoveAt(i);
                    continue;
                }

                while (Time.time >= dot.nextTickAt && dot.tickIndex < dot.ability.DamageTickCount)
                {
                    var damage = dot.ability.DamageAtTick(dot.tickIndex);
                    if (damage > 0)
                        ApplyAbilityDamage(
                            dot.castType,
                            dot.center,
                            dot.lockedTarget,
                            dot.ability.effectRadius,
                            damage);
                    dot.tickIndex++;
                    dot.nextTickAt += dot.tickSpacing;
                }

                if (dot.tickIndex >= dot.ability.DamageTickCount)
                    pendingAbilityDots.RemoveAt(i);
            }
        }

        private int ApplyAbilityDamage(
            HeroAbilityCastType castType,
            Vector3 center,
            CharacterBase directTarget,
            float radius,
            int damage)
        {
            abilityTargets.Clear();
            if (castType == HeroAbilityCastType.LockedTarget && directTarget != null)
            {
                if (!directTarget.IsDead && IsValidFocusTarget(directTarget))
                    abilityTargets.Add(directTarget);
            }
            else
            {
                CollectAreaTargets(center, radius);
            }

            for (var i = 0; i < abilityTargets.Count; i++)
            {
                var target = abilityTargets[i];
                if (target != null && !target.IsDead) target.ApplyDamage(damage, this);
            }
            return abilityTargets.Count;
        }

        private int CollectAreaTargets(Vector3 targetPoint, float radius)
        {
            abilityTargets.Clear();
            var radiusSqr = radius * radius;

            var towers = TowerCharacter.Active;
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                // Core is a separate raid objective, not a defensive structure that Breach Charge can bypass.
                if (tower == null || tower.IsDead || tower is FortCore) continue;
                if (HorizontalSqrDistance(tower.transform.position, targetPoint) <= radiusSqr)
                    abilityTargets.Add(tower);
            }

            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.IsDead || monster.Side == side) continue;
                if (HorizontalSqrDistance(monster.transform.position, targetPoint) <= radiusSqr)
                    abilityTargets.Add(monster);
            }

            var heroes = instances;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (hero == null || hero == this || hero.IsDead || hero.side == side) continue;
                if (HorizontalSqrDistance(hero.transform.position, targetPoint) <= radiusSqr)
                    abilityTargets.Add(hero);
            }
            return abilityTargets.Count;
        }

        public static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        [ClientRpc]
        private void PlayAbilityPresentationClientRpc(
            HeroAbilitySlot slot,
            Vector3 targetPoint,
            Vector3 originPoint)
        {
            PlayAbilityPresentation(slot, targetPoint, originPoint);
        }

        private void PlayAbilityPresentation(
            HeroAbilitySlot slot,
            Vector3 targetPoint,
            Vector3 originPoint)
        {
            var ability = GetAbility(slot);
            if (ability == null) return;
            PlayActionState(ResolveAbilityAnimationState(slot, ability));
            if (ability.HasStagedVfx)
            {
                HeroAbilityVfxSequencePlayer.Play(
                    ability,
                    transform,
                    abilityEffectAnchor,
                    targetPoint,
                    originPoint);
                return;
            }
            if (ability.castEffectPrefab == null) return;
            var effectLifetime = ability.damageMode == HeroAbilityDamageMode.DamageOverTime
                ? Mathf.Max(ability.castEffectLifetime, ability.damageDurationSeconds)
                : ability.castEffectLifetime;

            switch (ability.effectPlacement)
            {
                case HeroAbilityEffectPlacement.HeroRoot:
                    OneShotEffect.SpawnAttached(
                        ability.castEffectPrefab,
                        transform,
                        effectLifetime,
                        ability.effectLocalOffset);
                    break;
                case HeroAbilityEffectPlacement.HeroEffectAnchor:
                    if (slot == HeroAbilitySlot.Blink)
                        OneShotEffect.SpawnAttachedTrail(
                            ability.castEffectPrefab,
                            abilityEffectAnchor != null ? abilityEffectAnchor : transform,
                            originPoint,
                            targetPoint,
                            effectLifetime,
                            ability.effectLocalOffset);
                    else
                        OneShotEffect.SpawnAttached(
                            ability.castEffectPrefab,
                            abilityEffectAnchor != null ? abilityEffectAnchor : transform,
                            effectLifetime,
                            ability.effectLocalOffset);
                    break;
                case HeroAbilityEffectPlacement.WorldPoint:
                    OneShotEffect.Spawn(
                        ability.castEffectPrefab,
                        targetPoint + ability.effectLocalOffset,
                        Quaternion.identity,
                        effectLifetime);
                    break;
                default:
                    GroundSurfaceResolver.TrySnap(
                        targetPoint,
                        transform,
                        out targetPoint,
                        ability.groundEffectOffset);
                    OneShotEffect.Spawn(
                        ability.castEffectPrefab,
                        targetPoint + ability.effectLocalOffset,
                        Quaternion.identity,
                        effectLifetime);
                    break;
            }
        }

        [ClientRpc]
        private void PlayNormalAttackPresentationClientRpc(bool useAttack2)
        {
            PlayNormalAttackPresentation(useAttack2);
        }

        private void PlayNormalAttackPresentation(bool useAttack2 = false)
        {
            var set = definition != null ? definition.animSet : null;
            var stateName = set != null
                ? (useAttack2 && !string.IsNullOrWhiteSpace(set.attack2) ? set.attack2 : set.attack1)
                : "Attack";
            PlayActionState(stateName);
            if (definition != null && definition.normalAttackEffectPrefab != null)
                VfxPoolService.Spawn(
                    definition.normalAttackEffectPrefab,
                    transform.position,
                    transform.rotation,
                    definition.normalAttackEffectLifetime,
                    transform);
        }

        private string ResolveAbilityAnimationState(
            HeroAbilitySlot slot,
            HeroAbilityDefinitionSO ability)
        {
            if (!string.IsNullOrWhiteSpace(ability.animationState)) return ability.animationState;
            var set = definition != null ? definition.animSet : null;
            if (set == null) return string.Empty;
            return slot switch
            {
                HeroAbilitySlot.Blink => set.sprint,
                HeroAbilitySlot.Skill1 => set.skill1,
                HeroAbilitySlot.Skill2 => set.skill2,
                HeroAbilitySlot.Skill3 => set.skill3,
                _ => string.Empty
            };
        }

        private void InitializePresentation()
        {
            ResolveAnimator();
            lastPresentationPosition = transform.position;
            lastPresentationMovementTime = float.NegativeInfinity;
            actionAnimationUntil = 0f;
            currentPresentationState = null;
            presentationInitialized = true;
            if (definition != null)
                SetPresentationState(AnimationStateName(HeroAnimationState.Idle), true);
        }

        private void ResolveAnimator()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        private void PlayActionState(string stateName)
        {
            ResolveAnimator();
            if (!SetPresentationState(stateName, true)) return;
            actionAnimationUntil = Time.unscaledTime + GetAnimationDuration(stateName);
        }

        private bool SetPresentationState(string stateName, bool force = false)
        {
            if (!force && currentPresentationState == stateName) return true;
            if (!AnimatorUtil.SafeCrossFade(animator, stateName, 0.05f)) return false;
            currentPresentationState = stateName;
            return true;
        }

        private string AnimationStateName(HeroAnimationState state)
        {
            var set = definition != null ? definition.animSet : null;
            return state switch
            {
                HeroAnimationState.Walk => set != null ? set.walk : "Walk",
                HeroAnimationState.Death => set != null ? set.death : "Death",
                _ => set != null ? set.idle : "Idle"
            };
        }

        private float GetAnimationDuration(string stateName)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                for (var i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    if (clip != null && clip.name == stateName)
                        return Mathf.Max(0.1f, clip.length);
                }
            }
            return 0.45f;
        }

        private void TryInteractNearby()
        {
            var kind = FindNearbyInteraction(out var nearestLoot, out var nearestExtraction);
            if (kind == HeroInteractionKind.Loot)
            {
                var moved = nearestLoot.TryCollectAll();
                PublishFeedback(moved > 0 ? HeroFeedback.LootCollected : HeroFeedback.InteractionUnavailable, moved);
                return;
            }

            if (kind == HeroInteractionKind.Extraction)
            {
                var completed = nearestExtraction.TryExtract();
                PublishFeedback(completed ? HeroFeedback.ExtractionCompleted : HeroFeedback.ExtractionRejected);
                return;
            }

            PublishFeedback(HeroFeedback.NothingNearby);
        }

        // Presentation-safe proximity query. It only reports replicated/world state; the actual interaction
        // still goes through RequestInteractServerRpc and is re-validated by the server.
        public HeroInteractionKind GetNearbyInteractionKind()
        {
            if (definition == null || !CanAct || (RaidManager.Instance != null && RaidManager.Instance.IsOver))
                return HeroInteractionKind.None;
            return FindNearbyInteraction(out _, out _);
        }

        private HeroInteractionKind FindNearbyInteraction(
            out RaidLootSource nearestLoot,
            out ExtractionPoint nearestExtraction)
        {
            nearestLoot = null;
            nearestExtraction = null;
            if (definition == null) return HeroInteractionKind.None;

            var rangeSqr = definition.interactionRange * definition.interactionRange;
            var nearestLootSqr = rangeSqr;

            foreach (var source in FindObjectsByType<RaidLootSource>(FindObjectsSortMode.None))
            {
                if (source == null || source.IsDepleted) continue;
                var sqr = (source.transform.position - transform.position).sqrMagnitude;
                if (sqr > nearestLootSqr) continue;
                nearestLoot = source;
                nearestLootSqr = sqr;
            }

            // Loot has interaction priority when both a chest and extraction overlap.
            if (nearestLoot != null) return HeroInteractionKind.Loot;

            var nearestExtractionSqr = rangeSqr;
            foreach (var extraction in FindObjectsByType<ExtractionPoint>(FindObjectsSortMode.None))
            {
                if (extraction == null) continue;
                var sqr = (extraction.transform.position - transform.position).sqrMagnitude;
                if (sqr > nearestExtractionSqr) continue;
                nearestExtraction = extraction;
                nearestExtractionSqr = sqr;
            }
            return nearestExtraction != null ? HeroInteractionKind.Extraction : HeroInteractionKind.None;
        }

        protected override void HandleDeath()
        {
            if (!IsServer || definition == null) return;
            manualInput = Vector2.zero;
            normalAttackPending = false;
            pendingNormalAttackTarget = null;
            lifeState.Value = HeroLifeState.Downed;
            downedRemaining.Value = definition.downedWindowSeconds;
        }

        // Server-authoritative manual reborn. RaidHeroCharacter is never despawned on death, so restoring
        // health here naturally revives at the exact position where the Hero fell.
        public bool TryRevive()
        {
            if (!IsServer) return false;
            if (!CanReborn)
            {
                PublishFeedback(HeroFeedback.ReviveRejected);
                return false;
            }

            var health = Mathf.CeilToInt(definition.maxHealth * definition.reviveHealthPercent);
            if (!RestoreHealthFromZero(health))
            {
                PublishFeedback(HeroFeedback.ReviveRejected);
                return false;
            }

            revivesRemaining.Value--;
            lifeState.Value = HeroLifeState.Active;
            downedRemaining.Value = 0f;
            PublishFeedback(HeroFeedback.Revived, health);
            return true;
        }

        private void PublishFeedback(HeroFeedback feedback, int value = 0)
        {
            if (!IsServer) return;
            lastFeedback.Value = feedback;
            lastFeedbackValue.Value = value;
            feedbackSequence.Value = feedbackSequence.Value == uint.MaxValue ? 1u : feedbackSequence.Value + 1u;
        }

        private void HandleFeedbackSequenceChanged(uint previous, uint current)
        {
            if (current == 0 || current == previous) return;
            FeedbackReceived?.Invoke(lastFeedback.Value, lastFeedbackValue.Value);
        }

        [ContextMenu("Debug/Knock Down Hero")]
        private void DebugKnockDown()
        {
            if (!Application.isPlaying || !IsServer || !CanAct) return;

            // ApplyDamage is correctly reduced by Armor and absorbed by Shield. The old debug command sent
            // CurrentHealth as raw damage, so an armored Hero survived and kept walking. Convert the HP +
            // shield we must remove back into sufficient pre-mitigation damage, with one point of margin.
            var requiredAfterArmor = CurrentHealth + Shield;
            var rawDamage = Mathf.CeilToInt(requiredAfterArmor * (100f + Armor) / 100f) + 1;
            ApplyDamage(rawDamage);
        }

        [ContextMenu("Debug/Revive Hero")]
        private void DebugRevive()
        {
            if (!TryRevive()) Debug.LogWarning("[Hero] Revive rejected.", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, definition.interactionRange);
            if (definition.tacticalAbility != null)
                RangeGizmo.DrawFlatCircle(
                    transform.position,
                    definition.tacticalAbility.castRange,
                    new Color(1f, 0.55f, 0.1f));
        }
    }
}
