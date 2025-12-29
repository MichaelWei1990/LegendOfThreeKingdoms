using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Configuration;
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
public sealed class ZhiHengTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithPlayerConfigs(int playerCount, List<PlayerConfig> playerConfigs)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
        var config = new GameConfig
        {
            PlayerConfigs = playerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions
        };
        return Game.FromConfig(config);
    }

    private static Card CreateTestCard(int id, Suit suit = Suit.Spade, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = suit,
            Rank = 5
        };
    }

    private static Card CreateEquipmentCard(int id, CardSubType subType, string name = "Equipment")
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"equipment_{id}",
            Name = name,
            CardType = CardType.Equip,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that ZhiHengSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void ZhiHengSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new ZhiHengSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("zhiheng", skill.Id);
        Assert.AreEqual("制衡", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve ZhiHeng skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterZhiHengSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new ZhiHengSkillFactory();

        // Act
        registry.RegisterSkill("zhiheng", factory);
        var skill = registry.GetSkill("zhiheng");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("zhiheng", skill.Id);
        Assert.AreEqual("制衡", skill.Name);
    }

    #endregion

    #region GenerateAction Tests

    /// <summary>
    /// Tests that GenerateAction returns action when in play phase and owner has discardable cards.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsActionWhenInPlayPhaseWithDiscardableCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNotNull(action);
        Assert.AreEqual("UseZhiHeng", action.ActionId);
        Assert.IsFalse(action.RequiresTargets);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when not in play phase.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsNullWhenNotInPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Draw;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when not owner's turn.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsNullWhenNotOwnersTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 1; // Not player 0's turn
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when player has no discardable cards.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsNullWhenNoDiscardableCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Clear all cards (no hand cards, no equipment)
        ((Zone)player.HandZone).MutableCards.Clear();
        ((Zone)player.EquipmentZone).MutableCards.Clear();

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns action when player has equipment cards only.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsActionWhenHasEquipmentOnly()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Clear hand cards, add equipment
        ((Zone)player.HandZone).MutableCards.Clear();
        var equipment = CreateEquipmentCard(1, CardSubType.Weapon, "Weapon");
        ((Zone)player.EquipmentZone).MutableCards.Add(equipment);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNotNull(action);
        Assert.AreEqual("UseZhiHeng", action.ActionId);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when skill is already used this play phase.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsNullWhenAlreadyUsed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Mark skill as used
        skill.MarkAsUsed(game, player);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that IsAlreadyUsed returns false initially and true after MarkAsUsed.
    /// </summary>
    [TestMethod]
    public void IsAlreadyUsedReturnsFalseInitiallyAndTrueAfterMarkAsUsed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var skill = new ZhiHengSkill();

        // Act & Assert
        Assert.IsFalse(skill.IsAlreadyUsed(game, player), "Skill should not be used initially");

        skill.MarkAsUsed(game, player);

        Assert.IsTrue(skill.IsAlreadyUsed(game, player), "Skill should be marked as used");
    }

    #endregion

    #region Skill Execution Tests

    /// <summary>
    /// Tests that ZhiHeng successfully discards 2 hand cards and draws 2 cards.
    /// </summary>
    [TestMethod]
    public void ZhiHengSuccessfullyDiscards2HandCardsAndDraws2Cards()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunquan", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];

        // Add hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);
        var initialHandCount = owner.HandZone.Cards.Count;

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
        {
            var drawCard = CreateTestCard(1000 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseZhiHengHandler(cardMoveService, ruleService, (request) =>
        {
            // Owner selects 2 cards to discard
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new List<int> { card1.Id, card2.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseZhiHeng");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        // Cards should be discarded
        Assert.IsFalse(owner.HandZone.Cards.Any(c => c.Id == card1.Id || c.Id == card2.Id));
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == card1.Id || c.Id == card2.Id));

        // Player should have drawn 2 cards
        Assert.AreEqual(initialHandCount, owner.HandZone.Cards.Count, "Player should have same hand count (discarded 2, drew 2)");

        // Skill should be marked as used
        var zhiHengSkill = skillManager.GetActiveSkills(game, owner).First(s => s.Id == "zhiheng") as ZhiHengSkill;
        Assert.IsNotNull(zhiHengSkill);
        Assert.IsTrue(zhiHengSkill.IsAlreadyUsed(game, owner));
    }

    /// <summary>
    /// Tests that ZhiHeng can discard equipment cards.
    /// </summary>
    [TestMethod]
    public void ZhiHengCanDiscardEquipmentCards()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunquan", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];

        // Add equipment
        var equipment = CreateEquipmentCard(1, CardSubType.Weapon, "Weapon");
        ((Zone)owner.EquipmentZone).MutableCards.Add(equipment);

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
        {
            var drawCard = CreateTestCard(1000 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseZhiHengHandler(cardMoveService, ruleService, (request) =>
        {
            // Owner selects equipment to discard
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new List<int> { equipment.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseZhiHeng");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        // Equipment should be discarded
        Assert.IsFalse(owner.EquipmentZone.Cards.Any(c => c.Id == equipment.Id));
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == equipment.Id));

        // Player should have drawn 1 card
        Assert.AreEqual(1, owner.HandZone.Cards.Count, "Player should have drawn 1 card");
    }

    /// <summary>
    /// Tests that ZhiHeng can discard both hand cards and equipment.
    /// </summary>
    [TestMethod]
    public void ZhiHengCanDiscardBothHandCardsAndEquipment()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunquan", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];

        // Add hand card and equipment
        var handCard = CreateTestCard(1);
        var equipment = CreateEquipmentCard(2, CardSubType.Weapon, "Weapon");
        ((Zone)owner.HandZone).MutableCards.Add(handCard);
        ((Zone)owner.EquipmentZone).MutableCards.Add(equipment);
        var initialHandCount = owner.HandZone.Cards.Count;

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
        {
            var drawCard = CreateTestCard(1000 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseZhiHengHandler(cardMoveService, ruleService, (request) =>
        {
            // Owner selects both hand card and equipment to discard
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new List<int> { handCard.Id, equipment.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseZhiHeng");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        // Both cards should be discarded
        Assert.IsFalse(owner.HandZone.Cards.Any(c => c.Id == handCard.Id));
        Assert.IsFalse(owner.EquipmentZone.Cards.Any(c => c.Id == equipment.Id));
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == handCard.Id || c.Id == equipment.Id));

        // Player should have drawn 2 cards
        Assert.AreEqual(initialHandCount + 1, owner.HandZone.Cards.Count, "Player should have drawn 2 cards (discarded 1 hand + 1 equipment)");
    }

    /// <summary>
    /// Tests that ZhiHeng can only be used once per play phase.
    /// </summary>
    [TestMethod]
    public void ZhiHengCanOnlyBeUsedOncePerPlayPhase()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunquan", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];

        // Add multiple hand cards
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        var card3 = CreateTestCard(3);
        ((Zone)owner.HandZone).MutableCards.Add(card1);
        ((Zone)owner.HandZone).MutableCards.Add(card2);
        ((Zone)owner.HandZone).MutableCards.Add(card3);

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
        {
            var drawCard = CreateTestCard(1000 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseZhiHengHandler(cardMoveService, ruleService, (request) =>
        {
            // Owner selects 1 card to discard
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new List<int> { card1.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseZhiHeng");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act - Use skill once
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert - Skill should not be available again
        var actionResult2 = ruleService.GetAvailableActions(ruleContext);
        var zhiHengAction2 = actionResult2.Items.FirstOrDefault(a => a.ActionId == "UseZhiHeng");
        Assert.IsNull(zhiHengAction2, "ZhiHeng should not be available after being used once");
    }

    /// <summary>
    /// Tests that ZhiHeng draws cards equal to the number of cards discarded.
    /// </summary>
    [TestMethod]
    public void ZhiHengDrawsCardsEqualToDiscardedCount()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunquan", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];

        // Clear initial hand and add 5 cards
        ((Zone)owner.HandZone).MutableCards.Clear();
        var cards = new List<Card>();
        for (int i = 1; i <= 5; i++)
        {
            var card = CreateTestCard(i);
            cards.Add(card);
            ((Zone)owner.HandZone).MutableCards.Add(card);
        }

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
        {
            var drawCard = CreateTestCard(1000 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseZhiHengHandler(cardMoveService, ruleService, (request) =>
        {
            // Owner selects 3 cards to discard
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new List<int> { cards[0].Id, cards[1].Id, cards[2].Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseZhiHeng");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        var initialHandCount = owner.HandZone.Cards.Count; // Should be 5

        // Act
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        // Should have discarded 3 and drawn 3, so hand count should be 5 - 3 + 3 = 5
        Assert.AreEqual(5, owner.HandZone.Cards.Count, "Player should have same hand count (discarded 3, drew 3)");
        Assert.AreEqual(3, game.DiscardPile.Cards.Count(c => cards.Take(3).Any(dc => dc.Id == c.Id)), "3 cards should be in discard pile");
    }

    /// <summary>
    /// Tests that ZhiHeng can discard all available cards (boundary case).
    /// </summary>
    [TestMethod]
    public void ZhiHengCanDiscardAllAvailableCards()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, HeroId = "sunquan", MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var owner = game.Players[0];

        // Clear initial hand and add 3 cards
        ((Zone)owner.HandZone).MutableCards.Clear();
        var cards = new List<Card>();
        for (int i = 1; i <= 3; i++)
        {
            var card = CreateTestCard(i);
            cards.Add(card);
            ((Zone)owner.HandZone).MutableCards.Add(card);
        }

        // Add equipment
        var equipment = CreateEquipmentCard(10, CardSubType.Weapon, "Weapon");
        ((Zone)owner.EquipmentZone).MutableCards.Add(equipment);

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
        {
            var drawCard = CreateTestCard(1000 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var skillRegistry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(skillRegistry);
        var skillManager = new SkillManager(skillRegistry, new BasicEventBus());
        skillManager.LoadSkillsForPlayer(game, owner);
        var ruleService = new RuleService(skillManager: skillManager);
        var cardMoveService = new BasicCardMoveService(new BasicEventBus());
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseZhiHengHandler(cardMoveService, ruleService, (request) =>
        {
            // Owner selects all cards to discard (3 hand + 1 equipment = 4 total)
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                var allCardIds = cards.Select(c => c.Id).Concat(new[] { equipment.Id }).ToList();
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: allCardIds,
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true);
        });

        // Get action
        var ruleContext = new RuleContext(game, owner);
        var actionResult = ruleService.GetAvailableActions(ruleContext);
        var action = actionResult.Items.First(a => a.ActionId == "UseZhiHeng");

        // Create choice
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true);

        // Act
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        // All cards should be discarded
        Assert.AreEqual(0, owner.HandZone.Cards.Count, "All hand cards should be discarded");
        Assert.AreEqual(0, owner.EquipmentZone.Cards.Count, "All equipment should be discarded");

        // Player should have drawn 4 cards
        Assert.AreEqual(4, owner.HandZone.Cards.Count, "Player should have drawn 4 cards (discarded 4 total)");
    }

    #endregion
}

