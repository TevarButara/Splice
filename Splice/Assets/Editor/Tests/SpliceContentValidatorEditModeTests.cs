#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Splice.Data;
using Splice.Editor.Validation;
using Splice.Validation;
using UnityEngine;

namespace Splice.Tests.EditMode
{
    public sealed class SpliceContentValidatorEditModeTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void ProjectContent_HasNoValidationErrors()
        {
            var report = SpliceContentValidatorMenu.ValidateProject();
            Assert.That(report.ErrorCount, Is.Zero, report.DetailedSummary());
        }

        [Test]
        public void BrokenContent_ProducesStableRegressionCodes()
        {
            var faction = Make<FactionSO>();
            faction.factionId = "bad/faction";
            faction.displayName = "Bad Faction";
            var monster = Make<MonsterDefinitionSO>();
            monster.monsterId = "broken_monster";
            monster.displayName = "Broken Monster";
            monster.maxHealth = 0;
            monster.attackCooldown = 1f;
            monster.attackRange = 1f;
            monster.moveSpeed = 1f;
            monster.footprint = 1f;
            monster.defenseCapacityCost = 1;
            var first = Make<CardDefinitionSO>();
            first.cardId = "duplicate";
            first.displayName = "First";
            first.cardType = CardType.Monster;
            first.linkedMonster = monster;
            first.requiredLevel = 1;
            var second = Make<CardDefinitionSO>();
            second.cardId = "duplicate";
            second.displayName = "Second";
            second.cardType = CardType.Miner;
            second.requiredLevel = 1;
            faction.cards.Add(first);
            faction.minerCards.Add(second);
            var badProjectilePrefab = new GameObject("ProjectileWithoutVisual");
            created.Add(badProjectilePrefab);
            var projectile = Make<ProjectileDefinitionSO>();
            projectile.projectilePrefab = badProjectilePrefab;
            projectile.startSpeed = 1f;
            projectile.endSpeed = 1f;
            projectile.maxLifetime = 1f;
            var registry = Make<FactionRegistrySO>();
            SetPrivateList(registry, "factions", faction);
            var heroRegistry = Make<HeroRegistrySO>();

            var report = SpliceContentValidationCore.Validate(
                new[] { registry }, new[] { heroRegistry }, new[] { faction }, new[] { first, second },
                null, new[] { monster }, null, null, null, new[] { projectile });

            AssertCode(report, "FACTION_ID_SEPARATOR");
            AssertCode(report, "CARD_ID_DUPLICATE");
            AssertCode(report, "CARD_MINER_MISSING");
            AssertCode(report, "MONSTER_HEALTH_INVALID");
            AssertCode(report, "PREFAB_MISSING");
            AssertCode(report, "PROJECTILE_VISUAL_MISSING");
        }

        [Test]
        public void FactionRegistry_RebuildsCacheAfterInvalidation()
        {
            var faction = Make<FactionSO>();
            faction.factionId = "natural";
            var first = Make<CardDefinitionSO>();
            first.cardId = "first";
            var second = Make<CardDefinitionSO>();
            second.cardId = "second";
            faction.cards.Add(first);
            var registry = Make<FactionRegistrySO>();
            SetPrivateList(registry, "factions", faction);

            Assert.That(registry.ResolveCard("natural/first"), Is.SameAs(first));
            faction.cards.Clear();
            faction.cards.Add(second);
            registry.InvalidateCache();

            Assert.That(registry.ResolveCard("natural/first"), Is.Null);
            Assert.That(registry.ResolveCard("natural/second"), Is.SameAs(second));
        }

        private T Make<T>() where T : ScriptableObject
        {
            var item = ScriptableObject.CreateInstance<T>();
            created.Add(item);
            return item;
        }

        private static void SetPrivateList<T>(Object target, string fieldName, params T[] values)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, new List<T>(values));
        }

        private static void AssertCode(ContentValidationReport report, string code)
        {
            Assert.That(report.Issues, Has.Some.Matches<ContentValidationIssue>(issue => issue.Code == code), report.DetailedSummary());
        }
    }
}
#endif
