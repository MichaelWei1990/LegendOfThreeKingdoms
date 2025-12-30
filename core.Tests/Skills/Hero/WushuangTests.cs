using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using BasicCardMoveService = LegendOfThreeKingdoms.Core.Zones.BasicCardMoveService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResolutionExtensions = LegendOfThreeKingdoms.Core.Resolution.ResolutionExtensions;

namespace core.Tests;

[TestClass]
public sealed class WushuangTests
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

    private static Card CreateJinkCard(int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "jink",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Heart,
            Rank = 5
        };
    }

    private static Card CreateDuelCard(int id = 3)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "duel",
            Name = "决斗",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Duel,
            Suit = Suit.Spade,
            Rank = 1
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that WushuangSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void WushuangSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new WushuangSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("wushuang", skill.Id);
        Assert.AreEqual("无双", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.IsTrue(skill is IResponseRequirementModifyingSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Wushuang skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterWushuangSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new WushuangSkillFactory();

        // Act
        registry.RegisterSkill("wushuang", factory);
        var skill = registry.GetSkill("wushuang");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("wushuang", skill.Id);
    }

    #endregion

    #region Slash Response Requirement Tests

    /// <summary>
    /// Tests that Slash from Wushuang owner requires 2 Jinks to dodge.
    /// </summary>
    [TestMethod]
    public void SlashFromWushuangOwnerRequiresTwoJinks()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var target = game.Players[1];

        // Add Wushuang skill to owner
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var skill = new WushuangSkill();
        skillManager.AddEquipmentSkill(game, owner, skill);

        var slashCard = CreateSlashCard();

        // Act
        var requiredCount = ResponseRequirementCalculator.CalculateJinkRequirementForSlash(
            game, owner, target, slashCard, skillManager);

        // Assert
        Assert.AreEqual(2, requiredCount);
    }

    /// <summary>
    /// Tests that Slash from non-Wushuang owner requires 1 Jink (default).
    /// </summary>
    [TestMethod]
    public void SlashFromNonWushuangOwnerRequiresOneJink()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var target = game.Players[1];

        // Don't add Wushuang skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);

        var slashCard = CreateSlashCard();

        // Act
        var requiredCount = ResponseRequirementCalculator.CalculateJinkRequirementForSlash(
            game, owner, target, slashCard, skillManager);

        // Assert
        Assert.AreEqual(1, requiredCount);
    }

    /// <summary>
    /// Tests that Slash response window with Wushuang requires 2 Jinks and succeeds when 2 are provided.
    /// </summary>
    [TestMethod]
    public void SlashResponseWindowWithWushuangRequiresTwoJinksAndSucceeds()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var target = game.Players[1];

        // Add Wushuang skill to owner
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var skill = new WushuangSkill();
        skillManager.AddEquipmentSkill(game, owner, skill);

        // Add 2 Jink cards to target's hand
        var jink1 = CreateJinkCard(1);
        var jink2 = CreateJinkCard(2);
        ((Zone)target.HandZone).MutableCards.Add(jink1);
        ((Zone)target.HandZone).MutableCards.Add(jink2);

        var slashCard = CreateSlashCard(3);
        ((Zone)owner.HandZone).MutableCards.Add(slashCard);

        // Set up response tracking
        var responseCount = 0;

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == target.Seat && request.ChoiceType == ChoiceType.SelectCards)
            {
                responseCount++;
                if (responseCount == 1)
                {
                    // First response: provide 1 Jink
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new List<int> { jink1.Id },
                        SelectedOptionId: null,
                        Confirmed: true);
                }
                else if (responseCount == 2)
                {
                    // Second response: provide 1 more Jink
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new List<int> { jink2.Id },
                        SelectedOptionId: null,
                        Confirmed: true);
                }
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };

        // Create response window
        var sourceEvent = new { Type = "Slash", SourceSeat = owner.Seat, TargetSeat = target.Seat, SlashCard = slashCard };
        var requiredCount = ResponseRequirementCalculator.CalculateJinkRequirementForSlash(
            game, owner, target, slashCard, skillManager);

        var responseRuleService = new ResponseRuleService(skillManager);
        var windowContext = new ResponseWindowContext(
            Game: game,
            ResponseType: ResponseType.JinkAgainstSlash,
            ResponderOrder: new[] { target },
            SourceEvent: sourceEvent,
            RuleService: new RuleService(skillManager: skillManager),
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(),
            CardMoveService: new BasicCardMoveService(eventBus),
            SkillManager: skillManager,
            RequiredResponseCount: requiredCount
        );

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State);
        Assert.AreEqual(2, result.ResponseUnitsProvided);
        Assert.AreEqual(2, responseCount); // Should have been called twice
    }

    /// <summary>
    /// Tests that Slash response window with Wushuang fails when only 1 Jink is provided.
    /// </summary>
    [TestMethod]
    public void SlashResponseWindowWithWushuangFailsWithOnlyOneJink()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var target = game.Players[1];

        // Add Wushuang skill to owner
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var skill = new WushuangSkill();
        skillManager.AddEquipmentSkill(game, owner, skill);

        // Add 1 Jink card to target's hand
        var jink1 = CreateJinkCard(1);
        ((Zone)target.HandZone).MutableCards.Add(jink1);

        var slashCard = CreateSlashCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(slashCard);

        // Set up response tracking
        var responseCount = 0;

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == target.Seat && request.ChoiceType == ChoiceType.SelectCards)
            {
                responseCount++;
                if (responseCount == 1)
                {
                    // First response: provide 1 Jink
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new List<int> { jink1.Id },
                        SelectedOptionId: null,
                        Confirmed: true);
                }
            }
            // Second time: pass (cannot provide more)
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };

        // Create response window
        var sourceEvent = new { Type = "Slash", SourceSeat = owner.Seat, TargetSeat = target.Seat, SlashCard = slashCard };
        var requiredCount = ResponseRequirementCalculator.CalculateJinkRequirementForSlash(
            game, owner, target, slashCard, skillManager);

        var responseRuleService = new ResponseRuleService(skillManager);
        var windowContext = new ResponseWindowContext(
            Game: game,
            ResponseType: ResponseType.JinkAgainstSlash,
            ResponderOrder: new[] { target },
            SourceEvent: sourceEvent,
            RuleService: new RuleService(skillManager: skillManager),
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(),
            CardMoveService: new BasicCardMoveService(eventBus),
            SkillManager: skillManager,
            RequiredResponseCount: requiredCount
        );

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.NoResponse, result.State);
        Assert.AreEqual(1, result.ResponseUnitsProvided); // Only 1 unit provided, but need 2
    }

    #endregion

    #region Duel Response Requirement Tests

    /// <summary>
    /// Tests that Duel with Wushuang owner requires 2 Slashes to continue.
    /// </summary>
    [TestMethod]
    public void DuelWithWushuangOwnerRequiresTwoSlashes()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var opponent = game.Players[1];

        // Add Wushuang skill to owner
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var skill = new WushuangSkill();
        skillManager.AddEquipmentSkill(game, owner, skill);

        var duelCard = CreateDuelCard();

        // Act
        var requiredCount = ResponseRequirementCalculator.CalculateSlashRequirementForDuel(
            game, opponent, owner, duelCard, skillManager);

        // Assert
        Assert.AreEqual(2, requiredCount);
    }

    /// <summary>
    /// Tests that Duel with non-Wushuang owner requires 1 Slash (default).
    /// </summary>
    [TestMethod]
    public void DuelWithNonWushuangOwnerRequiresOneSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var opponent = game.Players[1];

        // Don't add Wushuang skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);

        var duelCard = CreateDuelCard();

        // Act
        var requiredCount = ResponseRequirementCalculator.CalculateSlashRequirementForDuel(
            game, opponent, owner, duelCard, skillManager);

        // Assert
        Assert.AreEqual(1, requiredCount);
    }

    /// <summary>
    /// Tests that Duel response window with Wushuang requires 2 Slashes and succeeds when 2 are provided.
    /// </summary>
    [TestMethod]
    public void DuelResponseWindowWithWushuangRequiresTwoSlashesAndSucceeds()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var opponent = game.Players[1];

        // Add Wushuang skill to owner
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var skill = new WushuangSkill();
        skillManager.AddEquipmentSkill(game, owner, skill);

        // Add 2 Slash cards to opponent's hand
        var slash1 = CreateSlashCard(1);
        var slash2 = CreateSlashCard(2);
        ((Zone)opponent.HandZone).MutableCards.Add(slash1);
        ((Zone)opponent.HandZone).MutableCards.Add(slash2);

        var duelCard = CreateDuelCard(3);

        // Set up response tracking
        var responseCount = 0;

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == opponent.Seat && request.ChoiceType == ChoiceType.SelectCards)
            {
                responseCount++;
                if (responseCount == 1)
                {
                    // First response: provide 1 Slash
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new List<int> { slash1.Id },
                        SelectedOptionId: null,
                        Confirmed: true);
                }
                else if (responseCount == 2)
                {
                    // Second response: provide 1 more Slash
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new List<int> { slash2.Id },
                        SelectedOptionId: null,
                        Confirmed: true);
                }
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };

        // Create response window
        var sourceEvent = new { Type = "Duel", OpposingPlayerSeat = owner.Seat };
        var requiredCount = ResponseRequirementCalculator.CalculateSlashRequirementForDuel(
            game, opponent, owner, duelCard, skillManager);

        var responseRuleService = new ResponseRuleService(skillManager);
        var windowContext = new ResponseWindowContext(
            Game: game,
            ResponseType: ResponseType.SlashAgainstDuel,
            ResponderOrder: new[] { opponent },
            SourceEvent: sourceEvent,
            RuleService: new RuleService(skillManager: skillManager),
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(),
            CardMoveService: new BasicCardMoveService(eventBus),
            SkillManager: skillManager,
            RequiredResponseCount: requiredCount
        );

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State);
        Assert.AreEqual(2, result.ResponseUnitsProvided);
        Assert.AreEqual(2, responseCount); // Should have been called twice
    }

    /// <summary>
    /// Tests that Duel response window with Wushuang fails when only 1 Slash is provided.
    /// </summary>
    [TestMethod]
    public void DuelResponseWindowWithWushuangFailsWithOnlyOneSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var opponent = game.Players[1];

        // Add Wushuang skill to owner
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var skill = new WushuangSkill();
        skillManager.AddEquipmentSkill(game, owner, skill);

        // Add 1 Slash card to opponent's hand
        var slash1 = CreateSlashCard(1);
        ((Zone)opponent.HandZone).MutableCards.Add(slash1);

        var duelCard = CreateDuelCard(2);

        // Set up response tracking
        var responseCount = 0;

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == opponent.Seat && request.ChoiceType == ChoiceType.SelectCards)
            {
                responseCount++;
                if (responseCount == 1)
                {
                    // First response: provide 1 Slash
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new List<int> { slash1.Id },
                        SelectedOptionId: null,
                        Confirmed: true);
                }
            }
            // Second time: pass (cannot provide more)
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };

        // Create response window
        var sourceEvent = new { Type = "Duel", OpposingPlayerSeat = owner.Seat };
        var requiredCount = ResponseRequirementCalculator.CalculateSlashRequirementForDuel(
            game, opponent, owner, duelCard, skillManager);

        var responseRuleService = new ResponseRuleService(skillManager);
        var windowContext = new ResponseWindowContext(
            Game: game,
            ResponseType: ResponseType.SlashAgainstDuel,
            ResponderOrder: new[] { opponent },
            SourceEvent: sourceEvent,
            RuleService: new RuleService(skillManager: skillManager),
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(),
            CardMoveService: new BasicCardMoveService(eventBus),
            SkillManager: skillManager,
            RequiredResponseCount: requiredCount
        );

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.NoResponse, result.State);
        Assert.AreEqual(1, result.ResponseUnitsProvided); // Only 1 unit provided, but need 2
    }

    #endregion
}

