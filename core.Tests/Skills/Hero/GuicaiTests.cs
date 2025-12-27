using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
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
public sealed class GuicaiTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithCardsInDrawPile(int playerCount = 2, int cardCount = 10)
    {
        var game = CreateDefaultGame(playerCount);

        // Add cards to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            for (int i = 0; i < cardCount; i++)
            {
                var card = new Card
                {
                    Id = i + 1,
                    DefinitionId = $"test_card_{i}",
                    CardType = CardType.Basic,
                    CardSubType = CardSubType.Slash,
                    Suit = Suit.Spade,
                    Rank = 5
                };
                drawZone.MutableCards.Add(card);
            }
        }

        return game;
    }

    private static Card CreateTestCard(int id, Suit suit, int rank)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"card_{id}",
            Name = $"Card {id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
    }

    private static IEffectSource CreateTestEffectSource(string sourceId = "test_source", string sourceType = "Skill", string? displayName = "Test Source")
    {
        return new TestEffectSource(sourceId, sourceType, displayName);
    }

    private sealed class TestEffectSource : IEffectSource
    {
        public string SourceId { get; }
        public string SourceType { get; }
        public string? DisplayName { get; }

        public TestEffectSource(string sourceId, string sourceType, string? displayName)
        {
            SourceId = sourceId;
            SourceType = sourceType;
            DisplayName = displayName;
        }
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that GuicaiSkillFactory creates correct skill instance.
    /// Input: GuicaiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void GuicaiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new GuicaiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("guicai", skill.Id);
        Assert.AreEqual("鬼才", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Guicai skill.
    /// Input: Empty registry, GuicaiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterGuicaiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new GuicaiSkillFactory();

        // Act
        registry.RegisterSkill("guicai", factory);
        var skill = registry.GetSkill("guicai");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("guicai", skill.Id);
        Assert.AreEqual("鬼才", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that GuicaiSkill has correct properties.
    /// Input: GuicaiSkill instance.
    /// Expected: Skill has correct Id, Name, Type, and Capabilities.
    /// </summary>
    [TestMethod]
    public void GuicaiSkillHasCorrectProperties()
    {
        // Arrange
        var skill = new GuicaiSkill();

        // Act & Assert
        Assert.AreEqual("guicai", skill.Id);
        Assert.AreEqual("鬼才", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    /// <summary>
    /// Tests that GuicaiSkill implements IJudgementModifier interface.
    /// Input: GuicaiSkill instance.
    /// Expected: Skill implements IJudgementModifier.
    /// </summary>
    [TestMethod]
    public void GuicaiSkillImplementsIJudgementModifier()
    {
        // Arrange
        var skill = new GuicaiSkill();

        // Act & Assert
        Assert.IsTrue(skill is IJudgementModifier, "GuicaiSkill should implement IJudgementModifier.");
    }

    #endregion

    #region CanModify Tests

    /// <summary>
    /// Tests that CanModify returns true when player has hand cards and skill is active.
    /// Input: Game, player with hand cards, GuicaiSkill, JudgementContext.
    /// Expected: CanModify returns true.
    /// </summary>
    [TestMethod]
    public void CanModifyReturnsTrueWhenPlayerHasHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var handCard = CreateTestCard(100, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(handCard);
        }

        var skill = new GuicaiSkill();
        skill.Attach(game, player, new BasicEventBus());

        var judgeOwner = game.Players[1];
        var originalCard = CreateTestCard(1, Suit.Spade, 5);
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            CreateTestEffectSource(),
            new RedJudgementRule(),
            null,
            true);
        var ctx = new JudgementContext(game, judgeOwner, originalCard, request);

        // Act
        var canModify = skill.CanModify(ctx, player);

        // Assert
        Assert.IsTrue(canModify, "CanModify should return true when player has hand cards.");
    }

    /// <summary>
    /// Tests that CanModify returns false when player has no hand cards.
    /// Input: Game, player with no hand cards, GuicaiSkill, JudgementContext.
    /// Expected: CanModify returns false.
    /// </summary>
    [TestMethod]
    public void CanModifyReturnsFalseWhenPlayerHasNoHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var skill = new GuicaiSkill();
        skill.Attach(game, player, new BasicEventBus());

        var judgeOwner = game.Players[1];
        var originalCard = CreateTestCard(1, Suit.Spade, 5);
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            CreateTestEffectSource(),
            new RedJudgementRule(),
            null,
            true);
        var ctx = new JudgementContext(game, judgeOwner, originalCard, request);

        // Act
        var canModify = skill.CanModify(ctx, player);

        // Assert
        Assert.IsFalse(canModify, "CanModify should return false when player has no hand cards.");
    }

    /// <summary>
    /// Tests that CanModify returns false when skill is inactive (player is dead).
    /// Input: Game, dead player, GuicaiSkill, JudgementContext.
    /// Expected: CanModify returns false.
    /// </summary>
    [TestMethod]
    public void CanModifyReturnsFalseWhenSkillInactive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = false; // Skill should not be active

        var handCard = CreateTestCard(100, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(handCard);
        }

        var skill = new GuicaiSkill();
        skill.Attach(game, player, new BasicEventBus());

        var judgeOwner = game.Players[1];
        var originalCard = CreateTestCard(1, Suit.Spade, 5);
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            CreateTestEffectSource(),
            new RedJudgementRule(),
            null,
            true);
        var ctx = new JudgementContext(game, judgeOwner, originalCard, request);

        // Act
        var canModify = skill.CanModify(ctx, player);

        // Assert
        Assert.IsFalse(canModify, "CanModify should return false when skill is inactive.");
    }

    #endregion

    #region GetDecision Tests

    /// <summary>
    /// Tests that GetDecision returns null when getPlayerChoice is null.
    /// Input: Game, player, GuicaiSkill, JudgementContext, null getPlayerChoice.
    /// Expected: GetDecision returns null.
    /// </summary>
    [TestMethod]
    public void GetDecisionReturnsNullWhenGetPlayerChoiceIsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var handCard = CreateTestCard(100, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(handCard);
        }

        var skill = new GuicaiSkill();
        skill.Attach(game, player, new BasicEventBus());

        var judgeOwner = game.Players[1];
        var originalCard = CreateTestCard(1, Suit.Spade, 5);
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            CreateTestEffectSource(),
            new RedJudgementRule(),
            null,
            true);
        var ctx = new JudgementContext(game, judgeOwner, originalCard, request);

        // Act
        var decision = skill.GetDecision(ctx, player, null);

        // Assert
        Assert.IsNull(decision, "GetDecision should return null when getPlayerChoice is null.");
    }

    /// <summary>
    /// Tests that GetDecision returns null when player chooses not to use skill.
    /// Input: Game, player, GuicaiSkill, JudgementContext, getPlayerChoice that returns Confirmed=false.
    /// Expected: GetDecision returns null.
    /// </summary>
    [TestMethod]
    public void GetDecisionReturnsNullWhenPlayerChoosesNotToUse()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var handCard = CreateTestCard(100, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(handCard);
        }

        var skill = new GuicaiSkill();
        skill.Attach(game, player, new BasicEventBus());

        var judgeOwner = game.Players[1];
        var originalCard = CreateTestCard(1, Suit.Spade, 5);
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            CreateTestEffectSource(),
            new RedJudgementRule(),
            null,
            true);
        var ctx = new JudgementContext(game, judgeOwner, originalCard, request);

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, false); // Not confirmed
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        // Act
        var decision = skill.GetDecision(ctx, player, getPlayerChoice);

        // Assert
        Assert.IsNull(decision, "GetDecision should return null when player chooses not to use skill.");
    }

    /// <summary>
    /// Tests that GetDecision returns correct decision when player chooses to use skill and selects a card.
    /// Input: Game, player with hand cards, GuicaiSkill, JudgementContext, getPlayerChoice that confirms and selects card.
    /// Expected: GetDecision returns JudgementModifyDecision with correct card.
    /// </summary>
    [TestMethod]
    public void GetDecisionReturnsCorrectDecisionWhenPlayerUsesSkill()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var handCard = CreateTestCard(100, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(handCard);
        }

        var skill = new GuicaiSkill();
        skill.Attach(game, player, new BasicEventBus());

        var judgeOwner = game.Players[1];
        var originalCard = CreateTestCard(1, Suit.Spade, 5);
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            CreateTestEffectSource(),
            new RedJudgementRule(),
            null,
            true);
        var ctx = new JudgementContext(game, judgeOwner, originalCard, request);

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true); // Confirmed
            }
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, new[] { handCard.Id }, null, null);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        // Act
        var decision = skill.GetDecision(ctx, player, getPlayerChoice);

        // Assert
        Assert.IsNotNull(decision, "GetDecision should return a decision when player uses skill.");
        Assert.AreEqual(player.Seat, decision.ModifierSeat);
        Assert.AreEqual("鬼才", decision.ModifierSource);
        Assert.AreEqual(handCard.Id, decision.ReplacementCard.Id);
    }

    #endregion

    #region Integration Tests with Judgement Modification Window

    /// <summary>
    /// Tests that GuicaiSkill can modify judgement through modification window.
    /// Input: Game with cards, player with GuicaiSkill and hand cards, judgement request with AllowModify=true.
    /// Expected: After judgement, original card is in discard pile, replacement card is in JudgementZone.
    /// </summary>
    [TestMethod]
    public void GuicaiSkillModifiesJudgementThroughModificationWindow()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var judgeOwner = game.Players[0];
        var guicaiPlayer = game.Players[1];
        judgeOwner.IsAlive = true;
        guicaiPlayer.IsAlive = true;

        // Add hand card for Guicai player
        var replacementCard = CreateTestCard(100, Suit.Heart, 5);
        if (guicaiPlayer.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(replacementCard);
        }

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, guicaiPlayer, new GuicaiSkill());

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            source,
            rule,
            null,
            true); // Allow modification

        var judgementService = new BasicJudgementService(eventBus);
        var stack = new BasicResolutionStack();

        // Setup getPlayerChoice to confirm and select card
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == guicaiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true);
            }
            if (request.ChoiceType == ChoiceType.SelectCards && request.PlayerSeat == guicaiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, new[] { replacementCard.Id }, null, null);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var intermediateResults = new Dictionary<string, object>
        {
            ["JudgementRequest"] = request
        };

        var context = new ResolutionContext(
            game,
            judgeOwner,
            null,
            null,
            stack,
            cardMoveService,
            new RuleService(),
            null,
            null,
            getPlayerChoice,
            intermediateResults,
            eventBus,
            null,
            skillManager,
            null,
            judgementService);

        // Act - Push and execute JudgementResolver
        stack.Push(new JudgementResolver(), context);
        while (!stack.IsEmpty)
        {
            var resolutionResult = stack.Pop();
            Assert.IsTrue(resolutionResult.Success, "Resolution should succeed.");
        }

        // Assert - Get result from intermediate results
        if (intermediateResults.TryGetValue("JudgementResult", out var resultObj) && resultObj is JudgementResult judgementResult)
        {
            // Original card should be in discard pile
            var originalCard = judgementResult.OriginalCard;
            Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == originalCard.Id), "Original card should be in discard pile.");

            // Final card should be the replacement card
            var finalCard = judgementResult.FinalCard;
            Assert.AreEqual(replacementCard.Id, finalCard.Id, "Final card should be the replacement card.");

            // Replacement card should be in JudgementZone
            Assert.IsTrue(judgeOwner.JudgementZone.Cards.Any(c => c.Id == replacementCard.Id), "Replacement card should be in JudgementZone.");

            // Modification should be recorded
            Assert.IsTrue(judgementResult.ModifiersApplied.Count > 0, "Modification should be recorded.");
            var modification = judgementResult.ModifiersApplied[0];
            Assert.AreEqual(guicaiPlayer.Seat, modification.ModifierSeat);
            Assert.AreEqual("鬼才", modification.ModifierSource);
        }
        else
        {
            Assert.Fail("JudgementResult should be in intermediate results.");
        }
    }

    /// <summary>
    /// Tests that GuicaiSkill does not modify when AllowModify is false.
    /// Input: Game with cards, player with GuicaiSkill, judgement request with AllowModify=false.
    /// Expected: No modification occurs, original card is used for judgement.
    /// </summary>
    [TestMethod]
    public void GuicaiSkillDoesNotModifyWhenAllowModifyIsFalse()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var judgeOwner = game.Players[0];
        var guicaiPlayer = game.Players[1];
        judgeOwner.IsAlive = true;
        guicaiPlayer.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, guicaiPlayer, new GuicaiSkill());

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            judgeOwner.Seat,
            JudgementReason.Skill,
            source,
            rule,
            null,
            false); // Do not allow modification

        var judgementService = new BasicJudgementService(eventBus);
        var stack = new BasicResolutionStack();

        var intermediateResults = new Dictionary<string, object>
        {
            ["JudgementRequest"] = request
        };

        var context = new ResolutionContext(
            game,
            judgeOwner,
            null,
            null,
            stack,
            cardMoveService,
            new RuleService(),
            null,
            null,
            null,
            intermediateResults,
            eventBus,
            null,
            skillManager,
            null,
            judgementService);

        // Act - Push and execute JudgementResolver
        stack.Push(new JudgementResolver(), context);
        while (!stack.IsEmpty)
        {
            var resolutionResult = stack.Pop();
            Assert.IsTrue(resolutionResult.Success, "Resolution should succeed.");
        }

        // Assert - Get result from intermediate results
        if (intermediateResults.TryGetValue("JudgementResult", out var resultObj) && resultObj is JudgementResult judgementResult)
        {
            // No modifications should be applied
            Assert.AreEqual(0, judgementResult.ModifiersApplied.Count, "No modifications should be applied when AllowModify is false.");
            Assert.AreEqual(judgementResult.OriginalCard.Id, judgementResult.FinalCard.Id, "Original and final cards should be the same.");
        }
        else
        {
            Assert.Fail("JudgementResult should be in intermediate results.");
        }
    }

    #endregion
}

