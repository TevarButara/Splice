#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Splice.RaidWorker;
using UnityEngine;

namespace Splice.Editor.Tests
{
    public sealed class DeterministicRaidSimulatorEditModeTests
    {
        private static RaidSimulationInput Input() => new()
        {
            raidId = "10000000-0000-0000-0000-000000000001",
            targetSnapshotId = "20000000-0000-0000-0000-000000000001",
            loadoutSnapshotId = "30000000-0000-0000-0000-000000000001",
            attackerPower = 1000,
            defenderPower = 800,
        };

        internal static FixedTickRaidSimulationInput FixedInput() => new()
        {
            raidId = "10000000-0000-0000-0000-000000000001",
            targetSnapshotId = "20000000-0000-0000-0000-000000000001",
            loadoutSnapshotId = "30000000-0000-0000-0000-000000000001",
            attackerPower = 3160,
            armyPower = 130,
            heroPower = 2830,
            gearPower = 200,
            defenderPower = 405,
            hero = new RaidWorkerHeroAuthority
            {
                contentId = "hero/hero_test",
                level = 1,
                basePower = 2830,
                scaledPower = 2830,
                combat = new RaidWorkerCombatPayload
                {
                    maxHealth = 30000,
                    armor = 10,
                    attackDamage = 1000,
                    attackCooldownMs = 800,
                    moveSpeedMilli = 9000,
                    abilityId = "breach_charge",
                    abilityDamage = 300,
                    maxTargets = 1,
                },
            },
            gearItems = new List<RaidWorkerGearAuthority>
            {
                new()
                {
                    instanceId = "61000000-0000-0000-0000-000000000001",
                    contentId = "gear/test-blade",
                    level = 1,
                    basePower = 200,
                    scaledPower = 200,
                    combat = new RaidWorkerCombatPayload(),
                },
            },
            loadoutEntries = new List<RaidWorkerLoadoutEntry>
            {
                new() { cardId = "1/1", count = 2 },
            },
            armyUnits = new List<RaidWorkerUnitAuthority>
            {
                new()
                {
                    actorId = "army:1/1",
                    contentId = "1/1",
                    unitKind = "ARMY",
                    count = 2,
                    basePower = 65,
                    scaledPower = 65,
                    combat = ArmyCombat(),
                },
            },
            defenseUnits = new List<RaidWorkerUnitAuthority>
            {
                Unit("tower:a", "1/1", "TOWER", 100, TowerCombat(), 0f, 12f),
                Unit("tower:b", "1/1", "TOWER", 100, TowerCombat(), 2f, 3f),
                Unit("garrison:a", "1/1", "GARRISON", 65, ArmyCombat(), -2f, 7f),
                Unit("core", "town-core", "CORE", 140, CoreCombat(), 0f, 0f),
            },
            targetSnapshot = new RaidWorkerBaseLayout
            {
                version = 1,
                factionId = "1",
                towers = new List<RaidWorkerTower>
                {
                    new()
                    {
                        towerId = "1/1",
                        position = new RaidWorkerVector3 { x = 0f, z = 12f },
                        attackLevel = 1,
                    },
                    new()
                    {
                        towerId = "1/1",
                        position = new RaidWorkerVector3 { x = 2f, z = 3f },
                    },
                },
                garrison = new List<RaidWorkerGarrison>
                {
                    new()
                    {
                        cardId = "1/1",
                        position = new RaidWorkerVector3 { x = -2f, z = 7f },
                    },
                },
            },
        };

        private static RaidWorkerCombatPayload ArmyCombat() => new()
        {
            maxHealth = 450,
            attackDamage = 35,
            attackCooldownMs = 2000,
            attackRangeMilli = 3000,
            moveSpeedMilli = 5000,
            maxTargets = 1,
        };

        private static RaidWorkerCombatPayload TowerCombat() => new()
        {
            maxHealth = 1000,
            armor = 50,
            attackDamage = 10,
            attackCooldownMs = 500,
            attackRangeMilli = 15000,
            maxTargets = 1,
        };

        private static RaidWorkerCombatPayload CoreCombat() => new()
        {
            maxHealth = 6000,
            armor = 20,
            attackDamage = 50,
            attackCooldownMs = 1000,
            attackRangeMilli = 10000,
            maxTargets = 1,
        };

        private static RaidWorkerUnitAuthority Unit(string actorId, string contentId, string kind,
            long power, RaidWorkerCombatPayload combat, float x, float z) => new()
        {
            actorId = actorId,
            contentId = contentId,
            unitKind = kind,
            count = 1,
            basePower = power,
            scaledPower = power,
            combat = combat,
            position = new RaidWorkerVector3 { x = x, z = z },
        };

