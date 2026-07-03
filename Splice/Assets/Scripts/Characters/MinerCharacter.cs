using System.Collections.Generic;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Splice.Characters
{
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
        [SerializeField] private Team team = Team.Invaders;

        private NavMeshAgent agent;
        private MinerState state = MinerState.SeekingNode;
        private GoldNode targetNode;
        private bool baseRouteIssued;
        private int carrying;
        private float mineTimer;

        public Team Team => team;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void Initialize(MinerDefinitionSO minerDefinition, Team owningTeam)
        {
            definition = minerDefinition;
            team = owningTeam;
            InitializeHealth(definition.maxHealth);
            ApplyDefinitionToAgent();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            agent.enabled = IsServer;
            if (!IsServer) return;

            active.Add(this);
            if (definition != null && CurrentHealth <= 0) InitializeHealth(definition.maxHealth);
            ApplyDefinitionToAgent();
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            base.OnNetworkDespawn();
        }

        private void ApplyDefinitionToAgent()
        {
            if (agent == null || !agent.enabled || definition == null) return;
            agent.speed = definition.moveSpeed;
            agent.stoppingDistance = definition.arrivalRadius;
        }

        private void Update()
        {
            if (!IsServer || IsDead || definition == null) return;

            switch (state)
            {
                case MinerState.SeekingNode: TickSeeking(); break;
                case MinerState.Mining: TickMining(); break;
                case MinerState.Returning: TickReturning(); break;
            }
        }

        private void TickSeeking()
        {
            if (targetNode == null || targetNode.IsDepleted)
            {
                targetNode = FindNearestNode();
                if (targetNode == null) return; // no gold left anywhere — idle until a node refills/appears
                // Issue the path once on acquisition. Don't compare agent.destination each frame —
                // it returns the NavMesh-snapped point, never exactly the node pivot, so a re-check
                // would re-path forever and never reach the arrival test below.
                agent.SetDestination(targetNode.transform.position);
                return;
            }

            if (agent.pathPending) return;
            // "Arrived" = touching the node's collider, not reaching its pivot — so a big node
            // doesn't force a big arrivalRadius.
            if (targetNode.DistanceToSurface(transform.position) > definition.arrivalRadius) return;
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
            var home = MinerBase.For(team);
            if (home == null) return; // base not wired — hold cargo until one exists

            if (!baseRouteIssued)
            {
                agent.SetDestination(home.transform.position);
                baseRouteIssued = true;
                return;
            }

            if (agent.pathPending) return;
            // Base has no collider and isn't carved, so agent.remainingDistance is reliable here.
            // (Raw Vector3.Distance would include any Y offset if the base is placed above the floor,
            // and could stay above arrivalRadius forever.)
            if (agent.remainingDistance > agent.stoppingDistance) return;

            var bank = GoldController.For(team);
            if (bank != null) bank.Add(carrying);
            carrying = 0;
            targetNode = null;
            state = MinerState.SeekingNode;
        }

        private GoldNode FindNearestNode()
        {
            GoldNode nearest = null;
            var nearestDistance = float.MaxValue;
            var nodes = GoldNode.Active;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.IsDepleted) continue;

                var distance = Vector3.Distance(transform.position, node.transform.position);
                if (distance > nearestDistance) continue;

                nearest = node;
                nearestDistance = distance;
            }

            return nearest;
        }
    }
}
