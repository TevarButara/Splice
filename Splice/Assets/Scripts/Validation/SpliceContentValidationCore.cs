using System;
using System.Collections.Generic;
using Splice.Characters;
using Splice.Combat;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Validation
{
    public static class SpliceContentValidationCore
    {
        public static ContentValidationReport Validate(
            IEnumerable<FactionRegistrySO> factionRegistries,
            IEnumerable<HeroRegistrySO> heroRegistries,
            IEnumerable<FactionSO> allFactions = null,
            IEnumerable<CardDefinitionSO> allCards = null,
            IEnumerable<TowerDefinitionSO> allTowers = null,
            IEnumerable<MonsterDefinitionSO> allMonsters = null,
            IEnumerable<MinerDefinitionSO> allMiners = null,
            IEnumerable<HeroDefinitionSO> allHeroes = null,
            IEnumerable<HeroAbilityDefinitionSO> allHeroAbilities = null,
            IEnumerable<ProjectileDefinitionSO> allProjectiles = null)
        {
            var report = new ContentValidationReport();
            var factions = Materialize(allFactions);
            var cards = Materialize(allCards);
            var towers = Materialize(allTowers);
            var monsters = Materialize(allMonsters);
            var miners = Materialize(allMiners);
            var heroes = Materialize(allHeroes);
            var abilities = Materialize(allHeroAbilities);
            var projectiles = Materialize(allProjectiles);

            ValidateFactionRegistries(Materialize(factionRegistries), report, factions, cards, towers, monsters, miners, projectiles);
            ValidateHeroRegistries(Materialize(heroRegistries), report, heroes, abilities);
            foreach (var faction in factions) ValidateFaction(faction, report);
            foreach (var card in cards) ValidateCard(card, report);
            foreach (var tower in towers) ValidateTower(tower, report);
            foreach (var monster in monsters) ValidateMonster(monster, report);
            foreach (var miner in miners) ValidateMiner(miner, report);
            foreach (var hero in heroes) ValidateHero(hero, report);
            foreach (var ability in abilities) ValidateHeroAbility(ability, report);
            foreach (var projectile in projectiles) ValidateProjectile(projectile, report);

            report.FactionCount = factions.Count;
            report.CardCount = cards.Count;
            report.TowerCount = towers.Count;
            report.MonsterCount = monsters.Count;
            report.MinerCount = miners.Count;
            report.HeroCount = heroes.Count;
            return report;
        }

        private static List<T> Materialize<T>(IEnumerable<T> values) where T : UnityEngine.Object
        {
            var result = new List<T>();
            if (values == null) return result;
            foreach (var value in values)
                if (value != null && !result.Contains(value)) result.Add(value);
            return result;
        }

        private static void ValidateFactionRegistries(List<FactionRegistrySO> registries, ContentValidationReport report,
            List<FactionSO> factions, List<CardDefinitionSO> cards, List<TowerDefinitionSO> towers,
            List<MonsterDefinitionSO> monsters, List<MinerDefinitionSO> miners,
            List<ProjectileDefinitionSO> projectiles)
        {
            report.FactionRegistryCount = registries.Count;
            if (registries.Count == 0) report.Error("REGISTRY_MISSING", "No FactionRegistrySO asset exists.");
            if (registries.Count > 1) report.Warning("REGISTRY_MULTIPLE", $"Found {registries.Count} faction registries; confirm which is authoritative.");

            foreach (var registry in registries)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                if (registry.Factions.Count == 0) report.Error("REGISTRY_EMPTY", $"Faction registry '{registry.name}' contains no factions.", registry);
                for (var i = 0; i < registry.Factions.Count; i++)
                {
                    var faction = registry.Factions[i];
                    if (faction == null)
                    {
                        report.Error("REGISTRY_NULL_FACTION", $"Faction registry '{registry.name}' has a null entry at index {i}.", registry);
                        continue;
                    }
                    AddUnique(factions, faction);
                    if (!string.IsNullOrWhiteSpace(faction.factionId) && !ids.Add(faction.factionId))
                        report.Error("FACTION_ID_DUPLICATE", $"Faction id '{faction.factionId}' is duplicated in '{registry.name}'.", faction);
                    CollectFactionContent(faction, cards, towers, monsters, miners, projectiles);
                }
            }
        }

        private static void CollectFactionContent(FactionSO faction, List<CardDefinitionSO> cards,
            List<TowerDefinitionSO> towers, List<MonsterDefinitionSO> monsters, List<MinerDefinitionSO> miners,
            List<ProjectileDefinitionSO> projectiles)
        {
            foreach (var card in faction.cards) CollectCard(card, cards, monsters, miners, projectiles);
            foreach (var card in faction.minerCards) CollectCard(card, cards, monsters, miners, projectiles);
            foreach (var tower in faction.towers) AddUnique(towers, tower);
        }

        private static void CollectCard(CardDefinitionSO card, List<CardDefinitionSO> cards,
            List<MonsterDefinitionSO> monsters, List<MinerDefinitionSO> miners,
            List<ProjectileDefinitionSO> projectiles)
        {
            if (card == null) return;
            AddUnique(cards, card);
            AddUnique(monsters, card.linkedMonster);
            AddUnique(miners, card.linkedMiner);
            if (card.linkedMonster != null) AddUnique(projectiles, card.linkedMonster.projectile);
        }

        private static void ValidateHeroRegistries(List<HeroRegistrySO> registries, ContentValidationReport report,
            List<HeroDefinitionSO> heroes, List<HeroAbilityDefinitionSO> abilities)
        {
            report.HeroRegistryCount = registries.Count;
            if (registries.Count == 0) report.Error("HERO_REGISTRY_MISSING", "No HeroRegistrySO asset exists.");
            if (registries.Count > 1) report.Warning("HERO_REGISTRY_MULTIPLE", $"Found {registries.Count} hero registries; confirm which is authoritative.");
            foreach (var registry in registries)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                if (registry.Heroes.Count == 0) report.Error("HERO_REGISTRY_EMPTY", $"Hero registry '{registry.name}' contains no heroes.", registry);
                for (var i = 0; i < registry.Heroes.Count; i++)
                {
                    var hero = registry.Heroes[i];
                    if (hero == null)
                    {
                        report.Error("HERO_REGISTRY_NULL", $"Hero registry '{registry.name}' has a null entry at index {i}.", registry);
                        continue;
                    }
                    AddUnique(heroes, hero);
                    AddUnique(abilities, hero.tacticalAbility);
                    if (!string.IsNullOrWhiteSpace(hero.heroId) && !ids.Add(hero.heroId))
                        report.Error("HERO_ID_DUPLICATE", $"Hero id '{hero.heroId}' is duplicated in '{registry.name}'.", hero);
                }
            }
        }

        private static void ValidateFaction(FactionSO faction, ContentValidationReport report)
        {
            RequireId(faction.factionId, "FACTION_ID_MISSING", "Faction id", faction, report);
            RejectSeparator(faction.factionId, "FACTION_ID_SEPARATOR", "Faction id", faction, report);
            RequireDisplayName(faction.displayName, "faction", faction, report);
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            ValidateFactionCardList(faction, faction.cards, "cards", false, cardIds, report);
            ValidateFactionCardList(faction, faction.minerCards, "minerCards", true, cardIds, report);
            var towerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < faction.towers.Count; i++)
            {
                var tower = faction.towers[i];
                if (tower == null)
                {
                    report.Error("FACTION_NULL_TOWER", $"Faction '{faction.factionId}' has a null tower at index {i}.", faction);
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(tower.towerId) && !towerIds.Add(tower.towerId))
                    report.Error("TOWER_ID_DUPLICATE", $"Tower id '{tower.towerId}' is duplicated in faction '{faction.factionId}'.", tower);
            }
            if (faction.towers.Count == 0) report.Warning("FACTION_NO_TOWERS", $"Faction '{faction.factionId}' has no towers.", faction);
        }

        private static void ValidateFactionCardList(FactionSO faction, IReadOnlyList<CardDefinitionSO> list,
            string listName, bool expectMiner, HashSet<string> ids, ContentValidationReport report)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var card = list[i];
                if (card == null)
                {
                    report.Error("FACTION_NULL_CARD", $"Faction '{faction.factionId}' has a null card in {listName}[{i}].", faction);
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(card.cardId) && !ids.Add(card.cardId))
                    report.Error("CARD_ID_DUPLICATE", $"Card id '{card.cardId}' is duplicated across cards/minerCards in faction '{faction.factionId}'.", card);
                if (expectMiner && card.cardType != CardType.Miner)
                    report.Error("MINER_LIST_WRONG_TYPE", $"Card '{card.cardId}' is in minerCards but type is {card.cardType}.", card);
                if (!expectMiner && card.cardType == CardType.Miner)
                    report.Warning("MINER_CARD_WRONG_LIST", $"Miner card '{card.cardId}' should be in minerCards, not cards.", card);
            }
        }

        private static void ValidateCard(CardDefinitionSO card, ContentValidationReport report)
        {
            RequireId(card.cardId, "CARD_ID_MISSING", "Card id", card, report);
            RejectSeparator(card.cardId, "CARD_ID_SEPARATOR", "Card id", card, report);
            RequireDisplayName(card.displayName, "card", card, report);
            if (card.goldCost < 0) report.Error("CARD_NEGATIVE_COST", $"Card '{card.cardId}' has negative gold cost.", card);
            if (card.requiredLevel < 1) report.Error("CARD_INVALID_LEVEL", $"Card '{card.cardId}' requires level {card.requiredLevel}; minimum is 1.", card);
            if (card.cardType == CardType.Monster && card.linkedMonster == null)
                report.Error("CARD_MONSTER_MISSING", $"Monster card '{card.cardId}' has no linked monster.", card);
            if (card.cardType == CardType.Miner && card.linkedMiner == null)
                report.Error("CARD_MINER_MISSING", $"Miner card '{card.cardId}' has no linked miner.", card);
            if (card.cardType == CardType.Monster && card.linkedMiner != null)
                report.Warning("CARD_UNUSED_MINER", $"Monster card '{card.cardId}' also links a miner that runtime ignores.", card);
            if (card.cardType == CardType.Miner && card.linkedMonster != null)
                report.Warning("CARD_UNUSED_MONSTER", $"Miner card '{card.cardId}' also links a monster that runtime ignores.", card);
        }

        private static void ValidateTower(TowerDefinitionSO tower, ContentValidationReport report)
        {
            RequireId(tower.towerId, "TOWER_ID_MISSING", "Tower id", tower, report);
            RejectSeparator(tower.towerId, "TOWER_ID_SEPARATOR", "Tower id", tower, report);
            RequireDisplayName(tower.displayName, "tower", tower, report);
            Positive(tower.maxHealth, "TOWER_HEALTH_INVALID", $"Tower '{tower.towerId}' max health", tower, report);
            NonNegative(tower.attackDamage, "TOWER_DAMAGE_INVALID", $"Tower '{tower.towerId}' attack damage", tower, report);
            Positive(tower.attackRange, "TOWER_RANGE_INVALID", $"Tower '{tower.towerId}' attack range", tower, report);
            Positive(tower.attackCooldown, "TOWER_COOLDOWN_INVALID", $"Tower '{tower.towerId}' attack cooldown", tower, report);
            Positive(tower.footprint, "TOWER_FOOTPRINT_INVALID", $"Tower '{tower.towerId}' footprint", tower, report);
            Positive(tower.defenseCapacityCost, "TOWER_CAPACITY_INVALID", $"Tower '{tower.towerId}' defense capacity cost", tower, report);
            ValidateNetworkPrefab<TowerCharacter>(tower.prefab, "tower", tower.towerId, tower, report);
            if (tower.nextTier == tower) report.Error("TOWER_TIER_SELF", $"Tower '{tower.towerId}' points to itself as next tier.", tower);
        }

        private static void ValidateMonster(MonsterDefinitionSO monster, ContentValidationReport report)
        {
            RequireId(monster.monsterId, "MONSTER_ID_MISSING", "Monster id", monster, report);
            RejectSeparator(monster.monsterId, "MONSTER_ID_SEPARATOR", "Monster id", monster, report);
            RequireDisplayName(monster.displayName, "monster", monster, report);
            Positive(monster.maxHealth, "MONSTER_HEALTH_INVALID", $"Monster '{monster.monsterId}' max health", monster, report);
            NonNegative(monster.attackDamage, "MONSTER_DAMAGE_INVALID", $"Monster '{monster.monsterId}' attack damage", monster, report);
            Positive(monster.attackCooldown, "MONSTER_COOLDOWN_INVALID", $"Monster '{monster.monsterId}' attack cooldown", monster, report);
            Positive(monster.attackRange, "MONSTER_RANGE_INVALID", $"Monster '{monster.monsterId}' attack range", monster, report);
            Positive(monster.moveSpeed, "MONSTER_SPEED_INVALID", $"Monster '{monster.monsterId}' move speed", monster, report);
            Positive(monster.footprint, "MONSTER_FOOTPRINT_INVALID", $"Monster '{monster.monsterId}' footprint", monster, report);
            Positive(monster.defenseCapacityCost, "MONSTER_CAPACITY_INVALID", $"Monster '{monster.monsterId}' defense capacity cost", monster, report);
            if (monster.role == MonsterRole.Supporter && monster.spell == null)
                report.Error("SUPPORTER_SPELL_MISSING", $"Supporter '{monster.monsterId}' has no spell.", monster);
            ValidateNetworkPrefab<MonsterCharacter>(monster.prefab, "monster", monster.monsterId, monster, report);
        }

        private static void ValidateMiner(MinerDefinitionSO miner, ContentValidationReport report)
        {
            RequireId(miner.minerId, "MINER_ID_MISSING", "Miner id", miner, report);
            RejectSeparator(miner.minerId, "MINER_ID_SEPARATOR", "Miner id", miner, report);
            RequireDisplayName(miner.displayName, "miner", miner, report);
            Positive(miner.maxHealth, "MINER_HEALTH_INVALID", $"Miner '{miner.minerId}' max health", miner, report);
            Positive(miner.moveSpeed, "MINER_SPEED_INVALID", $"Miner '{miner.minerId}' move speed", miner, report);
            Positive(miner.carryCapacity, "MINER_CAPACITY_INVALID", $"Miner '{miner.minerId}' carry capacity", miner, report);
            Positive(miner.mineDurationSeconds, "MINER_DURATION_INVALID", $"Miner '{miner.minerId}' mine duration", miner, report);
            ValidateNetworkPrefab<MinerCharacter>(miner.prefab, "miner", miner.minerId, miner, report);
        }

        private static void ValidateHero(HeroDefinitionSO hero, ContentValidationReport report)
        {
            RequireId(hero.heroId, "HERO_ID_MISSING", "Hero id", hero, report);
            RejectSeparator(hero.heroId, "HERO_ID_SEPARATOR", "Hero id", hero, report);
            RequireDisplayName(hero.displayName, "hero", hero, report);
            Positive(hero.maxHealth, "HERO_HEALTH_INVALID", $"Hero '{hero.heroId}' max health", hero, report);
            Positive(hero.attackDamage, "HERO_DAMAGE_INVALID", $"Hero '{hero.heroId}' attack damage", hero, report);
            Positive(hero.attackCooldown, "HERO_COOLDOWN_INVALID", $"Hero '{hero.heroId}' attack cooldown", hero, report);
            Positive(hero.moveSpeed, "HERO_SPEED_INVALID", $"Hero '{hero.heroId}' move speed", hero, report);
            if (hero.reviveHealthPercent <= 0f || hero.reviveHealthPercent > 1f)
                report.Error("HERO_REVIVE_PERCENT_INVALID", $"Hero '{hero.heroId}' revive health percent must be within (0, 1].", hero);
            ValidateNetworkPrefab<RaidHeroCharacter>(hero.prefab, "hero", hero.heroId, hero, report);
        }

        private static void ValidateHeroAbility(HeroAbilityDefinitionSO ability, ContentValidationReport report)
        {
            RequireId(ability.abilityId, "ABILITY_ID_MISSING", "Ability id", ability, report);
            RejectSeparator(ability.abilityId, "ABILITY_ID_SEPARATOR", "Ability id", ability, report);
            RequireDisplayName(ability.displayName, "ability", ability, report);
            Positive(ability.castRange, "ABILITY_RANGE_INVALID", $"Ability '{ability.abilityId}' cast range", ability, report);
            Positive(ability.effectRadius, "ABILITY_RADIUS_INVALID", $"Ability '{ability.abilityId}' effect radius", ability, report);
            Positive(ability.damage, "ABILITY_DAMAGE_INVALID", $"Ability '{ability.abilityId}' damage", ability, report);
            Positive(ability.cooldownSeconds, "ABILITY_COOLDOWN_INVALID", $"Ability '{ability.abilityId}' cooldown", ability, report);
        }

        private static void ValidateProjectile(ProjectileDefinitionSO projectile, ContentValidationReport report)
        {
            if (projectile.projectilePrefab == null)
            {
                report.Error("PROJECTILE_PREFAB_MISSING", $"Projectile definition '{projectile.name}' has no prefab.", projectile);
                return;
            }
            if (projectile.projectilePrefab.GetComponentInChildren<ProjectileVisual>(true) == null)
                report.Error("PROJECTILE_VISUAL_MISSING", $"Projectile prefab '{projectile.projectilePrefab.name}' has no ProjectileVisual component.", projectile);
            Positive(projectile.startSpeed, "PROJECTILE_START_SPEED_INVALID", $"Projectile '{projectile.name}' start speed", projectile, report);
            Positive(projectile.endSpeed, "PROJECTILE_END_SPEED_INVALID", $"Projectile '{projectile.name}' end speed", projectile, report);
            Positive(projectile.maxLifetime, "PROJECTILE_LIFETIME_INVALID", $"Projectile '{projectile.name}' max lifetime", projectile, report);
            if (projectile.maxHeight < projectile.minHeight)
                report.Error("PROJECTILE_HEIGHT_INVALID", $"Projectile '{projectile.name}' max height is below min height.", projectile);
        }

        private static void ValidateNetworkPrefab<T>(GameObject prefab, string kind, string id, UnityEngine.Object context,
            ContentValidationReport report) where T : Component
        {
            if (prefab == null)
            {
                report.Error("PREFAB_MISSING", $"{kind} '{id}' has no prefab.", context);
                return;
            }
            if (prefab.GetComponent<NetworkObject>() == null)
                report.Error("NETWORK_OBJECT_MISSING", $"{kind} prefab '{prefab.name}' needs NetworkObject on its root.", context);
            if (prefab.GetComponentInChildren<T>(true) == null)
                report.Error("CHARACTER_COMPONENT_MISSING", $"{kind} prefab '{prefab.name}' needs {typeof(T).Name}.", context);
        }

        private static void RequireId(string value, string code, string label, UnityEngine.Object context, ContentValidationReport report)
        { if (string.IsNullOrWhiteSpace(value)) report.Error(code, $"{label} is empty.", context); }

        private static void RejectSeparator(string value, string code, string label, UnityEngine.Object context, ContentValidationReport report)
        { if (!string.IsNullOrEmpty(value) && value.Contains("/")) report.Error(code, $"{label} '{value}' contains '/'.", context); }

        private static void RequireDisplayName(string value, string kind, UnityEngine.Object context, ContentValidationReport report)
        { if (string.IsNullOrWhiteSpace(value)) report.Warning("DISPLAY_NAME_MISSING", $"{kind} '{context.name}' has no display name.", context); }

        private static void Positive(float value, string code, string label, UnityEngine.Object context, ContentValidationReport report)
        { if (value <= 0f) report.Error(code, $"{label} must be greater than zero (is {value}).", context); }

        private static void NonNegative(float value, string code, string label, UnityEngine.Object context, ContentValidationReport report)
        { if (value < 0f) report.Error(code, $"{label} cannot be negative (is {value}).", context); }

        private static void AddUnique<T>(List<T> list, T item) where T : UnityEngine.Object
        { if (item != null && !list.Contains(item)) list.Add(item); }
    }
}
