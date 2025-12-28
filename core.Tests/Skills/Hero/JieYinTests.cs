using System;
using System.Collections.Generic;
using System.Linq;
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
public sealed class JieYinTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithPlayerConfigs(int playerCount, List<PlayerConfig> playerConfigs)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
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

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that JieYinSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void JieYinSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new JieYinSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jieyin", skill.Id);
        Assert.AreEqual("结姻", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve JieYin skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterJieYinSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new JieYinSkillFactory();

        // Act
        registry.RegisterSkill("jieyin", factory);
        var skill = registry.GetSkill("jieyin");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jieyin", skill.Id);
        Assert.AreEqual("结姻", skill.Name);
    }

    #endregion

    #region ActionQueryService Tests

    /// <summary>
    /// Tests that ActionQueryService generates UseJieYin action when conditions are met.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceGeneratesUseJieYinActionWhenConditionsMet()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 2 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 2 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);

        // Get wounded male target
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var jieYinAction = result.Items.FirstOrDefault(a => a.ActionId == "UseJieYin");
        Assert.IsNotNull(jieYinAction);
        Assert.IsTrue(jieYinAction.RequiresTargets);
        Assert.AreEqual(2, jieYinAction.CardCandidates?.Count);
    }

    /// <summary>
    /// Tests that UseJieYin action is not generated when owner has less than 2 hand cards.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseJieYinWhenInsufficientHandCards()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 2 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add only 1 hand card
        var card1 = CreateTestCard(1);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        // Get wounded male target
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var jieYinAction = result.Items.FirstOrDefault(a => a.ActionId == "UseJieYin");
        Assert.IsNull(jieYinAction);
    }

    /// <summary>
    /// Tests that UseJieYin action is not generated when no valid target exists.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseJieYinWhenNoValidTarget()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, Gender = Gender.Female, MaxHealth = 3, InitialHealth = 2 }, // Not male
            new PlayerConfig { Seat = 2, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 3 } // Not wounded
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 2 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);

        // All other players are not valid targets (not male or not wounded)
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var jieYinAction = result.Items.FirstOrDefault(a => a.ActionId == "UseJieYin");
        Assert.IsNull(jieYinAction);
    }

    /// <summary>
    /// Tests that UseJieYin action is not generated when already used this play phase.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseJieYinWhenAlreadyUsed()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 2 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 2 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);

        // Mark skill as used this play phase
        var usageKey = $"jieyin_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
        owner.Flags[usageKey] = true;

        // Get wounded male target
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var jieYinAction = result.Items.FirstOrDefault(a => a.ActionId == "UseJieYin");
        Assert.IsNull(jieYinAction);
    }

    #endregion

    #region Resolution Tests

    /// <summary>
    /// Tests that JieYin skill successfully discards 2 cards and recovers 1 HP for both owner and target.
    /// </summary>
    [TestMethod]
    public void JieYinSuccessfullyDiscardsCardsAndRecoversHealth()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 3, InitialHealth = 2 },
            new PlayerConfig { Seat = 1, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 1 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 3 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        var card3 = CreateTestCard(3);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);
        ((Zone)owner.HandZone).MutableCards.Add(card3);

        // Get wounded male target
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseJieYinHandler(cardMoveService, ruleService);

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseJieYin");

        // Create choice: select 2 cards and target
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var stack = new BasicResolutionStack();
        var resolutionContext = new ResolutionContext(
            game,
            owner,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: null,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        // Act
        var resolver = JieYinSkill.CreateMainResolver(owner);
        var result = resolver.Resolve(resolutionContext);

        // Execute the entire stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        // Owner should have 1 hand card left
        Assert.AreEqual(1, owner.HandZone.Cards.Count);
        Assert.IsTrue(owner.HandZone.Cards.Any(c => c.Id == card3.Id));

        // Owner should have recovered 1 HP
        Assert.AreEqual(3, owner.CurrentHealth);

        // Target should have recovered 1 HP
        Assert.AreEqual(2, target.CurrentHealth);

        // Skill should be marked as used
        var usageKey = $"jieyin_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
        Assert.IsTrue(owner.Flags.ContainsKey(usageKey));
    }

    /// <summary>
    /// Tests that JieYin does not recover beyond MaxHealth.
    /// </summary>
    [TestMethod]
    public void JieYinDoesNotRecoverBeyondMaxHealth()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 3, InitialHealth = 3 }, // Already at max
            new PlayerConfig { Seat = 1, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 2 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 2 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);

        // Get wounded male target
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseJieYinHandler(cardMoveService, ruleService);

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseJieYin");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var stack = new BasicResolutionStack();
        var resolutionContext = new ResolutionContext(
            game,
            owner,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: null,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        // Act
        var resolver = JieYinSkill.CreateMainResolver(owner);
        var result = resolver.Resolve(resolutionContext);

        // Execute the entire stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        // Owner should not exceed MaxHealth
        Assert.AreEqual(3, owner.CurrentHealth);

        // Target should have recovered 1 HP
        Assert.AreEqual(3, target.CurrentHealth);
    }

    /// <summary>
    /// Tests that JieYin fails when target is not male.
    /// </summary>
    [TestMethod]
    public void JieYinFailsWhenTargetIsNotMale()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, Gender = Gender.Female, MaxHealth = 3, InitialHealth = 2 }, // Invalid: not male
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 2 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);

        // Get wounded female target (invalid)
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());

        // Create action descriptor for testing (even though target is invalid, we test resolver validation)
        var action = new ActionDescriptor(
            ActionId: "UseJieYin",
            DisplayKey: "action.useJieYin",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            CardCandidates: owner.HandZone.Cards.ToList());

        // Create choice with invalid target
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var stack = new BasicResolutionStack();
        var resolutionContext = new ResolutionContext(
            game,
            owner,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: null,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        // Act
        var resolver = JieYinSkill.CreateMainResolver(owner);
        var result = resolver.Resolve(resolutionContext);

        // Assert
        Assert.IsFalse(result.Success, "Resolution should fail with invalid target");
    }

    /// <summary>
    /// Tests that JieYin fails when target is not wounded.
    /// </summary>
    [TestMethod]
    public void JieYinFailsWhenTargetIsNotWounded()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunshangxiang", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, Gender = Gender.Male, MaxHealth = 3, InitialHealth = 3 }, // Invalid: not wounded
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 2 hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);

        // Get male target at full health (invalid)
        var target = game.Players[1];

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());

        // Create action descriptor for testing (even though target is invalid, we test resolver validation)
        var action = new ActionDescriptor(
            ActionId: "UseJieYin",
            DisplayKey: "action.useJieYin",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            CardCandidates: owner.HandZone.Cards.ToList());

        // Create choice with invalid target
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var stack = new BasicResolutionStack();
        var resolutionContext = new ResolutionContext(
            game,
            owner,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: null,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        // Act
        var resolver = JieYinSkill.CreateMainResolver(owner);
        var result = resolver.Resolve(resolutionContext);

        // Assert
        Assert.IsFalse(result.Success, "Resolution should fail with invalid target");
    }

    #endregion
}

