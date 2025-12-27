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
public sealed class GanglieTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateTestCard(int id, Suit suit = Suit.Spade, int rank = 5)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that GanglieSkillFactory creates correct skill instance.
    /// Input: GanglieSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void GanglieSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new GanglieSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("ganglie", skill.Id);
        Assert.AreEqual("刚烈", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Ganglie skill.
    /// Input: Empty registry, GanglieSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterGanglieSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new GanglieSkillFactory();

        // Act
        registry.RegisterSkill("ganglie", factory);
        var skill = registry.GetSkill("ganglie");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("ganglie", skill.Id);
        Assert.AreEqual("刚烈", skill.Name);
    }

    #endregion

    #region Judgement Tests

    /// <summary>
    /// Tests that Ganglie skill performs judgement when damage is resolved.
    /// Input: Game, player with Ganglie skill takes damage, draw pile has non-Heart card.
    /// Expected: Judgement is performed, card is moved to judgement zone then discard pile.
    /// </summary>
    [TestMethod]
    public void GanglieSkillPerformsJudgementOnDamageResolved()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Add a non-Heart card to draw pile (Spade)
        var judgementCard = CreateTestCard(1, Suit.Spade, 5);
        ((Zone)game.DrawPile).MutableCards.Insert(0, judgementCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("ganglie", new GanglieSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var ganglieSkill = skillRegistry.GetSkill("ganglie");
        skillManager.AddEquipmentSkill(game, target, ganglieSkill);

        var judgementService = new BasicJudgementService(eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (ganglieSkill is GanglieSkill ganglie)
        {
            ganglie.SetJudgementService(judgementService);
            ganglie.SetCardMoveService(cardMoveService);
        }

        var initialJudgementZoneCount = target.JudgementZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Act: Publish DamageResolvedEvent
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var damageEvent = new DamageResolvedEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(damageEvent);

        // Assert: Judgement should have been performed
        // Note: The judgement card should be moved to judgement zone, then to discard pile
        // Since CompleteJudgement is called, the card should end up in discard pile
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == judgementCard.Id), 
            "Judgement card should be in discard pile after completion");
    }

    /// <summary>
    /// Tests that Ganglie skill does not trigger choice when judgement is Heart (fails).
    /// Input: Game, player with Ganglie skill takes damage, draw pile has Heart card.
    /// Expected: Judgement is performed, but no choice is triggered (judgement fails).
    /// </summary>
    [TestMethod]
    public void GanglieSkillDoesNotTriggerChoiceWhenJudgementIsHeart()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];
        var source = game.Players[1];

        // Add a Heart card to draw pile (judgement will fail)
        var judgementCard = CreateTestCard(1, Suit.Heart, 5);
        ((Zone)game.DrawPile).MutableCards.Insert(0, judgementCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("ganglie", new GanglieSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var ganglieSkill = skillRegistry.GetSkill("ganglie");
        skillManager.AddEquipmentSkill(game, target, ganglieSkill);

        var judgementService = new BasicJudgementService(eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (ganglieSkill is GanglieSkill ganglie)
        {
            ganglie.SetJudgementService(judgementService);
            ganglie.SetCardMoveService(cardMoveService);
        }

        var initialSourceHealth = source.CurrentHealth;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        // Act: Publish DamageResolvedEvent
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var damageEvent = new DamageResolvedEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(damageEvent);

        // Assert: Source should not be affected (no choice triggered)
        Assert.AreEqual(initialSourceHealth, source.CurrentHealth, 
            "Source health should not change when judgement fails");
        Assert.AreEqual(initialSourceHandCount, source.HandZone.Cards.Count, 
            "Source hand count should not change when judgement fails");
    }

    #endregion

    #region Choice and Execution Tests

    /// <summary>
    /// Tests that GanglieChoiceResolver executes discard option when player selects 2 cards.
    /// Input: GanglieChoiceResolver with choice result containing 2 card IDs.
    /// Expected: 2 cards are discarded from damage source's hand.
    /// </summary>
    [TestMethod]
    public void GanglieChoiceResolverExecutesDiscardOption()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var ganglieOwner = game.Players[0];
        var damageSource = game.Players[1];

        // Add 2 cards to damage source's hand
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)damageSource.HandZone).MutableCards.Add(card1);
        ((Zone)damageSource.HandZone).MutableCards.Add(card2);

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: damageSource.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            ganglieOwner,
            null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var initialHandCount = damageSource.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        // Act
        var resolver = new GanglieChoiceResolver(ganglieOwner.Seat, damageSource.Seat);
        var result = resolver.Resolve(context);

        // Execute the stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        Assert.AreEqual(initialHandCount - 2, damageSource.HandZone.Cards.Count, 
            "Damage source should have 2 fewer cards");
        Assert.AreEqual(initialDiscardPileCount + 2, game.DiscardPile.Cards.Count, 
            "Discard pile should have 2 more cards");
    }

    /// <summary>
    /// Tests that GanglieChoiceResolver executes damage option when player passes or has less than 2 cards.
    /// Input: GanglieChoiceResolver with choice result that passes or damage source has < 2 cards.
    /// Expected: Damage source takes 1 damage.
    /// </summary>
    [TestMethod]
    public void GanglieChoiceResolverExecutesDamageOption()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var ganglieOwner = game.Players[0];
        var damageSource = game.Players[1];

        // Set damage source to less than max health
        damageSource.CurrentHealth = 2;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        // Choice with no cards selected (pass = choose damage)
        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: damageSource.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null, // Pass = choose damage
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            ganglieOwner,
            null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var initialHealth = damageSource.CurrentHealth;

        // Act
        var resolver = new GanglieChoiceResolver(ganglieOwner.Seat, damageSource.Seat);
        var result = resolver.Resolve(context);

        // Execute the stack (including DamageResolver)
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        Assert.AreEqual(initialHealth - 1, damageSource.CurrentHealth, 
            "Damage source should take 1 damage");
    }

    /// <summary>
    /// Tests that GanglieChoiceResolver automatically chooses damage when damage source has less than 2 cards.
    /// Input: GanglieChoiceResolver, damage source has 1 card.
    /// Expected: Damage source takes 1 damage (no choice needed).
    /// </summary>
    [TestMethod]
    public void GanglieChoiceResolverAutoChoosesDamageWhenHandCardsLessThan2()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var ganglieOwner = game.Players[0];
        var damageSource = game.Players[1];

        // Add only 1 card to damage source's hand
        var card1 = CreateTestCard(1);
        ((Zone)damageSource.HandZone).MutableCards.Add(card1);

        // Set damage source to less than max health
        damageSource.CurrentHealth = 2;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            ganglieOwner,
            null,
            null, // No choice needed
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var initialHealth = damageSource.CurrentHealth;
        var initialHandCount = damageSource.HandZone.Cards.Count;

        // Act
        var resolver = new GanglieChoiceResolver(ganglieOwner.Seat, damageSource.Seat);
        var result = resolver.Resolve(context);

        // Execute the stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        Assert.AreEqual(initialHealth - 1, damageSource.CurrentHealth, 
            "Damage source should take 1 damage");
        Assert.AreEqual(initialHandCount, damageSource.HandZone.Cards.Count, 
            "Hand count should not change (damage option, not discard)");
    }

    #endregion
}
