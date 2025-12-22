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
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class YingziTests
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

    #region Skill Registry Tests

    /// <summary>
    /// Tests that YingziSkillFactory creates correct skill instance.
    /// Input: YingziSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void YingziSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new YingziSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("yingzi", skill.Id);
        Assert.AreEqual("英姿", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Yingzi skill.
    /// Input: Empty registry, YingziSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterYingziSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new YingziSkillFactory();

        // Act
        registry.RegisterSkill("yingzi", factory);
        var skill = registry.GetSkill("yingzi");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("yingzi", skill.Id);
        Assert.AreEqual("英姿", skill.Name);
    }

    /// <summary>
    /// Tests that SkillRegistry prevents duplicate skill registrations.
    /// Input: Registry with "yingzi" already registered, attempting to register again.
    /// Expected: ArgumentException is thrown when trying to register duplicate skill ID.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterYingziSkillWithDuplicateIdThrowsException()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory1 = new YingziSkillFactory();
        var factory2 = new YingziSkillFactory();

        // Act
        registry.RegisterSkill("yingzi", factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterSkill("yingzi", factory2));
    }

    #endregion

    #region Draw Count Modification Tests

    /// <summary>
    /// Tests that YingziSkill increases draw count by 1 when active.
    /// Input: 2-player game, active player with yingzi skill, current draw count = 2.
    /// Expected: ModifyDrawCount returns 3 (2 + 1).
    /// </summary>
    [TestMethod]
    public void YingziSkillModifyDrawCountIncreasesByOne()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new YingziSkill();

        // Act
        var result = skill.ModifyDrawCount(2, game, player);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Value);
    }

    /// <summary>
    /// Tests that YingziSkill does not modify draw count when the owner is not active.
    /// Input: 2-player game, player is dead (IsAlive = false), yingzi skill.
    /// Expected: ModifyDrawCount returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void YingziSkillModifyDrawCountWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = false; // Skill should not be active
        var skill = new YingziSkill();

        // Act
        var result = skill.ModifyDrawCount(2, game, player);

        // Assert
        Assert.IsNull(result);
    }

    /// <summary>
    /// Tests that YingziSkill increases draw count by 1 regardless of the current count value.
    /// Input: 2-player game, active player, current draw count values of 1, 2, and 5.
    /// Expected: ModifyDrawCount always returns current + 1 when skill is active.
    /// </summary>
    [TestMethod]
    public void YingziSkillModifyDrawCountAlwaysIncreasesByOneRegardlessOfCurrent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new YingziSkill();

        // Act & Assert
        var result1 = skill.ModifyDrawCount(1, game, player);
        Assert.IsNotNull(result1);
        Assert.AreEqual(2, result1.Value);

        var result2 = skill.ModifyDrawCount(2, game, player);
        Assert.IsNotNull(result2);
        Assert.AreEqual(3, result2.Value);

        var result5 = skill.ModifyDrawCount(5, game, player);
        Assert.IsNotNull(result5);
        Assert.AreEqual(6, result5.Value);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that DrawPhaseResolver draws 3 cards when player has Yingzi skill.
    /// Input: Game with cards, player with yingzi skill, DrawPhaseResolver.
    /// Expected: Player's hand has 3 more cards (2 base + 1 from yingzi), draw pile has 3 fewer cards.
    /// </summary>
    [TestMethod]
    public void DrawPhaseResolverWithYingziDrawsThreeCards()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("yingzi", new YingziSkillFactory());
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "yingzi" });

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
        Assert.AreEqual(initialHandCount + 3, player.HandZone.Cards.Count, "Player should have 3 more cards (2 base + 1 from yingzi).");
        Assert.AreEqual(initialDrawPileCount - 3, game.DrawPile.Cards.Count, "Draw pile should have 3 fewer cards.");
    }

    /// <summary>
    /// Tests that DrawPhaseService automatically draws 3 cards when player has Yingzi skill.
    /// Input: Game with cards, DrawPhaseService subscribed to events, player with yingzi skill, PhaseStartEvent for Draw phase.
    /// Expected: Player's hand has 3 more cards after phase start event.
    /// </summary>
    [TestMethod]
    public void DrawPhaseServiceWithYingziDrawsThreeCardsOnPhaseStart()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("yingzi", new YingziSkillFactory());
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "yingzi" });

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

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialHandCount + 3, player.HandZone.Cards.Count, "Player should have 3 more cards after draw phase with yingzi skill.");
    }

    /// <summary>
    /// Tests that Yingzi skill stacks with other draw count modifying skills.
    /// Input: Game with cards, player with yingzi skill and another +1 draw skill, DrawPhaseResolver.
    /// Expected: Player draws 4 cards (2 base + 1 from yingzi + 1 from other skill).
    /// </summary>
    [TestMethod]
    public void YingziSkillStacksWithOtherDrawCountModifyingSkills()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var source = game.Players[0];
        var initialHandCount = source.HandZone.Cards.Count;

        // Create a test skill that also modifies draw count (+1)
        var testSkill = new TestDrawCountModifyingSkill(modification: 1);

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("yingzi", new YingziSkillFactory());
        skillRegistry.RegisterSkill("test_draw_modifier", new TestDrawCountModifyingSkillFactory(modification: 1));
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "yingzi", "test_draw_modifier" });

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
        Assert.AreEqual(initialHandCount + 4, player.HandZone.Cards.Count, "Player should have 4 more cards (2 base + 1 from yingzi + 1 from other skill).");
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
