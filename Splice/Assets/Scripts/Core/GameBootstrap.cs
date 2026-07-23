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
        public const ushort LocalPveEphemeralPort = 0;

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
                    if (!TryConfigureLocalPveTransport(netManager, out var error))
                    {
                        Debug.LogError($"[GameBootstrap] {error}", this);
                        return;
                    }
                    if (!netManager.StartHost())
                        Debug.LogError("[GameBootstrap] Local PvE host failed to start.", this);
                    break;
                case GameMode.PvBot:
                case GameMode.PvP:
                    ConfigureTransport(netManager);
                    netManager.StartClient();
                    break;
            }
        }

        // ปิด network ตอนออก play (กด Stop) เพื่อคืน UDP socket และไม่ทิ้ง stale network session
        // ไว้ใน Editor เมื่อปิด Domain Reload.
        // ใช้ OnApplicationQuit (ยิงตอนหยุด play) ไม่ใช่ OnDestroy — กัน shutdown หลุดตอนสลับซีนระหว่างเกม
        private void OnApplicationQuit()
        {
            var netManager = NetworkManager.Singleton;
            if (netManager != null && (netManager.IsListening || netManager.IsServer || netManager.IsClient))
                netManager.Shutdown();
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

        public static bool TryConfigureLocalPveTransport(NetworkManager netManager, out string error)
        {
            if (netManager == null)
            {
                error = "NetworkManager not found in scene.";
                return false;
            }

            var transport = netManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                error = "UnityTransport component missing on NetworkManager.";
                return false;
            }

            ConfigureLocalPveTransport(transport);
            error = string.Empty;
            return true;
        }

        public static void ConfigureLocalPveTransport(UnityTransport transport)
        {
            if (transport == null) return;

            // A local host never accepts a remote player. Let the OS choose a free UDP port so an
            // Editor session left on 7777 cannot block Confirm Raid after a scene/domain reload.
            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.ServerListenAddress = "127.0.0.1";
            transport.ConnectionData.Port = LocalPveEphemeralPort;
        }
    }
}
