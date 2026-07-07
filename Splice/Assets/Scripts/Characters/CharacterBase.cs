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

        // Percentage damage reduction: incoming dmg × 100/(100+armor). Server-authoritative.
        private readonly NetworkVariable<int> armor = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int CurrentHealth => currentHealth.Value;
        public int MaxHealth => maxHealth.Value;
        public int Armor => armor.Value;
        public bool IsDead => currentHealth.Value <= 0;

        protected void SetArmor(int value)
        {
            if (IsServer) armor.Value = Mathf.Max(0, value);
        }

        // Server-only: permanently raise max HP (e.g. an HP upgrade) and add the same amount to current HP.
        public void RaiseMaxHealth(int delta)
        {
            if (!IsServer || IsDead || delta <= 0) return;
            maxHealth.Value += delta;
            currentHealth.Value += delta;
        }

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

            // Armor mitigates by percentage; always at least 1 so armor can never fully negate a hit.
            var mitigated = Mathf.Max(1, Mathf.RoundToInt(amount * 100f / (100 + Mathf.Max(0, armor.Value))));
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - mitigated);
            if (currentHealth.Value == 0)
            {
                OnDeath?.Invoke(this);
                HandleDeath();
            }
        }

        // What happens to the object once HP hits 0. Default: remove it from the world. Overridable so
        // special characters (e.g. the Fort core) can stay in the scene as a destroyed husk for a death
        // animation. `IsDead` is already true here regardless of whether the object is despawned.
        protected virtual void HandleDeath()
        {
            if (NetworkObject.IsSpawned)
            {
                // In-scene placed objects (e.g. a Fort/Tower dropped into the scene, not runtime-Instantiate'd)
                // must not be destroyed on despawn per Netcode's guidance — only runtime-spawned ones should be.
                NetworkObject.Despawn(destroy: NetworkObject.IsSceneObject != true);
            }
        }
    }
}
