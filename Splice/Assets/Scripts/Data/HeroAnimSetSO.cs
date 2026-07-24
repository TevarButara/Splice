using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "AnimSet_Hero", menuName = "Splice/Hero Animation Set")]
    public sealed class HeroAnimSetSO : ScriptableObject
    {
        [Header("Locomotion")]
        public string idle = "Idle";
        public string walk = "Walk";
        public string sprint = "Sprint";
        public string landing = "Landing";

        [Header("Normal Attack")]
        [FormerlySerializedAs("attack")] public string attack1 = "Attack";
        [Tooltip("A second normal attack state. It may temporarily match Attack 1 until a second clip is authored.")]
        public string attack2 = "Attack";

        [Header("Hero Skills")]
        public string skill1 = "Skill1";
        public string skill2 = "Skill2";
        public string skill3 = "Skill3";

        [Header("Life & Results")]
        [FormerlySerializedAs("defeated")] public string death = "Death";
        [FormerlySerializedAs("victory")] public string win = "Win";
        [FormerlySerializedAs("defeat")] public string lose = "Lose";
        public string dance = "Dance";
    }
}
