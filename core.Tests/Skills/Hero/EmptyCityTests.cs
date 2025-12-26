using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class EmptyCityTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateSlashCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateDuelCard(int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "duel",
            Name = "决斗",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Duel,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateOtherCard(int id = 3)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "other",
            Name = "其他",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that EmptyCitySkillFactory creates correct skill instance.
    /// Input: EmptyCitySkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void EmptyCitySkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new EmptyCitySkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("empty_city", skill.Id);
        Assert.AreEqual("空城", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.IsTrue(skill is ITargetFilteringSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Empty City skill.
    /// Input: Empty registry, EmptyCitySkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterEmptyCitySkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new EmptyCitySkillFactory();

        // Act
        registry.RegisterSkill("empty_city", factory);
        var skill = registry.GetSkill("empty_city");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("empty_city", skill.Id);
        Assert.AreEqual("空城", skill.Name);
    }

    #endregion

    #region Target Filtering Tests

    /// <summary>
    /// Tests that EmptyCitySkill excludes the owner from Slash targets when owner has no hand cards.
    /// Input: Game with 2 players, target has Empty City skill and no hand cards, source uses Slash.
    /// Expected: Target is excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void EmptyCitySkillExcludesOwnerFromSlashTargetsWhenNoHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Empty City skill

        var slash = CreateSlashCard();
        ((Zone)source.HandZone).MutableCards.Add(slash);

        // Ensure target has no hand cards
        ((Zone)target.HandZone).MutableCards.Clear();

        // Setup skill manager and register Empty City skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("empty_city", new EmptyCitySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Empty City skill to target player
        var emptyCitySkill = skillRegistry.GetSkill("empty_city");
        if (emptyCitySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, emptyCitySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        // In a 2-player game, if the target is excluded, there should be no legal targets
        Assert.IsFalse(legalTargets.HasAny, 
            "Target with Empty City skill and no hand cards should be excluded from Slash targets, leaving no legal targets in a 2-player game.");
    }

    /// <summary>
    /// Tests that EmptyCitySkill does NOT exclude the owner from Slash targets when owner has hand cards.
    /// Input: Game with 2 players, target has Empty City skill but has hand cards, source uses Slash.
    /// Expected: Target is NOT excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void EmptyCitySkillDoesNotExcludeOwnerFromSlashTargetsWhenHasHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Empty City skill

        var slash = CreateSlashCard();
        ((Zone)source.HandZone).MutableCards.Add(slash);

        // Give target a hand card (skill should not be active)
        var targetCard = CreateOtherCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        // Setup skill manager and register Empty City skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("empty_city", new EmptyCitySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Empty City skill to target player
        var emptyCitySkill = skillRegistry.GetSkill("empty_city");
        if (emptyCitySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, emptyCitySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "There should be legal targets.");
        Assert.IsTrue(legalTargets.Items.Any(p => p.Seat == target.Seat), 
            "Target with Empty City skill but with hand cards should NOT be excluded from Slash targets.");
    }

    /// <summary>
    /// Tests that EmptyCitySkill excludes the owner from Duel targets when owner has no hand cards.
    /// Input: Game with 2 players, target has Empty City skill and no hand cards, source uses Duel.
    /// Expected: Target is excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void EmptyCitySkillExcludesOwnerFromDuelTargetsWhenNoHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Empty City skill

        var duel = CreateDuelCard();
        ((Zone)source.HandZone).MutableCards.Add(duel);

        // Ensure target has no hand cards
        ((Zone)target.HandZone).MutableCards.Clear();

        // Setup skill manager and register Empty City skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("empty_city", new EmptyCitySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Empty City skill to target player
        var emptyCitySkill = skillRegistry.GetSkill("empty_city");
        if (emptyCitySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, emptyCitySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            duel,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsFalse(legalTargets.HasAny, 
            "Target with Empty City skill and no hand cards should be excluded from Duel targets, leaving no legal targets.");
    }

    /// <summary>
    /// Tests that EmptyCitySkill does NOT exclude the owner from Duel targets when owner has hand cards.
    /// Input: Game with 2 players, target has Empty City skill but has hand cards, source uses Duel.
    /// Expected: Target is NOT excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void EmptyCitySkillDoesNotExcludeOwnerFromDuelTargetsWhenHasHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Empty City skill

        var duel = CreateDuelCard();
        ((Zone)source.HandZone).MutableCards.Add(duel);

        // Give target a hand card (skill should not be active)
        var targetCard = CreateOtherCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        // Setup skill manager and register Empty City skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("empty_city", new EmptyCitySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Empty City skill to target player
        var emptyCitySkill = skillRegistry.GetSkill("empty_city");
        if (emptyCitySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, emptyCitySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            duel,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "There should be legal targets.");
        Assert.IsTrue(legalTargets.Items.Any(p => p.Seat == target.Seat), 
            "Target with Empty City skill but with hand cards should NOT be excluded from Duel targets.");
    }

    /// <summary>
    /// Tests that EmptyCitySkill does NOT exclude the owner from other card types (e.g., GuoheChaiqiao).
    /// Input: Game with 2 players, target has Empty City skill and no hand cards, source uses GuoheChaiqiao.
    /// Expected: Target is NOT excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void EmptyCitySkillDoesNotExcludeOwnerFromOtherCardTypes()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Empty City skill

        var guoheChaiqiao = new Card
        {
            Id = 1,
            DefinitionId = "guohe_chaiqiao",
            Name = "过河拆桥",
            CardType = CardType.Trick,
            CardSubType = CardSubType.GuoheChaiqiao,
            Suit = Suit.Heart,
            Rank = 3
        };
        ((Zone)source.HandZone).MutableCards.Add(guoheChaiqiao);

        // Ensure target has no hand cards
        ((Zone)target.HandZone).MutableCards.Clear();

        // Setup skill manager and register Empty City skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("empty_city", new EmptyCitySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Empty City skill to target player
        var emptyCitySkill = skillRegistry.GetSkill("empty_city");
        if (emptyCitySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, emptyCitySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            guoheChaiqiao,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "There should be legal targets.");
        Assert.IsTrue(legalTargets.Items.Any(p => p.Seat == target.Seat), 
            "Target with Empty City skill should NOT be excluded from GuoheChaiqiao targets (only Slash and Duel are affected).");
    }

    #endregion
}
