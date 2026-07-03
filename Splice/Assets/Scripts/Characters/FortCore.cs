namespace Splice.Characters
{
    // The single win/lose objective per raid: monsters win by destroying this (architecture 5.1).
    public class FortCore : TowerCharacter
    {
        public static FortCore Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }
    }
}
