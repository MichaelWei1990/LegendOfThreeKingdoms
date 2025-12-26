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
public sealed class NanmanRushinTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateNanmanRushinCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "nanman_rushin",
            Name = "南蛮入侵",
            CardType = CardType.Trick,
            CardSubType = CardSubType.NanmanRushin,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateSlashCard(int id = 100)
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

    #region Rule Tests

    /// <summary>
    /// Tests that NanmanRushin can be used during Play phase.
    /// Input: Game with 3 players, source player in Play phase, NanmanRushin card in hand.
    /// Expected: CanUseCard returns Allowed.
    /// </summary>
    [TestMethod]
    public void NanmanRushinCanBeUsedDuringPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = game.Players[0].Seat;
        var source = game.Players[0];
        var nanmanRushin = CreateNanmanRushinCard();
        ((Zone)source.HandZone).MutableCards.Add(nanmanRushin);

        var ruleService = new RuleService();
        var usageContext = new CardUsageContext(
            game,
            source,
            nanmanRushin,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var result = ruleService.CanUseCard(usageContext);

        // Assert
        Assert.IsTrue(result.IsAllowed, "NanmanRushin should be usable during Play phase.");
    }

    /// <summary>
    /// Tests that NanmanRushin requires at least one other alive player.
    /// Input: Game with 1 player (only source), NanmanRushin card in hand.
    /// Expected: GetLegalTargets returns NoLegalOptions.
    /// </summary>
    [TestMethod]
    public void NanmanRushinRequiresAtLeastOneOtherAlivePlayer()
    {
        // Arrange
        var game = CreateDefaultGame(1);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = game.Players[0].Seat;
        var source = game.Players[0];
        var nanmanRushin = CreateNanmanRushinCard();
        ((Zone)source.HandZone).MutableCards.Add(nanmanRushin);

        var ruleService = new RuleService();
        var usageContext = new CardUsageContext(
            game,
            source,
            nanmanRushin,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = ruleService.GetLegalTargetsForUse(usageContext);

        // Assert
        Assert.IsFalse(legalTargets.HasAny, "NanmanRushin should have no legal targets when no other players exist.");
        Assert.AreEqual(RuleErrorCode.NoLegalOptions, legalTargets.ErrorCode);
    }

    #endregion

    #region Resolver Tests

    /// <summary>
    /// Tests that NanmanRushinResolver processes all targets in turn order.
    /// Input: Game with 3 players, all targets have Slash cards.
    /// Expected: All targets can respond with Slash, no damage dealt.
    /// </summary>
    [TestMethod]
    public void NanmanRushinResolverProcessesAllTargetsInTurnOrder()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        var nanmanRushin = CreateNanmanRushinCard();
        ((Zone)source.HandZone).MutableCards.Add(nanmanRushin);

        // Give each target a Slash card
        var slash1 = CreateSlashCard(100);
        var slash2 = CreateSlashCard(101);
        ((Zone)target1.HandZone).MutableCards.Add(slash1);
        ((Zone)target2.HandZone).MutableCards.Add(slash2);

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        // Mock GetPlayerChoice to return Slash for each target
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

            // Find Slash card in responder's hand
            var slashCard = responder.HandZone.Cards.FirstOrDefault(c => c.CardSubType == CardSubType.Slash);
            if (slashCard is not null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedCardIds: new[] { slashCard.Id },
                    SelectedTargetSeats: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // No Slash, pass
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
            SelectedTargetSeats: null, // NanmanRushin doesn't require target selection
            SelectedCardIds: new[] { nanmanRushin.Id },
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

        var resolver = new NanmanRushinResolver();

        // Act
        var result = resolver.Resolve(context);
        
        // Execute stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "NanmanRushinResolver should succeed.");
        // All targets should have used their Slash cards (moved to discard pile)
        Assert.IsFalse(target1.HandZone.Cards.Contains(slash1), "Target 1's Slash should be used.");
        Assert.IsFalse(target2.HandZone.Cards.Contains(slash2), "Target 2's Slash should be used.");
        // No damage should be dealt
        Assert.AreEqual(4, target1.CurrentHealth, "Target 1 should not take damage (Slash played).");
        Assert.AreEqual(4, target2.CurrentHealth, "Target 2 should not take damage (Slash played).");
    }

    /// <summary>
    /// Tests that NanmanRushinResolver deals damage to targets without Slash.
    /// Input: Game with 3 players, targets don't have Slash cards.
    /// Expected: All targets take 1 damage.
    /// </summary>
    [TestMethod]
    public void NanmanRushinResolverDealsDamageToTargetsWithoutSlash()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        var nanmanRushin = CreateNanmanRushinCard();
        ((Zone)source.HandZone).MutableCards.Add(nanmanRushin);

        // Targets don't have Slash cards
        var initialHealth1 = target1.CurrentHealth;
        var initialHealth2 = target2.CurrentHealth;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        // Mock GetPlayerChoice to return no Slash (pass)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedCardIds: null, // Pass (no Slash)
                SelectedTargetSeats: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { nanmanRushin.Id },
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

        var resolver = new NanmanRushinResolver();

        // Act
        var result = resolver.Resolve(context);
        
        // Execute stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "NanmanRushinResolver should succeed.");
        // All targets should take 1 damage
        Assert.AreEqual(initialHealth1 - 1, target1.CurrentHealth, "Target 1 should take 1 damage.");
        Assert.AreEqual(initialHealth2 - 1, target2.CurrentHealth, "Target 2 should take 1 damage.");
    }

    #endregion
}
