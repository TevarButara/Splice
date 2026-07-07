using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Data
{
    public enum CardType
    {
        Monster,
        Miner,
        Spell,
        Upgrade
    }

    [CreateAssetMenu(fileName = "NewCard", menuName = "Splice/Card Definition")]
    public class CardDefinitionSO : ScriptableObject
    {
        [Tooltip("id เฉพาะในเผ่าตัวเอง (local) — ไม่ต้อง unique ทั้งเกม. network id เต็ม = factionId/cardId ประกอบให้เองใน FactionRegistry")]
        public string cardId;
        public string displayName;
        public CardType cardType;
        [FormerlySerializedAs("manaCost")] public int goldCost;
        [Tooltip("เลเวลผู้เล่นขั้นต่ำที่ปลดล็อกการ์ดนี้ — ถ้า level ปัจจุบันต่ำกว่า การ์ดจะเป็นสีเทาและเรียกไม่ได้")]
        public int requiredLevel = 1;
        [Tooltip("การ์ดมอน → ใส่ตรงนี้")]
        public MonsterDefinitionSO linkedMonster;
        [Tooltip("การ์ด miner → ใส่ตรงนี้ (cardType = Miner)")]
        public MinerDefinitionSO linkedMiner;
        public Sprite artwork;
        [TextArea] public string description;
    }
}
