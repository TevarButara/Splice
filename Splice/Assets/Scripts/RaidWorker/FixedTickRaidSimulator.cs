using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Splice.RaidWorker
{
    [Serializable]
    public sealed class FixedTickRaidSimulationInput
    {
        public string raidId;
        public string targetSnapshotId;
        public string loadoutSnapshotId;
        public long attackerPower;
        public long armyPower;
        public long heroPower;
        public long gearPower;
        public long defenderPower;
        public RaidWorkerBaseLayout targetSnapshot;
        public List<RaidWorkerLoadoutEntry> loadoutEntries = new();
        public RaidWorkerHeroAuthority hero;
        public List<RaidWorkerGearAuthority> gearItems = new();
        public int maximumTicks = FixedTickRaidSimulator.DefaultMaximumTicks;

        public static FixedTickRaidSimulationInput FromJob(RaidJobResponse job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            return new FixedTickRaidSimulationInput
            {
                raidId = job.raidId,
                targetSnapshotId = job.targetSnapshotId,
                loadoutSnapshotId = job.loadoutSnapshotId,
                attackerPower = job.attackerPower,
                armyPower = job.armyPower,
                heroPower = job.heroPower,
                gearPower = job.gearPower,
                defenderPower = job.defenderPower,
                targetSnapshot = job.targetSnapshot,
                loadoutEntries = job.loadoutEntries ?? new List<RaidWorkerLoadoutEntry>(),
                hero = job.hero,
                gearItems = job.gearItems ?? new List<RaidWorkerGearAuthority>(),
            };
        }
    }

    // C4C2A combat truth: integer-only fixed ticks. C4C2B may animate these commands, but presentation
    // must never feed state back into this kernel or settlement (architecture §4.1, §5.10).
    public static class FixedTickRaidSimulator
    {
        public const string SimulationVersion = "fixed-tick-c4c2a-v1";
        public const int TickMilliseconds = 100;
        public const int DefaultMaximumTicks = 1800;
        private const int RingCount = 3;

        public static RaidSimulationResult Simulate(FixedTickRaidSimulationInput input)
        {
            Validate(input);
            var commands = new List<RaidSimulationCommand>();
            var ringHealth = BuildRingHealth(input);
            var attackerHealth = checked((long)input.hero.combat.maxHealth + input.armyPower * 20L +
                                         input.gearPower * 10L);
            var heroCooldown = TicksForMilliseconds(Math.Max(TickMilliseconds,
                input.hero.combat.attackCooldownMs));
            var movePerTick = Math.Max(1, input.hero.combat.moveSpeedMilli * TickMilliseconds / 1000);
            var travelTicks = Math.Max(1, DivideRoundUp(3000, movePerTick));
            var defenderStrike = Math.Max(1L, input.defenderPower * 2L);
            var armyStrike = Math.Max(1L, input.armyPower);
            var gearStrike = Math.Max(0L, input.gearPower / 2L);
            var heroStrike = Math.Max(1, input.hero.combat.attackDamage);
            var abilityStrike = Math.Max(0, input.hero.combat.abilityDamage);

            Add(commands, 0, "SPAWN", input.hero.contentId, "attacker-entry", attackerHealth);
            Add(commands, 0, "MOVE", "attackers", "ring-1", travelTicks);
            var ring = 0;
            var tick = 0;
            var arrivalTick = travelTicks;
            var nextHeroStrike = arrivalTick;
            var nextArmyStrike = arrivalTick;
            var nextDefenderStrike = arrivalTick;
            var engaged = false;

            while (tick <= input.maximumTicks)
            {
                if (tick < arrivalTick)
                {
                    tick++;
                    continue;
                }

                if (!engaged)
                {
                    engaged = true;
                    Add(commands, tick, "ENGAGE", "attackers", RingId(ring), ringHealth[ring]);
                    if (abilityStrike > 0)
                    {
                        ringHealth[ring] -= abilityStrike;
                        Add(commands, tick, "ABILITY", input.hero.contentId, RingId(ring), abilityStrike);
                    }
                }

                if (tick >= nextHeroStrike && ringHealth[ring] > 0)
                {
                    ringHealth[ring] -= heroStrike;
                    Add(commands, tick, "ATTACK", input.hero.contentId, RingId(ring), heroStrike);
                    nextHeroStrike = checked(tick + heroCooldown);
                }
                if (tick >= nextArmyStrike && ringHealth[ring] > 0)
                {
                    var damage = checked(armyStrike + gearStrike);
                    ringHealth[ring] -= damage;
                    Add(commands, tick, "ATTACK", "army", RingId(ring), damage);
                    nextArmyStrike = checked(tick + 10);
                }
                if (tick >= nextDefenderStrike && ringHealth[ring] > 0)
                {
                    attackerHealth -= defenderStrike;
                    Add(commands, tick, "ATTACK", RingId(ring), "attackers", defenderStrike);
                    nextDefenderStrike = checked(tick + 10);
                }

                if (ringHealth[ring] <= 0)
                {
                    Add(commands, tick, "BREACH", "attackers", RingId(ring), ring + 1);
                    ring++;
                    if (ring == RingCount)
                        return Finish(input, commands, "FULL_VICTORY", RingCount, tick);
                    if (attackerHealth <= 0)
                        return Finish(input, commands, "EXTRACTED", ring, tick);
                    arrivalTick = checked(tick + travelTicks);
                    nextHeroStrike = arrivalTick;
                    nextArmyStrike = arrivalTick;
                    nextDefenderStrike = arrivalTick;
                    engaged = false;
                    Add(commands, tick, "MOVE", "attackers", RingId(ring), travelTicks);
                }
                else if (attackerHealth <= 0)
                {
                    return Finish(input, commands, ring > 0 ? "EXTRACTED" : "DEFEAT", ring, tick);
                }

                tick++;
            }

            return Finish(input, commands, ring > 0 ? "EXTRACTED" : "DEFEAT", ring,
                input.maximumTicks);
        }

        private static RaidSimulationResult Finish(FixedTickRaidSimulationInput input,
            List<RaidSimulationCommand> commands, string outcome, int breachedRings, int tick)
        {
            Add(commands, tick, "COMPLETE", "simulation", outcome, breachedRings);
            var commandHash = Sha256(string.Join("\n", commands.Select(CommandCanonical)));
            var simulationHash = Sha256(string.Join("|", SimulationVersion, CanonicalInput(input),
                outcome, breachedRings, tick, commandHash));
            return new RaidSimulationResult
            {
                outcome = outcome,
                breachedRings = breachedRings,
                durationMs = checked(Math.Max(1, tick) * TickMilliseconds),
                simulationHash = simulationHash,
                simulationVersion = SimulationVersion,
                tickCount = tick,
                commandCount = commands.Count,
                commandStreamHash = commandHash,
                commands = commands,
            };
        }

        private static long[] BuildRingHealth(FixedTickRaidSimulationInput input)
        {
            var weights = new long[RingCount] { 1, 1, 2 };
            var pieces = new List<(long Distance, string Id, long Weight)>();
            foreach (var tower in input.targetSnapshot.towers ?? new List<RaidWorkerTower>())
            {
                var upgrades = Math.Max(0, tower.attackLevel) + Math.Max(0, tower.healthLevel) +
                               Math.Max(0, tower.armorLevel) + Math.Max(0, tower.rangeLevel) +
                               Math.Max(0, tower.targetsLevel);
                pieces.Add((Distance(tower.position), tower.towerId ?? string.Empty, 3L + upgrades));
            }
            foreach (var unit in input.targetSnapshot.garrison ?? new List<RaidWorkerGarrison>())
                pieces.Add((Distance(unit.position), unit.cardId ?? string.Empty, 2L));

            var ordered = pieces.OrderByDescending(piece => piece.Distance)
                .ThenBy(piece => piece.Id, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                var ring = Math.Min(RingCount - 1, i * RingCount / Math.Max(1, ordered.Length));
                weights[ring] = checked(weights[ring] + ordered[i].Weight);
            }

            var totalWeight = weights.Sum();
            var totalHealth = checked(Math.Max(300L, input.defenderPower * 100L));
            var result = new long[RingCount];
            long assigned = 0;
            for (var i = 0; i < RingCount - 1; i++)
            {
                result[i] = Math.Max(1L, totalHealth * weights[i] / totalWeight);
                assigned += result[i];
            }
            result[RingCount - 1] = Math.Max(1L, totalHealth - assigned);
            return result;
        }

        private static void Validate(FixedTickRaidSimulationInput input)
        {
            if (input == null || !Guid.TryParse(input.raidId, out _) ||
                !Guid.TryParse(input.targetSnapshotId, out _) ||
                !Guid.TryParse(input.loadoutSnapshotId, out _))
                throw new ArgumentException("Immutable raid identities are invalid.", nameof(input));
            if (input.armyPower <= 0 || input.heroPower <= 0 || input.gearPower < 0 ||
                input.defenderPower < 0 || input.attackerPower != checked(input.armyPower +
                    input.heroPower + input.gearPower))
                throw new ArgumentException("Authoritative raid power breakdown is invalid.", nameof(input));
            if (input.hero?.combat == null || string.IsNullOrWhiteSpace(input.hero.contentId) ||
                input.hero.combat.maxHealth <= 0 || input.hero.combat.attackDamage <= 0 ||
                input.hero.combat.attackCooldownMs <= 0 || input.hero.combat.moveSpeedMilli <= 0)
                throw new ArgumentException("Authoritative Hero combat payload is invalid.", nameof(input));
            if (input.targetSnapshot == null || input.loadoutEntries == null ||
                input.loadoutEntries.Count == 0 || input.loadoutEntries.Any(entry =>
                    entry == null || string.IsNullOrWhiteSpace(entry.cardId) || entry.count <= 0))
                throw new ArgumentException("Immutable town or army payload is invalid.", nameof(input));
            if (input.maximumTicks is < 1 or > 36000)
                throw new ArgumentException("Simulation tick budget is invalid.", nameof(input));
        }

        private static string CanonicalInput(FixedTickRaidSimulationInput input)
        {
            var value = new StringBuilder();
            value.Append(input.raidId.ToLowerInvariant()).Append('|')
                .Append(input.targetSnapshotId.ToLowerInvariant()).Append('|')
                .Append(input.loadoutSnapshotId.ToLowerInvariant()).Append('|')
                .Append(input.attackerPower).Append('|').Append(input.armyPower).Append('|')
                .Append(input.heroPower).Append('|').Append(input.gearPower).Append('|')
                .Append(input.defenderPower).Append('|').Append(input.maximumTicks).Append('|')
                .Append(input.hero.contentId).Append('|').Append(input.hero.level).Append('|')
                .Append(input.hero.combat.maxHealth).Append('|').Append(input.hero.combat.armor).Append('|')
                .Append(input.hero.combat.attackDamage).Append('|')
                .Append(input.hero.combat.attackCooldownMs).Append('|')
                .Append(input.hero.combat.moveSpeedMilli).Append('|')
                .Append(input.hero.combat.abilityId).Append('|')
                .Append(input.hero.combat.abilityDamage).Append('|');
            foreach (var entry in input.loadoutEntries.OrderBy(entry => entry.cardId, StringComparer.Ordinal))
                value.Append("A:").Append(entry.cardId).Append(':').Append(entry.count).Append('|');
            foreach (var gear in (input.gearItems ?? new List<RaidWorkerGearAuthority>())
                         .OrderBy(item => item.instanceId, StringComparer.Ordinal))
                value.Append("G:").Append(gear.instanceId).Append(':').Append(gear.contentId)
                    .Append(':').Append(gear.level).Append(':').Append(gear.scaledPower).Append('|');
            foreach (var tower in (input.targetSnapshot.towers ?? new List<RaidWorkerTower>())
                         .OrderBy(tower => tower.towerId, StringComparer.Ordinal)
                         .ThenBy(tower => Distance(tower.position)))
                value.Append("T:").Append(tower.towerId).Append(':').Append(Position(tower.position))
                    .Append(':').Append(tower.attackLevel).Append(':').Append(tower.healthLevel)
                    .Append(':').Append(tower.armorLevel).Append(':').Append(tower.rangeLevel)
                    .Append(':').Append(tower.targetsLevel).Append('|');
            foreach (var unit in (input.targetSnapshot.garrison ?? new List<RaidWorkerGarrison>())
                         .OrderBy(unit => unit.cardId, StringComparer.Ordinal)
                         .ThenBy(unit => Distance(unit.position)))
                value.Append("D:").Append(unit.cardId).Append(':').Append(Position(unit.position)).Append('|');
            return value.ToString();
        }

        private static string CommandCanonical(RaidSimulationCommand command) =>
            string.Join("|", command.tick, command.type, command.actor, command.target, command.value);

        private static string RingId(int zeroBased) => "ring-" + (zeroBased + 1);

        private static void Add(ICollection<RaidSimulationCommand> commands, int tick, string type,
            string actor, string target, long value) =>
            commands.Add(new RaidSimulationCommand(tick, type, actor, target, value));

        private static int TicksForMilliseconds(int milliseconds) =>
            Math.Max(1, DivideRoundUp(milliseconds, TickMilliseconds));

        private static int DivideRoundUp(int value, int divisor) => checked((value + divisor - 1) / divisor);

        private static long Distance(RaidWorkerVector3 position)
        {
            var x = Milli(position?.x ?? 0f);
            var z = Milli(position?.z ?? 0f);
            return checked(x * x + z * z);
        }

        private static string Position(RaidWorkerVector3 position) =>
            string.Join(",", Milli(position?.x ?? 0f), Milli(position?.y ?? 0f), Milli(position?.z ?? 0f));

        private static long Milli(float value) =>
            Convert.ToInt64(Math.Round(value * 1000d, MidpointRounding.AwayFromZero));

        private static string Sha256(string value)
        {
            using var algorithm = SHA256.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
            return result.ToString();
        }
    }
}
