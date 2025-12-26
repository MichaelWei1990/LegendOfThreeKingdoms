using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class WanjianqifaTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateWanjianqifaCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "wanjian_qifa",
            Name = "万箭齐发",
            CardType = CardType.Trick,
            CardSubType = CardSubType.WanjianQifa,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateJinkCard(int id = 100)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "jink",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Heart,
            Rank = 2
        };
    }

    #region Rule Tests

    /// <summary>
    /// Tests that Wanjianqifa can be used during Play phase.
    /// Input: Game with 3 players, source player in Play phase, Wanjianqifa card in hand.
    /// Expected: CanUseCard returns Allowed.
    /// </summary>
    [TestMethod]
    public void WanjianqifaCanBeUsedDuringPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = game.Players[0].Seat;
        var source = game.Players[0];
        var wanjianqifa = CreateWanjianqifaCard();
        ((Zone)source.HandZone).MutableCards.Add(wanjianqifa);

        var ruleService = new RuleService();
        var usageContext = new CardUsageContext(
            game,
            source,
            wanjianqifa,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var result = ruleService.CanUseCard(usageContext);

        // Assert
        Assert.IsTrue(result.IsAllowed, "Wanjianqifa should be usable during Play phase.");
    }

    /// <summary>
    /// Tests that Wanjianqifa requires at least one other alive player.
    /// Input: Game with 1 player (only source), Wanjianqifa card in hand.
    /// Expected: GetLegalTargets returns NoLegalOptions.
    /// </summary>
    [TestMethod]
    public void WanjianqifaRequiresAtLeastOneOtherAlivePlayer()
    {
        // Arrange
        var game = CreateDefaultGame(1);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = game.Players[0].Seat;
        var source = game.Players[0];
        var wanjianqifa = CreateWanjianqifaCard();
        ((Zone)source.HandZone).MutableCards.Add(wanjianqifa);

        var ruleService = new RuleService();
        var usageContext = new CardUsageContext(
            game,
            source,
            wanjianqifa,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = ruleService.GetLegalTargetsForUse(usageContext);

        // Assert
        Assert.IsFalse(legalTargets.HasAny, "Wanjianqifa should have no legal targets when no other players exist.");
        Assert.AreEqual(RuleErrorCode.NoLegalOptions, legalTargets.ErrorCode);
    }

    #endregion

    #region Resolver Tests

    /// <summary>
    /// Tests that WanjianqifaResolver processes all targets in turn order.
    /// Input: Game with 3 players, all targets have Jink cards.
    /// Expected: All targets can respond with Jink, no damage dealt.
    /// </summary>
    [TestMethod]
    public void WanjianqifaResolverProcessesAllTargetsInTurnOrder()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        var wanjianqifa = CreateWanjianqifaCard();
        ((Zone)source.HandZone).MutableCards.Add(wanjianqifa);

        // Give each target a Jink card
        var jink1 = CreateJinkCard(100);
        var jink2 = CreateJinkCard(101);
        ((Zone)target1.HandZone).MutableCards.Add(jink1);
        ((Zone)target2.HandZone).MutableCards.Add(jink2);

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        // Mock GetPlayerChoice to return Jink for each target
        int choiceCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            choiceCount++;
            // Find the responder
            var responder = game.Players.FirstOrDefault(p => p.Seat == request.PlayerSeat);
            if (responder is null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedCardIds: null, // Pass
                    SelectedTargetSeats: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Find Jink card in responder's hand
            var jinkCard = responder.HandZone.Cards.FirstOrDefault(c => c.CardSubType == CardSubType.Dodge);
            if (jinkCard is not null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedCardIds: new[] { jinkCard.Id },
                    SelectedTargetSeats: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // No Jink, pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedCardIds: null,
                SelectedTargetSeats: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null, // Wanjianqifa doesn't require target selection
            SelectedCardIds: new[] { wanjianqifa.Id },
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
            GetPlayerChoice: getPlayerChoice,
            EventBus: eventBus
        );

        var resolver = new WanjianqifaResolver();

        // Act
        var result = resolver.Resolve(context);
        
        // Execute stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "WanjianqifaResolver should succeed.");
        // All targets should have used their Jink cards (moved to discard pile)
        Assert.IsFalse(target1.HandZone.Cards.Contains(jink1), "Target 1's Jink should be used.");
        Assert.IsFalse(target2.HandZone.Cards.Contains(jink2), "Target 2's Jink should be used.");
        // No damage should be dealt
        Assert.AreEqual(4, target1.CurrentHealth, "Target 1 should not take damage (Jink played).");
        Assert.AreEqual(4, target2.CurrentHealth, "Target 2 should not take damage (Jink played).");
    }

    /// <summary>
    /// Tests that WanjianqifaResolver deals damage to targets without Jink.
    /// Input: Game with 3 players, targets don't have Jink cards.
    /// Expected: All targets take 1 damage.
    /// </summary>
    [TestMethod]
    public void WanjianqifaResolverDealsDamageToTargetsWithoutJink()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        var wanjianqifa = CreateWanjianqifaCard();
        ((Zone)source.HandZone).MutableCards.Add(wanjianqifa);

        // Targets don't have Jink cards
        var initialHealth1 = target1.CurrentHealth;
        var initialHealth2 = target2.CurrentHealth;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        // Mock GetPlayerChoice to return no Jink (pass)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedCardIds: null, // Pass (no Jink)
                SelectedTargetSeats: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { wanjianqifa.Id },
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
            GetPlayerChoice: getPlayerChoice,
            EventBus: eventBus
        );

        var resolver = new WanjianqifaResolver();

        // Act
        var result = resolver.Resolve(context);
        
        // Execute stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "WanjianqifaResolver should succeed.");
        // All targets should take 1 damage
        Assert.AreEqual(initialHealth1 - 1, target1.CurrentHealth, "Target 1 should take 1 damage.");
        Assert.AreEqual(initialHealth2 - 1, target2.CurrentHealth, "Target 2 should take 1 damage.");
    }

    #endregion
}
