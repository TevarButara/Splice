using System.Collections.Generic;
using Splice.Characters;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // Server-authoritative three-ring progression. It polls authored objective records because normal
    // towers/garrisons despawn on death; each record retains its ring after the source component is gone.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class BreachRingController : NetworkBehaviour
    {
        private sealed class ObjectiveRecord
        {
            public BreachRing Ring;
            public CharacterBase Target;
        }

        public static BreachRingController Instance { get; private set; }

        [Header("Breach Rewards (secured prototype loot)")]
        [Min(0)] [SerializeField] private int outerRingSecuredBonus = 10;
        [Min(0)] [SerializeField] private int innerRingSecuredBonus = 20;
        [Min(0)] [SerializeField] private int coreRingSecuredBonus = 30;
        [Min(0.05f)] [SerializeField] private float refreshIntervalSeconds = 0.2f;
        [SerializeField] private RaidLootController lootController;

        private readonly NetworkVariable<int> outerTotal = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> outerRemaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> innerTotal = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> innerRemaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> coreTotal = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> coreRemaining = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> breachedRingCount = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> coreUnlocked = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<ObjectiveRecord> objectives = new();
        private bool objectivesInitialized;
        private bool runtimeReady;
        private int awardedBreachCount;
        private float nextRefreshAt;

        public int OuterTotal => outerTotal.Value;
        public int OuterRemaining => outerRemaining.Value;
        public int InnerTotal => innerTotal.Value;
        public int InnerRemaining => innerRemaining.Value;
        public int CoreTotal => coreTotal.Value;
        public int CoreRemaining => coreRemaining.Value;
        public int BreachedRingCount => breachedRingCount.Value;
        public bool CoreUnlocked => coreUnlocked.Value;
        public bool HasRingObjectives => OuterTotal + InnerTotal + CoreTotal > 0;
        public bool IsConfigurationValid => OuterTotal > 0 && InnerTotal > 0 && CoreTotal > 0;
        public bool CanExtract => BreachedRingCount > 0;
        public BreachRing CurrentRing => BreachedRingCount switch
        {
            <= 0 => BreachRing.Outer,
            1 => BreachRing.Inner,
            _ => BreachRing.Core
        };

        public int CurrentTotal => CurrentRing switch
        {
            BreachRing.Outer => OuterTotal,
            BreachRing.Inner => InnerTotal,
            _ => CoreTotal
        };

        public int CurrentRemaining => CurrentRing switch
        {
            BreachRing.Outer => OuterRemaining,
            BreachRing.Inner => InnerRemaining,
            _ => CoreRemaining
        };

        public int CurrentRingSecuredBonus => CurrentRing switch
        {
            BreachRing.Outer => outerRingSecuredBonus,
            BreachRing.Inner => innerRingSecuredBonus,
            _ => coreRingSecuredBonus
        };

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;

            breachedRingCount.Value = 0;
            coreUnlocked.Value = false;
            awardedBreachCount = 0;
            InitializeObjectives();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;
            if (!objectivesInitialized) InitializeObjectives();
            if (!runtimeReady)
            {
                if (!AreObjectivesRuntimeReady()) return;
                runtimeReady = true;
            }

            if (Time.time < nextRefreshAt) return;
            nextRefreshAt = Time.time + refreshIntervalSeconds;
            RefreshProgress();
        }

        private void InitializeObjectives()
        {
            objectives.Clear();
            var markers = FindObjectsByType<BreachRingObjective>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || !marker.Required || marker.Target == null || marker.Target is FortCore)
                    continue;
                objectives.Add(new ObjectiveRecord { Ring = marker.Ring, Target = marker.Target });
            }

            outerTotal.Value = CountTotal(BreachRing.Outer);
            innerTotal.Value = CountTotal(BreachRing.Inner);
            coreTotal.Value = CountTotal(BreachRing.Core);
            outerRemaining.Value = outerTotal.Value;
            innerRemaining.Value = innerTotal.Value;
            coreRemaining.Value = coreTotal.Value;
            objectivesInitialized = objectives.Count > 0;
            runtimeReady = false;

            if (!IsConfigurationValid)
                Debug.LogError(
                    $"[BreachRings] Invalid setup: Outer={OuterTotal}, Inner={InnerTotal}, Core={CoreTotal}. " +
                    "Every ring needs at least one required objective.",
                    this);
        }

        private bool AreObjectivesRuntimeReady()
        {
            if (!objectivesInitialized || !IsConfigurationValid) return false;
            for (var i = 0; i < objectives.Count; i++)
            {
                var target = objectives[i].Target;
                if (target == null || !target.IsSpawned || target.MaxHealth <= 0) return false;
            }
            return true;
        }

        private void RefreshProgress()
        {
            outerRemaining.Value = CountRemaining(BreachRing.Outer);
            innerRemaining.Value = CountRemaining(BreachRing.Inner);
            coreRemaining.Value = CountRemaining(BreachRing.Core);

            var sequentialBreaches = 0;
            if (OuterTotal > 0 && OuterRemaining <= 0)
            {
                sequentialBreaches = 1;
                if (InnerTotal > 0 && InnerRemaining <= 0)
                {
                    sequentialBreaches = 2;
                    if (CoreTotal > 0 && CoreRemaining <= 0) sequentialBreaches = 3;
                }
            }

            if (sequentialBreaches > breachedRingCount.Value)
            {
                for (var completed = awardedBreachCount; completed < sequentialBreaches; completed++)
                    AwardRingBreach((BreachRing)completed);
                awardedBreachCount = sequentialBreaches;
                breachedRingCount.Value = sequentialBreaches;
            }

            coreUnlocked.Value = sequentialBreaches >= 3;
        }

        private void AwardRingBreach(BreachRing ring)
        {
            if (lootController == null) lootController = FindFirstObjectByType<RaidLootController>();
            var amount = ring switch
            {
                BreachRing.Outer => outerRingSecuredBonus,
                BreachRing.Inner => innerRingSecuredBonus,
                _ => coreRingSecuredBonus
            };
            var granted = lootController != null ? lootController.GrantSecuredBreachLoot(amount) : 0;
            Debug.Log($"[BreachRings] {ring} breached. Secured loot +{granted}.", this);
        }

        private int CountTotal(BreachRing ring)
        {
            var count = 0;
            for (var i = 0; i < objectives.Count; i++)
                if (objectives[i].Ring == ring) count++;
            return count;
        }

        private int CountRemaining(BreachRing ring)
        {
            var count = 0;
            for (var i = 0; i < objectives.Count; i++)
            {
                var record = objectives[i];
                if (record.Ring != ring) continue;
                if (record.Target != null && !record.Target.IsDead) count++;
            }
            return count;
        }

        [ContextMenu("Debug/Refresh Breach Ring State")]
        private void DebugRefresh()
        {
            if (IsServer) RefreshProgress();
        }
    }
}
