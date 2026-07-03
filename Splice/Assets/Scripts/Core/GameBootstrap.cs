using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Splice.Core
{
    public enum GameMode
    {
        PvE,
        PvBot,
        PvP
    }

    // Single entry point for all 3 phases (technical-architecture.md 4.2).
    // PvE runs Netcode as a local host with no real networking; PvBot/PvP connect to a dedicated server.
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameMode mode = GameMode.PvE;
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private ushort serverPort = 7777;

        private void Start()
        {
            var netManager = NetworkManager.Singleton;
            if (netManager == null)
            {
                Debug.LogError("NetworkManager not found in scene.");
                return;
            }

            // NetworkManager คงสถานะ listen ข้ามการ reload ซีน (Play Again) หรือข้ามรอบ Play เมื่อปิด Domain Reload
            // — เรียก start ซ้ำจะได้ warning "Can't start while listening" เฉยๆ จึงข้ามถ้ากำลังรันอยู่แล้ว
            if (netManager.IsListening || netManager.IsServer || netManager.IsClient) return;

            switch (mode)
            {
                case GameMode.PvE:
                    netManager.StartHost();
                    break;
                case GameMode.PvBot:
                case GameMode.PvP:
                    ConfigureTransport(netManager);
                    netManager.StartClient();
                    break;
            }
        }

        private void ConfigureTransport(NetworkManager netManager)
        {
            var transport = netManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport component missing on NetworkManager.");
                return;
            }

            transport.ConnectionData.Address = serverAddress;
            transport.ConnectionData.Port = serverPort;
        }
    }
}
