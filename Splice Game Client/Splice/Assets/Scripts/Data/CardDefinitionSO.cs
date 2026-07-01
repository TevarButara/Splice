using UnityEngine;

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
        public int manaCost;
        public MonsterDefinitionSO linkedMonster;
        public Sprite artwork;
        [TextArea] public string description;
    }
}
