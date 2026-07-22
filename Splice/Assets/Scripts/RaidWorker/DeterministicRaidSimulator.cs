using System;
using System.Security.Cryptography;
using System.Text;

namespace Splice.RaidWorker
{
    [Serializable]
    public sealed class RaidSimulationInput
    {
        public string raidId;
        public string targetSnapshotId;
        public string loadoutSnapshotId;
        public long attackerPower;
        public long defenderPower;
    }

    [Serializable]
    public sealed class RaidSimulationResult
    {
        public string outcome;
        public int breachedRings;
        public int durationMs;
        public string simulationHash;
    }

    // C4B deterministic proxy. It is deliberately pure and integer-only so replaying the same immutable
    // snapshots produces the exact same settlement result on every headless worker platform.
    public static class DeterministicRaidSimulator
    {
        public const string SimulationVersion = "headless-proxy-c4b-v1";

        public static RaidSimulationResult Simulate(RaidSimulationInput input)
        {
            if (input == null || !Guid.TryParse(input.raidId, out _) ||
                !Guid.TryParse(input.targetSnapshotId, out _) ||
                !Guid.TryParse(input.loadoutSnapshotId, out _) ||
                input.attackerPower <= 0 || input.defenderPower < 0)
                throw new ArgumentException("Immutable raid simulation input is invalid.", nameof(input));

            var canonical = string.Join("|", SimulationVersion, input.raidId.ToLowerInvariant(),
                input.targetSnapshotId.ToLowerInvariant(), input.loadoutSnapshotId.ToLowerInvariant(),
                input.attackerPower, input.defenderPower);
            var seed = Hash(Encoding.UTF8.GetBytes(canonical));
            var variance = seed[0] % 21 - 10; // stable -10..+10; no mutable RNG state
            var score = checked(input.attackerPower * 100L / Math.Max(1L, input.defenderPower)) + variance;

            string outcome;
            int rings;
            if (score >= 125) { outcome = "FULL_VICTORY"; rings = 3; }
            else if (score >= 100) { outcome = "EXTRACTED"; rings = 3; }
            else if (score >= 80) { outcome = "EXTRACTED"; rings = 2; }
            else if (score >= 60) { outcome = "EXTRACTED"; rings = 1; }
            else { outcome = "DEFEAT"; rings = 0; }

            var duration = 45000 + seed[1] * 250 + Math.Abs((int)Math.Clamp(100 - score, -100, 100)) * 100;
            var resultCanonical = string.Join("|", canonical, outcome, rings, duration);
            var resultHash = Hex(Hash(Encoding.UTF8.GetBytes(resultCanonical)));
            return new RaidSimulationResult
            {
                outcome = outcome,
                breachedRings = rings,
                durationMs = duration,
                simulationHash = resultHash,
            };
        }

        private static byte[] Hash(byte[] value)
        {
            using var algorithm = SHA256.Create();
            return algorithm.ComputeHash(value);
        }

        private static string Hex(byte[] value)
        {
            var result = new StringBuilder(value.Length * 2);
            for (var i = 0; i < value.Length; i++) result.Append(value[i].ToString("x2"));
            return result.ToString();
        }
    }
}
