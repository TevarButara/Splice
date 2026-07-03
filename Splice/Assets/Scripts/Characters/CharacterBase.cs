using System;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Characters
{
    // Health lives on the server only; NetworkVariable replicates it to clients for UI (architecture 3, 10).
    public abstract class CharacterBase : NetworkBehaviour
    {
        public event Action<CharacterBase> OnDeath;

        private readonly NetworkVariable<int> currentHealth = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> maxHealth = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int CurrentHealth => currentHealth.Value;
        public int MaxHealth => maxHealth.Value;
        public bool IsDead => currentHealth.Value <= 0;

        protected void InitializeHealth(int max)
        {
            if (!IsServer) return;
            maxHealth.Value = max;
            currentHealth.Value = max;
        }

        // Server-only heal, clamped to MaxHealth. A dead character can't be healed — resurrection would
        // need a separate spawn path, and the Fort/tower repair flow only ever targets living towers.
        public void Heal(int amount)
        {
            if (!IsServer || IsDead || amount <= 0) return;
            currentHealth.Value = Mathf.Min(maxHealth.Value, currentHealth.Value + amount);
        }

        public void ApplyDamage(int amount)
        {
            if (!IsServer || IsDead || amount <= 0) return;

            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
            if (currentHealth.Value == 0)
            {
                OnDeath?.Invoke(this);
                if (NetworkObject.IsSpawned)
                {
                    // In-scene placed objects (e.g. a Fort/Tower dropped into the scene, not runtime-Instantiate'd)
                    // must not be destroyed on despawn per Netcode's guidance — only runtime-spawned ones should be.
                    NetworkObject.Despawn(destroy: NetworkObject.IsSceneObject != true);
                }
            }
        }
    }
}
