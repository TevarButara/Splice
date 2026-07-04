using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Data
{
    public enum CardType
    {
        Monster,
        Spell,
        Upgrade
    }

    [CreateAssetMenu(fileName = "NewCard", menuName = "Splice/Card Definition")]
    public class CardDefinitionSO : ScriptableObject
    {
        public string cardId;
        public string displayName;
        public CardType cardType;
        [FormerlySerializedAs("manaCost")] public int goldCost;
        [Tooltip("เลเวลผู้เล่นขั้นต่ำที่ปลดล็อกการ์ดนี้ — ถ้า level ปัจจุบันต่ำกว่า การ์ดจะเป็นสีเทาและเรียกไม่ได้")]
        public int requiredLevel = 1;
        public MonsterDefinitionSO linkedMonster;
        public Sprite artwork;
        [TextArea] public string description;
    }
}
