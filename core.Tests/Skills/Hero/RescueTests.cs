using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class RescueTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithFactions(int playerCount, Dictionary<int, string> factionMap)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
        var playerConfigs = new List<PlayerConfig>();
        
        for (int i = 0; i < playerCount; i++)
        {
            var playerConfig = new PlayerConfig
            {
                Seat = i,
                MaxHealth = 4,
                InitialHealth = 4,
                FactionId = factionMap.TryGetValue(i, out var faction) ? faction : null
            };
            playerConfigs.Add(playerConfig);
        }

        var config = new GameConfig
        {
            PlayerConfigs = playerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions
        };

        return Game.FromConfig(config);
    }

    private static Card CreatePeachCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "peach",
            Name = "桃",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that RescueSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void RescueSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new RescueSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("rescue", skill.Id);
        Assert.AreEqual("救援", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.IsTrue(skill is IRecoverAmountModifyingSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Rescue skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterRescueSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new RescueSkillFactory();

        // Act
        registry.RegisterSkill("rescue", factory);
        var skill = registry.GetSkill("rescue");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("rescue", skill.Id);
        Assert.AreEqual("救援", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that RescueSkill has correct properties.
    /// </summary>
    [TestMethod]
    public void RescueSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new RescueSkill();

        // Assert
        Assert.AreEqual("rescue", skill.Id);
        Assert.AreEqual("救援", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.IntervenesResolution));
    }

    #endregion

    #region Recovery Modification Tests

    /// <summary>
    /// Tests that RescueSkill increases recovery by 1 when Wu faction character uses Peach on Lord.
    /// </summary>
    [TestMethod]
    public void RescueSkill_IncreasesRecovery_WhenWuFactionUsesPeachOnLord()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Lord (target)
            { 1, "Wu" }  // Wu faction character (source)
        };
        var game = CreateGameWithFactions(2, factionMap);
        var lord = game.Players[0];
        var wuPlayer = game.Players[1];
        
        lord.Flags["IsLord"] = true;
        lord.CurrentHealth = 2; // Injured
        // MaxHealth is set via PlayerConfig (4)

        var peach = CreatePeachCard(1);
        var eventBus = new BasicEventBus();
        var skill = new RescueSkill();
        
        skill.Attach(game, lord, eventBus);

        var beforeRecoverEvent = new BeforeRecoverEvent(
            game,
            wuPlayer,
            lord,
            BaseAmount: 1,
            peach,
            Reason: "Peach");

        // Act
        eventBus.Publish(beforeRecoverEvent);

        // Assert
        Assert.AreEqual(1, beforeRecoverEvent.RecoveryModification, "Recovery should be increased by 1");
    }

    /// <summary>
    /// Tests that RescueSkill does not increase recovery when target is not Lord.
    /// </summary>
    [TestMethod]
    public void RescueSkill_DoesNotIncreaseRecovery_WhenTargetIsNotLord()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Not Lord (target)
            { 1, "Wu" }  // Wu faction character (source)
        };
        var game = CreateGameWithFactions(2, factionMap);
        var target = game.Players[0];
        var wuPlayer = game.Players[1];
        
        // Target is not Lord
        target.CurrentHealth = 2;
        // MaxHealth is set via PlayerConfig (4)

        var peach = CreatePeachCard(1);
        var eventBus = new BasicEventBus();
        var skill = new RescueSkill();
        
        skill.Attach(game, target, eventBus);

        var beforeRecoverEvent = new BeforeRecoverEvent(
            game,
            wuPlayer,
            target,
            BaseAmount: 1,
            peach,
            Reason: "Peach");

        // Act
        eventBus.Publish(beforeRecoverEvent);

        // Assert
        Assert.AreEqual(0, beforeRecoverEvent.RecoveryModification, "Recovery should not be modified when target is not Lord");
    }

    /// <summary>
    /// Tests that RescueSkill does not increase recovery when source is not Wu faction.
    /// </summary>
    [TestMethod]
    public void RescueSkill_DoesNotIncreaseRecovery_WhenSourceIsNotWuFaction()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Lord (target)
            { 1, "Wei" } // Wei faction character (source)
        };
        var game = CreateGameWithFactions(2, factionMap);
        var lord = game.Players[0];
        var weiPlayer = game.Players[1];
        
        lord.Flags["IsLord"] = true;
        lord.CurrentHealth = 2;
        // MaxHealth is set via PlayerConfig (4)

        var peach = CreatePeachCard(1);
        var eventBus = new BasicEventBus();
        var skill = new RescueSkill();
        
        skill.Attach(game, lord, eventBus);

        var beforeRecoverEvent = new BeforeRecoverEvent(
            game,
            weiPlayer,
            lord,
            BaseAmount: 1,
            peach,
            Reason: "Peach");

        // Act
        eventBus.Publish(beforeRecoverEvent);

        // Assert
        Assert.AreEqual(0, beforeRecoverEvent.RecoveryModification, "Recovery should not be modified when source is not Wu faction");
    }

    /// <summary>
    /// Tests that RescueSkill does not increase recovery when source is the same as target (self).
    /// </summary>
    [TestMethod]
    public void RescueSkill_DoesNotIncreaseRecovery_WhenSourceIsSelf()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, "Wu" } // Lord with Wu faction (source and target are the same)
        };
        var game = CreateGameWithFactions(1, factionMap);
        var lord = game.Players[0];
        
        lord.Flags["IsLord"] = true;
        lord.CurrentHealth = 2;
        // MaxHealth is set via PlayerConfig (4)

        var peach = CreatePeachCard(1);
        var eventBus = new BasicEventBus();
        var skill = new RescueSkill();
        
        skill.Attach(game, lord, eventBus);

        var beforeRecoverEvent = new BeforeRecoverEvent(
            game,
            lord, // Source is the same as target
            lord,
            BaseAmount: 1,
            peach,
            Reason: "Peach");

        // Act
        eventBus.Publish(beforeRecoverEvent);

        // Assert
        Assert.AreEqual(0, beforeRecoverEvent.RecoveryModification, "Recovery should not be modified when source is self");
    }

    /// <summary>
    /// Tests that RescueSkill does not increase recovery when card is not Peach.
    /// </summary>
    [TestMethod]
    public void RescueSkill_DoesNotIncreaseRecovery_WhenCardIsNotPeach()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Lord (target)
            { 1, "Wu" }  // Wu faction character (source)
        };
        var game = CreateGameWithFactions(2, factionMap);
        var lord = game.Players[0];
        var wuPlayer = game.Players[1];
        
        lord.Flags["IsLord"] = true;
        lord.CurrentHealth = 2;
        // MaxHealth is set via PlayerConfig (4)

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };

        var eventBus = new BasicEventBus();
        var skill = new RescueSkill();
        
        skill.Attach(game, lord, eventBus);

        var beforeRecoverEvent = new BeforeRecoverEvent(
            game,
            wuPlayer,
            lord,
            BaseAmount: 1,
            slash, // Not Peach
            Reason: "Other");

        // Act
        eventBus.Publish(beforeRecoverEvent);

        // Assert
        Assert.AreEqual(0, beforeRecoverEvent.RecoveryModification, "Recovery should not be modified when card is not Peach");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that RescueSkill works correctly in actual Peach resolution.
    /// When Wu faction character uses Peach on Lord, recovery should be 2 instead of 1.
    /// </summary>
    [TestMethod]
    public void RescueSkill_Integration_RecoveryIncreasedInPeachResolution()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Lord (target)
            { 1, "Wu" }  // Wu faction character (source)
        };
        var game = CreateGameWithFactions(2, factionMap);
        var lord = game.Players[0];
        var wuPlayer = game.Players[1];
        
        lord.Flags["IsLord"] = true;
        lord.CurrentHealth = 2; // Injured
        lord.IsAlive = true; // Ensure player is alive
        wuPlayer.IsAlive = true; // Ensure player is alive
        // MaxHealth is set via PlayerConfig (4)

        var peach = CreatePeachCard(1);
        if (wuPlayer.HandZone is Zone wuHand)
        {
            wuHand.MutableCards.Add(peach);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("rescue", new RescueSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        
        // Manually add Rescue skill to lord (since LoadSkillsForPlayer requires HeroId)
        var rescueSkill = new RescueSkill();
        skillManager.AddEquipmentSkill(game, lord, rescueSkill);

        // Verify skill is attached
        var attachedSkills = skillManager.GetAllSkills(lord).ToList();
        Assert.IsTrue(attachedSkills.Contains(rescueSkill), "Rescue skill should be attached to lord");

        // Setup services
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        // Create choice result
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: wuPlayer.Seat,
            SelectedTargetSeats: new[] { lord.Seat },
            SelectedCardIds: new[] { peach.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Create action descriptor
        var action = new ActionDescriptor(
            ActionId: "UsePeach",
            DisplayKey: "action.usePeach",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(
                MinTargets: 0,
                MaxTargets: 0,
                FilterType: TargetFilterType.SelfOrFriends),
            CardCandidates: new[] { peach }
        );

        // Create resolution context
        var context = new ResolutionContext(
            game,
            wuPlayer,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            eventBus,
            LogCollector: null,
            skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        // Verify event bus is set
        Assert.IsNotNull(context.EventBus, "EventBus should be set in context");

        // Verify card can be extracted
        var extractedCard = context.ExtractCausingCard();
        Assert.IsNotNull(extractedCard, "Card should be extractable from context");
        Assert.AreEqual(CardSubType.Peach, extractedCard.CardSubType, "Extracted card should be Peach");

        var previousHealth = lord.CurrentHealth;

        // Act: Resolve Peach
        var resolver = new PeachResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Peach resolution should succeed");
        
        // Recovery should be 2 (base 1 + Rescue 1) instead of 1
        var expectedHealth = Math.Min(previousHealth + 2, lord.MaxHealth);
        Assert.AreEqual(expectedHealth, lord.CurrentHealth, 
            $"Lord should recover 2 HP (from {previousHealth} to {expectedHealth}) due to Rescue skill. " +
            $"Actual recovery: {lord.CurrentHealth - previousHealth}");
    }

    /// <summary>
    /// Tests that RescueSkill does not affect recovery when conditions are not met.
    /// When non-Wu faction character uses Peach on Lord, recovery should be 1 (normal).
    /// </summary>
    [TestMethod]
    public void RescueSkill_Integration_RecoveryNotIncreasedWhenConditionsNotMet()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Lord (target)
            { 1, "Wei" } // Wei faction character (source, not Wu)
        };
        var game = CreateGameWithFactions(2, factionMap);
        var lord = game.Players[0];
        var weiPlayer = game.Players[1];
        
        lord.Flags["IsLord"] = true;
        lord.CurrentHealth = 2; // Injured
        // MaxHealth is set via PlayerConfig (4)

        var peach = CreatePeachCard(1);
        if (weiPlayer.HandZone is Zone weiHand)
        {
            weiHand.MutableCards.Add(peach);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("rescue", new RescueSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        
        // Manually add Rescue skill to lord (since LoadSkillsForPlayer requires HeroId)
        var rescueSkill = new RescueSkill();
        skillManager.AddEquipmentSkill(game, lord, rescueSkill);

        // Setup services
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        // Create choice result
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: weiPlayer.Seat,
            SelectedTargetSeats: new[] { lord.Seat },
            SelectedCardIds: new[] { peach.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Create action descriptor
        var action = new ActionDescriptor(
            ActionId: "UsePeach",
            DisplayKey: "action.usePeach",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(
                MinTargets: 0,
                MaxTargets: 0,
                FilterType: TargetFilterType.SelfOrFriends),
            CardCandidates: new[] { peach }
        );

        // Create resolution context
        var context = new ResolutionContext(
            game,
            weiPlayer,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            eventBus,
            LogCollector: null,
            skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var previousHealth = lord.CurrentHealth;

        // Act: Resolve Peach
        var resolver = new PeachResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Peach resolution should succeed");
        
        // Recovery should be 1 (normal, no Rescue bonus)
        var expectedHealth = Math.Min(previousHealth + 1, lord.MaxHealth);
        Assert.AreEqual(expectedHealth, lord.CurrentHealth, 
            $"Lord should recover 1 HP (from {previousHealth} to {expectedHealth}) without Rescue skill bonus");
    }

    #endregion
}

