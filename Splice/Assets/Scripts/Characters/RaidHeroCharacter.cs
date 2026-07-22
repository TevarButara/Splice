using Splice.Combat;
using Splice.Core;
using Splice.Data;
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
        AbilityOutOfRange,
        AbilityNoTargets,
        AbilityUnavailable,
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
        public static RaidHeroCharacter Instance { get; private set; }

        [SerializeField] private HeroDefinitionSO definition;
        [SerializeField] private RaidSide side = RaidSide.Attacker;
        [Tooltip("ขอบเขตตำแหน่ง Hero — เว้นว่าง = ไม่ clamp")]
        [SerializeField] private BoxCollider movementBounds;

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
        private readonly NetworkVariable<float> downedRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> revivesRemaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> tacticalAbilityCooldownRemaining = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
        private static readonly System.Collections.Generic.List<CharacterBase> abilityTargets = new();

        public event System.Action<HeroFeedback, int> FeedbackReceived;

        public HeroDefinitionSO Definition => definition;
        public RaidSide Side => side;
        public HeroControlMode ControlMode => controlMode.Value;
        public HeroLifeState LifeState => lifeState.Value;
        public float DownedRemaining => downedRemaining.Value;
        public int RevivesRemaining => revivesRemaining.Value;
        public HeroAbilityDefinitionSO TacticalAbility => definition != null ? definition.tacticalAbility : null;
        public float TacticalAbilityCooldownRemaining => tacticalAbilityCooldownRemaining.Value;
        public bool IsTacticalAbilityReady => TacticalAbility != null && tacticalAbilityCooldownRemaining.Value <= 0f;
        public bool HasFocusTarget => hasFocusTarget.Value;
        public bool CanAct => lifeState.Value == HeroLifeState.Active && !IsDead;
        public float SquadCommandRadius => squadCommandRadius;

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
            Instance = this;
            feedbackSequence.OnValueChanged += HandleFeedbackSequenceChanged;
            if (IsServer && definition != null && CurrentHealth <= 0) InitializeFromDefinition();
        }

        public override void OnNetworkDespawn()
        {
            feedbackSequence.OnValueChanged -= HandleFeedbackSequenceChanged;
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void InitializeFromDefinition()
        {
            InitializeHealth(definition.maxHealth);
            SetArmor(definition.armor);
            controlMode.Value = definition.startsInAutoMode ? HeroControlMode.Auto : HeroControlMode.Manual;
            lifeState.Value = HeroLifeState.Active;
            downedRemaining.Value = 0f;
            revivesRemaining.Value = Mathf.Max(0, definition.maxRevivesPerRaid);
            tacticalAbilityCooldownRemaining.Value = 0f;
            hasFocusTarget.Value = false;
            focusTarget.Value = default;
            lastFeedback.Value = HeroFeedback.None;
            lastFeedbackValue.Value = 0;
            manualInput = Vector2.zero;
        }

        private void Update()
        {
            if (!IsServer || definition == null) return;
            TickShield();
            if (tacticalAbilityCooldownRemaining.Value > 0f)
                tacticalAbilityCooldownRemaining.Value = Mathf.Max(
                    0f, tacticalAbilityCooldownRemaining.Value - Time.deltaTime);

            if (hasFocusTarget.Value && !TryResolveFocusTarget(out _))
                ClearFocusTarget(HeroFeedback.FocusTargetCompleted);

            if (lifeState.Value == HeroLifeState.Downed)
            {
                downedRemaining.Value = Mathf.Max(0f, downedRemaining.Value - Time.deltaTime);
                if (downedRemaining.Value <= 0f) lifeState.Value = HeroLifeState.Defeated;
                return;
            }

            if (!CanAct || (RaidManager.Instance != null && RaidManager.Instance.IsOver)) return;

            attackTimer += Time.deltaTime;
            TickCombat();

            if (controlMode.Value == HeroControlMode.Manual) TickManualMovement();
            else TickAutoMovement();
        }

        private void TickCombat()
        {
            if (attackTimer < definition.attackCooldown) return;
            var hasOrder = TryResolveFocusTarget(out var orderedTarget);
            var target = hasOrder
                ? IsWithinHorizontalRange(orderedTarget, definition.attackRange) ? orderedTarget : null
                : FindNearestEnemy(definition.attackRange);
            if (target == null) return;

            Face(target.transform.position - transform.position);
            target.ApplyDamage(definition.attackDamage, this);
            attackTimer = 0f;
        }

        private void TickManualMovement()
        {
            if (Time.time - lastManualInputTime > 0.35f) manualInput = Vector2.zero;
            Move(new Vector3(manualInput.x, 0f, manualInput.y));
        }

        private void TickAutoMovement()
        {
            var target = TryResolveFocusTarget(out var orderedTarget)
                ? orderedTarget
                : FindNearestEnemy(definition.autoAggroRange);
            if (target == null) target = FortCore.Instance;
            if (target == null || target.IsDead) return;

            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude <= definition.attackRange * 0.9f) return;
            Move(delta.normalized);
        }

        private void Move(Vector3 direction)
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

            transform.position = position;
            Face(direction);
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

        private static void Consider(CharacterBase candidate, Vector3 position, ref CharacterBase nearest, ref float bestSqr)
        {
            if (candidate == null || candidate.IsDead) return;
            var sqr = (candidate.transform.position - position).sqrMagnitude;
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
            focusTarget.Value = requestedTarget;
            hasFocusTarget.Value = true;
            var assignedUnitCount = IssueSquadFocusOrder(target);
            PublishFeedback(HeroFeedback.FocusTargetSet, assignedUnitCount);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestClearFocusTargetServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            ClearFocusTarget(HeroFeedback.FocusTargetCleared);
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

            var ability = TacticalAbility;
            if (ability == null || !CanAct || (RaidManager.Instance != null && RaidManager.Instance.IsOver))
            {
                PublishFeedback(HeroFeedback.AbilityUnavailable);
                return;
            }

            if (tacticalAbilityCooldownRemaining.Value > 0f)
            {
                PublishFeedback(
                    HeroFeedback.AbilityCooldown,
                    Mathf.CeilToInt(tacticalAbilityCooldownRemaining.Value));
                return;
            }

            if (!IsFinite(requestedTargetPoint))
            {
                PublishFeedback(HeroFeedback.AbilityOutOfRange);
                return;
            }

            var targetPoint = requestedTargetPoint;
            // Preserve the ground hit height for presentation, but cap vertical input so a client cannot
            // spawn the cosmetic effect at an arbitrary altitude. Damage/range always use XZ distance.
            targetPoint.y = Mathf.Clamp(targetPoint.y, transform.position.y - 3f, transform.position.y + 3f);
            if (HorizontalSqrDistance(transform.position, targetPoint) > ability.castRange * ability.castRange)
            {
                PublishFeedback(HeroFeedback.AbilityOutOfRange);
                return;
            }

            var hitCount = CollectBreachChargeTargets(targetPoint, ability.effectRadius);
            if (hitCount <= 0)
            {
                PublishFeedback(HeroFeedback.AbilityNoTargets);
                return;
            }

            for (var i = 0; i < abilityTargets.Count; i++)
            {
                var target = abilityTargets[i];
                if (target != null && !target.IsDead) target.ApplyDamage(ability.damage, this);
            }

            tacticalAbilityCooldownRemaining.Value = ability.cooldownSeconds;
            PublishFeedback(HeroFeedback.AbilityCast, hitCount);
            PlayTacticalAbilityEffectClientRpc(targetPoint);
        }

        // Lets the target-mode button produce server-confirmed feedback without entering targeting while the
        // Hero is downed or the ability is cooling down.
        [ServerRpc(RequireOwnership = false)]
        public void RequestTacticalAbilityStatusFeedbackServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!CanControl(rpcParams.Receive.SenderClientId)) return;
            if (TacticalAbility == null || !CanAct)
            {
                PublishFeedback(HeroFeedback.AbilityUnavailable);
                return;
            }

            if (tacticalAbilityCooldownRemaining.Value > 0f)
                PublishFeedback(
                    HeroFeedback.AbilityCooldown,
                    Mathf.CeilToInt(tacticalAbilityCooldownRemaining.Value));
        }

        private bool CanControl(ulong senderClientId) =>
            senderClientId == OwnerClientId || senderClientId == NetworkManager.ServerClientId;

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

        private bool IsValidFocusTarget(CharacterBase target)
        {
            if (target == null || target.IsDead) return false;
            if (target is TowerCharacter tower) return tower is not FortCore;
            return target is MonsterCharacter monster && monster.Side != side;
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

        private int CollectBreachChargeTargets(Vector3 targetPoint, float radius)
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
            return abilityTargets.Count;
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
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
        private void PlayTacticalAbilityEffectClientRpc(Vector3 targetPoint)
        {
            var ability = TacticalAbility;
            if (ability == null || ability.castEffectPrefab == null) return;
            var effect = Instantiate(ability.castEffectPrefab, targetPoint, Quaternion.identity);
            if (ability.castEffectLifetime > 0f) Destroy(effect, ability.castEffectLifetime);
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
            lifeState.Value = HeroLifeState.Downed;
            downedRemaining.Value = definition.downedWindowSeconds;
        }

        // Server-side ally/revive-point API. The owner cannot call this through input in step 3;
        // ContextMenu is provided only for Editor acceptance.
        public bool TryRevive()
        {
            if (!IsServer) return false;
            if (lifeState.Value != HeroLifeState.Downed || definition == null || revivesRemaining.Value <= 0)
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
