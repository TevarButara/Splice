using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    // One faction as a single data asset: its monster cards + its towers, grouped together. Adding a faction
    // is pure data — create a FactionSO, fill the two lists, and drop it into the FactionRegistry (no code).
    // id ของ card/tower ต้อง unique เฉพาะ "ภายในเผ่านี้" เท่านั้น (network id = factionId/localId ประกอบให้เอง).
    [CreateAssetMenu(fileName = "NewFaction", menuName = "Splice/Faction")]
    public class FactionSO : ScriptableObject
    {
        [Tooltip("id เสถียรของเผ่า (ใช้ประกอบ network id) — unique ทั้งเกม ห้ามมี '/'. เช่น \"human\", \"demon\"")]
        public string factionId;
        public string displayName;
        [Tooltip("หมวดใหญ่ (visual/lore) ที่เผ่านี้สังกัด")]
        public FactionFamily family;
        public Sprite icon;
        public Color color = Color.white;

        [Tooltip("มอน (การ์ด) ของเผ่านี้ — cardId แต่ละใบ unique เฉพาะในลิสต์นี้พอ")]
        public List<CardDefinitionSO> cards = new();
        [Tooltip("miner (การ์ด) ของเผ่านี้ — cardType = Miner, linkedMiner ตั้งไว้. cardId unique ในเผ่าพอ")]
        public List<CardDefinitionSO> minerCards = new();
        [Tooltip("ป้อมของเผ่านี้ — towerId แต่ละตัว unique เฉพาะในลิสต์นี้พอ")]
        public List<TowerDefinitionSO> towers = new();
    }
}
