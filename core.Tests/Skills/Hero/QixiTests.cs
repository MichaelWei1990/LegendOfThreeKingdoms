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
public sealed class QixiTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateBlackCard(CardSubType subType = CardSubType.Slash, int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"black_card_{id}",
            Name = "Black Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade, // Black suit
            Rank = 5
        };
    }

    private static Card CreateRedCard(CardSubType subType = CardSubType.Slash, int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"red_card_{id}",
            Name = "Red Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Heart, // Red suit
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
    /// Tests that QixiSkillFactory creates correct skill instance.
    /// Input: QixiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Active.
    /// </summary>
    [TestMethod]
    public void QixiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new QixiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qixi", skill.Id);
        Assert.AreEqual("奇袭", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill is ICardConversionSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Qixi skill.
    /// Input: Empty registry, QixiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterQixiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new QixiSkillFactory();

        // Act
        registry.RegisterSkill("qixi", factory);
        var skill = registry.GetSkill("qixi");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qixi", skill.Id);
        Assert.AreEqual("奇袭", skill.Name);
    }

    #endregion

    #region Card Conversion Tests

    /// <summary>
    /// Tests that QixiSkill.CreateVirtualCard creates a virtual GuoheChaiqiao card from a black card.
    /// Input: QixiSkill, black card (Spade Slash).
    /// Expected: Returns a virtual card with CardSubType.GuoheChaiqiao, same ID, Suit, and Rank.
    /// </summary>
    [TestMethod]
    public void QixiSkillCreateVirtualCardFromBlackCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1);
        var skill = new QixiSkill();

        // Act
        var virtualCard = skill.CreateVirtualCard(blackCard, game, player);

        // Assert
        Assert.IsNotNull(virtualCard);
        Assert.AreEqual(blackCard.Id, virtualCard.Id);
        Assert.AreEqual(CardSubType.GuoheChaiqiao, virtualCard.CardSubType);
        Assert.AreEqual(CardType.Trick, virtualCard.CardType);
        Assert.AreEqual(blackCard.Suit, virtualCard.Suit);
        Assert.AreEqual(blackCard.Rank, virtualCard.Rank);
        Assert.AreEqual("过河拆桥", virtualCard.Name);
    }

    /// <summary>
    /// Tests that QixiSkill.CreateVirtualCard returns null for a red card.
    /// Input: QixiSkill, red card (Heart Slash).
    /// Expected: Returns null.
    /// </summary>
    [TestMethod]
    public void QixiSkillCreateVirtualCardFromRedCardReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var redCard = CreateRedCard(CardSubType.Slash, 2);
        var skill = new QixiSkill();

        // Act
        var virtualCard = skill.CreateVirtualCard(redCard, game, player);

        // Assert
        Assert.IsNull(virtualCard);
    }

    #endregion

    #region ActionQueryService Tests

    /// <summary>
    /// Tests that ActionQueryService generates UseGuoheChaiqiao action for black cards when player has Qixi skill.
    /// Input: Game in Play phase, player has Qixi skill and black card in hand.
    /// Expected: UseGuoheChaiqiao action is available with black card as candidate.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceGeneratesUseGuoheChaiqiaoActionForBlackCardsWithQixi()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1);
        ((Zone)player.HandZone).MutableCards.Add(blackCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qixi", new QixiSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("qixi"));

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
        var guoheChaiqiaoAction = actions.Items.FirstOrDefault(a => a.ActionId == "UseGuoheChaiqiao");
        Assert.IsNotNull(guoheChaiqiaoAction, "UseGuoheChaiqiao action should be available");
        Assert.IsTrue(guoheChaiqiaoAction.CardCandidates?.Any(c => c.Id == blackCard.Id) == true, "Black card should be a candidate");
    }

    #endregion

    #region UseCardResolver Conversion Tests

    /// <summary>
    /// Tests that UseCardResolver converts black card to virtual GuoheChaiqiao when Qixi skill is used.
    /// Input: Game in Play phase, player has Qixi skill, uses black card as GuoheChaiqiao.
    /// Expected: Virtual GuoheChaiqiao card is created and used for resolution.
    /// </summary>
    [TestMethod]
    public void UseCardResolverConvertsBlackCardToGuoheChaiqiao()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var source = game.Players[0];
        var target = game.Players[1];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1);
        ((Zone)source.HandZone).MutableCards.Add(blackCard);

        var targetCard = CreateTestCard(100);
        ((Zone)target.HandZone).MutableCards.Add(targetCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qixi", new QixiSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, source, skillRegistry.GetSkill("qixi"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var action = new ActionDescriptor(
            ActionId: "UseGuoheChaiqiao",
            DisplayKey: "action.useGuoheChaiqiao",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(MinTargets: 1, MaxTargets: 1, FilterType: TargetFilterType.Any),
            CardCandidates: new[] { blackCard }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { blackCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { targetCard.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            null,
            null,
            getPlayerChoice,
            null,
            eventBus,
            null,
            skillManager,
            null,
            null
        );

        var initialTargetHandCount = target.HandZone.Cards.Count;
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
        Assert.AreEqual(initialTargetHandCount - 1, target.HandZone.Cards.Count, "Target should have one less card");
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, "Source should have one less card (original black card moved to discard)");
        Assert.AreEqual(initialDiscardPileCount + 2, game.DiscardPile.Cards.Count, "Discard pile should have 2 more cards (target card + original black card)");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == blackCard.Id), "Original black card should be in discard pile");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == targetCard.Id), "Target card should be in discard pile");
    }

    #endregion
}
