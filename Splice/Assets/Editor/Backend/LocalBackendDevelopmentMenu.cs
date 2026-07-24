#if UNITY_EDITOR
using Splice.Backend;
using Splice.Core;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Backend
{
    public static class LocalBackendDevelopmentMenu
    {
        [MenuItem("Splice/Backend/Enable Local Remote Meta", priority = 1850)]
        public static void Enable()
        {
            LocalBackendDevelopmentBootstrap.EnableForCurrentPlayer();
            Debug.Log($"[Backend] Local remote profile enabled. URL=" +
                      $"{LocalBackendDevelopmentBootstrap.BaseUrl}, Player={PlayerProfile.AccountId}. " +
                      "Start it from the Git root with: bash Tools/run-local-backend-dev.sh " +
                      PlayerProfile.AccountId + " — then enter Play Mode.");
        }

        [MenuItem("Splice/Backend/Disable Local Remote Meta", priority = 1851)]
        public static void Disable()
        {
            LocalBackendDevelopmentBootstrap.Disable();
            Debug.Log("[Backend] Local remote profile disabled; local prototype services restored.");
        }

        [MenuItem("Splice/Backend/Enable Local Remote Meta", true)]
        private static bool ValidateEnable() => !LocalBackendDevelopmentBootstrap.Enabled;

        [MenuItem("Splice/Backend/Disable Local Remote Meta", true)]
        private static bool ValidateDisable() => LocalBackendDevelopmentBootstrap.Enabled;

        [MenuItem("Splice/Testing/Grant 1,000 War Gems", priority = 1900)]
        public static void GrantWarGems()
        {
            var balance = LocalWarGemEconomy.GrantEditorTestGems(1000);
            Debug.Log($"[WarGem] Editor test grant +1,000 complete. Balance={balance:N0}. " +
                      "This command is not compiled into player builds.");
        }

        [MenuItem("Splice/Testing/Grant 1,000 War Gems", true)]
        private static bool ValidateGrantWarGems() =>
            !LocalBackendDevelopmentBootstrap.Enabled && !EditorApplication.isCompiling;
    }
}
#endif
