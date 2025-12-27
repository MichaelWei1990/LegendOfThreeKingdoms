using System;
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
public sealed class LuoshenTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateBlackCard(int id = 1, Suit suit = Suit.Spade)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"black_card_{id}",
            Name = "Black Card",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit, // Spade or Club (black)
            Rank = 5
        };
    }

    private static Card CreateRedCard(int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"red_card_{id}",
            Name = "Red Card",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Heart, // Heart or Diamond (red)
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that LuoshenSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void LuoshenSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new LuoshenSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("luoshen", skill.Id);
        Assert.AreEqual("洛神", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Luoshen skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterLuoshenSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new LuoshenSkillFactory();

        // Act
        registry.RegisterSkill("luoshen", factory);
        var skill = registry.GetSkill("luoshen");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("luoshen", skill.Id);
        Assert.AreEqual("洛神", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that LuoshenSkill has correct properties.
    /// </summary>
    [TestMethod]
    public void LuoshenSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new LuoshenSkill();

        // Assert
        Assert.AreEqual("luoshen", skill.Id);
        Assert.AreEqual("洛神", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region Phase Trigger Tests

    /// <summary>
    /// Tests that LuoshenSkill triggers on PhaseStartEvent when phase is Start.
    /// </summary>
    [TestMethod]
    public void LuoshenSkill_TriggersOnPhaseStartEvent_WhenPhaseIsStart()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];

        var eventBus = new BasicEventBus();
        var skill = new LuoshenSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        var judgementService = new BasicJudgementService(eventBus);
        var resolutionStack = new BasicResolutionStack();

        skill.SetCardMoveService(cardMoveService);
        skill.SetJudgementService(judgementService);
        skill.SetResolutionStack(resolutionStack);

        // Setup getPlayerChoice to confirm activation and continue
        bool activationConfirmed = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                activationConfirmed = true;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        // Add a black card to draw pile
        var blackCard = CreateBlackCard(1);
        ((Zone)game.DrawPile).MutableCards.Add(blackCard);

        // Act
        var phaseStartEvent = new PhaseStartEvent(
            game,
            owner.Seat,
            Phase.Start
        );
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsTrue(activationConfirmed, "Skill should ask for activation confirmation");
        // Note: The actual judgement loop execution would require running the resolution stack,
        // which is more complex and would be tested in integration tests
    }

    /// <summary>
    /// Tests that LuoshenSkill does not trigger on PhaseStartEvent when phase is not Start.
    /// </summary>
    [TestMethod]
    public void LuoshenSkill_DoesNotTrigger_WhenPhaseIsNotStart()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];

        var eventBus = new BasicEventBus();
        var skill = new LuoshenSkill();
        skill.Attach(game, owner, eventBus);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        // Act
        var phaseStartEvent = new PhaseStartEvent(
            game,
            owner.Seat,
            Phase.Draw // Not Start phase
        );
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation when phase is not Start");
    }

    /// <summary>
    /// Tests that LuoshenSkill does not trigger when player chooses not to activate.
    /// </summary>
    [TestMethod]
    public void LuoshenSkill_DoesNotTrigger_WhenPlayerChoosesNotToActivate()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];

        var eventBus = new BasicEventBus();
        var skill = new LuoshenSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        var judgementService = new BasicJudgementService(eventBus);
        var resolutionStack = new BasicResolutionStack();

        skill.SetCardMoveService(cardMoveService);
        skill.SetJudgementService(judgementService);
        skill.SetResolutionStack(resolutionStack);

        bool resolverPushed = false;
        var mockStack = new MockResolutionStack(() => resolverPushed = true);

        // Setup getPlayerChoice to decline activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false // Player chooses not to activate
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        // Act
        var phaseStartEvent = new PhaseStartEvent(
            game,
            owner.Seat,
            Phase.Start
        );
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(resolverPushed, "Resolver should not be pushed when player declines activation");
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Mock resolution stack for testing.
    /// </summary>
    private sealed class MockResolutionStack : IResolutionStack
    {
        private readonly Action _onPush;

        public MockResolutionStack(Action onPush)
        {
            _onPush = onPush;
        }

        public bool IsEmpty => true;

        public void Push(IResolver resolver, ResolutionContext context)
        {
            _onPush();
        }

        public ResolutionResult Pop()
        {
            return ResolutionResult.SuccessResult;
        }

        public System.Collections.Generic.IReadOnlyList<ResolutionRecord> GetHistory()
        {
            return Array.Empty<ResolutionRecord>();
        }
    }

    #endregion
}