        [Test]
        public void SameImmutableInputsProduceSameResultAndHash()
        {
            var first = DeterministicRaidSimulator.Simulate(Input());
            var second = DeterministicRaidSimulator.Simulate(Input());
            Assert.AreEqual(first.outcome, second.outcome);
            Assert.AreEqual(first.breachedRings, second.breachedRings);
            Assert.AreEqual(first.durationMs, second.durationMs);
            Assert.AreEqual(first.simulationHash, second.simulationHash);
            StringAssert.IsMatch("^[0-9a-f]{64}$", first.simulationHash);
        }

        [Test]
        public void StrongerArmyCannotProduceSchemaInvalidResult()
        {
            var input = Input();
            input.attackerPower = 10000;
            var result = DeterministicRaidSimulator.Simulate(input);
            Assert.AreEqual("FULL_VICTORY", result.outcome);
            Assert.AreEqual(3, result.breachedRings);
            Assert.That(result.durationMs, Is.InRange(1000, 3600000));
        }

        [Test]
        public void TrustedClientRejectsPublicAndTraversalRoutes()
        {
            Assert.IsTrue(TrustedRaidWorkerClient.IsAllowedInternalPath("/internal/v1/raid-jobs/claim"));
            Assert.IsFalse(TrustedRaidWorkerClient.IsAllowedInternalPath("/v1/wallet"));
            Assert.IsFalse(TrustedRaidWorkerClient.IsAllowedInternalPath("/internal/v1/../wallet"));
        }

        [Test]
        public void WorkerClaimDeserializesImmutableHeroAndGearPayload()
        {
            const string json = "{\"hasJob\":true,\"attackerPower\":3160," +
                "\"armyPower\":130,\"heroPower\":2830,\"gearPower\":200," +
                "\"hero\":{\"contentId\":\"hero/hero_test\",\"level\":1," +
                "\"basePower\":2830,\"scaledPower\":2830,\"combat\":{" +
                "\"maxHealth\":30000,\"attackDamage\":1000,\"abilityId\":\"breach_charge\"}}," +
                "\"gearItems\":[{\"instanceId\":\"61000000-0000-0000-0000-000000000001\"," +
                "\"contentId\":\"gear/test-blade\",\"level\":1,\"basePower\":200," +
                "\"scaledPower\":200,\"combat\":{}}]," +
                "\"armyUnits\":[{\"actorId\":\"army:1/1\",\"contentId\":\"1/1\"," +
                "\"unitKind\":\"ARMY\",\"count\":2,\"basePower\":65,\"scaledPower\":65," +
                "\"combat\":{\"maxHealth\":450,\"attackDamage\":35,\"attackCooldownMs\":2000," +
                "\"moveSpeedMilli\":5000,\"maxTargets\":1}}]," +
                "\"defenseUnits\":[{\"actorId\":\"core\",\"contentId\":\"town-core\"," +
                "\"unitKind\":\"CORE\",\"count\":1,\"basePower\":140,\"scaledPower\":140," +
                "\"combat\":{\"maxHealth\":6000,\"attackDamage\":50,\"attackCooldownMs\":1000," +
                "\"maxTargets\":1}}]," +
                "\"targetSnapshot\":{\"version\":1,\"factionId\":\"1\"," +
                "\"towers\":[{\"towerId\":\"1/1\",\"position\":{\"x\":2,\"y\":0,\"z\":7}}]," +
                "\"garrison\":[],\"minerCardIds\":[],\"storedGold\":100}}";

            var job = JsonUtility.FromJson<RaidJobResponse>(json);

            Assert.That(job.attackerPower, Is.EqualTo(3160));
            Assert.That(job.hero.contentId, Is.EqualTo("hero/hero_test"));
            Assert.That(job.hero.combat.maxHealth, Is.EqualTo(30000));
            Assert.That(job.hero.combat.abilityId, Is.EqualTo("breach_charge"));
            Assert.That(job.gearItems, Has.Count.EqualTo(1));
            Assert.That(job.gearItems[0].instanceId,
                Is.EqualTo("61000000-0000-0000-0000-000000000001"));
            Assert.That(job.armyUnits.Single().combat.maxHealth, Is.EqualTo(450));
            Assert.That(job.defenseUnits.Single().unitKind, Is.EqualTo("CORE"));
            Assert.That(job.targetSnapshot.towers, Has.Count.EqualTo(1));
            Assert.That(job.targetSnapshot.towers[0].position.z, Is.EqualTo(7f));
        }

