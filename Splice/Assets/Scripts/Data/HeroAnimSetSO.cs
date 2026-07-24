using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "AnimSet_Hero", menuName = "Splice/Hero Animation Set")]
    public sealed class HeroAnimSetSO : ScriptableObject
    {
        [Header("Locomotion")]
        public string idle = "Idle";
        public string walk = "Walk";

        [Header("Combat & Life")]
        public string attack = "Attack";
        public string downed = "Idle";
        public string defeated = "Death";
        public string victory = "Win";
        public string defeat = "Lose";
    }
}
