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
public sealed class QingnangTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
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
    /// Tests that QingnangSkillFactory creates correct skill instance.
    /// Input: QingnangSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Active.
    /// </summary>
    [TestMethod]
    public void QingnangSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new QingnangSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qingnang", skill.Id);
        Assert.AreEqual("青囊", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Qingnang skill.
    /// Input: Empty registry, QingnangSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterQingnangSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new QingnangSkillFactory();

        // Act
        registry.RegisterSkill("qingnang", factory);
        var skill = registry.GetSkill("qingnang");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qingnang", skill.Id);
        Assert.AreEqual("青囊", skill.Name);
    }

    #endregion

    #region ActionQueryService Tests

    /// <summary>
    /// Tests that ActionQueryService generates UseQingnang action when player has Qingnang skill and hand cards.
    /// Input: Game in Play phase, player has Qingnang skill and hand cards.
    /// Expected: UseQingnang action is available with hand cards as candidates.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceGeneratesUseQingnangActionWithHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var handCard = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(handCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingnang", new QingnangSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("qingnang"));

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
        var qingnangAction = actions.Items.FirstOrDefault(a => a.ActionId == "UseQingnang");
        Assert.IsNotNull(qingnangAction, "UseQingnang action should be available");
        Assert.IsTrue(qingnangAction.CardCandidates?.Any(c => c.Id == handCard.Id) == true, "Hand card should be a candidate");
        Assert.IsTrue(qingnangAction.RequiresTargets, "Qingnang action should require targets");
    }

    /// <summary>
    /// Tests that ActionQueryService does not generate UseQingnang action when player has no hand cards.
    /// Input: Game in Play phase, player has Qingnang skill but no hand cards.
    /// Expected: UseQingnang action is not available.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseQingnangActionWithoutHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        // No hand cards

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingnang", new QingnangSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("qingnang"));

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
        var qingnangAction = actions.Items.FirstOrDefault(a => a.ActionId == "UseQingnang");
        Assert.IsNull(qingnangAction, "UseQingnang action should not be available without hand cards");
    }

    /// <summary>
    /// Tests that ActionQueryService does not generate UseQingnang action when skill already used this turn.
    /// Input: Game in Play phase, player has Qingnang skill, hand cards, but already used skill this turn.
    /// Expected: UseQingnang action is not available.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceDoesNotGenerateUseQingnangActionAfterUseThisTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var player = game.Players[0];
        var handCard = CreateTestCard(1);
        ((Zone)player.HandZone).MutableCards.Add(handCard);

        // Mark skill as used this turn
        var usageKey = $"qingnang_used_turn_{game.TurnNumber}_seat_{game.CurrentPlayerSeat}";
        player.Flags[usageKey] = true;

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingnang", new QingnangSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("qingnang"));

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
        var qingnangAction = actions.Items.FirstOrDefault(a => a.ActionId == "UseQingnang");
        Assert.IsNull(qingnangAction, "UseQingnang action should not be available after use this turn");
    }

    #endregion

    #region QingnangResolver Tests

    /// <summary>
    /// Tests that QingnangResolver discards hand card and heals target.
    /// Input: Game in Play phase, player has Qingnang skill, uses skill with hand card and target.
    /// Expected: Hand card is discarded, target is healed by 1 HP.
    /// </summary>
    [TestMethod]
    public void QingnangResolverDiscardsCardAndHealsTarget()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var source = game.Players[0];
        var target = game.Players[1];
        var handCard = CreateTestCard(1);
        ((Zone)source.HandZone).MutableCards.Add(handCard);

        // Set target to less than max health
        // Note: MaxHealth is init-only, so we can only modify CurrentHealth
        // Assuming default MaxHealth is 4, set CurrentHealth to 2
        target.CurrentHealth = 2;

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingnang", new QingnangSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, source, skillRegistry.GetSkill("qingnang"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var action = new ActionDescriptor(
            ActionId: "UseQingnang",
            DisplayKey: "action.useQingnang",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(MinTargets: 1, MaxTargets: 1, FilterType: TargetFilterType.Any),
            CardCandidates: new[] { handCard }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { handCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

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
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        var initialTargetHealth = target.CurrentHealth;
        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        // Act
        var resolver = new QingnangResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        Assert.AreEqual(initialTargetHealth + 1, target.CurrentHealth, "Target should be healed by 1 HP");
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, "Source should have one less card");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Discard pile should have one more card");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == handCard.Id), "Hand card should be in discard pile");
    }

    /// <summary>
    /// Tests that QingnangResolver does not heal beyond max health.
    /// Input: Game in Play phase, player has Qingnang skill, uses skill on target at max health.
    /// Expected: Target health remains at max, no overflow.
    /// </summary>
    [TestMethod]
    public void QingnangResolverDoesNotHealBeyondMaxHealth()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var source = game.Players[0];
        var target = game.Players[1];
        var handCard = CreateTestCard(1);
        ((Zone)source.HandZone).MutableCards.Add(handCard);

        // Set target to max health
        // Note: MaxHealth is init-only, so we can only modify CurrentHealth
        // Assuming default MaxHealth is 4, set CurrentHealth to 4
        target.CurrentHealth = 4;

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingnang", new QingnangSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, source, skillRegistry.GetSkill("qingnang"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var action = new ActionDescriptor(
            ActionId: "UseQingnang",
            DisplayKey: "action.useQingnang",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(MinTargets: 1, MaxTargets: 1, FilterType: TargetFilterType.Any),
            CardCandidates: new[] { handCard }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { handCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

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
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        // Act
        var resolver = new QingnangResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        Assert.AreEqual(4, target.CurrentHealth, "Target health should remain at max (4)");
        Assert.AreEqual(4, target.MaxHealth, "Target max health should remain unchanged");
    }

    /// <summary>
    /// Tests that QingnangResolver marks skill as used this turn.
    /// Input: Game in Play phase, player uses Qingnang skill.
    /// Expected: Player's Flags contains usage marker for this turn.
    /// </summary>
    [TestMethod]
    public void QingnangResolverMarksSkillAsUsedThisTurn()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var source = game.Players[0];
        var target = game.Players[1];
        var handCard = CreateTestCard(1);
        ((Zone)source.HandZone).MutableCards.Add(handCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingnang", new QingnangSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, source, skillRegistry.GetSkill("qingnang"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var action = new ActionDescriptor(
            ActionId: "UseQingnang",
            DisplayKey: "action.useQingnang",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(MinTargets: 1, MaxTargets: 1, FilterType: TargetFilterType.Any),
            CardCandidates: new[] { handCard }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { handCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

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
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null);

        // Act
        var resolver = new QingnangResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed");
        var usageKey = $"qingnang_used_turn_{game.TurnNumber}_seat_{game.CurrentPlayerSeat}";
        Assert.IsTrue(source.Flags.ContainsKey(usageKey), "Skill should be marked as used this turn");
        Assert.AreEqual(true, source.Flags[usageKey], "Usage flag should be set to true");
    }

    #endregion
}
