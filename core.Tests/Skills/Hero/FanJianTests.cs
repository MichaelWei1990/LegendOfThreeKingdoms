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
public sealed class FanJianTests
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

    private static Card CreateTestCard(int id, Suit suit = Suit.Spade, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = suit,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that FanJianSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void FanJianSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new FanJianSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("fanjian", skill.Id);
        Assert.AreEqual("反间", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve FanJian skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterFanJianSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new FanJianSkillFactory();

        // Act
        registry.RegisterSkill("fanjian", factory);
        var skill = registry.GetSkill("fanjian");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("fanjian", skill.Id);
        Assert.AreEqual("反间", skill.Name);
    }

    #endregion

    #region ActionQueryService Tests

    /// <summary>
    /// Tests that ActionQueryService generates UseFanJian action when conditions are met.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceGeneratesUseFanJianActionWhenConditionsMet()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 1 hand card
        var card1 = CreateTestCard(1);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var fanJianAction = result.Items.FirstOrDefault(a => a.ActionId == "UseFanJian");
        Assert.IsNotNull(fanJianAction);
        Assert.IsTrue(fanJianAction.RequiresTargets);
    }

    /// <summary>
    /// Tests that UseFanJian action is not generated when owner has no hand cards.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseFanJianWhenNoHandCards()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        // No hand cards added

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var fanJianAction = result.Items.FirstOrDefault(a => a.ActionId == "UseFanJian");
        Assert.IsNull(fanJianAction);
    }

    /// <summary>
    /// Tests that UseFanJian action is not generated when already used this play phase.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseFanJianWhenAlreadyUsed()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add 1 hand card
        var card1 = CreateTestCard(1);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);

        // Mark skill as used
        var fanJianSkill = skillManager.GetAllSkills(owner).FirstOrDefault(s => s.Id == "fanjian") as IPhaseLimitedActionProvidingSkill;
        Assert.IsNotNull(fanJianSkill);
        fanJianSkill.MarkAsUsed(game, owner);

        // Act
        var ruleContext = new RuleContext(game, owner);
        var result = ruleService.GetAvailableActions(ruleContext);

        // Assert
        var fanJianAction = result.Items.FirstOrDefault(a => a.ActionId == "UseFanJian");
        Assert.IsNull(fanJianAction);
    }

    #endregion

    #region Skill Execution Tests

    /// <summary>
    /// Tests that FanJian successfully executes when target guesses wrong suit and takes damage.
    /// </summary>
    [TestMethod]
    public void FanJianSuccessfullyExecutesWhenTargetGuessesWrongSuit()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        var target = game.Players[1];
        
        // Add hand card with Spade suit
        var card1 = CreateTestCard(1, Suit.Spade);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseFanJianHandler(cardMoveService, ruleService, (request) =>
        {
            // Target guesses Heart (wrong)
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: "Heart", // Wrong guess
                    Confirmed: true);
            }
            // Owner selects target
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: new List<int> { target.Seat },
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseFanJian");

        // Create choice: select target
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new List<int> { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(new RuleContext(game, owner), action, null, choice);

        // Assert
        // Card should be moved to target's hand
        Assert.IsTrue(target.HandZone.Cards.Any(c => c.Id == card1.Id));
        Assert.IsFalse(owner.HandZone.Cards.Any(c => c.Id == card1.Id));
        
        // Target should take 1 damage (guessed Heart but card is Spade)
        Assert.AreEqual(2, target.CurrentHealth); // 3 - 1 = 2
    }

    /// <summary>
    /// Tests that FanJian does not deal damage when target guesses correct suit.
    /// </summary>
    [TestMethod]
    public void FanJianDoesNotDealDamageWhenTargetGuessesCorrectSuit()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        var target = game.Players[1];
        
        // Add hand card with Club suit
        var card1 = CreateTestCard(1, Suit.Club);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseFanJianHandler(cardMoveService, ruleService, (request) =>
        {
            // Target guesses Club (correct)
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: "Club", // Correct guess
                    Confirmed: true);
            }
            // Owner selects target
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: new List<int> { target.Seat },
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseFanJian");

        // Create choice: select target
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new List<int> { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(new RuleContext(game, owner), action, null, choice);

        // Assert
        // Card should be moved to target's hand
        Assert.IsTrue(target.HandZone.Cards.Any(c => c.Id == card1.Id));
        Assert.IsFalse(owner.HandZone.Cards.Any(c => c.Id == card1.Id));
        
        // Target should not take damage (guessed correctly)
        Assert.AreEqual(3, target.CurrentHealth); // No damage
    }

    /// <summary>
    /// Tests that FanJian marks skill as used after execution.
    /// </summary>
    [TestMethod]
    public void FanJianMarksSkillAsUsedAfterExecution()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        var target = game.Players[1];
        
        // Add hand card
        var card1 = CreateTestCard(1, Suit.Spade);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseFanJianHandler(cardMoveService, ruleService, (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: "Heart",
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: new List<int> { target.Seat },
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseFanJian");

        // Create choice: select target
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new List<int> { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(new RuleContext(game, owner), action, null, choice);

        // Assert
        var fanJianSkill = skillManager.GetAllSkills(owner).FirstOrDefault(s => s.Id == "fanjian") as IPhaseLimitedActionProvidingSkill;
        Assert.IsNotNull(fanJianSkill);
        Assert.IsTrue(fanJianSkill.IsAlreadyUsed(game, owner));
    }

    /// <summary>
    /// Tests that FanJian cannot target self.
    /// </summary>
    [TestMethod]
    public void FanJianCannotTargetSelf()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "zhouyu", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 3, InitialHealth = 3 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];
        
        // Add hand card
        var card1 = CreateTestCard(1, Suit.Spade);
        ((Zone)owner.HandZone).MutableCards.Add(card1);

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseFanJianHandler(cardMoveService, ruleService);

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.FirstOrDefault(a => a.ActionId == "UseFanJian");
        Assert.IsNotNull(action);

        // Create choice: try to target self (should fail)
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new List<int> { owner.Seat }, // Self
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            mapper.Resolve(new RuleContext(game, owner), action, null, choice);
        });
    }

    #endregion
}

