using System;

namespace Splice.Backend
{
    public enum RaidObserverPhase
    {
        Incoming,
        InProgress,
        ReplayReady,
        CompletedWithoutReplay,
        Unavailable,
    }

    public readonly struct RaidObserverDecision
    {
        public readonly RaidObserverPhase phase;
        public readonly bool showIncomingNotification;
        public readonly bool canOpenReplay;
        public readonly bool claimsLiveCommandStream;
        public readonly string status;

        public RaidObserverDecision(RaidObserverPhase phase, bool showIncomingNotification,
            bool canOpenReplay, string status)
        {
            this.phase = phase;
            this.showIncomingNotification = showIncomingNotification;
            this.canOpenReplay = canOpenReplay;
            // The current authoritative worker publishes a verified stream at settlement.
            // Never present polling as a fake real-time spectator feed.
            claimsLiveCommandStream = false;
            this.status = status;
        }
    }

    public static class RaidObserverPolicy
    {
        public static RaidObserverDecision Resolve(IncomingDefenseRaidDto raid)
        {
            if (raid == null)
                return new RaidObserverDecision(RaidObserverPhase.Unavailable, false, false,
                    "No incoming defense raid.");
            if (raid.replayAvailable)
                return new RaidObserverDecision(RaidObserverPhase.ReplayReady, false, true,
                    "Verified defender replay is ready.");
            return (raid.state ?? string.Empty).ToUpperInvariant() switch
            {
                "FUNDED" => new RaidObserverDecision(RaidObserverPhase.Incoming, true, false,
                    $"Incoming raid from {SafeName(raid.attackerDisplayName)}."),
                "ACTIVE" or "SETTLING" => new RaidObserverDecision(RaidObserverPhase.InProgress,
                    true, false, "Raid is being resolved by the authoritative server."),
                "SETTLED" => new RaidObserverDecision(RaidObserverPhase.CompletedWithoutReplay,
                    false, false, "Raid settled; verified replay is still processing."),
                _ => new RaidObserverDecision(RaidObserverPhase.Unavailable, false, false,
                    "Raid is no longer observable."),
            };
        }

        private static string SafeName(string value) =>
            string.IsNullOrWhiteSpace(value) ? "another commander" : value.Trim();
    }
}
