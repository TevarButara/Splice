using Splice.Core;
using Splice.Base;
using Splice.RaidWorker;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.UI
{
    public static class PrototypeFlowRouter
    {
        public static void LoadHub()
        {
            ShutdownNetworkSession();
            RaidSessionContext.Clear();
            RaidContext.Clear();
            RaidReplayLaunchContext.Clear();
            SceneManager.LoadScene(PrototypeFlowContract.HubScene);
        }

        public static void LoadRaid()
        {
            ShutdownNetworkSession();
            SceneManager.LoadScene(PrototypeFlowContract.RaidScene);
        }

        public static void ShutdownNetworkSession()
        {
            var network = NetworkManager.Singleton;
            if (network != null &&
                (network.IsListening || network.IsServer || network.IsClient))
                network.Shutdown();
        }
    }
}
