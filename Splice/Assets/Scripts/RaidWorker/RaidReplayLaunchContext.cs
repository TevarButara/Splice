using System;

namespace Splice.RaidWorker
{
    // One-shot navigation handoff from defense history to RaidArena. It carries only the opaque
    // authoritative raid UUID; lifecycle/replay payloads are still fetched and verified from backend.
    public static class RaidReplayLaunchContext
    {
        private static string pendingRaidId = string.Empty;

        public static bool HasPendingHistoryReplay =>
            Guid.TryParse(pendingRaidId, out _);

        public static bool TryPrepare(string raidId)
        {
            if (!Guid.TryParse(raidId, out var parsed)) return false;
            pendingRaidId = parsed.ToString("D");
            return true;
        }

        public static bool TryConsume(out string raidId)
        {
            raidId = pendingRaidId;
            pendingRaidId = string.Empty;
            return Guid.TryParse(raidId, out _);
        }

        public static void Clear() => pendingRaidId = string.Empty;
    }
}
