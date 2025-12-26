using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
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
public sealed class TieqiTests
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

    private static Card CreateDodgeCard(int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "dodge",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Heart,
            Rank = 2
        };
    }

    private static Card CreateRedCard(int id = 100)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "red_card",
            Name = "红桃",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
        };
    }

    private static Card CreateBlackCard(int id = 101)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "black_card",
            Name = "黑桃",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that TieqiSkillFactory creates correct skill instance.
    /// Input: TieqiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void TieqiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new TieqiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("tieqi", skill.Id);
        Assert.AreEqual("铁骑", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.IsTrue(skill is ISlashResponseModifier);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Tieqi skill.
    /// Input: Empty registry, TieqiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterTieqiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new TieqiSkillFactory();

        // Act
        registry.RegisterSkill("tieqi", factory);
        var skill = registry.GetSkill("tieqi");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("tieqi", skill.Id);
        Assert.AreEqual("铁骑", skill.Name);
    }

    #endregion

    #region ProcessSlashTargetConfirmed Tests

    /// <summary>
    /// Tests that TieqiSkill performs judgement and returns true when judgement is red.
    /// Input: Game with 2 players, source has Tieqi skill, red card on draw pile top.
    /// Expected: ProcessSlashTargetConfirmed returns true, judgement card moved to discard pile.
    /// </summary>
    [TestMethod]
    public void TieqiSkillReturnsTrueWhenJudgementIsRed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Put a red card on top of draw pile for judgement
        var redCard = CreateRedCard(100);
        ((Zone)game.DrawPile).MutableCards.Insert(0, redCard);

        var slash = CreateSlashCard(1);
        var skill = new TieqiSkill();
        var judgementService = new BasicJudgementService();
        var cardMoveService = new BasicCardMoveService();

        // Act
        var result = skill.ProcessSlashTargetConfirmed(
            game,
            source,
            slash,
            target,
            judgementService,
            cardMoveService,
            eventBus: null);

        // Assert
        Assert.IsTrue(result, "Tieqi should return true when judgement is red.");
        Assert.IsFalse(game.DrawPile.Cards.Contains(redCard), "Judgement card should be moved from draw pile.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(redCard), "Judgement card should be in discard pile after CompleteJudgement.");
    }

    /// <summary>
    /// Tests that TieqiSkill performs judgement and returns false when judgement is black.
    /// Input: Game with 2 players, source has Tieqi skill, black card on draw pile top.
    /// Expected: ProcessSlashTargetConfirmed returns false, judgement card moved to discard pile.
    /// </summary>
    [TestMethod]
    public void TieqiSkillReturnsFalseWhenJudgementIsBlack()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Put a black card on top of draw pile for judgement
        var blackCard = CreateBlackCard(101);
        ((Zone)game.DrawPile).MutableCards.Insert(0, blackCard);

        var slash = CreateSlashCard(1);
        var skill = new TieqiSkill();
        var judgementService = new BasicJudgementService();
        var cardMoveService = new BasicCardMoveService();

        // Act
        var result = skill.ProcessSlashTargetConfirmed(
            game,
            source,
            slash,
            target,
            judgementService,
            cardMoveService,
            eventBus: null);

        // Assert
        Assert.IsFalse(result, "Tieqi should return false when judgement is black (failed).");
        Assert.IsFalse(game.DrawPile.Cards.Contains(blackCard), "Judgement card should be moved from draw pile.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(blackCard), "Judgement card should be in discard pile after CompleteJudgement.");
    }

    /// <summary>
    /// Tests that TieqiSkill returns false when skill is not active (owner is dead).
    /// Input: Game with 2 players, source is dead, red card on draw pile top.
    /// Expected: ProcessSlashTargetConfirmed returns false (skill not active).
    /// </summary>
    [TestMethod]
    public void TieqiSkillReturnsFalseWhenSkillNotActive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Kill the source player (set health to 0 and IsAlive to false)
        source.CurrentHealth = 0;
        source.IsAlive = false;

        // Put a red card on top of draw pile for judgement
        var redCard = CreateRedCard(100);
        ((Zone)game.DrawPile).MutableCards.Insert(0, redCard);

        var slash = CreateSlashCard(1);
        var skill = new TieqiSkill();
        var judgementService = new BasicJudgementService();
        var cardMoveService = new BasicCardMoveService();

        // Act
        var result = skill.ProcessSlashTargetConfirmed(
            game,
            source,
            slash,
            target,
            judgementService,
            cardMoveService,
            eventBus: null);

        // Assert
        Assert.IsFalse(result, "Tieqi should return false when skill is not active (owner is dead).");
        // Judgement should not be performed, so card should still be in draw pile
        Assert.IsTrue(game.DrawPile.Cards.Contains(redCard), "Judgement should not be performed when skill is not active.");
    }

    #endregion

    #region Integration Tests with SlashResolver

    /// <summary>
    /// Tests that TieqiSkill prevents target from using Dodge when judgement is red.
    /// Input: Game with 2 players, source has Tieqi skill, red judgement card, target has Dodge.
    /// Expected: Target cannot use Dodge to respond to Slash.
    /// </summary>
    [TestMethod]
    public void TieqiSkillPreventsDodgeWhenJudgementIsRed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Put a red card on top of draw pile for judgement
        var redCard = CreateRedCard(100);
        ((Zone)game.DrawPile).MutableCards.Insert(0, redCard);

        var slash = CreateSlashCard(1);
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var dodge = CreateDodgeCard(2);
        ((Zone)target.HandZone).MutableCards.Add(dodge);

        // Setup skill manager and register Tieqi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new TieqiSkillFactory();
        skillRegistry.RegisterSkill("tieqi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Tieqi skill to source player
        var tieqiSkill = skillRegistry.GetSkill("tieqi");
        skillManager.AddEquipmentSkill(game, source, tieqiSkill);

        // Setup resolution context
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();
        var judgementService = new BasicJudgementService(eventBus);

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { slash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { slash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        // Create a shared IntermediateResults dictionary so we can check it after resolution
        var intermediateResults = new Dictionary<string, object>();
        
        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: (req) => new ChoiceResult(
                RequestId: req.RequestId,
                PlayerSeat: req.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: Array.Empty<int>(), // No response
                SelectedOptionId: null,
                Confirmed: null
            ),
            IntermediateResults: intermediateResults,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: judgementService
        );

        // Act - Resolve Slash
        var resolver = new SlashResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Slash resolution should succeed.");

        // Verify that judgement was performed
        Assert.IsFalse(game.DrawPile.Cards.Contains(redCard), "Judgement card should be moved from draw pile.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(redCard), "Judgement card should be in discard pile after CompleteJudgement.");

        // Verify that IntermediateResults contains the flag
        Assert.IsNotNull(intermediateResults, "IntermediateResults should be initialized.");
        var key = $"SlashCannotUseDodge_{slash.Id}_{target.Seat}";
        Assert.IsTrue(intermediateResults.ContainsKey(key), "IntermediateResults should contain the flag for cannot use Dodge.");
        Assert.IsTrue((bool)intermediateResults[key], "Flag should be true (target cannot use Dodge).");
    }

    /// <summary>
    /// Tests that TieqiSkill does NOT prevent target from using Dodge when judgement is black.
    /// Input: Game with 2 players, source has Tieqi skill, black judgement card, target has Dodge.
    /// Expected: Target can use Dodge to respond to Slash.
    /// </summary>
    [TestMethod]
    public void TieqiSkillDoesNotPreventDodgeWhenJudgementIsBlack()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Put a black card on top of draw pile for judgement
        var blackCard = CreateBlackCard(101);
        ((Zone)game.DrawPile).MutableCards.Insert(0, blackCard);

        var slash = CreateSlashCard(1);
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var dodge = CreateDodgeCard(2);
        ((Zone)target.HandZone).MutableCards.Add(dodge);

        // Setup skill manager and register Tieqi skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var factory = new TieqiSkillFactory();
        skillRegistry.RegisterSkill("tieqi", factory);
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Tieqi skill to source player
        var tieqiSkill = skillRegistry.GetSkill("tieqi");
        skillManager.AddEquipmentSkill(game, source, tieqiSkill);

        // Setup resolution context
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();
        var judgementService = new BasicJudgementService(eventBus);

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { slash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { slash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        // Create a shared IntermediateResults dictionary so we can check it after resolution
        var intermediateResults2 = new Dictionary<string, object>();
        
        var context2 = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: (req) => new ChoiceResult(
                RequestId: req.RequestId,
                PlayerSeat: req.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: Array.Empty<int>(), // No response
                SelectedOptionId: null,
                Confirmed: null
            ),
            IntermediateResults: intermediateResults2,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: judgementService
        );

        // Act - Resolve Slash
        var resolver2 = new SlashResolver();
        var result2 = resolver2.Resolve(context2);

        // Assert
        Assert.IsTrue(result2.Success, "Slash resolution should succeed.");

        // Verify that judgement was performed
        Assert.IsFalse(game.DrawPile.Cards.Contains(blackCard), "Judgement card should be moved from draw pile.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(blackCard), "Judgement card should be in discard pile after CompleteJudgement.");

        // Verify that IntermediateResults does NOT contain the flag (or contains false)
        Assert.IsNotNull(intermediateResults2, "IntermediateResults should be initialized.");
        var key2 = $"SlashCannotUseDodge_{slash.Id}_{target.Seat}";
        // When judgement fails, the flag should not be set, or should be false
        if (intermediateResults2.TryGetValue(key2, out var value))
        {
            Assert.IsFalse((bool)value, "Flag should be false when judgement is black (failed).");
        }
        // It's also acceptable if the flag is not set at all
    }

    #endregion
}
