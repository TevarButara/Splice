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
        public List<RaidWorkerUnitAuthority> armyUnits = new();
        public List<RaidWorkerUnitAuthority> defenseUnits = new();
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
                armyUnits = job.armyUnits ?? new List<RaidWorkerUnitAuthority>(),
                defenseUnits = job.defenseUnits ?? new List<RaidWorkerUnitAuthority>(),
                hero = job.hero,
                gearItems = job.gearItems ?? new List<RaidWorkerGearAuthority>(),
            };
        }
    }

    // C4C2C combat truth. All state is integer-only, per-actor and local to one stateless worker.
    // Presentation and settlement consume the result but can never feed state back into this kernel.
    public static class FixedTickRaidSimulator
    {
        public const string SimulationVersion = "fixed-tick-c4c2c-v2";
        public const int TickMilliseconds = 100;
        public const int DefaultMaximumTicks = 1800;
        public const int MaximumCommands = 25000;
        private const int RingCount = 3;
        private const int MaximumArmyActors = 50;
        private const int MaximumDefenseActors = 201;

        private sealed class Actor
        {
            public string Id;
            public string ContentId;
            public string Kind;
            public long Health;
            public int Armor;
            public int Damage;
            public int CooldownTicks;
            public int MoveSpeedMilli;
            public int AbilityDamage;
            public int NextAttackTick;
            public int Ring;
            public bool Alive => Health > 0;
        }

        public static RaidSimulationResult Simulate(FixedTickRaidSimulationInput input)
        {
            Validate(input);
            var commands = new List<RaidSimulationCommand>(512);
            var attackers = BuildAttackers(input);
            var defenders = BuildDefenders(input);
            var travelTicks = TravelTicks(attackers);
            var tick = 0;
            var breached = 0;

            foreach (var actor in attackers)
                Add(commands, 0, "SPAWN", actor.Id, "attacker-entry", actor.Health);

            for (var ring = 0; ring < RingCount; ring++)
            {
                Add(commands, tick, "MOVE", "attackers", RingId(ring), travelTicks);
                tick = checked(tick + travelTicks);
                if (tick > input.maximumTicks)
                    return Finish(input, commands, breached > 0 ? "EXTRACTED" : "DEFEAT",
                        breached, input.maximumTicks);

                var ringDefenders = defenders.Where(actor => actor.Ring == ring)
                    .OrderBy(actor => actor.Id, StringComparer.Ordinal).ToList();
                Add(commands, tick, "ENGAGE", "attackers", RingId(ring),
                    ringDefenders.Sum(actor => actor.Health));

                var hero = attackers.First(actor => actor.Kind == "HERO");
                var abilityTarget = FirstAlive(ringDefenders);
                if (hero.Alive && hero.AbilityDamage > 0 && abilityTarget != null)
                {
                    var dealt = Damage(hero.AbilityDamage, abilityTarget.Armor);
                    ApplyDamage(commands, tick, "ABILITY", hero, abilityTarget, dealt, ring);
                }

                foreach (var actor in attackers) actor.NextAttackTick = tick;
                foreach (var actor in ringDefenders) actor.NextAttackTick = tick;

                while (FirstAlive(ringDefenders) != null && FirstAlive(attackers) != null &&
                       tick <= input.maximumTicks)
                {
                    foreach (var attacker in attackers)
                    {
                        if (!attacker.Alive || tick < attacker.NextAttackTick) continue;
                        var target = FirstAlive(ringDefenders);
                        if (target == null) break;
                        ApplyDamage(commands, tick, "ATTACK", attacker, target,
                            Damage(attacker.Damage, target.Armor), ring);
                        attacker.NextAttackTick = checked(tick + attacker.CooldownTicks);
                        EnsureCommandBudget(commands);
                    }

                    foreach (var defender in ringDefenders)
                    {
                        if (!defender.Alive || tick < defender.NextAttackTick) continue;
                        var target = FirstAlive(attackers);
                        if (target == null) break;
                        ApplyDamage(commands, tick, "ATTACK", defender, target,
                            Damage(defender.Damage, target.Armor), ring);
                        defender.NextAttackTick = checked(tick + defender.CooldownTicks);
                        EnsureCommandBudget(commands);
                    }

                    if (FirstAlive(ringDefenders) == null || FirstAlive(attackers) == null) break;
                    tick++;
                }

                if (FirstAlive(ringDefenders) != null)
                    return Finish(input, commands, breached > 0 ? "EXTRACTED" : "DEFEAT",
                        breached, Math.Min(tick, input.maximumTicks));

                breached++;
                Add(commands, tick, "BREACH", "attackers", RingId(ring), breached);
                if (breached == RingCount)
                    return Finish(input, commands, "FULL_VICTORY", breached, tick);
            }

            return Finish(input, commands, breached > 0 ? "EXTRACTED" : "DEFEAT", breached, tick);
        }

        private static void ApplyDamage(ICollection<RaidSimulationCommand> commands, int tick,
            string type, Actor actor, Actor target, long damage, int ring)
        {
            target.Health = Math.Max(0L, target.Health - damage);
            Add(commands, tick, type, actor.Id, target.Id, damage);
            if (!target.Alive)
                Add(commands, tick, "DEFEATED", target.Id, RingId(ring), 0);
        }

        private static Actor FirstAlive(IEnumerable<Actor> actors) =>
            actors.FirstOrDefault(actor => actor.Alive);

        private static List<Actor> BuildAttackers(FixedTickRaidSimulationInput input)
        {
            var heroCombat = Copy(input.hero.combat);
            var gearPower = (input.gearItems ?? new List<RaidWorkerGearAuthority>())
                .Sum(item => item.scaledPower);
            heroCombat.maxHealth = checked(heroCombat.maxHealth + (int)Math.Min(int.MaxValue,
                gearPower * 5L));
            heroCombat.attackDamage = checked(heroCombat.attackDamage + (int)Math.Min(int.MaxValue,
                gearPower / 10L));
            var actors = new List<Actor>
            {
                ActorFrom("hero:" + input.hero.contentId, input.hero.contentId, "HERO", heroCombat, 0),
            };
            foreach (var authority in input.armyUnits.OrderBy(item => item.actorId, StringComparer.Ordinal)
                         .ThenBy(item => item.contentId, StringComparer.Ordinal))
            {
                for (var index = 0; index < authority.count; index++)
                {
                    var id = authority.actorId + "#" + (index + 1).ToString("D3");
                    actors.Add(ActorFrom(id, authority.contentId, "ARMY", authority.combat, 0));
                }
            }
            return actors.OrderBy(actor => actor.Id, StringComparer.Ordinal).ToList();
        }

        private static List<Actor> BuildDefenders(FixedTickRaidSimulationInput input)
        {
            var authorities = input.defenseUnits.OrderByDescending(item => Distance(item.position))
                .ThenBy(item => item.actorId, StringComparer.Ordinal).ToArray();
            var nonCore = authorities.Where(item => item.unitKind != "CORE").ToArray();
            var actors = new List<Actor>(authorities.Length);
            for (var index = 0; index < nonCore.Length; index++)
            {
                var ring = Math.Min(RingCount - 1,
                    index * RingCount / Math.Max(1, nonCore.Length));
                actors.Add(ActorFrom(nonCore[index].actorId, nonCore[index].contentId,
                    nonCore[index].unitKind, nonCore[index].combat, ring));
            }
            foreach (var core in authorities.Where(item => item.unitKind == "CORE"))
                actors.Add(ActorFrom(core.actorId, core.contentId, "CORE", core.combat, RingCount - 1));
            return actors.OrderBy(actor => actor.Ring)
                .ThenBy(actor => actor.Id, StringComparer.Ordinal).ToList();
        }

        private static Actor ActorFrom(string id, string contentId, string kind,
            RaidWorkerCombatPayload combat, int ring) => new()
        {
            Id = id,
            ContentId = contentId,
            Kind = kind,
            Health = combat.maxHealth,
            Armor = Math.Max(0, combat.armor),
            Damage = Math.Max(1, combat.attackDamage),
            CooldownTicks = TicksForMilliseconds(combat.attackCooldownMs),
            MoveSpeedMilli = Math.Max(0, combat.moveSpeedMilli),
            AbilityDamage = Math.Max(0, combat.abilityDamage),
            Ring = ring,
        };

        private static RaidWorkerCombatPayload Copy(RaidWorkerCombatPayload value) => new()
        {
            maxHealth = value.maxHealth,
            armor = value.armor,
            attackDamage = value.attackDamage,
            attackCooldownMs = value.attackCooldownMs,
            attackRangeMilli = value.attackRangeMilli,
            moveSpeedMilli = value.moveSpeedMilli,
            abilityId = value.abilityId,
            abilityDamage = value.abilityDamage,
            abilityCooldownMs = value.abilityCooldownMs,
            abilityCastRangeMilli = value.abilityCastRangeMilli,
            abilityRadiusMilli = value.abilityRadiusMilli,
            maxTargets = value.maxTargets,
        };

        private static int TravelTicks(IEnumerable<Actor> attackers)
        {
            var slowest = attackers.Where(actor => actor.MoveSpeedMilli > 0)
                .Select(actor => actor.MoveSpeedMilli).DefaultIfEmpty(1000).Min();
            var movePerTick = Math.Max(1, slowest * TickMilliseconds / 1000);
            return Math.Max(1, DivideRoundUp(3000, movePerTick));
        }

        private static long Damage(long raw, int armor) =>
            Math.Max(1L, checked(raw * 100L / checked(100L + Math.Max(0, armor))));

        private static RaidSimulationResult Finish(FixedTickRaidSimulationInput input,
            List<RaidSimulationCommand> commands, string outcome, int breachedRings, int tick)
        {
            Add(commands, tick, "COMPLETE", "simulation", outcome, breachedRings);
            EnsureCommandBudget(commands);
            var commandHash = ComputeCommandStreamHash(commands);
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
                !ValidCombat(input.hero.combat, mobile: true) || input.hero.scaledPower != input.heroPower)
                throw new ArgumentException("Authoritative Hero combat payload is invalid.", nameof(input));
            if (input.gearItems == null || input.gearItems.Sum(item => item.scaledPower) != input.gearPower)
                throw new ArgumentException("Authoritative gear payload is invalid.", nameof(input));
            if (input.loadoutEntries == null || input.armyUnits == null || input.defenseUnits == null ||
                input.armyUnits.Count == 0 || input.defenseUnits.Count == 0)
                throw new ArgumentException("Per-unit combat authority is required.", nameof(input));
            if (input.armyUnits.Sum(item => item.count) is < 1 or > MaximumArmyActors ||
                input.defenseUnits.Sum(item => item.count) is < 1 or > MaximumDefenseActors)
                throw new ArgumentException("Authoritative actor count is invalid.", nameof(input));
            if (input.armyUnits.Any(item => !ValidAuthority(item, mobile: true)) ||
                input.defenseUnits.Any(item => !ValidAuthority(item, mobile: false)))
                throw new ArgumentException("Per-unit combat payload is invalid.", nameof(input));
            if (input.defenseUnits.Count(item => item.unitKind == "CORE") != 1)
                throw new ArgumentException("Exactly one authoritative town Core is required.", nameof(input));
            var ids = input.armyUnits.Select(item => item.actorId)
                .Concat(input.defenseUnits.Select(item => item.actorId)).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Authoritative actor IDs must be unique.", nameof(input));
            var entryCounts = input.loadoutEntries.ToDictionary(item => item.cardId, item => item.count,
                StringComparer.Ordinal);
            var unitCounts = input.armyUnits.GroupBy(item => item.contentId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.count), StringComparer.Ordinal);
            if (entryCounts.Count != unitCounts.Count ||
                entryCounts.Any(pair => !unitCounts.TryGetValue(pair.Key, out var count) || count != pair.Value))
                throw new ArgumentException("Army entries and immutable units do not match.", nameof(input));
            if (input.armyUnits.Sum(item => checked(item.scaledPower * item.count)) != input.armyPower)
                throw new ArgumentException("Army unit power does not match the immutable total.", nameof(input));
            if (input.maximumTicks is < 1 or > 36000)
                throw new ArgumentException("Simulation tick budget is invalid.", nameof(input));
        }

        private static bool ValidAuthority(RaidWorkerUnitAuthority item, bool mobile) =>
            item != null && !string.IsNullOrWhiteSpace(item.actorId) &&
            !string.IsNullOrWhiteSpace(item.contentId) && !string.IsNullOrWhiteSpace(item.unitKind) &&
            item.count is >= 1 and <= MaximumArmyActors && item.basePower > 0 &&
            item.scaledPower > 0 && ValidCombat(item.combat, mobile);

        private static bool ValidCombat(RaidWorkerCombatPayload combat, bool mobile) =>
            combat != null && combat.maxHealth > 0 && combat.armor >= 0 &&
            combat.attackDamage > 0 && combat.attackCooldownMs is >= TickMilliseconds and <= 60000 &&
            combat.attackRangeMilli >= 0 && (!mobile || combat.moveSpeedMilli > 0) &&
            combat.maxTargets is >= 1 and <= 32;

        private static string CanonicalInput(FixedTickRaidSimulationInput input)
        {
            var value = new StringBuilder();
            value.Append(input.raidId.ToLowerInvariant()).Append('|')
                .Append(input.targetSnapshotId.ToLowerInvariant()).Append('|')
                .Append(input.loadoutSnapshotId.ToLowerInvariant()).Append('|')
                .Append(input.attackerPower).Append('|').Append(input.armyPower).Append('|')
                .Append(input.heroPower).Append('|').Append(input.gearPower).Append('|')
                .Append(input.defenderPower).Append('|').Append(input.maximumTicks).Append('|')
                .Append("H:").Append(input.hero.contentId).Append(':').Append(input.hero.level)
                .Append(':').Append(Combat(input.hero.combat)).Append('|');
            foreach (var gear in input.gearItems.OrderBy(item => item.instanceId, StringComparer.Ordinal))
                value.Append("G:").Append(gear.instanceId).Append(':').Append(gear.contentId)
                    .Append(':').Append(gear.level).Append(':').Append(gear.scaledPower)
                    .Append(':').Append(Combat(gear.combat)).Append('|');
            foreach (var unit in input.armyUnits.OrderBy(item => item.actorId, StringComparer.Ordinal))
                AppendAuthority(value, "A", unit);
            foreach (var unit in input.defenseUnits.OrderBy(item => item.actorId, StringComparer.Ordinal))
                AppendAuthority(value, "D", unit);
            return value.ToString();
        }

        private static void AppendAuthority(StringBuilder value, string prefix,
            RaidWorkerUnitAuthority unit) =>
            value.Append(prefix).Append(':').Append(unit.actorId).Append(':').Append(unit.contentId)
                .Append(':').Append(unit.unitKind).Append(':').Append(unit.count)
                .Append(':').Append(unit.basePower).Append(':').Append(unit.scaledPower)
                .Append(':').Append(Position(unit.position)).Append(':').Append(Combat(unit.combat)).Append('|');

        private static string Combat(RaidWorkerCombatPayload value) =>
            string.Join(",", value?.maxHealth ?? 0, value?.armor ?? 0, value?.attackDamage ?? 0,
                value?.attackCooldownMs ?? 0, value?.attackRangeMilli ?? 0,
                value?.moveSpeedMilli ?? 0, value?.abilityId ?? string.Empty,
                value?.abilityDamage ?? 0, value?.abilityCooldownMs ?? 0,
                value?.abilityCastRangeMilli ?? 0, value?.abilityRadiusMilli ?? 0,
                value?.maxTargets ?? 0);

        private static string CommandCanonical(RaidSimulationCommand command) =>
            string.Join("|", command.tick, command.type, command.actor, command.target, command.value);

        public static string ComputeCommandStreamHash(
            IReadOnlyList<RaidSimulationCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            return Sha256(string.Join("\n", commands.Select(CommandCanonical)));
        }

        private static string RingId(int zeroBased) => "ring-" + (zeroBased + 1);

        private static void Add(ICollection<RaidSimulationCommand> commands, int tick, string type,
            string actor, string target, long value) =>
            commands.Add(new RaidSimulationCommand(tick, type, actor, target, value));

        private static void EnsureCommandBudget(ICollection<RaidSimulationCommand> commands)
        {
            if (commands.Count > MaximumCommands)
                throw new InvalidOperationException("Deterministic command budget exceeded.");
        }

        private static int TicksForMilliseconds(int milliseconds) =>
            Math.Max(1, DivideRoundUp(milliseconds, TickMilliseconds));

        private static int DivideRoundUp(int value, int divisor) =>
            checked((value + divisor - 1) / divisor);

        private static long Distance(RaidWorkerVector3 position)
        {
            var x = Milli(position?.x ?? 0f);
            var z = Milli(position?.z ?? 0f);
            return checked(x * x + z * z);
        }

        private static string Position(RaidWorkerVector3 position) =>
            string.Join(",", Milli(position?.x ?? 0f), Milli(position?.y ?? 0f),
                Milli(position?.z ?? 0f));

        private static long Milli(float value) =>
            Convert.ToInt64(Math.Round(value * 1000d, MidpointRounding.AwayFromZero));

        private static string Sha256(string value)
        {
            using var algorithm = SHA256.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("x2"));
            return result.ToString();
        }
    }
}
