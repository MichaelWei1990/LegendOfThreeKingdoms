using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class GuoheChaiqiaoTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateGuoheChaiqiaoCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "guohe_chaiqiao",
            Name = "过河拆桥",
            CardType = CardType.Trick,
            CardSubType = CardSubType.GuoheChaiqiao,
            Suit = Suit.Heart,
            Rank = 3
        };
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

    #region GuoheChaiqiaoResolver Tests

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver successfully discards a card from target's hand zone.
    /// Input: Game with 2 players, target has a card in hand, source player uses GuoheChaiqiao.
    /// Expected: Card is moved from target's hand to discard pile.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverDiscardsCardFromHand()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var targetCard = CreateTestCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        // Mock GetPlayerChoice to return the target card
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { targetCard.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialTargetHandCount - 1, target.HandZone.Cards.Count, "Target player should have 1 fewer card.");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Discard pile should have 1 more card.");
        Assert.IsFalse(target.HandZone.Cards.Contains(targetCard), "Target card should not be in target player's hand.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(targetCard), "Target card should be in discard pile.");
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver successfully discards a card from target's equipment zone.
    /// Input: Game with 2 players, target has a card in equipment zone, source player uses GuoheChaiqiao.
    /// Expected: Card is moved from target's equipment zone to discard pile.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverDiscardsCardFromEquipment()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var equipmentCard = new Card
        {
            Id = 200,
            DefinitionId = "test_equipment",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
        ((Zone)target.EquipmentZone).MutableCards.Add(equipmentCard);

        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { equipmentCard.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialTargetEquipmentCount - 1, target.EquipmentZone.Cards.Count, "Target player should have 1 fewer equipment.");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Discard pile should have 1 more card.");
        Assert.IsFalse(target.EquipmentZone.Cards.Contains(equipmentCard), "Equipment card should not be in target player's equipment zone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(equipmentCard), "Equipment card should be in discard pile.");
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver successfully discards a card from target's judgement zone.
    /// Input: Game with 2 players, target has a delayed trick in judgement zone, source player uses GuoheChaiqiao.
    /// Expected: Card is moved from target's judgement zone to discard pile.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverDiscardsCardFromJudgement()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var judgementCard = new Card
        {
            Id = 300,
            DefinitionId = "test_judgement",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Lebusishu,
            Suit = Suit.Spade,
            Rank = 5
        };
        ((Zone)target.JudgementZone).MutableCards.Add(judgementCard);

        var initialTargetJudgementCount = target.JudgementZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { judgementCard.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialTargetJudgementCount - 1, target.JudgementZone.Cards.Count, "Target player should have 1 fewer judgement card.");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Discard pile should have 1 more card.");
        Assert.IsFalse(target.JudgementZone.Cards.Contains(judgementCard), "Judgement card should not be in target player's judgement zone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(judgementCard), "Judgement card should be in discard pile.");
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver works with targets at any distance (no distance restriction).
    /// Input: Game with 4 players, source and target are far apart (distance > 1).
    /// Expected: Resolver succeeds (unlike ShunshouQianyang which would fail).
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverWorksWithDistantTarget()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var source = game.Players[0];
        var target = game.Players[2]; // Distance 2 in a 4-player game

        var targetCard = CreateTestCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { targetCard.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "GuoheChaiqiao should work with distant targets (no distance restriction).");
        Assert.AreEqual(initialTargetHandCount - 1, target.HandZone.Cards.Count, "Target player should have 1 fewer card.");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Discard pile should have 1 more card.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(targetCard), "Target card should be in discard pile.");
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver fails when target is not alive.
    /// Input: Game with 2 players, target is dead.
    /// Expected: Resolver returns failure with TargetNotAlive error code.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverFailsWhenTargetNotAlive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        target.IsAlive = false;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.TargetNotAlive, result.ErrorCode);
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver fails when target has no discardable cards.
    /// Input: Game with 2 players, target has no cards in hand, equipment, or judgement zones.
    /// Expected: Resolver returns failure with InvalidState error code.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverFailsWhenNoDiscardableCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Ensure target has no cards
        ((Zone)target.HandZone).MutableCards.Clear();
        ((Zone)target.EquipmentZone).MutableCards.Clear();
        ((Zone)target.JudgementZone).MutableCards.Clear();

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);
        Assert.IsTrue(result.MessageKey.Contains("noDiscardableCards") || result.MessageKey.Contains("guohechaiqiao"));
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver fails when GetPlayerChoice is not available.
    /// Input: Game with 2 players, context without GetPlayerChoice function.
    /// Expected: Resolver returns failure with InvalidState error code.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverFailsWhenGetPlayerChoiceNotAvailable()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var targetCard = CreateTestCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: null  // No GetPlayerChoice provided
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);
        Assert.IsTrue(result.MessageKey.Contains("getPlayerChoiceNotAvailable") || result.MessageKey.Contains("guohechaiqiao"));
    }

    /// <summary>
    /// Tests that GuoheChaiqiaoResolver fails when player selects an invalid card.
    /// Input: Game with 2 players, player selects a card that's not in the discardable list.
    /// Expected: Resolver returns failure with CardNotFound error code.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoResolverFailsWhenInvalidCardSelected()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var targetCard = CreateTestCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        var invalidCardId = 999; // Card ID that doesn't exist

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { invalidCardId }, // Invalid card ID
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.CardNotFound, result.ErrorCode);
    }

    #endregion

    #region Integration Tests (UseCardResolver)

    /// <summary>
    /// Verifies that UseCardResolver successfully processes a valid GuoheChaiqiao usage through the complete flow.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Source player has a GuoheChaiqiao card in hand
    /// - Target player has a card in hand
    /// - Creates a UseGuoheChaiqiao action
    /// - Executes UseCardResolver with the action and choice
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - GuoheChaiqiao card is moved from source player's hand to the discard pile
    /// - GuoheChaiqiaoResolver is pushed onto the stack for further processing
    /// - Target card is moved to discard pile
    /// </summary>
    [TestMethod]
    public void UseCardResolverProcessesValidGuoheChaiqiao()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var guoheChaiqiao = CreateGuoheChaiqiaoCard();
        ((Zone)source.HandZone).MutableCards.Add(guoheChaiqiao);

        var targetCard = CreateTestCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseGuoheChaiqiao",
            DisplayKey: "action.useGuoheChaiqiao",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Any),
            CardCandidates: new[] { guoheChaiqiao }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { guoheChaiqiao.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { targetCard.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Verify GuoheChaiqiao card was moved from hand to discard pile
        Assert.IsFalse(source.HandZone.Cards.Contains(guoheChaiqiao), "GuoheChaiqiao card should be removed from hand.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(guoheChaiqiao), "GuoheChaiqiao card should be in discard pile.");

        // Verify ImmediateTrickResolver (which will delegate to GuoheChaiqiaoResolver) was pushed onto the stack
        Assert.IsFalse(stack.IsEmpty, "Stack should not be empty after UseCardResolver.");

        // Execute the stack to trigger GuoheChaiqiaoResolver
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify target card was moved to discard pile
        Assert.AreEqual(initialTargetHandCount - 1, target.HandZone.Cards.Count, "Target player should have 1 fewer card.");
        Assert.AreEqual(initialDiscardPileCount + 2, game.DiscardPile.Cards.Count, "Discard pile should have 2 more cards (GuoheChaiqiao + target card).");
        Assert.IsFalse(target.HandZone.Cards.Contains(targetCard), "Target card should not be in target player's hand.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(targetCard), "Target card should be in discard pile.");
    }

    /// <summary>
    /// Verifies that UseCardResolver properly handles the error case when the selected GuoheChaiqiao card
    /// is not found in the player's hand.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Creates a GuoheChaiqiao card but does NOT add it to the source player's hand
    /// - Creates a UseGuoheChaiqiao action that references the non-existent card
    /// - Executes UseCardResolver with invalid card selection
    /// 
    /// Expected results:
    /// - Resolution fails with CardNotFound error code
    /// - Card is NOT moved (validation fails before card movement)
    /// - Game state remains unchanged
    /// </summary>
    [TestMethod]
    public void UseCardResolverFailsWhenGuoheChaiqiaoCardNotFound()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        // Don't add card to hand - it should fail
        var guoheChaiqiao = CreateGuoheChaiqiaoCard();

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseGuoheChaiqiao",
            DisplayKey: "action.useGuoheChaiqiao",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Any),
            CardCandidates: new[] { guoheChaiqiao }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { game.Players[1].Seat },
            SelectedCardIds: new[] { guoheChaiqiao.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.CardNotFound, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);

        // Verify card was NOT moved
        Assert.IsFalse(source.HandZone.Cards.Contains(guoheChaiqiao));
        Assert.IsFalse(game.DiscardPile.Cards.Contains(guoheChaiqiao));
    }

    #endregion
}
