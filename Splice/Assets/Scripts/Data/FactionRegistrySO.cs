using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    // Single entry point to every faction's content. Resolves the composite network id "factionId/localId"
    // to a card/tower and back. Add a faction by dropping its FactionSO into `factions` — no code changes.
    // (Server RPCs send the composite id string; this maps it back to the definition.)
    [CreateAssetMenu(fileName = "FactionRegistry", menuName = "Splice/Faction Registry")]
    public class FactionRegistrySO : ScriptableObject
    {
        [SerializeField] private List<FactionSO> factions = new();

        public IReadOnlyList<FactionSO> Factions => factions;

        // หา FactionSO จาก factionId — ใช้ resolve เผ่าที่ผู้เล่นเลือก (ActiveFactionId) เพื่อดึง towers/cards
        // ไปทำ palette แบบ dynamic ฯลฯ
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

        // Composite ids are stable across list reordering (built from ids, not indices).
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

        // Flat list of every faction's cards — e.g. for a draft pool (scope by faction later when needed).
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

                // Miner cards resolve through the same id space (composite factionId/cardId).
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
