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
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Resolution;

[TestClass]
public sealed class ActionHandlerRegistrationTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreatePeachCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "peach_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
        };
    }

    private static Card CreateWeaponCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "weapon_basic",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateTrickCard(CardSubType trickType, int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"trick_{trickType.ToString().ToLower()}",
            CardType = CardType.Trick,
            CardSubType = trickType,
            Suit = Suit.Spade,
            Rank = 1
        };
    }

    #region Handler Registration Tests

    /// <summary>
    /// Verifies that RegisterUsePeachHandler correctly registers the handler.
    /// </summary>
    [TestMethod]
    public void RegisterUsePeachHandler_RegistersHandlerCorrectly()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        // Act
        mapper.RegisterUsePeachHandler(cardMoveService, ruleService);

        // Assert - verify handler is registered by attempting to resolve an action
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        // MaxHealth is init-only, use existing value and set CurrentHealth to be less than MaxHealth
        player.CurrentHealth = player.MaxHealth - 1;
        var peach = CreatePeachCard();
        ((Zone)player.HandZone).MutableCards.Add(peach);

        var context = new RuleContext(game, player);
        var action = new ActionDescriptor(
            ActionId: "UsePeach",
            DisplayKey: "action.usePeach",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.SelfOrFriends),
            CardCandidates: new[] { peach }
        );
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: player.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { peach.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Should not throw InvalidOperationException (handler not found)
        try
        {
            mapper.Resolve(context, action, null, choice);
            // If we get here, handler was registered successfully
            Assert.IsTrue(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No action resolution handler registered"))
        {
            Assert.Fail("Handler was not registered: " + ex.Message);
        }
    }

    /// <summary>
    /// Verifies that RegisterUseEquipHandler correctly registers the handler.
    /// </summary>
    [TestMethod]
    public void RegisterUseEquipHandler_RegistersHandlerCorrectly()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        // Act
        mapper.RegisterUseEquipHandler(cardMoveService, ruleService);

        // Assert - verify handler is registered
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        var weapon = CreateWeaponCard();
        ((Zone)player.HandZone).MutableCards.Add(weapon);

        var context = new RuleContext(game, player);
        var action = new ActionDescriptor(
            ActionId: "UseEquip",
            DisplayKey: "action.useEquip",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
            CardCandidates: new[] { weapon }
        );
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: player.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { weapon.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Should not throw InvalidOperationException (handler not found)
        try
        {
            mapper.Resolve(context, action, null, choice);
            Assert.IsTrue(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No action resolution handler registered"))
        {
            Assert.Fail("Handler was not registered: " + ex.Message);
        }
    }

    /// <summary>
    /// Verifies that RegisterUseTrickHandlers registers all trick card handlers.
    /// </summary>
    [TestMethod]
    public void RegisterUseTrickHandlers_RegistersAllTrickHandlers()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        // Act
        mapper.RegisterUseTrickHandlers(cardMoveService, ruleService);

        // Assert - verify all trick handlers are registered
        var trickActionIds = new[]
        {
            "UseWuzhongShengyou",
            "UseTaoyuanJieyi",
            "UseShunshouQianyang",
            "UseGuoheChaiqiao",
            "UseWanjianQifa",
            "UseNanmanRushin",
            "UseDuel",
            "UseHarvest",
            "UseJieDaoShaRen",
            "UseLebusishu",
            "UseShandian"
        };

        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        var context = new RuleContext(game, player);

        foreach (var actionId in trickActionIds)
        {
            var action = new ActionDescriptor(
                ActionId: actionId,
                DisplayKey: $"action.{actionId.ToLower()}",
                RequiresTargets: false,
                TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
                CardCandidates: null
            );

            // Should not throw InvalidOperationException (handler not found)
            try
            {
                // Create a minimal choice - some resolvers may fail due to missing cards, but that's OK
                // We're just checking that the handler is registered
                var choice = new ChoiceResult(
                    RequestId: Guid.NewGuid().ToString(),
                    PlayerSeat: player.Seat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );

                mapper.Resolve(context, action, null, choice);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No action resolution handler registered"))
            {
                Assert.Fail($"Handler for {actionId} was not registered: " + ex.Message);
            }
            catch (Exception)
            {
                // Other exceptions (like missing cards) are OK - we're just checking registration
            }
        }
    }

    /// <summary>
    /// Verifies that all card types in ActionIdMapper have registered handlers.
    /// </summary>
    [TestMethod]
    public void AllCardTypesHaveRegisteredHandlers()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        // Register all handlers
        mapper.RegisterUseSlashHandler(cardMoveService, ruleService, null, skillManager, eventBus);
        mapper.RegisterUsePeachHandler(cardMoveService, ruleService, null, skillManager, eventBus);
        mapper.RegisterUseEquipHandler(cardMoveService, ruleService, null, skillManager, eventBus);
        mapper.RegisterUseTrickHandlers(cardMoveService, ruleService, null, skillManager, eventBus);

        // Get all card subtypes that have action IDs
        var cardSubTypesWithActions = new[]
        {
            CardSubType.Slash,
            CardSubType.Peach,
            CardSubType.Weapon,
            CardSubType.Armor,
            CardSubType.OffensiveHorse,
            CardSubType.DefensiveHorse,
            CardSubType.WuzhongShengyou,
            CardSubType.TaoyuanJieyi,
            CardSubType.ShunshouQianyang,
            CardSubType.GuoheChaiqiao,
            CardSubType.WanjianQifa,
            CardSubType.NanmanRushin,
            CardSubType.Duel,
            CardSubType.Harvest,
            CardSubType.JieDaoShaRen,
            CardSubType.Lebusishu,
            CardSubType.Shandian
        };

        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        var context = new RuleContext(game, player);

        foreach (var cardSubType in cardSubTypesWithActions)
        {
            var actionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
            if (actionId is null)
            {
                Assert.Fail($"CardSubType {cardSubType} does not have an action ID mapping.");
                continue;
            }

            // Verify handler is registered
            var action = new ActionDescriptor(
                ActionId: actionId,
                DisplayKey: $"action.{actionId.ToLower()}",
                RequiresTargets: false,
                TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
                CardCandidates: null
            );

            try
            {
                var choice = new ChoiceResult(
                    RequestId: Guid.NewGuid().ToString(),
                    PlayerSeat: player.Seat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );

                mapper.Resolve(context, action, null, choice);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No action resolution handler registered"))
            {
                Assert.Fail($"Handler for {actionId} (CardSubType: {cardSubType}) was not registered: " + ex.Message);
            }
            catch (Exception)
            {
                // Other exceptions are OK - we're just checking registration
            }
        }
    }

    #endregion

    #region Handler Execution Tests

    /// <summary>
    /// Verifies that UsePeachHandler executes successfully.
    /// </summary>
    [TestMethod]
    public void UsePeachHandler_ExecutesSuccessfully()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        mapper.RegisterUsePeachHandler(cardMoveService, ruleService, null, skillManager, eventBus);

        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        // MaxHealth is init-only, use existing value and set CurrentHealth to be less than MaxHealth
        player.CurrentHealth = player.MaxHealth - 1;
        var peach = CreatePeachCard();
        ((Zone)player.HandZone).MutableCards.Add(peach);

        var context = new RuleContext(game, player);
        var action = new ActionDescriptor(
            ActionId: "UsePeach",
            DisplayKey: "action.usePeach",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.SelfOrFriends),
            CardCandidates: new[] { peach }
        );
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: player.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { peach.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Act
        mapper.Resolve(context, action, null, choice);

        // Assert
        Assert.AreEqual(player.MaxHealth, player.CurrentHealth, "Player health should be restored to max health");
        Assert.IsFalse(player.HandZone.Cards.Contains(peach), "Peach card should be removed from hand");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(peach), "Peach card should be in discard pile");
    }

    /// <summary>
    /// Verifies that UseEquipHandler executes successfully.
    /// </summary>
    [TestMethod]
    public void UseEquipHandler_ExecutesSuccessfully()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        mapper.RegisterUseEquipHandler(cardMoveService, ruleService, null, skillManager, eventBus);

        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        var weapon = CreateWeaponCard();
        ((Zone)player.HandZone).MutableCards.Add(weapon);

        var context = new RuleContext(game, player);
        var action = new ActionDescriptor(
            ActionId: "UseEquip",
            DisplayKey: "action.useEquip",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
            CardCandidates: new[] { weapon }
        );
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: player.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { weapon.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Act
        mapper.Resolve(context, action, null, choice);

        // Assert
        Assert.IsFalse(player.HandZone.Cards.Contains(weapon), "Weapon card should be removed from hand");
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(weapon), "Weapon card should be in equipment zone");
    }

    /// <summary>
    /// Verifies that trick card handlers execute successfully.
    /// </summary>
    [TestMethod]
    public void UseTrickHandlers_ExecuteSuccessfully()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        mapper.RegisterUseTrickHandlers(cardMoveService, ruleService, null, skillManager, eventBus);

        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];

        // Test with WuzhongShengyou (simple trick that draws cards)
        var trick = CreateTrickCard(CardSubType.WuzhongShengyou);
        ((Zone)player.HandZone).MutableCards.Add(trick);

        // Add some cards to draw pile
        for (int i = 0; i < 5; i++)
        {
            var drawCard = CreatePeachCard(100 + i);
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }

        var context = new RuleContext(game, player);
        var action = new ActionDescriptor(
            ActionId: "UseWuzhongShengyou",
            DisplayKey: "action.usewuzhongshengyou",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
            CardCandidates: new[] { trick }
        );
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: player.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { trick.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var initialHandCount = player.HandZone.Cards.Count;

        // Act
        mapper.Resolve(context, action, null, choice);

        // Assert
        Assert.IsFalse(player.HandZone.Cards.Contains(trick), "Trick card should be removed from hand");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(trick), "Trick card should be in discard pile");
        // WuzhongShengyou draws 2 cards, so hand count should increase by 1 (trick removed, 2 added)
        Assert.AreEqual(initialHandCount + 1, player.HandZone.Cards.Count, "Player should have drawn 2 cards");
    }

    /// <summary>
    /// Verifies that handler execution throws exception on failure.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void HandlerExecution_ThrowsExceptionOnFailure()
    {
        // Arrange
        var mapper = new ActionResolutionMapper();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        mapper.RegisterUsePeachHandler(cardMoveService, ruleService, null, skillManager, eventBus);

        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        // At full health, cannot use Peach
        player.CurrentHealth = player.MaxHealth;
        var peach = CreatePeachCard();
        ((Zone)player.HandZone).MutableCards.Add(peach);

        var context = new RuleContext(game, player);
        var action = new ActionDescriptor(
            ActionId: "UsePeach",
            DisplayKey: "action.usePeach",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.SelfOrFriends),
            CardCandidates: new[] { peach }
        );
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: player.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { peach.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        // Act - should throw InvalidOperationException because Peach cannot be used at full health
        mapper.Resolve(context, action, null, choice);
    }

    #endregion
}
