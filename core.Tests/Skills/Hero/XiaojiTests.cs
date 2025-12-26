using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class XiaojiTests
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
                    Id = 1000 + i,
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

    private static Card CreateEquipmentCard(CardSubType subType, int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = subType.ToString().ToLower(),
            Name = subType.ToString(),
            CardType = CardType.Equip,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 1
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that XiaojiSkillFactory creates correct skill instance.
    /// Input: XiaojiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void XiaojiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new XiaojiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("xiaoji", skill.Id);
        Assert.AreEqual("枭姬", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Xiaoji skill.
    /// Input: Empty registry, XiaojiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterXiaojiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();

        // Act
        registry.RegisterSkill("xiaoji", factory);
        var skill = registry.GetSkill("xiaoji");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("xiaoji", skill.Id);
        Assert.AreEqual("枭姬", skill.Name);
    }

    #endregion

    #region Equipment Removal Trigger Tests

    /// <summary>
    /// Tests that XiaojiSkill triggers and draws 2 cards when equipment is removed from equipment zone.
    /// Input: Game with 2 players, source has Xiaoji skill, equipment in equipment zone, equipment removed.
    /// Expected: Source player draws 2 cards.
    /// </summary>
    [TestMethod]
    public void XiaojiSkillDrawsTwoCardsWhenEquipmentRemoved()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10); // Ensure draw pile has enough cards
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Put equipment in equipment zone
        var equipment = CreateEquipmentCard(CardSubType.OffensiveHorse, 1);
        ((Zone)source.EquipmentZone).MutableCards.Add(equipment);

        // Setup skill manager and register Xiaoji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();
        skillRegistry.RegisterSkill("xiaoji", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Xiaoji skill to source player
        var xiaojiSkill = skillRegistry.GetSkill("xiaoji");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (xiaojiSkill is XiaojiSkill xiaoji)
        {
            xiaoji.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, source, xiaojiSkill);

        // Act - Remove equipment (move from equipment zone to discard pile)
        var unequipDescriptor = new CardMoveDescriptor(
            SourceZone: source.EquipmentZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { equipment },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(unequipDescriptor);

        // Assert
        Assert.IsFalse(source.EquipmentZone.Cards.Contains(equipment), "Equipment should be removed from equipment zone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(equipment), "Equipment should be in discard pile.");
        Assert.AreEqual(initialHandCount + 2, source.HandZone.Cards.Count, "Source player should have drawn 2 cards.");
        Assert.AreEqual(initialDrawPileCount - 2, game.DrawPile.Cards.Count, "Draw pile should have 2 fewer cards.");
    }

    /// <summary>
    /// Tests that XiaojiSkill does NOT trigger when equipment is added to equipment zone.
    /// Input: Game with 2 players, source has Xiaoji skill, equipment moved to equipment zone.
    /// Expected: Source player does NOT draw cards.
    /// </summary>
    [TestMethod]
    public void XiaojiSkillDoesNotTriggerWhenEquipmentAdded()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        
        // Put equipment in hand zone
        var equipment = CreateEquipmentCard(CardSubType.OffensiveHorse, 1);
        ((Zone)source.HandZone).MutableCards.Add(equipment);
        var initialHandCount = source.HandZone.Cards.Count;

        // Setup skill manager and register Xiaoji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();
        skillRegistry.RegisterSkill("xiaoji", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Xiaoji skill to source player
        var xiaojiSkill = skillRegistry.GetSkill("xiaoji");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (xiaojiSkill is XiaojiSkill xiaoji)
        {
            xiaoji.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, source, xiaojiSkill);

        // Act - Add equipment (move from hand zone to equipment zone)
        var equipDescriptor = new CardMoveDescriptor(
            SourceZone: source.HandZone,
            TargetZone: source.EquipmentZone,
            Cards: new[] { equipment },
            Reason: CardMoveReason.Equip,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(equipDescriptor);

        // Assert
        Assert.IsTrue(source.EquipmentZone.Cards.Contains(equipment), "Equipment should be in equipment zone.");
        Assert.AreEqual(initialHandCount - 1, source.HandZone.Cards.Count, "Source player should have 1 fewer card in hand (equipment moved).");
        // No cards should be drawn
        Assert.AreEqual(initialHandCount - 1, source.HandZone.Cards.Count, "No additional cards should be drawn.");
    }

    /// <summary>
    /// Tests that XiaojiSkill does NOT trigger when other player's equipment is removed.
    /// Input: Game with 2 players, source has Xiaoji skill, target has equipment, target's equipment removed.
    /// Expected: Source player does NOT draw cards.
    /// </summary>
    [TestMethod]
    public void XiaojiSkillDoesNotTriggerWhenOtherPlayerEquipmentRemoved()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHandCount = source.HandZone.Cards.Count;

        // Put equipment in target's equipment zone
        var equipment = CreateEquipmentCard(CardSubType.OffensiveHorse, 1);
        ((Zone)target.EquipmentZone).MutableCards.Add(equipment);

        // Setup skill manager and register Xiaoji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();
        skillRegistry.RegisterSkill("xiaoji", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Xiaoji skill to source player (not target)
        var xiaojiSkill = skillRegistry.GetSkill("xiaoji");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (xiaojiSkill is XiaojiSkill xiaoji)
        {
            xiaoji.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, source, xiaojiSkill);

        // Act - Remove target's equipment
        var unequipDescriptor = new CardMoveDescriptor(
            SourceZone: target.EquipmentZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { equipment },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(unequipDescriptor);

        // Assert
        Assert.IsFalse(target.EquipmentZone.Cards.Contains(equipment), "Equipment should be removed from target's equipment zone.");
        Assert.AreEqual(initialHandCount, source.HandZone.Cards.Count, "Source player should NOT have drawn any cards.");
    }

    /// <summary>
    /// Tests that XiaojiSkill does NOT trigger when card is removed from non-equipment zone.
    /// Input: Game with 2 players, source has Xiaoji skill, card removed from hand zone.
    /// Expected: Source player does NOT draw cards.
    /// </summary>
    [TestMethod]
    public void XiaojiSkillDoesNotTriggerWhenCardRemovedFromHand()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;

        // Put a card in hand zone
        var card = new Card
        {
            Id = 1,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
        ((Zone)source.HandZone).MutableCards.Add(card);

        // Setup skill manager and register Xiaoji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();
        skillRegistry.RegisterSkill("xiaoji", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Xiaoji skill to source player
        var xiaojiSkill = skillRegistry.GetSkill("xiaoji");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (xiaojiSkill is XiaojiSkill xiaoji)
        {
            xiaoji.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, source, xiaojiSkill);

        // Act - Remove card from hand zone
        var discardDescriptor = new CardMoveDescriptor(
            SourceZone: source.HandZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { card },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(discardDescriptor);

        // Assert
        Assert.IsFalse(source.HandZone.Cards.Contains(card), "Card should be removed from hand zone.");
        Assert.AreEqual(initialHandCount, source.HandZone.Cards.Count, "Source player should NOT have drawn any cards.");
    }

    /// <summary>
    /// Tests that XiaojiSkill does NOT trigger when skill owner is dead.
    /// Input: Game with 2 players, source has Xiaoji skill but is dead, equipment removed.
    /// Expected: Source player does NOT draw cards.
    /// </summary>
    [TestMethod]
    public void XiaojiSkillDoesNotTriggerWhenOwnerIsDead()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        source.CurrentHealth = 0;
        source.IsAlive = false;
        var initialHandCount = source.HandZone.Cards.Count;

        // Put equipment in equipment zone
        var equipment = CreateEquipmentCard(CardSubType.OffensiveHorse, 1);
        ((Zone)source.EquipmentZone).MutableCards.Add(equipment);

        // Setup skill manager and register Xiaoji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();
        skillRegistry.RegisterSkill("xiaoji", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Xiaoji skill to source player
        var xiaojiSkill = skillRegistry.GetSkill("xiaoji");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (xiaojiSkill is XiaojiSkill xiaoji)
        {
            xiaoji.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, source, xiaojiSkill);

        // Act - Remove equipment
        var unequipDescriptor = new CardMoveDescriptor(
            SourceZone: source.EquipmentZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { equipment },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(unequipDescriptor);

        // Assert
        Assert.IsFalse(source.EquipmentZone.Cards.Contains(equipment), "Equipment should be removed from equipment zone.");
        Assert.AreEqual(initialHandCount, source.HandZone.Cards.Count, "Source player should NOT have drawn any cards (skill not active).");
    }

    /// <summary>
    /// Tests that XiaojiSkill triggers multiple times when multiple equipment are removed.
    /// Input: Game with 2 players, source has Xiaoji skill, 2 equipment in equipment zone, both removed.
    /// Expected: Source player draws 4 cards (2 per equipment).
    /// </summary>
    [TestMethod]
    public void XiaojiSkillTriggersMultipleTimesForMultipleEquipment()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10); // Ensure draw pile has enough cards
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Put 2 equipment in equipment zone
        var equipment1 = CreateEquipmentCard(CardSubType.OffensiveHorse, 1);
        var equipment2 = CreateEquipmentCard(CardSubType.DefensiveHorse, 2);
        ((Zone)source.EquipmentZone).MutableCards.Add(equipment1);
        ((Zone)source.EquipmentZone).MutableCards.Add(equipment2);

        // Setup skill manager and register Xiaoji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new XiaojiSkillFactory();
        skillRegistry.RegisterSkill("xiaoji", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Xiaoji skill to source player
        var xiaojiSkill = skillRegistry.GetSkill("xiaoji");
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (xiaojiSkill is XiaojiSkill xiaoji)
        {
            xiaoji.SetCardMoveService(cardMoveService);
        }
        skillManager.AddEquipmentSkill(game, source, xiaojiSkill);

        // Act - Remove first equipment
        var unequipDescriptor1 = new CardMoveDescriptor(
            SourceZone: source.EquipmentZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { equipment1 },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(unequipDescriptor1);

        // Remove second equipment
        var unequipDescriptor2 = new CardMoveDescriptor(
            SourceZone: source.EquipmentZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { equipment2 },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );
        cardMoveService.MoveMany(unequipDescriptor2);

        // Assert
        Assert.IsFalse(source.EquipmentZone.Cards.Contains(equipment1), "First equipment should be removed.");
        Assert.IsFalse(source.EquipmentZone.Cards.Contains(equipment2), "Second equipment should be removed.");
        Assert.AreEqual(initialHandCount + 4, source.HandZone.Cards.Count, "Source player should have drawn 4 cards (2 per equipment).");
        Assert.AreEqual(initialDrawPileCount - 4, game.DrawPile.Cards.Count, "Draw pile should have 4 fewer cards.");
    }

    #endregion
}
