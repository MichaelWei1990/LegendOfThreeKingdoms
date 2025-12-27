using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Equipment;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class JianxiongTests
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

    private static Card CreateNanmanRushinCard(int id = 10)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "nanman_rushin",
            Name = "南蛮入侵",
            CardType = CardType.Trick,
            CardSubType = CardSubType.NanmanRushin,
            Suit = Suit.Spade,
            Rank = 7
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that JianxiongSkillFactory creates correct skill instance.
    /// Input: JianxiongSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void JianxiongSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new JianxiongSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jianxiong", skill.Id);
        Assert.AreEqual("奸雄", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Jianxiong skill.
    /// Input: Empty registry, JianxiongSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterJianxiongSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();

        // Act
        registry.RegisterSkill("jianxiong", factory);
        var skill = registry.GetSkill("jianxiong");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jianxiong", skill.Id);
        Assert.AreEqual("奸雄", skill.Name);
    }

    #endregion

    #region Damage Trigger Tests

    /// <summary>
    /// Tests that JianxiongSkill triggers and obtains the causing card when damage is resolved.
    /// Input: Game with 2 players, target has Jianxiong skill, damage caused by a card in discard pile.
    /// Expected: Target player obtains the causing card.
    /// </summary>
    [TestMethod]
    public void JianxiongSkillObtainsCausingCardFromDiscardPile()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHandCount = target.HandZone.Cards.Count;

        // Put a Slash card in discard pile (simulating it was used and discarded)
        var slashCard = CreateSlashCard(1);
        ((Zone)game.DiscardPile).MutableCards.Add(slashCard);

        // Setup skill manager and register Jianxiong skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jianxiong skill to target player
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, target, jianxiongSkill);

        // Create damage descriptor with causing card
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard
        );

        // Act - Apply damage (this will publish AfterDamageEvent)
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            EventBus: eventBus
        );
        var damageResolver = new DamageResolver();
        var result = damageResolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Damage should be applied successfully.");
        Assert.IsTrue(target.HandZone.Cards.Contains(slashCard), "Target player should have obtained the causing card.");
        Assert.IsFalse(game.DiscardPile.Cards.Contains(slashCard), "Causing card should be removed from discard pile.");
        Assert.AreEqual(initialHandCount + 1, target.HandZone.Cards.Count, "Target player should have 1 more card in hand.");
    }

    /// <summary>
    /// Tests that JianxiongSkill does NOT trigger when damage has no causing card.
    /// Input: Game with 2 players, target has Jianxiong skill, damage without causing card.
    /// Expected: Target player does NOT obtain any card.
    /// </summary>
    [TestMethod]
    public void JianxiongSkillDoesNotTriggerWhenNoCausingCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHandCount = target.HandZone.Cards.Count;

        // Setup skill manager and register Jianxiong skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jianxiong skill to target player
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, target, jianxiongSkill);

        // Create damage descriptor without causing card (e.g., from Shandian)
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Shandian",
            CausingCard: null  // No causing card
        );

        // Act - Apply damage
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            EventBus: eventBus
        );
        var damageResolver = new DamageResolver();
        var result = damageResolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Damage should be applied successfully.");
        Assert.AreEqual(initialHandCount, target.HandZone.Cards.Count, "Target player should NOT have obtained any card.");
    }

    /// <summary>
    /// Tests that JianxiongSkill does NOT trigger when causing card is in another player's hand.
    /// Input: Game with 2 players, target has Jianxiong skill, causing card is in source player's hand.
    /// Expected: Target player does NOT obtain the card (card is not in obtainable zone).
    /// </summary>
    [TestMethod]
    public void JianxiongSkillDoesNotObtainCardFromOtherPlayerHand()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHandCount = target.HandZone.Cards.Count;
        
        // Put a Slash card in source player's hand (not obtainable)
        var slashCard = CreateSlashCard(1);
        ((Zone)source.HandZone).MutableCards.Add(slashCard);
        var initialSourceHandCount = source.HandZone.Cards.Count;

        // Setup skill manager and register Jianxiong skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jianxiong skill to target player
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, target, jianxiongSkill);

        // Create damage descriptor with causing card (in source's hand)
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard
        );

        // Act - Apply damage
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            EventBus: eventBus
        );
        var damageResolver = new DamageResolver();
        var result = damageResolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Damage should be applied successfully.");
        Assert.AreEqual(initialHandCount, target.HandZone.Cards.Count, "Target player should NOT have obtained the card.");
        Assert.AreEqual(initialSourceHandCount, source.HandZone.Cards.Count, "Source player should still have the card.");
        Assert.IsTrue(source.HandZone.Cards.Contains(slashCard), "Card should still be in source player's hand.");
    }

    /// <summary>
    /// Tests that JianxiongSkill does NOT trigger when skill owner is dead.
    /// Input: Game with 2 players, target has Jianxiong skill but is dead, damage caused by a card.
    /// Expected: Target player does NOT obtain the card (skill not active).
    /// </summary>
    [TestMethod]
    public void JianxiongSkillDoesNotTriggerWhenOwnerIsDead()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        // Note: We can't actually apply damage to a dead player, so we'll test by
        // making the skill inactive through IsActive check instead
        // For this test, we'll verify that the skill doesn't trigger when IsActive returns false
        var initialHandCount = target.HandZone.Cards.Count;

        // Put a Slash card in discard pile
        var slashCard = CreateSlashCard(1);
        ((Zone)game.DiscardPile).MutableCards.Add(slashCard);

        // Setup skill manager and register Jianxiong skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jianxiong skill to target player
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, target, jianxiongSkill);

        // Mark target as dead BEFORE publishing the event
        target.CurrentHealth = 0;
        target.IsAlive = false;

        // Create damage descriptor with causing card
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard
        );

        // Act - Manually publish AfterDamageEvent to test skill behavior when owner is dead
        // (We can't actually apply damage to a dead player via DamageResolver)
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: 1,
            CurrentHealth: 0
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(initialHandCount, target.HandZone.Cards.Count, "Target player should NOT have obtained the card (skill not active).");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(slashCard), "Card should still be in discard pile.");
    }

    /// <summary>
    /// Tests that JianxiongSkill does NOT trigger when damage target is not the skill owner.
    /// Input: Game with 3 players, player 0 has Jianxiong skill, damage dealt to player 1.
    /// Expected: Player 0 does NOT obtain the card.
    /// </summary>
    [TestMethod]
    public void JianxiongSkillDoesNotTriggerForOtherPlayersDamage()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var target = game.Players[1];
        var jianxiongOwner = game.Players[0];  // Same as source, but skill owner
        var initialHandCount = jianxiongOwner.HandZone.Cards.Count;

        // Put a Slash card in discard pile
        var slashCard = CreateSlashCard(1);
        ((Zone)game.DiscardPile).MutableCards.Add(slashCard);

        // Setup skill manager and register Jianxiong skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jianxiong skill to player 0
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, jianxiongOwner, jianxiongSkill);

        // Create damage descriptor: source=0, target=1 (not the skill owner)
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,  // Target is player 1, not player 0
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard
        );

        // Act - Apply damage
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            EventBus: eventBus
        );
        var damageResolver = new DamageResolver();
        var result = damageResolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Damage should be applied successfully.");
        Assert.AreEqual(initialHandCount, jianxiongOwner.HandZone.Cards.Count, "Jianxiong owner should NOT have obtained the card.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(slashCard), "Card should still be in discard pile.");
    }

    /// <summary>
    /// Tests that JianxiongSkill handles multiple damage events correctly.
    /// Input: Game with 2 players, target has Jianxiong skill, two separate damage events.
    /// Expected: Target player obtains both causing cards.
    /// </summary>
    [TestMethod]
    public void JianxiongSkillHandlesMultipleDamageEvents()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHandCount = target.HandZone.Cards.Count;

        // Put two Slash cards in discard pile
        var slashCard1 = CreateSlashCard(1);
        var slashCard2 = CreateSlashCard(2);
        ((Zone)game.DiscardPile).MutableCards.Add(slashCard1);
        ((Zone)game.DiscardPile).MutableCards.Add(slashCard2);

        // Setup skill manager and register Jianxiong skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jianxiong skill to target player
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, target, jianxiongSkill);

        // Act - Apply first damage
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var damage1 = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard1
        );
        var context1 = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage1,
            EventBus: eventBus
        );
        var damageResolver = new DamageResolver();
        var result1 = damageResolver.Resolve(context1);

        // Apply second damage
        var damage2 = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard2
        );
        var context2 = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage2,
            EventBus: eventBus
        );
        var result2 = damageResolver.Resolve(context2);

        // Assert
        Assert.IsTrue(result1.Success, "First damage should be applied successfully.");
        Assert.IsTrue(result2.Success, "Second damage should be applied successfully.");
        Assert.IsTrue(target.HandZone.Cards.Contains(slashCard1), "Target player should have obtained the first card.");
        Assert.IsTrue(target.HandZone.Cards.Contains(slashCard2), "Target player should have obtained the second card.");
        Assert.AreEqual(initialHandCount + 2, target.HandZone.Cards.Count, "Target player should have 2 more cards in hand.");
    }

    /// <summary>
    /// Tests that JianxiongSkill obtains both original cards when damage is caused by Serpent Spear (multi-card conversion).
    /// Input: Game with 2 players, source has Serpent Spear, target has Jianxiong skill.
    /// Source uses two hand cards as Slash via Serpent Spear, deals damage to target.
    /// Expected: Target player obtains both original hand cards that were used for conversion.
    /// </summary>
    [TestMethod]
    public void JianxiongSkillObtainsOriginalCardsFromSerpentSpearConversion()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialTargetHandCount = target.HandZone.Cards.Count;

        // Create two hand cards for source player to use as Slash via Serpent Spear
        var card1 = new Card
        {
            Id = 100,
            DefinitionId = "card1",
            Name = "手牌1",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 5
        };
        var card2 = new Card
        {
            Id = 101,
            DefinitionId = "card2",
            Name = "手牌2",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Spade,
            Rank = 6
        };
        ((Zone)source.HandZone).MutableCards.Add(card1);
        ((Zone)source.HandZone).MutableCards.Add(card2);

        // Setup skill manager and register skills
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        
        // Register Jianxiong skill (for hero skill)
        var jianxiongFactory = new JianxiongSkillFactory();
        skillRegistry.RegisterSkill("jianxiong", jianxiongFactory);
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add Serpent Spear to source player
        var serpentSpearCard = new Card
        {
            Id = 200,
            DefinitionId = "serpent_spear",
            Name = "丈八蛇矛",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
        ((Zone)source.EquipmentZone).MutableCards.Add(serpentSpearCard);
        var serpentSpearFactory = new SerpentSpearSkillFactory();
        var serpentSpearSkill = serpentSpearFactory.CreateSkill();
        skillManager.AddEquipmentSkill(game, source, serpentSpearSkill);

        // Add Jianxiong skill to target player
        var jianxiongSkill = skillRegistry.GetSkill("jianxiong");
        if (jianxiongSkill is JianxiongSkill jianxiong)
        {
            jianxiong.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, target, jianxiongSkill);

        // Move the two cards to discard pile (simulating they were used and discarded)
        ((Zone)game.DiscardPile).MutableCards.Add(card1);
        ((Zone)game.DiscardPile).MutableCards.Add(card2);
        ((Zone)source.HandZone).MutableCards.Remove(card1);
        ((Zone)source.HandZone).MutableCards.Remove(card2);

        // Create damage descriptor with CausingCards (the two original cards used for Serpent Spear conversion)
        var causingCards = new List<Card> { card1, card2 };
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: null,  // Virtual Slash card (not needed for Jianxiong)
            CausingCards: causingCards  // The two original cards used for conversion
        );

        // Act - Apply damage (this will publish AfterDamageEvent)
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            EventBus: eventBus
        );
        var damageResolver = new DamageResolver();
        var result = damageResolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Damage should be applied successfully.");
        Assert.IsTrue(target.HandZone.Cards.Contains(card1), "Target player should have obtained the first original card.");
        Assert.IsTrue(target.HandZone.Cards.Contains(card2), "Target player should have obtained the second original card.");
        Assert.AreEqual(initialTargetHandCount + 2, target.HandZone.Cards.Count, "Target player should have 2 more cards in hand.");
        Assert.IsFalse(game.DiscardPile.Cards.Contains(card1), "First card should no longer be in discard pile.");
        Assert.IsFalse(game.DiscardPile.Cards.Contains(card2), "Second card should no longer be in discard pile.");
    }

    #endregion
}
