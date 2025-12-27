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
using ResolutionExtensions = LegendOfThreeKingdoms.Core.Resolution.ResolutionExtensions;

namespace core.Tests;

[TestClass]
public sealed class GuoseTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateDiamondCard(CardSubType subType = CardSubType.Slash, int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"diamond_card_{id}",
            Name = "Diamond Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Diamond, // Diamond suit
            Rank = 5
        };
    }

    private static Card CreateNonDiamondCard(CardSubType subType = CardSubType.Slash, int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"non_diamond_card_{id}",
            Name = "Non-Diamond Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade, // Non-diamond suit
            Rank = 5
        };
    }

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that GuoseSkillFactory creates correct skill instance.
    /// Input: GuoseSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Active.
    /// </summary>
    [TestMethod]
    public void GuoseSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new GuoseSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("guose", skill.Id);
        Assert.AreEqual("国色", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill is ICardConversionSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Guose skill.
    /// Input: Empty registry, GuoseSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterGuoseSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new GuoseSkillFactory();

        // Act
        registry.RegisterSkill("guose", factory);
        var skill = registry.GetSkill("guose");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("guose", skill.Id);
        Assert.AreEqual("国色", skill.Name);
    }

    #endregion

    #region Card Conversion Tests

    /// <summary>
    /// Tests that GuoseSkill.CreateVirtualCard creates a virtual Lebusishu card from a diamond card.
    /// Input: GuoseSkill, diamond card (Diamond Slash).
    /// Expected: Returns a virtual card with CardSubType.Lebusishu, same ID, Suit, and Rank.
    /// </summary>
    [TestMethod]
    public void GuoseSkillCreateVirtualCardFromDiamondCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var diamondCard = CreateDiamondCard(CardSubType.Slash, 1);
        var skill = new GuoseSkill();

        // Act
        var virtualCard = skill.CreateVirtualCard(diamondCard, game, player);

        // Assert
        Assert.IsNotNull(virtualCard);
        Assert.AreEqual(diamondCard.Id, virtualCard.Id);
        Assert.AreEqual(CardSubType.Lebusishu, virtualCard.CardSubType);
        Assert.AreEqual(CardType.Trick, virtualCard.CardType);
        Assert.AreEqual(diamondCard.Suit, virtualCard.Suit);
        Assert.AreEqual(diamondCard.Rank, virtualCard.Rank);
        Assert.AreEqual("乐不思蜀", virtualCard.Name);
    }

    /// <summary>
    /// Tests that GuoseSkill.CreateVirtualCard returns null for a non-diamond card.
    /// Input: GuoseSkill, non-diamond card (Spade Slash).
    /// Expected: Returns null.
    /// </summary>
    [TestMethod]
    public void GuoseSkillCreateVirtualCardFromNonDiamondCardReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var nonDiamondCard = CreateNonDiamondCard(CardSubType.Slash, 2);
        var skill = new GuoseSkill();

        // Act
        var virtualCard = skill.CreateVirtualCard(nonDiamondCard, game, player);

        // Assert
        Assert.IsNull(virtualCard);
    }

    #endregion

    #region ActionQueryService Tests

    /// <summary>
    /// Tests that ActionQueryService generates UseLebusishu action for diamond cards when player has Guose skill.
    /// Input: Game in Play phase, player has Guose skill and diamond card in hand.
    /// Expected: UseLebusishu action is available with diamond card as candidate.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceGeneratesUseLebusishuActionForDiamondCardsWithGuose()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var diamondCard = CreateDiamondCard(CardSubType.Slash, 1);
        ((Zone)player.HandZone).MutableCards.Add(diamondCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("guose", new GuoseSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("guose"));

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRules = new RangeRuleService(modifierProvider);
        var cardUsageRules = new CardUsageRuleService(new PhaseRuleService(), rangeRules, new LimitRuleService(), modifierProvider, skillManager);
        var actionQuery = new ActionQueryService(
            new PhaseRuleService(),
            cardUsageRules,
            skillManager);

        var ruleContext = new RuleContext(game, player);

        // Act
        var actions = actionQuery.GetAvailableActions(ruleContext);

        // Assert
        Assert.IsTrue(actions.Items.Count > 0);
        var lebusishuAction = actions.Items.FirstOrDefault(a => a.ActionId == "UseLebusishu");
        Assert.IsNotNull(lebusishuAction, "UseLebusishu action should be available");
        Assert.IsTrue(lebusishuAction.CardCandidates?.Any(c => c.Id == diamondCard.Id) == true, "Diamond card should be a candidate");
    }

    #endregion

    #region UseCardResolver Conversion Tests

    /// <summary>
    /// Tests that UseCardResolver converts diamond card to virtual Lebusishu when Guose skill is used.
    /// Input: Game in Play phase, player has Guose skill, uses diamond card as Lebusishu.
    /// Expected: Virtual Lebusishu card is created and used for resolution, placed in target's judgement zone.
    /// </summary>
    [TestMethod]
    public void UseCardResolverConvertsDiamondCardToLebusishu()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var source = game.Players[0];
        var target = game.Players[1];
        var diamondCard = CreateDiamondCard(CardSubType.Slash, 1);
        ((Zone)source.HandZone).MutableCards.Add(diamondCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("guose", new GuoseSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, source, skillRegistry.GetSkill("guose"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var action = new ActionDescriptor(
            ActionId: "UseLebusishu",
            DisplayKey: "action.useLebusishu",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(MinTargets: 1, MaxTargets: 1, FilterType: TargetFilterType.Any),
            CardCandidates: new[] { diamondCard }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { diamondCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        // Use CreateResolutionContextWithCardConversion to prepare context with conversion information
        var context = ResolutionExtensions.CreateResolutionContextWithCardConversion(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            skillManager: skillManager,
            getPlayerChoice: null,
            eventBus: eventBus,
            logCollector: null,
            equipmentSkillRegistry: null,
            judgementService: null);

        var initialTargetJudgementCount = target.JudgementZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        // Act
        var resolver = new UseCardResolver();
        var result = resolver.Resolve(context);

        // Execute the entire stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        Assert.AreEqual(initialTargetJudgementCount + 1, target.JudgementZone.Cards.Count, "Target should have one more card in judgement zone");
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, "Source should have one less card (original diamond card moved to judgement zone)");
        // For delayed tricks, the original card is moved to judgement zone, not discard pile
        Assert.IsTrue(target.JudgementZone.Cards.Any(c => c.Id == diamondCard.Id), "Original diamond card should be in target's judgement zone");
    }

    #endregion
}
