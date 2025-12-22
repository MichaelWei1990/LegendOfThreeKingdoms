using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Phases;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class DrawPhaseTests
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
                    Id = i + 1,
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

    #region DrawPhaseResolver Tests

    /// <summary>
    /// Tests that DrawPhaseResolver draws 2 cards by default.
    /// Input: Game with cards in draw pile, player, DrawPhaseResolver.
    /// Expected: Player's hand has 2 more cards, draw pile has 2 fewer cards.
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverDrawsTwoCardsByDefault()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var player = game.Players[0];
        var initialHandCount = player.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DrawPhaseResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHandCount + 2, player.HandZone.Cards.Count, "Player should have 2 more cards in hand.");
        Assert.AreEqual(initialDrawPileCount - 2, game.DrawPile.Cards.Count, "Draw pile should have 2 fewer cards.");
    }

    /// <summary>
    /// Tests that DrawPhaseResolver handles draw pile being empty gracefully.
    /// Input: Game with empty draw pile, player, DrawPhaseResolver.
    /// Expected: Resolver returns failure result with appropriate error code.
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverHandlesEmptyDrawPile()
    {
        // Arrange
        var game = CreateDefaultGame(playerCount: 1);
        var player = game.Players[0];

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DrawPhaseResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);
    }

    /// <summary>
    /// Tests that DrawPhaseResolver handles insufficient cards in draw pile.
    /// Input: Game with only 1 card in draw pile, player, DrawPhaseResolver (tries to draw 2).
    /// Expected: Resolver returns failure result.
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverHandlesInsufficientCards()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 1);
        var player = game.Players[0];

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DrawPhaseResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
    }

    #endregion

    #region Rule Modification Tests

    /// <summary>
    /// Tests that DrawPhaseResolver applies rule modifiers to modify draw count.
    /// Input: Game with cards, player with skill that modifies draw count (+1), DrawPhaseResolver.
    /// Expected: Player draws 3 cards (2 base + 1 from modifier).
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverAppliesRuleModifiers()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;

        // Create a test skill that modifies draw count
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("test_draw_modifier", new TestDrawCountModifyingSkillFactory(modification: 1));
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "test_draw_modifier" });

        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Create player with hero ID
        var player = new Player
        {
            Seat = source.Seat,
            CampId = source.CampId,
            FactionId = source.FactionId,
            HeroId = "hero_test",
            MaxHealth = source.MaxHealth,
            CurrentHealth = source.CurrentHealth,
            IsAlive = source.IsAlive,
            HandZone = source.HandZone,
            EquipmentZone = source.EquipmentZone,
            JudgementZone = source.JudgementZone
        };

        skillManager.LoadSkillsForPlayer(game, player);

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var ruleService = new RuleService(modifierProvider: modifierProvider);
        var cardMoveService = new BasicCardMoveService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DrawPhaseResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHandCount + 3, player.HandZone.Cards.Count, "Player should have 3 more cards (2 base + 1 modifier).");
    }

    /// <summary>
    /// Tests that DrawPhaseResolver handles multiple rule modifiers stacking.
    /// Input: Game with cards, player with two skills that modify draw count (+1 each), DrawPhaseResolver.
    /// Expected: Player draws 4 cards (2 base + 1 + 1 from modifiers).
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverAppliesMultipleRuleModifiers()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;

        // Create test skills that modify draw count
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("test_draw_modifier_1", new TestDrawCountModifyingSkillFactory(modification: 1));
        skillRegistry.RegisterSkill("test_draw_modifier_2", new TestDrawCountModifyingSkillFactory(modification: 1));
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "test_draw_modifier_1", "test_draw_modifier_2" });

        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Create player with hero ID
        var player = new Player
        {
            Seat = source.Seat,
            CampId = source.CampId,
            FactionId = source.FactionId,
            HeroId = "hero_test",
            MaxHealth = source.MaxHealth,
            CurrentHealth = source.CurrentHealth,
            IsAlive = source.IsAlive,
            HandZone = source.HandZone,
            EquipmentZone = source.EquipmentZone,
            JudgementZone = source.JudgementZone
        };

        skillManager.LoadSkillsForPlayer(game, player);

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var ruleService = new RuleService(modifierProvider: modifierProvider);
        var cardMoveService = new BasicCardMoveService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DrawPhaseResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHandCount + 4, player.HandZone.Cards.Count, "Player should have 4 more cards (2 base + 1 + 1 from modifiers).");
    }

    /// <summary>
    /// Tests that DrawPhaseResolver ensures draw count is non-negative.
    /// Input: Game with cards, player with skill that modifies draw count to negative, DrawPhaseResolver.
    /// Expected: Player draws 0 cards (negative values are clamped to 0).
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverClampsNegativeDrawCountToZero()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;

        // Create a test skill that modifies draw count to negative
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("test_draw_modifier", new TestDrawCountModifyingSkillFactory(modification: -10));
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "test_draw_modifier" });

        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Create player with hero ID
        var player = new Player
        {
            Seat = source.Seat,
            CampId = source.CampId,
            FactionId = source.FactionId,
            HeroId = "hero_test",
            MaxHealth = source.MaxHealth,
            CurrentHealth = source.CurrentHealth,
            IsAlive = source.IsAlive,
            HandZone = source.HandZone,
            EquipmentZone = source.EquipmentZone,
            JudgementZone = source.JudgementZone
        };

        skillManager.LoadSkillsForPlayer(game, player);

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var ruleService = new RuleService(modifierProvider: modifierProvider);
        var cardMoveService = new BasicCardMoveService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DrawPhaseResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count, "Player should have no additional cards (negative clamped to 0).");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that DrawPhaseService automatically draws cards when Draw Phase starts.
    /// Input: Game with cards, DrawPhaseService subscribed to events, PhaseStartEvent for Draw phase.
    /// Expected: Player's hand has 2 more cards after phase start event.
    /// </summary>
    [TestMethod]
    public void DrawPhaseServiceAutomaticallyDrawsCardsOnPhaseStart()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var player = game.Players[0];
        var initialHandCount = player.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount + 2, player.HandZone.Cards.Count, "Player should have 2 more cards after draw phase.");
    }

    /// <summary>
    /// Tests that DrawPhaseService only triggers for Draw phase.
    /// Input: Game with cards, DrawPhaseService, PhaseStartEvent for Play phase.
    /// Expected: Player's hand count remains unchanged.
    /// </summary>
    [TestMethod]
    public void DrawPhaseServiceOnlyTriggersForDrawPhase()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var player = game.Players[0];
        var initialHandCount = player.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count, "Player's hand count should remain unchanged for non-Draw phases.");
    }

    /// <summary>
    /// Tests that DrawPhaseService does not trigger for dead players.
    /// Input: Game with cards, dead player, DrawPhaseService, PhaseStartEvent for Draw phase.
    /// Expected: Player's hand count remains unchanged.
    /// </summary>
    [TestMethod]
    public void DrawPhaseServiceDoesNotTriggerForDeadPlayers()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var player = game.Players[0];
        player.IsAlive = false;
        var initialHandCount = player.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count, "Dead player should not draw cards.");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Test skill that modifies draw count by a fixed amount.
    /// </summary>
    private sealed class TestDrawCountModifyingSkill : RuleModifyingSkillBase
    {
        private readonly int _modification;

        public TestDrawCountModifyingSkill(int modification)
        {
            _modification = modification;
        }

        public override string Id => "test_draw_modifier";
        public override string Name => "Test Draw Modifier";
        public override SkillType Type => SkillType.Locked;
        public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

        public override int? ModifyDrawCount(int current, Game game, Player owner)
        {
            if (!IsActive(game, owner))
                return null;

            return current + _modification;
        }
    }

    /// <summary>
    /// Factory for TestDrawCountModifyingSkill.
    /// </summary>
    private sealed class TestDrawCountModifyingSkillFactory : ISkillFactory
    {
        private readonly int _modification;

        public TestDrawCountModifyingSkillFactory(int modification)
        {
            _modification = modification;
        }

        public ISkill CreateSkill()
        {
            return new TestDrawCountModifyingSkill(_modification);
        }
    }

    #endregion
}
