using System.Collections.Generic;
using Splice.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Draft
{
    // Server-seeded card draft; clients only ever receive the resulting hand (architecture 5.2).
    // Permanent unlocks come from the Lair meta and are applied to cardPool before drawing.
    public class DraftManager : NetworkBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [SerializeField] private int handSize = 4;

        private readonly NetworkList<FixedString32Bytes> currentHand = new();

        public NetworkList<FixedString32Bytes> CurrentHand => currentHand;

        public override void OnNetworkSpawn()
        {
            if (IsServer) DrawNewHand();
        }

        public void DrawNewHand()
        {
            if (!IsServer) return;

            currentHand.Clear();
            var pool = registry.AllCards();

            for (var i = 0; i < handSize && pool.Count > 0; i++)
            {
                var index = Random.Range(0, pool.Count);
                currentHand.Add(new FixedString32Bytes(registry.IdOf(pool[index])));   // composite id: factionId/cardId
                pool.RemoveAt(index);
            }
        }
    }
}
