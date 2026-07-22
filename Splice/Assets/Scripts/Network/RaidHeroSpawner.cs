using Splice.Base;
using Splice.Characters;
using Splice.Core;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Network
{
    // Server spawns the selected raid Hero. Prototype ownership goes to the local host client; dedicated
    // server attacker assignment replaces ResolveOwnerClientId in a later networking phase.
    public class RaidHeroSpawner : NetworkBehaviour
    {
        [SerializeField] private HeroRegistrySO registry;
        [SerializeField] private Transform spawnPoint;
        [Tooltip("ใช้เมื่อ profile ยังไม่ได้เลือก Hero — เว้นว่าง = ตัวแรกใน registry")]
        [SerializeField] private string debugFallbackHeroId;

        private bool waitForCommittedSession;
        private bool spawnAttempted;

        public Transform SpawnPoint => spawnPoint;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            waitForCommittedSession = FindFirstObjectByType<RaidSceneAdapter>() != null;
            TrySpawnWhenReady();
        }

        public override void OnNetworkDespawn()
        {
            spawnAttempted = false;
            base.OnNetworkDespawn();
        }

        private void Update() => TrySpawnWhenReady();

        private void TrySpawnWhenReady()
        {
            if (!IsSpawned || !IsServer || spawnAttempted || RaidHeroCharacter.Instance != null) return;
            if (waitForCommittedSession && !RaidSessionContext.IsStarted) return;
            spawnAttempted = true;
            SpawnSelectedHero();
        }

        private void SpawnSelectedHero()
        {
            var definition = ResolveDefinition();
            if (definition == null || definition.prefab == null)
            {
                Debug.LogError("[HeroSpawner] ไม่พบ Hero definition/prefab สำหรับ spawn", this);
                return;
            }

            var position = spawnPoint != null ? spawnPoint.position : transform.position;
            var rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            var instance = Instantiate(definition.prefab, position, rotation);
            var networkObject = instance.GetComponent<NetworkObject>();
            var hero = instance.GetComponent<RaidHeroCharacter>();
            if (networkObject == null || hero == null)
            {
                Debug.LogError($"[HeroSpawner] prefab '{definition.prefab.name}' ต้องมี NetworkObject + RaidHeroCharacter", this);
                Destroy(instance);
                return;
            }

            networkObject.SpawnWithOwnership(ResolveOwnerClientId());
            hero.Initialize(definition, RaidSide.Attacker);
        }

        private HeroDefinitionSO ResolveDefinition()
        {
            if (registry == null) return null;
            var selected = registry.Resolve(PlayerHeroProfile.SelectedHeroId);
            if (selected != null) return selected;
            var fallback = registry.Resolve(debugFallbackHeroId);
            if (fallback != null) return fallback;
            return registry.Heroes.Count > 0 ? registry.Heroes[0] : null;
        }

        private ulong ResolveOwnerClientId()
        {
            return NetworkManager.ServerClientId;
        }
    }
}
