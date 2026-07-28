using UnityEngine;

namespace Splice.Data
{
    public enum MapGameMode
    {
        Town,
        AsyncRaid,
        World,
        Forest,
        PvP,
    }

    [CreateAssetMenu(menuName = "Splice/Maps/Map Definition", fileName = "MapDefinition")]
    public class MapDefinitionSO : ScriptableObject
    {
        [SerializeField] private string mapId = "map-default";
        [Min(1), SerializeField] private int mapVersion = 1;
        [SerializeField] private MapGameMode gameMode;
        [SerializeField] private string sceneName;
        [SerializeField] private Vector3 cameraFocus;
        [Min(1f), SerializeField] private float cameraRadius = 40f;

        public string MapId => mapId;
        public int MapVersion => Mathf.Max(1, mapVersion);
        public MapGameMode GameMode => gameMode;
        public string SceneName => sceneName;
        public Vector3 CameraFocus => cameraFocus;
        public float CameraRadius => Mathf.Max(1f, cameraRadius);
    }

    public enum WorldNodeKind
    {
        PlayerTown,
        RaidTarget,
        Forest,
        PvP,
        Locked,
    }

}
