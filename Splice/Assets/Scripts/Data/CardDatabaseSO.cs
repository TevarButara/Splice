using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Splice/Card Database")]
    public class CardDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<CardDefinitionSO> cards = new();

        private Dictionary<string, CardDefinitionSO> lookup;

        public IReadOnlyList<CardDefinitionSO> AllCards => cards;

        public CardDefinitionSO GetById(string cardId)
        {
            lookup ??= BuildLookup();
            return lookup.GetValueOrDefault(cardId);
        }

        private Dictionary<string, CardDefinitionSO> BuildLookup()
        {
            var map = new Dictionary<string, CardDefinitionSO>();
            foreach (var card in cards)
            {
                if (card != null) map[card.cardId] = card;
            }
            return map;
        }
    }
}
