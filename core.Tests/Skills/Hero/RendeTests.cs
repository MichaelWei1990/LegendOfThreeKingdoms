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

namespace core.Tests.Skills.Hero;

[TestClass]
public sealed class RendeTests
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

    #region Skill Factory Tests

    /// <summary>
    /// Tests that RendeSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void RendeSkillFactory_CreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new RendeSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("rende", skill.Id);
        Assert.AreEqual("仁德", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
        Assert.IsTrue(skill is IPhaseLimitedActionProvidingSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Rende skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistry_RegisterRendeSkill_CanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new RendeSkillFactory();

        // Act
        registry.RegisterSkill("rende", factory);
        var skill = registry.GetSkill("rende");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("rende", skill.Id);
        Assert.AreEqual("仁德", skill.Name);
    }

    #endregion

    #region GenerateAction Tests

    /// <summary>
    /// Tests that GenerateAction returns action when in play phase and owner has hand cards and other alive players exist.
    /// </summary>
    [TestMethod]
    public void GenerateAction_ReturnsAction_WhenInPlayPhaseWithHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.IsAlive = true;
        game.Players[1].IsAlive = true;
        var skill = new RendeSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNotNull(action);
        Assert.AreEqual("UseRende", action.ActionId);
        Assert.IsFalse(action.RequiresTargets);
        Assert.IsNotNull(action.CardCandidates);
        Assert.IsTrue(action.CardCandidates.Count > 0);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when not in play phase.
    /// </summary>
    [TestMethod]
    public void GenerateAction_ReturnsNull_WhenNotInPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Draw;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new RendeSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when owner has no hand cards.
    /// </summary>
    [TestMethod]
    public void GenerateAction_ReturnsNull_WhenNoHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.IsAlive = true;
        game.Players[1].IsAlive = true;
        var skill = new RendeSkill();

        // No hand cards

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when no other alive players exist.
    /// </summary>
    [TestMethod]
    public void GenerateAction_ReturnsNull_WhenNoOtherAlivePlayers()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.IsAlive = true;
        game.Players[1].IsAlive = false; // Other player is dead
        var skill = new RendeSkill();

        // Add hand card
        var card = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(card);

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when skill already used this play phase.
    /// </summary>
    [TestMethod]
    public void GenerateAction_ReturnsNull_WhenAlreadyUsed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.IsAlive = true;
        game.Players[1].IsAlive = true;
        var skill = new RendeSkill();

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

    #endregion

    #region Skill Execution Tests

    /// <summary>
    /// Acceptance Test 1: Give 1 card to another player - no heal.
    /// A gives B 1 card: B hand +1; A hand -1; A does not heal.
    /// </summary>
    [TestMethod]
    public void RendeSkill_GiveOneCard_NoHeal()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, MaxHealth = 4, InitialHealth = 3 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        // Add hand cards to A
        var card1 = CreateTestCard(1);
        ((Zone)playerA.HandZone).MutableCards.Add(card1);

        var initialHandCountA = playerA.HandZone.Cards.Count;
        var initialHandCountB = playerB.HandZone.Cards.Count;
        var initialHealthA = playerA.CurrentHealth;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseRendeHandler(cardMoveService, ruleService, (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select card 1
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { card1.Id },
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Select player B as recipient
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { playerB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false
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
        });

        var action = new ActionDescriptor(
            ActionId: "UseRende",
            DisplayKey: "action.useRende",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: playerA.HandZone.Cards.ToList()
        );

        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: playerA.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { card1.Id },
            SelectedOptionId: null,
            Confirmed: false
        );

        // Act
        var ruleContext = new RuleContext(game, playerA);
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        Assert.AreEqual(initialHandCountA - 1, playerA.HandZone.Cards.Count, "Player A should lose 1 card");
        Assert.AreEqual(initialHandCountB + 1, playerB.HandZone.Cards.Count, "Player B should gain 1 card");
        Assert.AreEqual(initialHealthA, playerA.CurrentHealth, "Player A should not heal (only 1 card given)");
        Assert.IsTrue(playerB.HandZone.Cards.Contains(card1), "Card should be in player B's hand");
    }

    /// <summary>
    /// Acceptance Test 2: Give 2 cards to same player - heal 1 HP.
    /// A gives B 2 cards: A heals 1 HP (if injured).
    /// </summary>
    [TestMethod]
    public void RendeSkill_GiveTwoCards_SamePlayer_HealsOneHP()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, MaxHealth = 4, InitialHealth = 3 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        // Add hand cards to A
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)playerA.HandZone).MutableCards.Add(card1);
        ((Zone)playerA.HandZone).MutableCards.Add(card2);

        var initialHandCountA = playerA.HandZone.Cards.Count;
        var initialHandCountB = playerB.HandZone.Cards.Count;
        var initialHealthA = playerA.CurrentHealth;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        var targetSelectionCount = 0;
        mapper.RegisterUseRendeHandler(cardMoveService, ruleService, (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select cards 1 and 2
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { card1.Id, card2.Id },
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Select player B as recipient for each card
                targetSelectionCount++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { playerB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false
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
        });

        var action = new ActionDescriptor(
            ActionId: "UseRende",
            DisplayKey: "action.useRende",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: playerA.HandZone.Cards.ToList()
        );

        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: playerA.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: false
        );

        // Act
        var ruleContext = new RuleContext(game, playerA);
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        Assert.AreEqual(initialHandCountA - 2, playerA.HandZone.Cards.Count, "Player A should lose 2 cards");
        Assert.AreEqual(initialHandCountB + 2, playerB.HandZone.Cards.Count, "Player B should gain 2 cards");
        Assert.AreEqual(initialHealthA + 1, playerA.CurrentHealth, "Player A should heal 1 HP (2 cards given)");
        Assert.AreEqual(2, targetSelectionCount, "Should ask for target selection twice (once per card)");
    }

    /// <summary>
    /// Acceptance Test 3: Give 3 cards to two players - heal 1 HP.
    /// A gives B 1 card, C 2 cards: total 3 >= 2 → A heals 1 HP.
    /// </summary>
    [TestMethod]
    public void RendeSkill_GiveThreeCards_TwoPlayers_HealsOneHP()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, MaxHealth = 4, InitialHealth = 3 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 2, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(3, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        var playerC = game.Players[2];
        playerA.IsAlive = true;
        playerB.IsAlive = true;
        playerC.IsAlive = true;

        // Add hand cards to A
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        var card3 = CreateTestCard(3);
        ((Zone)playerA.HandZone).MutableCards.Add(card1);
        ((Zone)playerA.HandZone).MutableCards.Add(card2);
        ((Zone)playerA.HandZone).MutableCards.Add(card3);

        var initialHandCountA = playerA.HandZone.Cards.Count;
        var initialHandCountB = playerB.HandZone.Cards.Count;
        var initialHandCountC = playerC.HandZone.Cards.Count;
        var initialHealthA = playerA.CurrentHealth;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        var targetSelectionCount = 0;
        mapper.RegisterUseRendeHandler(cardMoveService, ruleService, (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select cards 1, 2, and 3
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { card1.Id, card2.Id, card3.Id },
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // First card goes to B, next two go to C
                targetSelectionCount++;
                var targetSeat = targetSelectionCount == 1 ? playerB.Seat : playerC.Seat;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetSeat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false
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
        });

        var action = new ActionDescriptor(
            ActionId: "UseRende",
            DisplayKey: "action.useRende",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: playerA.HandZone.Cards.ToList()
        );

        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: playerA.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { card1.Id, card2.Id, card3.Id },
            SelectedOptionId: null,
            Confirmed: false
        );

        // Act
        var ruleContext = new RuleContext(game, playerA);
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        Assert.AreEqual(initialHandCountA - 3, playerA.HandZone.Cards.Count, "Player A should lose 3 cards");
        Assert.AreEqual(initialHandCountB + 1, playerB.HandZone.Cards.Count, "Player B should gain 1 card");
        Assert.AreEqual(initialHandCountC + 2, playerC.HandZone.Cards.Count, "Player C should gain 2 cards");
        Assert.AreEqual(initialHealthA + 1, playerA.CurrentHealth, "Player A should heal 1 HP (3 cards given >= 2)");
        Assert.AreEqual(3, targetSelectionCount, "Should ask for target selection 3 times (once per card)");
    }

    /// <summary>
    /// Acceptance Test 4: Same phase repeat activation - should be rejected.
    /// A already used Rende once this play phase, trying again → rejected.
    /// </summary>
    [TestMethod]
    public void RendeSkill_RepeatActivation_SamePhase_Rejected()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        // Add hand cards to A
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)playerA.HandZone).MutableCards.Add(card1);
        ((Zone)playerA.HandZone).MutableCards.Add(card2);

        var skill = new RendeSkill();

        // Mark skill as used
        skill.MarkAsUsed(game, playerA);

        // Act
        var action = skill.GenerateAction(game, playerA);

        // Assert
        Assert.IsNull(action, "Should not generate action when already used this phase");
        Assert.IsTrue(skill.IsAlreadyUsed(game, playerA), "Skill should be marked as used");
    }

    /// <summary>
    /// Acceptance Test 5: Invalid target - cannot give to self.
    /// Attempting to give card to self → validation should fail.
    /// </summary>
    [TestMethod]
    public void RendeSkill_InvalidTarget_CannotGiveToSelf()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        // Add hand cards to A
        var card1 = CreateTestCard(1);
        ((Zone)playerA.HandZone).MutableCards.Add(card1);

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseRendeHandler(cardMoveService, ruleService, (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select card 1
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { card1.Id },
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Try to select self (invalid)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { playerA.Seat }, // Invalid: cannot give to self
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false
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
        });

        var action = new ActionDescriptor(
            ActionId: "UseRende",
            DisplayKey: "action.useRende",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: playerA.HandZone.Cards.ToList()
        );

        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: playerA.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { card1.Id },
            SelectedOptionId: null,
            Confirmed: false
        );

        // Act & Assert
        try
        {
            var ruleContext = new RuleContext(game, playerA);
            mapper.Resolve(ruleContext, action, null, choice);
            Assert.Fail("Should throw exception when trying to give card to self");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("invalidRecipient") || ex.Message.Contains("InvalidTarget"), 
                "Exception should indicate invalid recipient");
        }
    }

    /// <summary>
    /// Tests that Rende does not heal when owner is at full health.
    /// </summary>
    [TestMethod]
    public void RendeSkill_GiveTwoCards_FullHealth_NoHeal()
    {
        // Arrange
        var playerConfigs = new List<PlayerConfig>
        {
            new PlayerConfig { Seat = 0, MaxHealth = 4, InitialHealth = 4 },
            new PlayerConfig { Seat = 1, MaxHealth = 4, InitialHealth = 4 }
        };
        var game = CreateGameWithPlayerConfigs(2, playerConfigs);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        // Add hand cards to A
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)playerA.HandZone).MutableCards.Add(card1);
        ((Zone)playerA.HandZone).MutableCards.Add(card2);

        var initialHealthA = playerA.CurrentHealth;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        var targetSelectionCount = 0;
        mapper.RegisterUseRendeHandler(cardMoveService, ruleService, (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { card1.Id, card2.Id },
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                targetSelectionCount++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { playerB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false
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
        });

        var action = new ActionDescriptor(
            ActionId: "UseRende",
            DisplayKey: "action.useRende",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: playerA.HandZone.Cards.ToList()
        );

        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: playerA.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { card1.Id, card2.Id },
            SelectedOptionId: null,
            Confirmed: false
        );

        // Act
        var ruleContext = new RuleContext(game, playerA);
        mapper.Resolve(ruleContext, action, null, choice);

        // Assert
        Assert.AreEqual(initialHealthA, playerA.CurrentHealth, "Player A should not heal when at full health");
        Assert.AreEqual(2, targetSelectionCount, "Should ask for target selection twice");
    }

    #endregion
}

