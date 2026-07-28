using Splice.Core;
using Splice.Base;
using Splice.RaidWorker;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.UI
{
    public enum PrototypeHubDestination
    {
        Town,
        Raid,
        History,
    }

    public static class PrototypeFlowRouter
    {
        private static PrototypeHubDestination pendingHubDestination;

        public static void LoadHub()
        {
            LoadHub(PrototypeHubDestination.Town);
        }

        public static void LoadRaidHub()
        {
            LoadHub(PrototypeHubDestination.Raid);
        }

        private static void LoadHub(PrototypeHubDestination destination)
        {
            pendingHubDestination = destination;
            ShutdownNetworkSession();
            RaidSessionContext.Clear();
            RaidContext.Clear();
            RaidReplayLaunchContext.Clear();
            SceneManager.LoadScene(PrototypeFlowContract.HubScene);
        }

        public static PrototypeHubDestination ConsumeHubDestination()
        {
            var destination = pendingHubDestination;
            pendingHubDestination = PrototypeHubDestination.Town;
            return destination;
        }

        public static void LoadRaid()
        {
            ShutdownNetworkSession();
            SceneManager.LoadScene(PrototypeFlowContract.RaidScene);
        }

        public static void LoadWorldMap()
        {
            ShutdownNetworkSession();
            SceneManager.LoadScene(PrototypeFlowContract.WorldMapScene);
        }

        public static void LoadForest()
        {
            ShutdownNetworkSession();
            SceneManager.LoadScene(PrototypeFlowContract.ForestScene);
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
