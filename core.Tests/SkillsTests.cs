using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class SkillsTests
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
            for (int i = 1; i <= cardCount; i++)
            {
                var card = new Card
                {
                    Id = i,
                    DefinitionId = $"test_card_{i}",
                    CardType = CardType.Basic,
                    CardSubType = CardSubType.Slash,
                    Suit = Suit.Spade,
                    Rank = (i % 13) + 1
                };
                drawZone.MutableCards.Add(card);
            }
        }

        return game;
    }

    private static Card CreateTestCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 7
        };
    }

    #region SkillRegistry Tests

    [TestMethod]
    public void skillRegistryRegisterSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new ExtraSlashSkillFactory();

        // Act
        registry.RegisterSkill("test_slash", factory);
        var skill = registry.GetSkill("test_slash");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("extra_slash", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    [TestMethod]
    public void skillRegistryRegisterSkillWithDuplicateIdThrowsException()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory1 = new ExtraSlashSkillFactory();
        var factory2 = new ExtraDrawSkillFactory(null);

        // Act
        registry.RegisterSkill("test_skill", factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterSkill("test_skill", factory2));
    }

    [TestMethod]
    public void skillRegistryGetSkillWithInvalidIdReturnsNull()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var skill = registry.GetSkill("nonexistent");

        // Assert
        Assert.IsNull(skill);
    }

    [TestMethod]
    public void skillRegistryRegisterHeroSkillsCanRetrieveSkillsForHero()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("skill1", new ExtraSlashSkillFactory());
        registry.RegisterSkill("skill2", new ExtraDrawSkillFactory(null));

        // Act
        registry.RegisterHeroSkills("hero_zhangfei", new[] { "skill1", "skill2" });
        var skills = registry.GetSkillsForHero("hero_zhangfei").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "extra_slash"));
        Assert.IsTrue(skills.Any(s => s.Id == "extra_draw"));
    }

    [TestMethod]
    public void skillRegistryRegisterHeroSkillsAppendsToExistingSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("skill1", new ExtraSlashSkillFactory());
        registry.RegisterSkill("skill2", new ExtraDrawSkillFactory(null));

        // Act
        registry.RegisterHeroSkills("hero_test", new[] { "skill1" });
        registry.RegisterHeroSkills("hero_test", new[] { "skill2" });
        var skills = registry.GetSkillsForHero("hero_test").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count);
    }

    [TestMethod]
    public void skillRegistryGetSkillsForHeroWithInvalidHeroReturnsEmpty()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var skills = registry.GetSkillsForHero("nonexistent_hero");

        // Assert
        Assert.IsFalse(skills.Any());
    }

    [TestMethod]
    public void skillRegistryIsSkillRegisteredReturnsCorrectValue()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("test_skill", new ExtraSlashSkillFactory());

        // Act & Assert
        Assert.IsTrue(registry.IsSkillRegistered("test_skill"));
        Assert.IsFalse(registry.IsSkillRegistered("nonexistent"));
    }

    [TestMethod]
    public void skillRegistryHasHeroSkillsReturnsCorrectValue()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("skill1", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "skill1" });

        // Act & Assert
        Assert.IsTrue(registry.HasHeroSkills("hero_test"));
        Assert.IsFalse(registry.HasHeroSkills("nonexistent_hero"));
    }

    [TestMethod]
    public void skillRegistryClearRemovesAllRegistrations()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("skill1", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "skill1" });

        // Act
        registry.Clear();

        // Assert
        Assert.IsFalse(registry.IsSkillRegistered("skill1"));
        Assert.IsFalse(registry.HasHeroSkills("hero_test"));
    }

    #endregion

    #region SkillManager Tests

    [TestMethod]
    public void skillManagerLoadSkillsForPlayerLoadsSkillsFromHeroId()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var manager = new SkillManager(registry, eventBus);
        var game = CreateDefaultGame();
        var player = game.Players[0];
        
        // Manually set HeroId since it's init-only
        var playerWithHero = new Player
        {
            Seat = player.Seat,
            CampId = player.CampId,
            FactionId = player.FactionId,
            HeroId = "hero_test",
            MaxHealth = player.MaxHealth,
            CurrentHealth = player.CurrentHealth,
            IsAlive = player.IsAlive,
            HandZone = player.HandZone,
            EquipmentZone = player.EquipmentZone,
            JudgementZone = player.JudgementZone
        };

        // Act
        manager.LoadSkillsForPlayer(game, playerWithHero);

        // Assert
        var skills = manager.GetAllSkills(playerWithHero).ToList();
        Assert.AreEqual(1, skills.Count);
        Assert.AreEqual("extra_slash", skills[0].Id);
    }

    [TestMethod]
    public void skillManagerLoadSkillsForPlayerHandlesPlayerWithoutHeroId()
    {
        // Arrange
        var registry = new SkillRegistry();
        var eventBus = new BasicEventBus();
        var manager = new SkillManager(registry, eventBus);
        var game = CreateDefaultGame();
        var player = game.Players[0];

        // Act
        manager.LoadSkillsForPlayer(game, player);

        // Assert
        var skills = manager.GetAllSkills(player).ToList();
        Assert.AreEqual(0, skills.Count);
    }

    [TestMethod]
    public void skillManagerLoadSkillsForAllPlayersLoadsSkillsForAllPlayers()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var manager = new SkillManager(registry, eventBus);
        var game = CreateDefaultGame(3);

        // Create players with HeroId
        var playersWithHeroes = game.Players.Select(p => new Player
        {
            Seat = p.Seat,
            CampId = p.CampId,
            FactionId = p.FactionId,
            HeroId = "hero_test",
            MaxHealth = p.MaxHealth,
            CurrentHealth = p.CurrentHealth,
            IsAlive = p.IsAlive,
            HandZone = p.HandZone,
            EquipmentZone = p.EquipmentZone,
            JudgementZone = p.JudgementZone
        }).ToList();

        var gameWithHeroes = new Game
        {
            Players = playersWithHeroes,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber,
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            IsFinished = game.IsFinished,
            WinnerDescription = game.WinnerDescription
        };

        // Act
        manager.LoadSkillsForAllPlayers(gameWithHeroes);

        // Assert
        foreach (var player in playersWithHeroes)
        {
            var skills = manager.GetAllSkills(player).ToList();
            Assert.AreEqual(1, skills.Count);
        }
    }

    [TestMethod]
    public void skillManagerGetActiveSkillsReturnsOnlyActiveSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var manager = new SkillManager(registry, eventBus);
        var game = CreateDefaultGame();
        var player = new Player
        {
            Seat = 0,
            HeroId = "hero_test",
            MaxHealth = 4,
            CurrentHealth = 4,
            IsAlive = true,
            HandZone = new Zone("Hand_0", 0, isPublic: false),
            EquipmentZone = new Zone("Equip_0", 0, isPublic: true),
            JudgementZone = new Zone("Judge_0", 0, isPublic: true)
        };

        manager.LoadSkillsForPlayer(game, player);

        // Act
        var activeSkills = manager.GetActiveSkills(game, player).ToList();

        // Assert
        Assert.AreEqual(1, activeSkills.Count);
        Assert.IsTrue(activeSkills[0].IsActive(game, player));
    }

    [TestMethod]
    public void skillManagerRemoveSkillsForPlayerDetachesAllSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var manager = new SkillManager(registry, eventBus);
        var game = CreateDefaultGame();
        var player = new Player
        {
            Seat = 0,
            HeroId = "hero_test",
            MaxHealth = 4,
            CurrentHealth = 4,
            IsAlive = true,
            HandZone = new Zone("Hand_0", 0, isPublic: false),
            EquipmentZone = new Zone("Equip_0", 0, isPublic: true),
            JudgementZone = new Zone("Judge_0", 0, isPublic: true)
        };

        manager.LoadSkillsForPlayer(game, player);
        Assert.AreEqual(1, manager.GetAllSkills(player).Count());

        // Act
        manager.RemoveSkillsForPlayer(game, player);

        // Assert
        Assert.AreEqual(0, manager.GetAllSkills(player).Count());
    }

    [TestMethod]
    public void skillManagerClearAllSkillsRemovesAllPlayerSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var manager = new SkillManager(registry, eventBus);
        var game = CreateDefaultGame(3);

        var playersWithHeroes = game.Players.Select(p => new Player
        {
            Seat = p.Seat,
            HeroId = "hero_test",
            MaxHealth = p.MaxHealth,
            CurrentHealth = p.CurrentHealth,
            IsAlive = p.IsAlive,
            HandZone = p.HandZone,
            EquipmentZone = p.EquipmentZone,
            JudgementZone = p.JudgementZone
        }).ToList();

        var gameWithHeroes = new Game
        {
            Players = playersWithHeroes,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber,
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            IsFinished = game.IsFinished,
            WinnerDescription = game.WinnerDescription
        };

        manager.LoadSkillsForAllPlayers(gameWithHeroes);

        // Act
        manager.ClearAllSkills(gameWithHeroes);

        // Assert
        foreach (var player in playersWithHeroes)
        {
            Assert.AreEqual(0, manager.GetAllSkills(player).Count());
        }
    }

    #endregion

    #region BaseSkill Tests

    [TestMethod]
    public void baseSkillIsActiveReturnsTrueForAlivePlayer()
    {
        // Arrange
        var skill = new ExtraSlashSkill();
        var game = CreateDefaultGame();
        var player = game.Players[0];
        player.IsAlive = true;

        // Act
        var isActive = skill.IsActive(game, player);

        // Assert
        Assert.IsTrue(isActive);
    }

    [TestMethod]
    public void baseSkillIsActiveReturnsFalseForDeadPlayer()
    {
        // Arrange
        var skill = new ExtraSlashSkill();
        var game = CreateDefaultGame();
        var player = game.Players[0];
        player.IsAlive = false;

        // Act
        var isActive = skill.IsActive(game, player);

        // Assert
        Assert.IsFalse(isActive);
    }

    #endregion

    #region ExtraDrawSkill Tests

    [TestMethod]
    public void extraDrawSkillAttachSubscribesToPhaseStartEvent()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var skill = new ExtraDrawSkill(cardMoveService);
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 5);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();

        var initialHandCount = player.HandZone.Cards.Count;

        // Act
        skill.Attach(game, player, eventBus);
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount + 1, player.HandZone.Cards.Count);
    }

    [TestMethod]
    public void extraDrawSkillOnPhaseStartOnlyTriggersForOwnerDrawPhase()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var skill = new ExtraDrawSkill(cardMoveService);
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 5);
        var player0 = game.Players[0];
        var player1 = game.Players[1];
        var eventBus = new BasicEventBus();

        var initialHandCount0 = player0.HandZone.Cards.Count;
        var initialHandCount1 = player1.HandZone.Cards.Count;

        skill.Attach(game, player0, eventBus);

        // Act - trigger for player 1's draw phase (should not trigger)
        var phaseStartEvent1 = new PhaseStartEvent(game, player1.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent1);

        // Assert
        Assert.AreEqual(initialHandCount0, player0.HandZone.Cards.Count);
        Assert.AreEqual(initialHandCount1, player1.HandZone.Cards.Count);

        // Act - trigger for player 0's draw phase (should trigger)
        var phaseStartEvent0 = new PhaseStartEvent(game, player0.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent0);

        // Assert
        Assert.AreEqual(initialHandCount0 + 1, player0.HandZone.Cards.Count);
    }

    [TestMethod]
    public void extraDrawSkillOnPhaseStartOnlyTriggersForDrawPhase()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var skill = new ExtraDrawSkill(cardMoveService);
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 5);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();

        var initialHandCount = player.HandZone.Cards.Count;

        skill.Attach(game, player, eventBus);

        // Act - trigger for Play phase (should not trigger)
        var playPhaseEvent = new PhaseStartEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(playPhaseEvent);

        // Assert
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count);

        // Act - trigger for Draw phase (should trigger)
        var drawPhaseEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(drawPhaseEvent);

        // Assert
        Assert.AreEqual(initialHandCount + 1, player.HandZone.Cards.Count);
    }

    [TestMethod]
    public void extraDrawSkillDetachUnsubscribesFromEvents()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var skill = new ExtraDrawSkill(cardMoveService);
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 5);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();

        var initialHandCount = player.HandZone.Cards.Count;

        skill.Attach(game, player, eventBus);
        skill.Detach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count);
    }

    [TestMethod]
    public void extraDrawSkillWithoutCardMoveServiceDoesNotDraw()
    {
        // Arrange
        var skill = new ExtraDrawSkill(cardMoveService: null);
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 5);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();

        var initialHandCount = player.HandZone.Cards.Count;

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert - should not draw because cardMoveService is null
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count);
    }

    [TestMethod]
    public void extraDrawSkillSetCardMoveServiceAllowsInjectionAfterCreation()
    {
        // Arrange
        var skill = new ExtraDrawSkill(cardMoveService: null);
        var cardMoveService = new BasicCardMoveService();
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 5);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();

        var initialHandCount = player.HandZone.Cards.Count;

        skill.SetCardMoveService(cardMoveService);
        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount + 1, player.HandZone.Cards.Count);
    }

    #endregion

    #region ExtraSlashSkill Tests

    [TestMethod]
    public void extraSlashSkillHasCorrectProperties()
    {
        // Arrange & Act
        var skill = new ExtraSlashSkill();

        // Assert
        Assert.AreEqual("extra_slash", skill.Id);
        Assert.AreEqual("Extra Slash", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.AreEqual(SkillCapability.ModifiesRules, skill.Capabilities);
    }

    #endregion

    #region SkillFactory Tests

    [TestMethod]
    public void extraDrawSkillFactoryCreateSkillReturnsNewInstance()
    {
        // Arrange
        var factory = new ExtraDrawSkillFactory();

        // Act
        var skill1 = factory.CreateSkill();
        var skill2 = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill1);
        Assert.IsNotNull(skill2);
        Assert.AreNotSame(skill1, skill2); // Should be different instances
        Assert.AreEqual("extra_draw", skill1.Id);
        Assert.AreEqual("extra_draw", skill2.Id);
    }

    [TestMethod]
    public void extraSlashSkillFactoryCreateSkillReturnsNewInstance()
    {
        // Arrange
        var factory = new ExtraSlashSkillFactory();

        // Act
        var skill1 = factory.CreateSkill();
        var skill2 = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill1);
        Assert.IsNotNull(skill2);
        Assert.AreNotSame(skill1, skill2); // Should be different instances
        Assert.AreEqual("extra_slash", skill1.Id);
        Assert.AreEqual("extra_slash", skill2.Id);
    }

    #endregion
}
