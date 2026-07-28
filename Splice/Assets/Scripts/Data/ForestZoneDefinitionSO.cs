using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(menuName = "Splice/Maps/Forest Zone Definition", fileName = "ForestZoneDefinition")]
    public sealed class ForestZoneDefinitionSO : MapDefinitionSO
    {
        [SerializeField] private string zoneId = "forest-01";
        [Min(1), SerializeField] private int encounterDurationSeconds = 60;
        [Min(1), SerializeField] private int monsterCount = 6;
        [Min(0), SerializeField] private int fragmentDropMin = 1;
        [Min(0), SerializeField] private int fragmentDropMax = 3;
        [Min(1), SerializeField] private int fragmentsPerDiamond = 100;
        [Min(0), SerializeField] private int weeklyDiamondCap = 3;

        public string ZoneId => zoneId;
        public int EncounterDurationSeconds => Mathf.Max(1, encounterDurationSeconds);
        public int MonsterCount => Mathf.Max(1, monsterCount);
        public int FragmentDropMin => Mathf.Max(0, fragmentDropMin);
        public int FragmentDropMax => Mathf.Max(FragmentDropMin, fragmentDropMax);
        public int FragmentsPerDiamond => Mathf.Max(1, fragmentsPerDiamond);
        public int WeeklyDiamondCap => Mathf.Max(0, weeklyDiamondCap);
    }
}
