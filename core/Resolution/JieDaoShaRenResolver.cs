using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Jie Dao Sha Ren (借刀杀人 / Borrow Knife) immediate trick card.
/// Effect: Force a player with a weapon to use Slash on a specified target, or transfer their weapon to the user.
/// </summary>
public sealed class JieDaoShaRenResolver : TargetedTrickResolverBase
{
    /// <inheritdoc />
    protected override string MessageKeyPrefix => "resolution.jiedaosharen";

    /// <inheritdoc />
    protected override string EffectKey => "JieDaoShaRen.Resolve";

    /// <inheritdoc />
    protected override string NullificationResultKeyPrefix => "JieDaoShaRenNullification";

    /// <inheritdoc />
    protected override string CannotTargetSelfMessageKey => "resolution.jiedaosharen.cannotTargetSelf";

    /// <inheritdoc />
    protected override string NoSelectableCardsMessageKey => "resolution.jiedaosharen.targetAHasNoSlashTargets";

    /// <inheritdoc />
    protected override ResolutionResult? ValidateTarget(
        ResolutionContext context,
        Player sourcePlayer,
        Player target)
    {
        // First do base validation (alive and not self)
        var baseResult = base.ValidateTarget(context, sourcePlayer, target);
        if (baseResult is not null)
        {
            return baseResult;
        }

        // Additional validation: target must have a weapon
        if (!HasWeapon(target))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.jiedaosharen.targetANoWeapon",
                details: new { TargetASeat = target.Seat });
        }

        return null; // Validation passed
    }

    /// <inheritdoc />
    protected override List<Card> CollectSelectableCards(Player target)
    {
        // JieDaoShaRen doesn't select cards from target's zones
        // Instead, it selects target B (a player that target A can use Slash on)
        // This method is not used for JieDaoShaRen, but we need to implement it
        return new List<Card>();
    }

    /// <inheritdoc />
    protected override IZone? GetSourceZone(Player target, int selectedCardId)
    {
        // JieDaoShaRen doesn't select cards from target's zones
        // This method is not used for JieDaoShaRen, but we need to implement it
        return null;
    }

    /// <inheritdoc />
    protected override IResolver CreateEffectHandlerResolver(
        Player target,
        Card selectedCard,
        IZone sourceZone,
        ResolutionContext context)
    {
        // For JieDaoShaRen, we need targetB which is stored in IntermediateResults
        // The selectedCard parameter is not used (it's a placeholder)
        var targetB = GetTargetBFromIntermediateResults(context, target);
        if (targetB is null)
        {
            // This should not happen if Resolve was called correctly
            throw new InvalidOperationException("Target B not found in intermediate results");
        }

        return new JieDaoShaRenEffectHandlerResolver(target, targetB, context.SourcePlayer);
    }

    /// <summary>
    /// Resolves JieDaoShaRen with two-step target selection (target A and target B).
    /// </summary>
    public new ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var choice = context.Choice;

        // Step 1: Validate choice
        if (choice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: $"{MessageKeyPrefix}.noChoice");
        }

        // Step 2: Extract and validate target A (player with weapon)
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: $"{MessageKeyPrefix}.noTargetA");
        }

        var targetASeat = selectedTargetSeats[0];
        var targetA = game.Players.FirstOrDefault(p => p.Seat == targetASeat);

        if (targetA is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: $"{MessageKeyPrefix}.targetANotFound",
                details: new { TargetASeat = targetASeat });
        }

        // Step 3: Validate target A (custom validation including weapon check)
        var customValidationResult = ValidateTarget(context, sourcePlayer, targetA);
        if (customValidationResult is not null)
        {
            return customValidationResult;
        }

        // Step 4: Get legal slash targets for A (first legality check)
        var slashTargetsResult = GetSlashLegalTargetsForPlayer(context, targetA);
        if (!slashTargetsResult.HasAny)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: NoSelectableCardsMessageKey,
                details: new { TargetASeat = targetA.Seat });
        }

        // Step 5: Request selection of target B (A's slash target)
        var targetBResult = RequestTargetBSelection(context, sourcePlayer, targetA, slashTargetsResult.Items);
        if (targetBResult.FailureResult is not null)
        {
            return targetBResult.FailureResult;
        }

        var targetB = targetBResult.TargetB!;

        // Step 6: Validate that B is a legal slash target for A (first legality check)
        if (!IsSlashLegal(context, targetA, targetB))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: $"{MessageKeyPrefix}.targetBNotLegalForA",
                details: new { TargetASeat = targetA.Seat, TargetBSeat = targetB.Seat });
        }

        // Step 7: Store targetB in IntermediateResults for effect handler
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        intermediateResults[$"JieDaoShaRenTargetB_{targetA.Seat}"] = targetB;

        // Step 8: Create a new context with updated IntermediateResults
        var updatedContext = context with { IntermediateResults = intermediateResults };

        // Step 9: Open nullification window and push effect handler
        OpenNullificationWindowAndPushHandler(updatedContext, targetA, targetB);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Opens nullification window and pushes the effect handler resolver.
    /// </summary>
    private void OpenNullificationWindowAndPushHandler(
        ResolutionContext context,
        Player targetA,
        Player targetB)
    {
        // Create nullifiable effect
        var causingCard = context.ExtractCausingCard();
        var nullifiableEffect = NullificationHelper.CreateNullifiableEffect(
            effectKey: EffectKey,
            targetPlayer: targetA,
            causingCard: causingCard,
            isNullifiable: true);

        var nullificationResultKey = $"{NullificationResultKeyPrefix}_{targetA.Seat}";

        // Create handler context
        var handlerContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Push handler resolver first (will execute after nullification window due to LIFO)
        // Use a dummy card and zone for the base class method signature
        var dummyCard = new Card
        {
            Id = -1,
            DefinitionId = "dummy",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 1
        };
        var handlerResolver = CreateEffectHandlerResolver(targetA, dummyCard, targetA.EquipmentZone, context);
        context.Stack.Push(handlerResolver, handlerContext);

        // Open nullification window (push last so it executes first)
        NullificationHelper.OpenNullificationWindow(context, nullifiableEffect, nullificationResultKey);
    }

    /// <summary>
    /// Gets target B from IntermediateResults.
    /// </summary>
    private static Player? GetTargetBFromIntermediateResults(ResolutionContext context, Player targetA)
    {
        if (context.IntermediateResults is null)
        {
            return null;
        }

        var key = $"JieDaoShaRenTargetB_{targetA.Seat}";
        if (context.IntermediateResults.TryGetValue(key, out var targetBObj) && targetBObj is Player targetB)
        {
            return targetB;
        }

        return null;
    }

    /// <summary>
    /// Gets legal slash targets for a player.
    /// </summary>
    private static RuleQueryResult<Player> GetSlashLegalTargetsForPlayer(
        ResolutionContext context,
        Player attacker)
    {
        if (context.RuleService is null)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        // Create a virtual Slash card for checking legal targets
        var virtualSlash = new Card
        {
            Id = -1, // Virtual card ID
            DefinitionId = "virtual_slash",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 1
        };

        var usageContext = new CardUsageContext(
            Game: context.Game,
            SourcePlayer: attacker,
            Card: virtualSlash,
            CandidateTargets: context.Game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        return context.RuleService.GetLegalTargetsForUse(usageContext);
    }

    /// <summary>
    /// Checks if a slash from attacker to target is legal.
    /// </summary>
    private static bool IsSlashLegal(
        ResolutionContext context,
        Player attacker,
        Player target)
    {
        if (context.RuleService is null)
        {
            return false;
        }

        // Create a virtual Slash card for checking legality
        var virtualSlash = new Card
        {
            Id = -1,
            DefinitionId = "virtual_slash",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 1
        };

        var usageContext = new CardUsageContext(
            Game: context.Game,
            SourcePlayer: attacker,
            Card: virtualSlash,
            CandidateTargets: context.Game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        var legalTargets = context.RuleService.GetLegalTargetsForUse(usageContext);
        return legalTargets.HasAny && legalTargets.Items.Contains(target);
    }

    /// <summary>
    /// Requests the user to select target B (A's slash target).
    /// </summary>
    private static TargetBResult RequestTargetBSelection(
        ResolutionContext context,
        Player sourcePlayer,
        Player targetA,
        IReadOnlyList<Player> legalSlashTargets)
    {
        if (context.GetPlayerChoice is null)
        {
            return TargetBResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.jiedaosharen.getPlayerChoiceNotAvailable"));
        }

        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: sourcePlayer.Seat,
            ChoiceType: ChoiceType.SelectTargets,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: false);

        var playerChoice = context.GetPlayerChoice(choiceRequest);

        if (playerChoice is null)
        {
            return TargetBResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.jiedaosharen.playerChoiceNull"));
        }

        var selectedTargetSeats = playerChoice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return TargetBResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.jiedaosharen.noTargetB"));
        }

        var targetBSeat = selectedTargetSeats[0];
        var targetB = context.Game.Players.FirstOrDefault(p => p.Seat == targetBSeat);

        if (targetB is null)
        {
            return TargetBResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.jiedaosharen.targetBNotFound",
                details: new { TargetBSeat = targetBSeat }));
        }

        // Validate that B is in the legal slash targets list
        if (!legalSlashTargets.Contains(targetB))
        {
            return TargetBResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.jiedaosharen.targetBNotLegalForA",
                details: new { TargetASeat = targetA.Seat, TargetBSeat = targetBSeat }));
        }

        return TargetBResult.CreateSuccess(targetB);
    }

    /// <summary>
    /// Checks if a player has a weapon in their equipment zone.
    /// </summary>
    private static bool HasWeapon(Player player)
    {
        if (player.EquipmentZone.Cards is null)
        {
            return false;
        }

        return player.EquipmentZone.Cards.Any(c => c.CardSubType == CardSubType.Weapon);
    }


    /// <summary>
    /// Result of target B selection.
    /// </summary>
    private sealed record TargetBResult(
        Player? TargetB,
        ResolutionResult? FailureResult)
    {
        public static TargetBResult CreateSuccess(Player targetB) => new(targetB, null);
        public static TargetBResult CreateFailure(ResolutionResult failureResult) => new(null, failureResult);
    }
}