        [Test]
        public void FixedTickKernelProducesDeterministicCommandStream()
        {
            var first = FixedTickRaidSimulator.Simulate(FixedInput());
            var second = FixedTickRaidSimulator.Simulate(FixedInput());

            Assert.That(first.outcome, Is.EqualTo("FULL_VICTORY"));
            Assert.That(first.breachedRings, Is.EqualTo(3));
            Assert.That(second.simulationHash, Is.EqualTo(first.simulationHash));
            Assert.That(second.commandStreamHash, Is.EqualTo(first.commandStreamHash));
            Assert.That(first.simulationVersion, Is.EqualTo("fixed-tick-c4c2c-v2"));
            Assert.That(first.durationMs % FixedTickRaidSimulator.TickMilliseconds, Is.Zero);
            Assert.That(first.commands.Last().type, Is.EqualTo("COMPLETE"));
            Assert.That(first.commands.Select(command => command.tick), Is.Ordered);
            StringAssert.IsMatch("^[0-9a-f]{64}$", first.commandStreamHash);
        }

        [Test]
        public void FixedTickKernelCanonicalizesImmutableCollectionOrder()
        {
            var original = FixedInput();
            var reordered = FixedInput();
            reordered.armyUnits.Reverse();
            reordered.defenseUnits.Reverse();

            var first = FixedTickRaidSimulator.Simulate(original);
            var second = FixedTickRaidSimulator.Simulate(reordered);

            Assert.That(second.simulationHash, Is.EqualTo(first.simulationHash));
            Assert.That(second.commandStreamHash, Is.EqualTo(first.commandStreamHash));
        }

        [Test]
        public void FixedTickKernelRejectsForgedPowerBreakdown()
        {
            var input = FixedInput();
            input.attackerPower++;

            Assert.Throws<System.ArgumentException>(() => FixedTickRaidSimulator.Simulate(input));
        }

        [Test]
        public void FixedTickKernelHashChangesWhenImmutableTownPositionChanges()
        {
            var firstInput = FixedInput();
            var changedInput = FixedInput();
            changedInput.defenseUnits[0].position.z += 1f;

            var first = FixedTickRaidSimulator.Simulate(firstInput);
            var changed = FixedTickRaidSimulator.Simulate(changedInput);

            Assert.That(changed.simulationHash, Is.Not.EqualTo(first.simulationHash));
        }

        [Test]
        public void FixedTickKernelEmitsPerActorTargetsAndDefeatedEvents()
        {
            var result = FixedTickRaidSimulator.Simulate(FixedInput());

            Assert.That(result.commands.Any(command => command.type == "ATTACK" &&
                command.actor.StartsWith("army:", System.StringComparison.Ordinal) &&
                command.target.StartsWith("tower:", System.StringComparison.Ordinal)), Is.True);
            Assert.That(result.commands.Any(command => command.type == "DEFEATED" &&
                command.actor.StartsWith("tower:", System.StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void FixedTickKernelRejectsArmyCombatPayloadWhosePowerWasNotSnapshotted()
        {
            var input = FixedInput();
            input.armyUnits[0].scaledPower++;

            Assert.Throws<System.ArgumentException>(() => FixedTickRaidSimulator.Simulate(input));
        }

        [Test]
        public void CommandStreamPresentationAcceptsOnlyCompletedOrderedFixedTickResults()
        {
            var valid = FixedTickRaidSimulator.Simulate(FixedInput());
            Assert.That(RaidCommandStreamPresentationController.TryValidateStream(valid, out var validError),
                Is.True, validError);

            var forged = FixedTickRaidSimulator.Simulate(FixedInput());
            forged.commands[1].tick = -1;
            Assert.That(RaidCommandStreamPresentationController.TryValidateStream(forged, out var error),
                Is.False);
            Assert.That(error, Does.Contain("order"));
        }

        [Test]
        public void VisibleRaidAnchorsStayOrderedInsideRealSpawnToCoreLane()
        {
            var spawn = new Vector3(10f, 3f, -90f);
            var core = new Vector3(6f, 3f, 60f);
            RaidCommandStreamPresentationController.CalculateVisibleRaidAnchors(spawn, core,
                out var entry, out var outer, out var inner, out var coreRing);

            Assert.That(entry.z, Is.GreaterThan(spawn.z));
            Assert.That(outer.z, Is.GreaterThan(entry.z));
            Assert.That(inner.z, Is.GreaterThan(outer.z));
            Assert.That(coreRing.z, Is.GreaterThan(inner.z).And.LessThan(core.z));
        }
    }
}
#endif
