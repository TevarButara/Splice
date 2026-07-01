using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // Mana regen is a server-side timer so clients cannot speed up regen locally (architecture 5.1).
    public class ManaController : NetworkBehaviour
    {
        [SerializeField] private int maxMana = 10;
        [SerializeField] private float regenIntervalSeconds = 1.5f;

        private readonly NetworkVariable<int> currentMana = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float regenTimer;

        public int CurrentMana => currentMana.Value;

        private void Update()
        {
            if (!IsServer) return;

            regenTimer += Time.deltaTime;
            if (regenTimer < regenIntervalSeconds) return;

            regenTimer = 0f;
            if (currentMana.Value < maxMana)
            {
                currentMana.Value++;
            }
        }

        public bool TrySpend(int amount)
        {
            if (!IsServer || currentMana.Value < amount) return false;
            currentMana.Value -= amount;
            return true;
        }
    }
}
