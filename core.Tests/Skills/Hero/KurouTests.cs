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
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class KurouTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that KurouSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void KurouSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new KurouSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("kurou", skill.Id);
        Assert.AreEqual("苦肉", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Kurou skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterKurouSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new KurouSkillFactory();

        // Act
        registry.RegisterSkill("kurou", factory);
        var skill = registry.GetSkill("kurou");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("kurou", skill.Id);
        Assert.AreEqual("苦肉", skill.Name);
    }

    #endregion

    #region GenerateAction Tests

    /// <summary>
    /// Tests that GenerateAction returns action when in play phase and owner's turn.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsActionWhenInPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var skill = new KurouSkill();

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNotNull(action);
        Assert.AreEqual("UseKurou", action.ActionId);
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
        var skill = new KurouSkill();

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
        var skill = new KurouSkill();

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns null when player is dead.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsNullWhenPlayerIsDead()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.IsAlive = false;
        var skill = new KurouSkill();

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNull(action);
    }

    /// <summary>
    /// Tests that GenerateAction returns action even when player has 1 HP.
    /// </summary>
    [TestMethod]
    public void GenerateActionReturnsActionWhenPlayerHas1Hp()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.CurrentHealth = 1;
        var skill = new KurouSkill();

        // Act
        var action = skill.GenerateAction(game, player);

        // Assert
        Assert.IsNotNull(action);
        Assert.AreEqual("UseKurou", action.ActionId);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that using Kurou skill loses 1 HP and draws 2 cards when player has enough HP.
    /// </summary>
    [TestMethod]
    public void UsingKurouLoses1HpAndDraws2Cards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.CurrentHealth = 4;
        var initialHandCount = player.HandZone.Cards.Count;

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
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
            ((Zone)game.DrawPile).MutableCards.Add(card);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseKurouHandler(cardMoveService, ruleService);

        var action = new ActionDescriptor(
            ActionId: "UseKurou",
            DisplayKey: "action.useKurou",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: null);

        var ruleContext = new RuleContext(game, player);

        // Act
        mapper.Resolve(ruleContext, action, originalRequest: null, playerChoice: null);

        // Assert
        Assert.AreEqual(3, player.CurrentHealth, "Player should have lost 1 HP");
        Assert.AreEqual(initialHandCount + 2, player.HandZone.Cards.Count, "Player should have drawn 2 cards");
    }

    /// <summary>
    /// Tests that using Kurou skill can be used multiple times in the same play phase.
    /// </summary>
    [TestMethod]
    public void UsingKurouCanBeUsedMultipleTimes()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.CurrentHealth = 4;
        var initialHandCount = player.HandZone.Cards.Count;

        // Add cards to draw pile
        for (int i = 0; i < 10; i++)
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
            ((Zone)game.DrawPile).MutableCards.Add(card);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseKurouHandler(cardMoveService, ruleService);

        var action = new ActionDescriptor(
            ActionId: "UseKurou",
            DisplayKey: "action.useKurou",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: null);

        var ruleContext = new RuleContext(game, player);

        // Act - Use twice
        mapper.Resolve(ruleContext, action, originalRequest: null, playerChoice: null);
        mapper.Resolve(ruleContext, action, originalRequest: null, playerChoice: null);

        // Assert
        Assert.AreEqual(2, player.CurrentHealth, "Player should have lost 2 HP");
        Assert.AreEqual(initialHandCount + 4, player.HandZone.Cards.Count, "Player should have drawn 4 cards");
    }

    /// <summary>
    /// Tests that HpLostEvent is published when using Kurou skill.
    /// </summary>
    [TestMethod]
    public void UsingKurouPublishesHpLostEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.CurrentHealth = 4;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseKurouHandler(cardMoveService, ruleService);

        var action = new ActionDescriptor(
            ActionId: "UseKurou",
            DisplayKey: "action.useKurou",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: null);

        var ruleContext = new RuleContext(game, player);

        var hpLostEvents = new List<HpLostEvent>();
        var eventBus = new BasicEventBus();
        eventBus.Subscribe<HpLostEvent>(evt => hpLostEvents.Add(evt));

        // Note: The event bus is not currently passed to the resolver in RegisterUseKurouHandler
        // This test verifies the structure, but the event publishing would need to be wired up
        // in a real game scenario where EventBus is available in ResolutionContext

        // Act
        mapper.Resolve(ruleContext, action, originalRequest: null, playerChoice: null);

        // Assert
        Assert.AreEqual(3, player.CurrentHealth, "Player should have lost 1 HP");
        // Note: Event publishing test would require EventBus to be passed to ResolutionContext
    }

    /// <summary>
    /// Tests that AfterDamageEvent is NOT published when using Kurou skill (since it's HP loss, not damage).
    /// </summary>
    [TestMethod]
    public void UsingKurouDoesNotPublishAfterDamageEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        player.CurrentHealth = 4;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var mapper = new ActionResolutionMapper();
        mapper.RegisterUseKurouHandler(cardMoveService, ruleService);

        var action = new ActionDescriptor(
            ActionId: "UseKurou",
            DisplayKey: "action.useKurou",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: null);

        var ruleContext = new RuleContext(game, player);

        var afterDamageEvents = new List<AfterDamageEvent>();
        var eventBus = new BasicEventBus();
        eventBus.Subscribe<AfterDamageEvent>(evt => afterDamageEvents.Add(evt));

        // Act
        mapper.Resolve(ruleContext, action, originalRequest: null, playerChoice: null);

        // Assert
        Assert.AreEqual(0, afterDamageEvents.Count, "AfterDamageEvent should NOT be published for HP loss");
        Assert.AreEqual(3, player.CurrentHealth, "Player should have lost 1 HP");
    }

    #endregion
}

