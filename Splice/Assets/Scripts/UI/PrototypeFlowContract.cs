using System.Collections.Generic;

namespace Splice.UI
{
    /// <summary>
    /// Small, engine-independent acceptance contract for the demonstrable prototype loop.
    /// Keeping navigation rules here makes scene wiring and regression tests agree.
    /// </summary>
    public static class PrototypeFlowContract
    {
        public const string BootstrapScene = "Bootstrap";
        public const string HubScene = "BuildZone";
        public const string RaidScene = "RaidArena";
        public const string AttackerPresentationScene = "RaidAttackerPresentation";
        public const string DefenderPresentationScene = "RaidDefenderPresentation";

        public static readonly string[] RequiredSceneNames =
        {
            BootstrapScene,
            HubScene,
            RaidScene,
            AttackerPresentationScene,
            DefenderPresentationScene,
        };

        public static bool ShouldAutoOpenRaidContract(bool hasSelectedTarget,
            bool isIncomingDefense, bool hasPendingReplay) =>
            hasSelectedTarget && !isIncomingDefense && !hasPendingReplay;

        public static bool ValidateEnabledSceneNames(IEnumerable<string> enabledSceneNames,
            out List<string> missing)
        {
            var enabled = new HashSet<string>(enabledSceneNames ?? System.Array.Empty<string>());
            missing = new List<string>();
            foreach (var required in RequiredSceneNames)
                if (!enabled.Contains(required)) missing.Add(required);
            return missing.Count == 0;
        }
    }
}
