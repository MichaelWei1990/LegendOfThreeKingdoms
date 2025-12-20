using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;

namespace core.Tests;

[TestClass]
public sealed class BasicRulesTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    /// <summary>
    /// Verifies that the active player in Play phase is allowed to use cards.
    /// Input: 2-player game, currentPhase = Play, player = currentPlayer.
    /// Expected: IsCardUsagePhase returns true.
    /// </summary>
    [TestMethod]
    public void phaseRuleServiceAllowsPlayPhaseForActivePlayer()
    {
        var game = CreateDefaultGame();
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];

        var service = new PhaseRuleService();

        Assert.IsTrue(service.IsCardUsagePhase(game, player));
    }

    /// <summary>
    /// Verifies that a player is not allowed to use cards outside of Play phase.
    /// Input: 2-player game, currentPhase = Draw, player = currentPlayer.
    /// Expected: IsCardUsagePhase returns false.
    /// </summary>
    [TestMethod]
    public void phaseRuleServiceBlocksNonPlayPhase()
    {
        var game = CreateDefaultGame();
        game.CurrentPhase = Phase.Draw;
        var player = game.Players[0];

        var service = new PhaseRuleService();

        Assert.IsFalse(service.IsCardUsagePhase(game, player));
    }

    /// <summary>
    /// Verifies that seat distance is symmetric and within default attack range.
    /// Input: 3-player game in a ring, players at seat 0 and 1.
    /// Expected: distance(0,1) = distance(1,0) = 1, both within attack range.
    /// </summary>
    [TestMethod]
    public void rangeRuleServiceSeatDistanceSymmetricAndWithinAttackRangeDefault()
    {
        var game = CreateDefaultGame(3);
        var p0 = game.Players[0];
        var p1 = game.Players[1];

        var service = new RangeRuleService();

        var d01 = service.GetSeatDistance(game, p0, p1);
        var d10 = service.GetSeatDistance(game, p1, p0);

        Assert.AreEqual(1, d01);
        Assert.AreEqual(1, d10);
        Assert.IsTrue(service.IsWithinAttackRange(game, p0, p1));
        Assert.IsTrue(service.IsWithinAttackRange(game, p1, p0));
    }

    /// <summary>
    /// Verifies that Slash can be used when below per-turn limit and there is at least one legal target.
    /// Input: 2-player game, source has one Slash in hand, currentPhase = Play, usageCountThisTurn = 0.
    /// Expected: CanUseCard returns allowed.
    /// </summary>
    [TestMethod]
    public void cardUsageRuleServiceAllowsSlashWhenWithinLimitAndHasTarget()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        // Put the card into the source player's hand so that future ownership
        // checks (when added) can be satisfied.
        Assert.IsInstanceOfType(source.HandZone, typeof(Zone));
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules);

        var context = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        var result = service.CanUseCard(context);

        Assert.IsTrue(result.IsAllowed, "Slash should be allowed when within limit and with legal target.");
    }

    /// <summary>
    /// Verifies that Slash cannot be used when the per-turn usage limit has been reached.
    /// Input: 2-player game, source has one Slash in hand, currentPhase = Play, usageCountThisTurn = 1.
    /// Expected: CanUseCard returns disallowed with UsageLimitReached.
    /// </summary>
    [TestMethod]
    public void cardUsageRuleServiceBlocksSlashWhenUsageLimitReached()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        Assert.IsInstanceOfType(source.HandZone, typeof(Zone));
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules);

        var context = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 1);

        var result = service.CanUseCard(context);

        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(RuleErrorCode.UsageLimitReached, result.ErrorCode);
    }

    /// <summary>
    /// Verifies that a player with a Dodge in hand is allowed to respond with JinkAgainstSlash.
    /// Input: 2-player game, responder has one Dodge card in hand, responseType = JinkAgainstSlash.
    /// Expected: CanRespondWithCard allowed, GetLegalResponseCards returns exactly that Dodge card.
    /// </summary>
    [TestMethod]
    public void responseRuleServiceAllowsJinkWhenPlayerHasDodge()
    {
        var game = CreateDefaultGame(2);
        var responder = game.Players[1];

        var dodge = new Card
        {
            Id = 2,
            DefinitionId = "dodge_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge
        };

        Assert.IsInstanceOfType(responder.HandZone, typeof(Zone));
        ((Zone)responder.HandZone).MutableCards.Add(dodge);

        var service = new ResponseRuleService();

        var context = new ResponseContext(
            game,
            responder,
            ResponseType.JinkAgainstSlash,
            SourceEvent: null);

        var result = service.CanRespondWithCard(context);
        var cards = service.GetLegalResponseCards(context);

        Assert.IsTrue(result.IsAllowed);
        Assert.IsTrue(cards.HasAny);
        Assert.AreEqual(1, cards.Items.Count);
        Assert.AreEqual(CardSubType.Dodge, cards.Items[0].CardSubType);
    }

    /// <summary>
    /// Verifies that response is not allowed when the player has no suitable response card.
    /// Input: 2-player game, responder has no cards, responseType = JinkAgainstSlash.
    /// Expected: CanRespondWithCard disallowed, GetLegalResponseCards empty with NoLegalOptions.
    /// </summary>
    [TestMethod]
    public void responseRuleServiceBlocksResponseWhenNoLegalCard()
    {
        var game = CreateDefaultGame(2);
        var responder = game.Players[1];

        var service = new ResponseRuleService();

        var context = new ResponseContext(
            game,
            responder,
            ResponseType.JinkAgainstSlash,
            SourceEvent: null);

        var result = service.CanRespondWithCard(context);
        var cards = service.GetLegalResponseCards(context);

        Assert.IsFalse(result.IsAllowed);
        Assert.IsFalse(cards.HasAny);
        Assert.AreEqual(RuleErrorCode.NoLegalOptions, cards.ErrorCode);
    }

    /// <summary>
    /// Verifies that GetAvailableActions returns basic actions in Play phase.
    /// Input: 2-player game, currentPhase = Play, source has one Slash and one Peach, both usable.
    /// Expected: actions contain UseSlash, UsePeach and EndPlayPhase with correct ids.
    /// </summary>
    [TestMethod]
    public void ruleServiceReturnsBasicActionsInPlayPhase()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        // Wound the player so that Peach becomes a legal action.
        player.CurrentHealth = player.MaxHealth - 1;

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        var peach = new Card
        {
            Id = 2,
            DefinitionId = "peach_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach
        };

        Assert.IsInstanceOfType(player.HandZone, typeof(Zone));
        ((Zone)player.HandZone).MutableCards.Add(slash);
        ((Zone)player.HandZone).MutableCards.Add(peach);

        var ruleService = new RuleService();
        var context = new RuleContext(game, player);

        var actionsResult = ruleService.GetAvailableActions(context);

        Assert.IsTrue(actionsResult.HasAny);
        var actionIds = actionsResult.Items.Select(a => a.ActionId).ToArray();

        // EndPlayPhase must always be available in Play phase.
        CollectionAssert.Contains(actionIds, "EndPlayPhase");

        // When Slash and Peach are present and usable, corresponding actions should also exist
        // with correct target constraints and card candidates.
        var slashAction = actionsResult.Items.Single(a => a.ActionId == "UseSlash");
        Assert.IsTrue(slashAction.RequiresTargets);
        Assert.AreEqual(1, slashAction.TargetConstraints.MinTargets);
        Assert.AreEqual(1, slashAction.TargetConstraints.MaxTargets);
        Assert.AreEqual(TargetFilterType.Enemies, slashAction.TargetConstraints.FilterType);
        Assert.IsNotNull(slashAction.CardCandidates);
        Assert.IsTrue(slashAction.CardCandidates.Any(c => c.CardSubType == CardSubType.Slash));

        var peachAction = actionsResult.Items.Single(a => a.ActionId == "UsePeach");
        Assert.IsFalse(peachAction.RequiresTargets);
        Assert.AreEqual(0, peachAction.TargetConstraints.MinTargets);
        Assert.AreEqual(0, peachAction.TargetConstraints.MaxTargets);
        Assert.AreEqual(TargetFilterType.SelfOrFriends, peachAction.TargetConstraints.FilterType);
        Assert.IsNotNull(peachAction.CardCandidates);
        Assert.IsTrue(peachAction.CardCandidates.Any(c => c.CardSubType == CardSubType.Peach));
    }

    /// <summary>
    /// Verifies that ValidateActionBeforeResolve approves a simple, well-formed action by default.
    /// Input: Play phase, EndPlayPhase action descriptor, no choice payload.
    /// Expected: ValidateActionBeforeResolve returns allowed with no error.
    /// </summary>
    [TestMethod]
    public void validateActionBeforeResolveApprovesEndPlayPhaseByDefault()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];

        var ruleService = new RuleService();
        var context = new RuleContext(game, player);

        var action = new ActionDescriptor(
            ActionId: "EndPlayPhase",
            DisplayKey: "action.endPlayPhase",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(
                MinTargets: 0,
                MaxTargets: 0,
                FilterType: TargetFilterType.Any),
            CardCandidates: null);

        var result = ruleService.ValidateActionBeforeResolve(context, action, choice: null);

        Assert.IsTrue(result.IsAllowed);
        Assert.AreEqual(RuleErrorCode.None, result.ErrorCode);
    }

    /// <summary>
    /// Verifies that ChoiceRequestFactory creates a target-selection request for UseSlash actions
    /// with matching target constraints and card candidates.
    /// Input: Play phase, player has one usable Slash and corresponding UseSlash action.
    /// Expected: CreateForAction returns SelectTargets choice for the same player and constraints.
    /// </summary>
    [TestMethod]
    public void choiceRequestFactoryCreatesSelectTargetsForUseSlash()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        var target = game.Players[1];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        Assert.IsInstanceOfType(player.HandZone, typeof(Zone));
        ((Zone)player.HandZone).MutableCards.Add(slash);

        var ruleService = new RuleService();
        var context = new RuleContext(game, player);

        var actionsResult = ruleService.GetAvailableActions(context);
        Assert.IsTrue(actionsResult.HasAny);
        var slashAction = actionsResult.Items.Single(a => a.ActionId == "UseSlash");

        var factory = new ChoiceRequestFactory();
        var choice = factory.CreateForAction(context, slashAction);

        Assert.AreEqual(player.Seat, choice.PlayerSeat);
        Assert.AreEqual(ChoiceType.SelectTargets, choice.ChoiceType);
        Assert.IsNotNull(choice.TargetConstraints);
        Assert.AreEqual(slashAction.TargetConstraints.MinTargets, choice.TargetConstraints!.MinTargets);
        Assert.AreEqual(slashAction.TargetConstraints.MaxTargets, choice.TargetConstraints!.MaxTargets);
        Assert.AreEqual(slashAction.TargetConstraints.FilterType, choice.TargetConstraints!.FilterType);
        Assert.IsNotNull(choice.AllowedCards);
        Assert.IsTrue(choice.AllowedCards!.Any(c => c.CardSubType == CardSubType.Slash));
    }

    /// <summary>
    /// Verifies that ChoiceRequestFactory creates a SelectCards request for a Jink response
    /// with the responder's Dodge cards as allowed cards.
    /// Input: responder has one Dodge card and response type JinkAgainstSlash.
    /// Expected: CreateForResponse returns SelectCards choice containing that Dodge card.
    /// </summary>
    [TestMethod]
    public void choiceRequestFactoryCreatesSelectCardsForJinkResponse()
    {
        var game = CreateDefaultGame(2);
        var responder = game.Players[1];

        var dodge = new Card
        {
            Id = 2,
            DefinitionId = "dodge_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge
        };

        Assert.IsInstanceOfType(responder.HandZone, typeof(Zone));
        ((Zone)responder.HandZone).MutableCards.Add(dodge);

        var responseContext = new ResponseContext(
            game,
            responder,
            ResponseType.JinkAgainstSlash,
            SourceEvent: null);

        var factory = new ChoiceRequestFactory();
        var choice = factory.CreateForResponse(responseContext);

        Assert.AreEqual(responder.Seat, choice.PlayerSeat);
        Assert.AreEqual(ChoiceType.SelectCards, choice.ChoiceType);
        Assert.IsNotNull(choice.AllowedCards);
        Assert.AreEqual(1, choice.AllowedCards!.Count);
        Assert.AreEqual(CardSubType.Dodge, choice.AllowedCards[0].CardSubType);
    }

    /// <summary>
    /// Verifies that ActionExecutionValidator accepts a well-formed target selection choice
    /// that satisfies the original target constraints.
    /// Input: UseSlash action with MinTargets = MaxTargets = 1 and a choice selecting one target seat.
    /// Expected: Validate returns allowed.
    /// </summary>
    [TestMethod]
    public void actionExecutionValidatorAcceptsValidTargetSelection()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        var target = game.Players[1];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        Assert.IsInstanceOfType(player.HandZone, typeof(Zone));
        ((Zone)player.HandZone).MutableCards.Add(slash);

        var ruleService = new RuleService();
        var context = new RuleContext(game, player);

        var actionsResult = ruleService.GetAvailableActions(context);
        var slashAction = actionsResult.Items.Single(a => a.ActionId == "UseSlash");

        var factory = new ChoiceRequestFactory();
        var request = factory.CreateForAction(context, slashAction);

        var choice = new ChoiceResult(
            RequestId: request.RequestId,
            PlayerSeat: player.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null);

        var validator = new ActionExecutionValidator();
        var result = validator.Validate(context, slashAction, request, choice);

        Assert.IsTrue(result.IsAllowed);
        Assert.AreEqual(RuleErrorCode.None, result.ErrorCode);
    }

    /// <summary>
    /// Verifies that ActionExecutionValidator rejects a target selection that violates
    /// the minimum target requirement.
    /// Input: UseSlash action with MinTargets = 1 and a choice selecting zero targets.
    /// Expected: Validate returns disallowed with TargetRequired.
    /// </summary>
    [TestMethod]
    public void actionExecutionValidatorRejectsTooFewTargets()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        Assert.IsInstanceOfType(player.HandZone, typeof(Zone));
        ((Zone)player.HandZone).MutableCards.Add(slash);

        var ruleService = new RuleService();
        var context = new RuleContext(game, player);

        var actionsResult = ruleService.GetAvailableActions(context);
        var slashAction = actionsResult.Items.Single(a => a.ActionId == "UseSlash");

        var factory = new ChoiceRequestFactory();
        var request = factory.CreateForAction(context, slashAction);

        var emptyChoice = new ChoiceResult(
            RequestId: request.RequestId,
            PlayerSeat: player.Seat,
            SelectedTargetSeats: Array.Empty<int>(),
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null);

        var validator = new ActionExecutionValidator();
        var result = validator.Validate(context, slashAction, request, emptyChoice);

        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(RuleErrorCode.TargetRequired, result.ErrorCode);
    }

    /// <summary>
    /// Verifies that CardUsageRuleService applies skill modifications to slash usage limit.
    /// Input: 2-player game, player has ExtraSlashSkill, usageCountThisTurn = 1 (normally would be blocked).
    /// Expected: CanUseCard returns allowed because skill increases limit from 1 to 2.
    /// </summary>
    [TestMethod]
    public void cardUsageRuleServiceAppliesSkillModificationToSlashLimit()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        Assert.IsInstanceOfType(source.HandZone, typeof(Zone));
        ((Zone)source.HandZone).MutableCards.Add(slash);

        // Setup skill system
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);

        // Create player with hero
        var playerWithHero = new Player
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

        skillManager.LoadSkillsForPlayer(game, playerWithHero);

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        var context = new CardUsageContext(
            game,
            playerWithHero,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 1); // Already used 1 slash, but skill allows 2

        var result = service.CanUseCard(context);

        // Should be allowed because skill increases limit from 1 to 2
        Assert.IsTrue(result.IsAllowed, "Slash should be allowed when skill increases limit to 2.");
    }

    /// <summary>
    /// Verifies that CardUsageRuleService blocks slash when limit is reached even with skill modification.
    /// Input: 2-player game, player has ExtraSlashSkill, usageCountThisTurn = 2 (exceeds modified limit).
    /// Expected: CanUseCard returns disallowed with UsageLimitReached.
    /// </summary>
    [TestMethod]
    public void cardUsageRuleServiceBlocksSlashWhenModifiedLimitReached()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };

        Assert.IsInstanceOfType(source.HandZone, typeof(Zone));
        ((Zone)source.HandZone).MutableCards.Add(slash);

        // Setup skill system
        var registry = new SkillRegistry();
        registry.RegisterSkill("extra_slash", new ExtraSlashSkillFactory());
        registry.RegisterHeroSkills("hero_test", new[] { "extra_slash" });

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);

        // Create player with hero
        var playerWithHero = new Player
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

        skillManager.LoadSkillsForPlayer(game, playerWithHero);

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        var context = new CardUsageContext(
            game,
            playerWithHero,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 2); // Already used 2 slashes, exceeds modified limit of 2

        var result = service.CanUseCard(context);

        // Should be blocked because limit is 2 and already used 2
        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(RuleErrorCode.UsageLimitReached, result.ErrorCode);
    }
}

