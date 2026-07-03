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
        public MonsterDefinitionSO linkedMonster;
        public Sprite artwork;
        [TextArea] public string description;
    }
}
