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
public sealed class JizhiTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithCardsInDrawPile(int playerCount = 2, int cardCount = 10)
    {
        var game = CreateDefaultGame(playerCount);
        var drawPile = (Zone)game.DrawPile;
        for (int i = 1; i <= cardCount; i++)
        {
            drawPile.MutableCards.Add(new Card
            {
                Id = 1000 + i,
                DefinitionId = $"draw_card_{i}",
                Name = $"Draw Card {i}",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Spade,
                Rank = 5
            });
        }
        return game;
    }

    private static Card CreateImmediateTrickCard(CardSubType subType, int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = subType.ToString().ToLower(),
            Name = subType.ToString(),
            CardType = CardType.Trick,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateDelayedTrickCard(CardSubType subType, int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = subType.ToString().ToLower(),
            Name = subType.ToString(),
            CardType = CardType.Trick,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateSlashCard(int id = 10)
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

    #region Skill Factory Tests

    /// <summary>
    /// Tests that JizhiSkillFactory creates correct skill instance.
    /// Input: JizhiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void JizhiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new JizhiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jizhi", skill.Id);
        Assert.AreEqual("集智", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Jizhi skill.
    /// Input: Empty registry, JizhiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterJizhiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new JizhiSkillFactory();

        // Act
        registry.RegisterSkill("jizhi", factory);
        var skill = registry.GetSkill("jizhi");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jizhi", skill.Id);
        Assert.AreEqual("集智", skill.Name);
    }

    #endregion

    #region Trigger Tests

    /// <summary>
    /// Tests that JizhiSkill draws 1 card when owner uses an immediate trick card.
    /// Input: Game with 2 players, player 0 has Jizhi skill, player 0 uses WuzhongShengyou.
    /// Expected: Player 0 draws 1 card.
    /// </summary>
    [TestMethod]
    public void JizhiSkillDrawsOneCardWhenUsingImmediateTrick()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var source = game.Players[0];
        
        var trickCard = CreateImmediateTrickCard(CardSubType.WuzhongShengyou, 1);
        ((Zone)source.HandZone).MutableCards.Add(trickCard);
        
        // Capture initial counts AFTER adding the trick card
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to source player
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, source, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish CardUsedEvent
        var cardUsedEvent = new CardUsedEvent(
            game,
            source.Seat,
            trickCard.Id,
            trickCard.CardSubType
        );
        eventBus.Publish(cardUsedEvent);

        // Assert
        Assert.AreEqual(initialHandCount + 1, source.HandZone.Cards.Count, "Owner should have drawn 1 card.");
        Assert.AreEqual(initialDrawPileCount - 1, game.DrawPile.Cards.Count, "Draw pile should have 1 fewer card.");
    }

    /// <summary>
    /// Tests that JizhiSkill does NOT trigger when owner uses a delayed trick card.
    /// Input: Game with 2 players, player 0 has Jizhi skill, player 0 uses Lebusishu.
    /// Expected: Player 0 does NOT draw a card.
    /// </summary>
    [TestMethod]
    public void JizhiSkillDoesNotTriggerForDelayedTrick()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var source = game.Players[0];
        
        var trickCard = CreateDelayedTrickCard(CardSubType.Lebusishu, 1);
        ((Zone)source.HandZone).MutableCards.Add(trickCard);
        
        // Capture initial counts AFTER adding the trick card
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to source player
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, source, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish CardUsedEvent
        var cardUsedEvent = new CardUsedEvent(
            game,
            source.Seat,
            trickCard.Id,
            trickCard.CardSubType
        );
        eventBus.Publish(cardUsedEvent);

        // Assert
        Assert.AreEqual(initialHandCount, source.HandZone.Cards.Count, "Owner should NOT have drawn a card.");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should be unchanged.");
    }

    /// <summary>
    /// Tests that JizhiSkill does NOT trigger when owner uses a non-trick card.
    /// Input: Game with 2 players, player 0 has Jizhi skill, player 0 uses Slash.
    /// Expected: Player 0 does NOT draw a card.
    /// </summary>
    [TestMethod]
    public void JizhiSkillDoesNotTriggerForNonTrickCard()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var source = game.Players[0];
        
        var slashCard = CreateSlashCard(1);
        ((Zone)source.HandZone).MutableCards.Add(slashCard);
        
        // Capture initial counts AFTER adding the slash card
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to source player
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, source, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish CardUsedEvent
        var cardUsedEvent = new CardUsedEvent(
            game,
            source.Seat,
            slashCard.Id,
            slashCard.CardSubType
        );
        eventBus.Publish(cardUsedEvent);

        // Assert
        Assert.AreEqual(initialHandCount, source.HandZone.Cards.Count, "Owner should NOT have drawn a card.");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should be unchanged.");
    }

    /// <summary>
    /// Tests that JizhiSkill does NOT trigger when another player uses a trick card.
    /// Input: Game with 2 players, player 0 has Jizhi skill, player 1 uses WuzhongShengyou.
    /// Expected: Player 0 does NOT draw a card.
    /// </summary>
    [TestMethod]
    public void JizhiSkillDoesNotTriggerForOtherPlayer()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var jizhiOwner = game.Players[0];
        var otherPlayer = game.Players[1];
        var initialHandCount = jizhiOwner.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var trickCard = CreateImmediateTrickCard(CardSubType.WuzhongShengyou, 1);
        ((Zone)otherPlayer.HandZone).MutableCards.Add(trickCard);

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to player 0 (not player 1)
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, jizhiOwner, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish CardUsedEvent for player 1
        var cardUsedEvent = new CardUsedEvent(
            game,
            otherPlayer.Seat,
            trickCard.Id,
            trickCard.CardSubType
        );
        eventBus.Publish(cardUsedEvent);

        // Assert
        Assert.AreEqual(initialHandCount, jizhiOwner.HandZone.Cards.Count, "Jizhi owner should NOT have drawn a card.");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should be unchanged.");
    }

    /// <summary>
    /// Tests that JizhiSkill does NOT trigger when owner is dead.
    /// Input: Game with 2 players, player 0 has Jizhi skill but is dead, player 0 uses WuzhongShengyou.
    /// Expected: Player 0 does NOT draw a card.
    /// </summary>
    [TestMethod]
    public void JizhiSkillDoesNotTriggerWhenOwnerIsDead()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var source = game.Players[0];
        
        var trickCard = CreateImmediateTrickCard(CardSubType.WuzhongShengyou, 1);
        ((Zone)source.HandZone).MutableCards.Add(trickCard);
        
        // Mark source as dead AFTER adding the trick card
        source.CurrentHealth = 0;
        source.IsAlive = false;
        
        // Capture initial counts AFTER marking as dead
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to source player
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, source, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish CardUsedEvent
        var cardUsedEvent = new CardUsedEvent(
            game,
            source.Seat,
            trickCard.Id,
            trickCard.CardSubType
        );
        eventBus.Publish(cardUsedEvent);

        // Assert
        Assert.AreEqual(initialHandCount, source.HandZone.Cards.Count, "Owner should NOT have drawn a card (skill not active).");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should be unchanged.");
    }

    /// <summary>
    /// Tests that JizhiSkill triggers multiple times for multiple immediate trick cards.
    /// Input: Game with 2 players, player 0 has Jizhi skill, player 0 uses multiple immediate trick cards.
    /// Expected: Player 0 draws 1 card for each immediate trick card used.
    /// </summary>
    [TestMethod]
    public void JizhiSkillTriggersMultipleTimesForMultipleTrickCards()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to source player
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, source, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish multiple CardUsedEvents for different immediate trick cards
        var trickCard1 = CreateImmediateTrickCard(CardSubType.WuzhongShengyou, 1);
        var trickCard2 = CreateImmediateTrickCard(CardSubType.ShunshouQianyang, 2);
        var trickCard3 = CreateImmediateTrickCard(CardSubType.GuoheChaiqiao, 3);

        eventBus.Publish(new CardUsedEvent(game, source.Seat, trickCard1.Id, trickCard1.CardSubType));
        eventBus.Publish(new CardUsedEvent(game, source.Seat, trickCard2.Id, trickCard2.CardSubType));
        eventBus.Publish(new CardUsedEvent(game, source.Seat, trickCard3.Id, trickCard3.CardSubType));

        // Assert
        Assert.AreEqual(initialHandCount + 3, source.HandZone.Cards.Count, "Owner should have drawn 3 cards (one for each trick card).");
        Assert.AreEqual(initialDrawPileCount - 3, game.DrawPile.Cards.Count, "Draw pile should have 3 fewer cards.");
    }

    /// <summary>
    /// Tests that JizhiSkill works for all immediate trick card types.
    /// Input: Game with 2 players, player 0 has Jizhi skill, player 0 uses various immediate trick cards.
    /// Expected: Player 0 draws 1 card for each immediate trick card used.
    /// </summary>
    [TestMethod]
    public void JizhiSkillWorksForAllImmediateTrickTypes()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 20);
        var source = game.Players[0];
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill manager and register Jizhi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new JizhiSkillFactory();
        skillRegistry.RegisterSkill("jizhi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Jizhi skill to source player
        var jizhiSkill = skillRegistry.GetSkill("jizhi");
        skillManager.AddEquipmentSkill(game, source, jizhiSkill);

        // Set card move service for the skill
        var cardMoveService = new BasicCardMoveService(eventBus);
        if (jizhiSkill is JizhiSkill jizhi)
        {
            jizhi.SetCardMoveService(cardMoveService);
        }

        // Act - Publish CardUsedEvents for all immediate trick card types
        var immediateTrickTypes = new[]
        {
            CardSubType.WuzhongShengyou,
            CardSubType.TaoyuanJieyi,
            CardSubType.ShunshouQianyang,
            CardSubType.GuoheChaiqiao,
            CardSubType.WanjianQifa,
            CardSubType.NanmanRushin,
            CardSubType.Duel
        };

        int cardId = 1;
        foreach (var trickType in immediateTrickTypes)
        {
            var trickCard = CreateImmediateTrickCard(trickType, cardId++);
            eventBus.Publish(new CardUsedEvent(game, source.Seat, trickCard.Id, trickCard.CardSubType));
        }

        // Assert
        Assert.AreEqual(immediateTrickTypes.Length, source.HandZone.Cards.Count, $"Owner should have drawn {immediateTrickTypes.Length} cards (one for each trick card).");
        Assert.AreEqual(initialDrawPileCount - immediateTrickTypes.Length, game.DrawPile.Cards.Count, $"Draw pile should have {immediateTrickTypes.Length} fewer cards.");
    }

    #endregion
}
