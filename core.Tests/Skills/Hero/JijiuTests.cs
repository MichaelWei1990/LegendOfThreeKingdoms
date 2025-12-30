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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResolutionExtensions = LegendOfThreeKingdoms.Core.Resolution.ResolutionExtensions;

namespace core.Tests;

[TestClass]
public sealed class JijiuTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateRedCard(CardSubType subType = CardSubType.Slash, int id = 1, Suit suit = Suit.Heart)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"red_card_{id}",
            Name = "Red Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = suit, // Heart or Diamond (red)
            Rank = 5
        };
    }

    private static Card CreateBlackCard(CardSubType subType = CardSubType.Slash, int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"black_card_{id}",
            Name = "Black Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade, // Spade or Club (black)
            Rank = 5
        };
    }

    private static Card CreatePeachCard(int id = 3)
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
    /// Tests that JijiuSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void JijiuSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new JijiuSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jijiu", skill.Id);
        Assert.AreEqual("急救", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill is ICardConversionSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Jijiu skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterJijiuSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new JijiuSkillFactory();

        // Act
        registry.RegisterSkill("jijiu", factory);
        var skill = registry.GetSkill("jijiu");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jijiu", skill.Id);
        Assert.AreEqual("急救", skill.Name);
    }

    #endregion

    #region Card Conversion Tests - Turn Check

    /// <summary>
    /// Tests that JijiuSkill.CreateVirtualCard creates a virtual Peach card from a red hand card when outside owner's turn.
    /// </summary>
    [TestMethod]
    public void JijiuSkillCreateVirtualCardFromRedHandCardOutsideTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var otherPlayer = game.Players[1];
        game.CurrentPlayerSeat = otherPlayer.Seat; // Other player's turn
        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        ((Zone)owner.HandZone).MutableCards.Add(redCard);
        var skill = new JijiuSkill();
        skill.Attach(game, owner, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(redCard, game, owner);

        // Assert
        Assert.IsNotNull(virtualCard);
        Assert.AreEqual(redCard.Id, virtualCard.Id);
        Assert.AreEqual(CardSubType.Peach, virtualCard.CardSubType);
        Assert.AreEqual(CardType.Basic, virtualCard.CardType);
        Assert.AreEqual(redCard.Suit, virtualCard.Suit);
        Assert.AreEqual(redCard.Rank, virtualCard.Rank);
        Assert.AreEqual("桃", virtualCard.Name);
        Assert.AreEqual("peach", virtualCard.DefinitionId);
    }

    /// <summary>
    /// Tests that JijiuSkill.CreateVirtualCard creates a virtual Peach card from a Diamond card.
    /// </summary>
    [TestMethod]
    public void JijiuSkillCreateVirtualCardFromDiamondCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var otherPlayer = game.Players[1];
        game.CurrentPlayerSeat = otherPlayer.Seat; // Other player's turn
        var diamondCard = CreateRedCard(CardSubType.Slash, 1, Suit.Diamond);
        ((Zone)owner.HandZone).MutableCards.Add(diamondCard);
        var skill = new JijiuSkill();
        skill.Attach(game, owner, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(diamondCard, game, owner);

        // Assert
        Assert.IsNotNull(virtualCard);
        Assert.AreEqual(CardSubType.Peach, virtualCard.CardSubType);
    }

    /// <summary>
    /// Tests that JijiuSkill.CreateVirtualCard returns null when it's the owner's turn.
    /// </summary>
    [TestMethod]
    public void JijiuSkillCreateVirtualCardDuringOwnersTurnReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        game.CurrentPlayerSeat = owner.Seat; // Owner's turn
        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        ((Zone)owner.HandZone).MutableCards.Add(redCard);
        var skill = new JijiuSkill();
        skill.Attach(game, owner, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(redCard, game, owner);

        // Assert
        Assert.IsNull(virtualCard, "Jijiu cannot be used during owner's turn");
    }

    /// <summary>
    /// Tests that JijiuSkill.CreateVirtualCard returns null for a black card.
    /// </summary>
    [TestMethod]
    public void JijiuSkillCreateVirtualCardFromBlackCardReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var otherPlayer = game.Players[1];
        game.CurrentPlayerSeat = otherPlayer.Seat; // Other player's turn
        var blackCard = CreateBlackCard(CardSubType.Slash, 2);
        ((Zone)owner.HandZone).MutableCards.Add(blackCard);
        var skill = new JijiuSkill();
        skill.Attach(game, owner, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(blackCard, game, owner);

        // Assert
        Assert.IsNull(virtualCard, "Jijiu can only convert red cards");
    }

    /// <summary>
    /// Tests that JijiuSkill.CreateVirtualCard returns null for a red card not in hand.
    /// </summary>
    [TestMethod]
    public void JijiuSkillCreateVirtualCardFromRedCardNotInHandReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var otherPlayer = game.Players[1];
        game.CurrentPlayerSeat = otherPlayer.Seat; // Other player's turn
        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        // Card is not added to hand zone
        var skill = new JijiuSkill();
        skill.Attach(game, owner, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(redCard, game, owner);

        // Assert
        Assert.IsNull(virtualCard, "Jijiu can only convert hand cards");
    }

    #endregion

    #region Response Window Tests

    /// <summary>
    /// Tests that ResponseRuleService includes convertible red cards in legal response cards for PeachForDying response when outside owner's turn.
    /// </summary>
    [TestMethod]
    public void ResponseRuleServiceIncludesConvertibleRedCardsForPeachResponseOutsideTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var otherPlayer = game.Players[1];
        game.CurrentPlayerSeat = otherPlayer.Seat; // Other player's turn
        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        ((Zone)player.HandZone).MutableCards.Add(redCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("jijiu", new JijiuSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("jijiu"));

        var responseRuleService = new ResponseRuleService(skillManager);
        var responseContext = new ResponseContext(
            game,
            player,
            ResponseType.PeachForDying,
            null);

        // Act
        var legalCards = responseRuleService.GetLegalResponseCards(responseContext);

        // Assert
        Assert.IsTrue(legalCards.HasAny, "Should have legal cards");
        Assert.IsTrue(legalCards.Items.Any(c => c.Id == redCard.Id), "Red card should be in legal response cards");
    }

    /// <summary>
    /// Tests that ResponseRuleService does NOT include convertible red cards for PeachForDying response when it's the owner's turn.
    /// </summary>
    [TestMethod]
    public void ResponseRuleServiceExcludesConvertibleRedCardsForPeachResponseDuringOwnersTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        game.CurrentPlayerSeat = player.Seat; // Owner's turn
        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        ((Zone)player.HandZone).MutableCards.Add(redCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("jijiu", new JijiuSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("jijiu"));

        var responseRuleService = new ResponseRuleService(skillManager);
        var responseContext = new ResponseContext(
            game,
            player,
            ResponseType.PeachForDying,
            null);

        // Act
        var legalCards = responseRuleService.GetLegalResponseCards(responseContext);

        // Assert
        // Red card should NOT be in legal cards because Jijiu cannot be used during owner's turn
        Assert.IsFalse(legalCards.Items.Any(c => c.Id == redCard.Id), "Red card should NOT be in legal response cards during owner's turn");
    }

    /// <summary>
    /// Tests that player can use red card as Peach in dying rescue window when outside their turn.
    /// </summary>
    [TestMethod]
    public void PlayerCanUseRedCardAsPeachInDyingRescueOutsideTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var rescuer = game.Players[0];
        var dyingPlayer = game.Players[1];
        var otherPlayer = game.Players[1]; // Rescuer is not the current turn player
        game.CurrentPlayerSeat = otherPlayer.Seat; // Not rescuer's turn
        dyingPlayer.CurrentHealth = 0;
        dyingPlayer.IsAlive = true;

        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        ((Zone)rescuer.HandZone).MutableCards.Add(redCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("jijiu", new JijiuSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, rescuer, skillRegistry.GetSkill("jijiu"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();
        var intermediateResults = new Dictionary<string, object>();
        intermediateResults["DyingPlayerSeat"] = dyingPlayer.Seat; // Required by DyingResolver

        var peachUsed = new List<int>();

        ChoiceResult getPlayerChoice(ChoiceRequest request)
        {
            if (request.PlayerSeat == rescuer.Seat && request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                // Use red card as Peach
                var availableCard = request.AllowedCards
                    .FirstOrDefault(c => c.Id == redCard.Id && !peachUsed.Contains(c.Id));

                if (availableCard is not null)
                {
                    peachUsed.Add(availableCard.Id);
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: rescuer.Seat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { availableCard.Id },
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        }

        var context = new ResolutionContext(
            game,
            dyingPlayer,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults,
            SkillManager: skillManager,
            EventBus: eventBus,
            JudgementService: null);

        var initialRescuerHandCount = rescuer.HandZone.Cards.Count;
        var initialDyingPlayerHealth = dyingPlayer.CurrentHealth;

        // Act
        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute the stack to process response window and handler
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(dyingPlayer.CurrentHealth > initialDyingPlayerHealth, "Dying player should recover health");
        Assert.AreEqual(initialRescuerHandCount - 1, rescuer.HandZone.Cards.Count, "Rescuer should have one less card");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == redCard.Id), "Red card should be in discard pile");
    }

    /// <summary>
    /// Tests that player CANNOT use red card as Peach in dying rescue window when it's their turn.
    /// </summary>
    [TestMethod]
    public void PlayerCannotUseRedCardAsPeachInDyingRescueDuringOwnTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var rescuer = game.Players[0];
        var dyingPlayer = game.Players[1];
        game.CurrentPlayerSeat = rescuer.Seat; // Rescuer's turn - Jijiu should not work
        dyingPlayer.CurrentHealth = 0;
        dyingPlayer.IsAlive = true;

        var redCard = CreateRedCard(CardSubType.Slash, 1, Suit.Heart);
        ((Zone)rescuer.HandZone).MutableCards.Add(redCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("jijiu", new JijiuSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, rescuer, skillRegistry.GetSkill("jijiu"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();
        var intermediateResults = new Dictionary<string, object>();
        intermediateResults["DyingPlayerSeat"] = dyingPlayer.Seat; // Required by DyingResolver

        ChoiceResult getPlayerChoice(ChoiceRequest request)
        {
            if (request.PlayerSeat == rescuer.Seat && request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                // Try to use red card as Peach
                var availableCard = request.AllowedCards.FirstOrDefault(c => c.Id == redCard.Id);
                if (availableCard is not null)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: rescuer.Seat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { availableCard.Id },
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        }

        var context = new ResolutionContext(
            game,
            dyingPlayer,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults,
            SkillManager: skillManager,
            EventBus: eventBus,
            JudgementService: null);

        var initialDyingPlayerHealth = dyingPlayer.CurrentHealth;

        // Act
        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute the stack to process response window and handler
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert - Red card should NOT be in allowed cards, so it cannot be used
        // The response window should not allow the red card to be used as Peach during owner's turn
        // If no valid Peach is used, dying player should remain at 0 health
        Assert.AreEqual(initialDyingPlayerHealth, dyingPlayer.CurrentHealth, "Dying player should not recover because Jijiu cannot be used during owner's turn");
    }

    #endregion
}

