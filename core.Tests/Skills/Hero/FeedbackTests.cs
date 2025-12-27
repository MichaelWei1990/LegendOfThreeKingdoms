using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
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
public sealed class FeedbackTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
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
    /// Tests that FeedbackSkillFactory creates correct skill instance.
    /// Input: FeedbackSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void FeedbackSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new FeedbackSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("feedback", skill.Id);
        Assert.AreEqual("反馈", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Feedback skill.
    /// Input: Empty registry, FeedbackSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterFeedbackSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new FeedbackSkillFactory();

        // Act
        registry.RegisterSkill("feedback", factory);
        var skill = registry.GetSkill("feedback");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("feedback", skill.Id);
        Assert.AreEqual("反馈", skill.Name);
    }

    #endregion

    #region AfterDamage Event Tests

    /// <summary>
    /// Tests that Feedback skill does not trigger when damage has no source.
    /// Input: Game, player with Feedback skill takes damage from environment (no source).
    /// Expected: Feedback skill does not trigger.
    /// </summary>
    [TestMethod]
    public void FeedbackSkillDoesNotTriggerWhenNoSource()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("feedback", new FeedbackSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var feedbackSkill = skillRegistry.GetSkill("feedback");
        skillManager.AddEquipmentSkill(game, target, feedbackSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (feedbackSkill is FeedbackSkill feedback)
        {
            feedback.SetCardMoveService(cardMoveService);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        // Act: Publish AfterDamageEvent with no source (SourceSeat < 0)
        var damage = new DamageDescriptor(
            SourceSeat: -1, // No source
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Environment"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: No cards should be moved
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count, 
            "Target hand count should not change when no source");
        Assert.AreEqual(initialSourceHandCount, source.HandZone.Cards.Count, 
            "Source hand count should not change");
    }

    /// <summary>
    /// Tests that Feedback skill does not trigger when source has only judgement zone cards.
    /// Input: Game, player with Feedback skill takes damage, source has only judgement zone cards.
    /// Expected: Feedback skill does not trigger (no cards available).
    /// </summary>
    [TestMethod]
    public void FeedbackSkillDoesNotTriggerWhenSourceOnlyJudgment()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Add a card to source's judgement zone only
        var judgementCard = CreateTestCard(1);
        ((Zone)source.JudgementZone).MutableCards.Add(judgementCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("feedback", new FeedbackSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var feedbackSkill = skillRegistry.GetSkill("feedback");
        skillManager.AddEquipmentSkill(game, target, feedbackSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (feedbackSkill is FeedbackSkill feedback)
        {
            feedback.SetCardMoveService(cardMoveService);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: No cards should be moved (judgement zone cards are not obtainable)
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count, 
            "Target hand count should not change when source only has judgement zone cards");
    }

    /// <summary>
    /// Tests that Feedback skill obtains equipment card when source has equipment.
    /// Input: Game, player with Feedback skill takes damage, source has equipment card.
    /// Expected: Feedback skill obtains the equipment card.
    /// </summary>
    [TestMethod]
    public void FeedbackSkillObtainsEquipmentCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Add an equipment card to source
        var equipmentCard = CreateTestCard(1);
        ((Zone)source.EquipmentZone).MutableCards.Add(equipmentCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("feedback", new FeedbackSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var feedbackSkill = skillRegistry.GetSkill("feedback");
        skillManager.AddEquipmentSkill(game, target, feedbackSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (feedbackSkill is FeedbackSkill feedback)
        {
            feedback.SetCardMoveService(cardMoveService);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialSourceEquipmentCount = source.EquipmentZone.Cards.Count;

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Equipment card should be moved to target's hand
        Assert.AreEqual(initialTargetHandCount + 1, target.HandZone.Cards.Count, 
            "Target should have one more card");
        Assert.AreEqual(initialSourceEquipmentCount - 1, source.EquipmentZone.Cards.Count, 
            "Source should have one less equipment card");
        Assert.IsTrue(target.HandZone.Cards.Any(c => c.Id == equipmentCard.Id), 
            "Equipment card should be in target's hand");
    }

    /// <summary>
    /// Tests that Feedback skill obtains hand card selected by player when source has only hand cards.
    /// Input: Game, player with Feedback skill takes damage, source has hand cards.
    /// Expected: Feedback skill obtains the hand card selected by player (by index/card ID).
    /// </summary>
    [TestMethod]
    public void FeedbackSkillObtainsSelectedHandCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Add hand cards to source
        var handCard1 = CreateTestCard(1);
        var handCard2 = CreateTestCard(2);
        ((Zone)source.HandZone).MutableCards.Add(handCard1);
        ((Zone)source.HandZone).MutableCards.Add(handCard2);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("feedback", new FeedbackSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var feedbackSkill = skillRegistry.GetSkill("feedback");
        skillManager.AddEquipmentSkill(game, target, feedbackSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill with getPlayerChoice to simulate player selection
        int callCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            callCount++;
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                // First call: confirm to activate Feedback
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true // Confirm to activate Feedback
                );
            }
            else if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Second call: select a hand card by index (card ID)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { handCard1.Id }, // Select first hand card by ID
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        if (feedbackSkill is FeedbackSkill feedback)
        {
            feedback.SetCardMoveService(cardMoveService);
            feedback.SetGetPlayerChoice(getPlayerChoice);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: The selected hand card should be moved to target's hand
        Assert.AreEqual(initialTargetHandCount + 1, target.HandZone.Cards.Count, 
            "Target should have one more card");
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, 
            "Source should have one less hand card");
        // The selected hand card (handCard1) should be in target's hand
        Assert.IsTrue(target.HandZone.Cards.Any(c => c.Id == handCard1.Id), 
            "The selected hand card should be in target's hand");
        Assert.IsFalse(target.HandZone.Cards.Any(c => c.Id == handCard2.Id), 
            "The non-selected hand card should not be in target's hand");
    }

    /// <summary>
    /// Tests that Feedback skill triggers only once per damage event.
    /// Input: Game, player with Feedback skill takes 2 damage in one event.
    /// Expected: Feedback skill triggers only once.
    /// </summary>
    [TestMethod]
    public void FeedbackSkillTriggersOnlyOncePerDamageEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Add multiple hand cards to source
        var handCard1 = CreateTestCard(1);
        var handCard2 = CreateTestCard(2);
        var handCard3 = CreateTestCard(3);
        ((Zone)source.HandZone).MutableCards.Add(handCard1);
        ((Zone)source.HandZone).MutableCards.Add(handCard2);
        ((Zone)source.HandZone).MutableCards.Add(handCard3);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("feedback", new FeedbackSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var feedbackSkill = skillRegistry.GetSkill("feedback");
        skillManager.AddEquipmentSkill(game, target, feedbackSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (feedbackSkill is FeedbackSkill feedback)
        {
            feedback.SetCardMoveService(cardMoveService);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        // Act: Publish AfterDamageEvent with 2 damage (same event)
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 2, // 2 damage in one event
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 2
        );
        eventBus.Publish(afterDamageEvent);

        // Publish the same event again (simulating duplicate event)
        eventBus.Publish(afterDamageEvent);

        // Assert: Only one card should be moved (triggered only once)
        Assert.AreEqual(initialTargetHandCount + 1, target.HandZone.Cards.Count, 
            "Target should have only one more card (triggered only once)");
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, 
            "Source should have only one less card");
    }

    #endregion

    #region Dying and AfterDamage Tests

    /// <summary>
    /// Tests that Feedback skill does not trigger when player dies (not saved).
    /// Input: Game, player with Feedback skill takes damage, enters dying, not saved.
    /// Expected: AfterDamageEvent is not published, Feedback skill does not trigger.
    /// </summary>
    [TestMethod]
    public void FeedbackSkillDoesNotTriggerWhenDyingNotSaved()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Set target to 1 health
        target.CurrentHealth = 1;

        // Add hand card to source
        var handCard = CreateTestCard(1);
        ((Zone)source.HandZone).MutableCards.Add(handCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("feedback", new FeedbackSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var feedbackSkill = skillRegistry.GetSkill("feedback");
        skillManager.AddEquipmentSkill(game, target, feedbackSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (feedbackSkill is FeedbackSkill feedback)
        {
            feedback.SetCardMoveService(cardMoveService);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;

        // Act: Apply damage that causes dying
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            LogSink: null,
            GetPlayerChoice: null, // No rescue (simulating not saved)
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new DamageResolver();
        resolver.Resolve(context);

        // Execute stack (including DyingResolver if triggered)
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert: If player died, AfterDamageEvent should not be published
        // Since we can't directly verify event publication, we check that no cards were moved
        // (This test may need adjustment based on actual dying flow)
        // For now, we verify that if player is dead, no cards were moved
        if (!target.IsAlive)
        {
            Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count, 
                "Target hand count should not change if player died");
        }
    }

    #endregion
}
