using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    // Single entry point to every faction's content. Resolves the composite network id "factionId/localId"
    // to a card/tower and back. Add a faction by dropping its FactionSO into `factions` — no code changes.
    [CreateAssetMenu(fileName = "FactionRegistry", menuName = "Splice/Faction Registry")]
    public class FactionRegistrySO : ScriptableObject
    {
        [SerializeField] private List<FactionSO> factions = new();
        public IReadOnlyList<FactionSO> Factions => factions;

        public FactionSO GetFaction(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;
            foreach (var faction in factions)
                if (faction != null && faction.factionId == factionId) return faction;
            return null;
        }

        private Dictionary<string, CardDefinitionSO> cardById;
        private Dictionary<CardDefinitionSO, string> idByCard;
        private Dictionary<string, TowerDefinitionSO> towerById;
        private Dictionary<TowerDefinitionSO, string> idByTower;

        public static string CardId(FactionSO faction, CardDefinitionSO card) => $"{faction.factionId}/{card.cardId}";
        public static string TowerId(FactionSO faction, TowerDefinitionSO tower) => $"{faction.factionId}/{tower.towerId}";

        public CardDefinitionSO ResolveCard(string id) { EnsureBuilt(); return cardById.GetValueOrDefault(id); }
        public TowerDefinitionSO ResolveTower(string id) { EnsureBuilt(); return towerById.GetValueOrDefault(id); }

        public string IdOf(CardDefinitionSO card)
        {
            EnsureBuilt();
            return card != null && idByCard.TryGetValue(card, out var id) ? id : null;
        }

        public string IdOf(TowerDefinitionSO tower)
        {
            EnsureBuilt();
            return tower != null && idByTower.TryGetValue(tower, out var id) ? id : null;
        }

        public List<CardDefinitionSO> AllCards()
        {
            var result = new List<CardDefinitionSO>();
            foreach (var faction in factions)
            {
                if (faction == null) continue;
                foreach (var card in faction.cards)
                    if (card != null) result.Add(card);
            }
            return result;
        }

        // Registry caches are runtime optimizations only. Invalidate after authoring changes so Resolve*
        // cannot retain definitions that were removed or renamed in the Inspector.
        public void InvalidateCache()
        {
            cardById = null;
            idByCard = null;
            towerById = null;
            idByTower = null;
        }

        private void OnEnable() => InvalidateCache();
        private void OnValidate() => InvalidateCache();

        private void EnsureBuilt()
        {
            if (cardById != null) return;
            cardById = new Dictionary<string, CardDefinitionSO>();
            idByCard = new Dictionary<CardDefinitionSO, string>();
            towerById = new Dictionary<string, TowerDefinitionSO>();
            idByTower = new Dictionary<TowerDefinitionSO, string>();

            foreach (var faction in factions)
            {
                if (faction == null) continue;
                foreach (var card in faction.cards)
                {
                    if (card == null) continue;
                    var id = CardId(faction, card);
                    cardById[id] = card;
                    idByCard[card] = id;
                }
                foreach (var card in faction.minerCards)
                {
                    if (card == null) continue;
                    var id = CardId(faction, card);
                    cardById[id] = card;
                    idByCard[card] = id;
                }
                foreach (var tower in faction.towers)
                {
                    if (tower == null) continue;
                    var id = TowerId(faction, tower);
                    towerById[id] = tower;
                    idByTower[tower] = id;
                }
            }
        }
    }
}
