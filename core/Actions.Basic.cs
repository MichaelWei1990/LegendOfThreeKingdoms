using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default implementation of <see cref="IChoiceRequestFactory"/> for the basic ruleset.
/// It focuses on core interactions: selecting targets for Slash and selecting
/// response cards for basic response windows.
/// </summary>
public sealed class ChoiceRequestFactory : IChoiceRequestFactory
{
    public ChoiceRequest CreateForAction(RuleContext context, ActionDescriptor action)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (action is null) throw new ArgumentNullException(nameof(action));

        // For now we only support actions that explicitly require targets.
        // Other actions (such as EndPlayPhase) do not require additional
        // player input and therefore should not go through this factory.
        if (!action.RequiresTargets)
        {
            throw new InvalidOperationException(
                $"Action '{action.ActionId}' does not require an explicit choice.");
        }

        var targetConstraints = action.TargetConstraints;

        return new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: context.CurrentPlayer.Seat,
            ChoiceType: ChoiceType.SelectTargets,
            TargetConstraints: targetConstraints,
            AllowedCards: action.CardCandidates);
    }

    public ChoiceRequest CreateForResponse(ResponseContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var responder = context.Responder;
        if (!responder.IsAlive)
        {
            throw new InvalidOperationException(
                "Cannot create a response choice for a dead player.");
        }

        IReadOnlyList<Card> allowedCards = context.ResponseType switch
        {
            ResponseType.JinkAgainstSlash => responder.HandZone.Cards
                .Where(c => c.CardSubType == CardSubType.Dodge)
                .ToArray(),
            ResponseType.PeachForDying => responder.HandZone.Cards
                .Where(c => c.CardSubType == CardSubType.Peach)
                .ToArray(),
            _ => Array.Empty<Card>()
        };

        return new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: responder.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: allowedCards);
    }
}

/// <summary>
/// Basic implementation of <see cref="IActionExecutionValidator"/> that performs
/// structural checks on player choices against the original <see cref="ChoiceRequest"/>.
/// It does not re-evaluate rules against the live <see cref="Game"/> state yet.
/// </summary>
public sealed class ActionExecutionValidator : IActionExecutionValidator
{
    public RuleResult Validate(
        RuleContext context,
        ActionDescriptor action,
        ChoiceRequest? originalRequest,
        ChoiceResult? playerChoice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (action is null) throw new ArgumentNullException(nameof(action));

        // If no choice was expected but one is supplied, treat as invalid.
        if (originalRequest is null)
        {
            if (playerChoice is not null)
            {
                return RuleResult.Disallowed(
                    RuleErrorCode.NoLegalOptions,
                    messageKey: "validation.choice.unexpected");
            }

            return RuleResult.Allowed;
        }

        // When a choice was requested but none is provided, fail fast.
        if (playerChoice is null)
        {
            return RuleResult.Disallowed(
                RuleErrorCode.TargetRequired,
                messageKey: "validation.choice.missing");
        }

        if (!string.Equals(originalRequest.RequestId, playerChoice.RequestId, StringComparison.Ordinal))
        {
            return RuleResult.Disallowed(
                RuleErrorCode.NoLegalOptions,
                messageKey: "validation.choice.requestIdMismatch");
        }

        if (originalRequest.PlayerSeat != playerChoice.PlayerSeat)
        {
            return RuleResult.Disallowed(
                RuleErrorCode.PlayerNotActive,
                messageKey: "validation.choice.wrongPlayer");
        }

        return originalRequest.ChoiceType switch
        {
            ChoiceType.SelectTargets => ValidateSelectTargets(originalRequest, playerChoice),
            ChoiceType.SelectCards => ValidateSelectCards(originalRequest, playerChoice),
            ChoiceType.Confirm => ValidateConfirm(playerChoice),
            ChoiceType.SelectOption => ValidateSelectOption(playerChoice),
            _ => RuleResult.Disallowed(RuleErrorCode.NoLegalOptions)
        };
    }

    private static RuleResult ValidateSelectTargets(ChoiceRequest request, ChoiceResult result)
    {
        var constraints = request.TargetConstraints;
        var seats = result.SelectedTargetSeats ?? Array.Empty<int>();

        if (constraints is null)
        {
            // No targets were expected, but some were supplied.
            return seats.Count > 0
                ? RuleResult.Disallowed(
                    RuleErrorCode.TargetRequired,
                    messageKey: "validation.targets.notExpected")
                : RuleResult.Allowed;
        }

        if (seats.Count < constraints.MinTargets)
        {
            return RuleResult.Disallowed(
                RuleErrorCode.TargetRequired,
                messageKey: "validation.targets.tooFew",
                details: new { constraints.MinTargets, Actual = seats.Count });
        }

        if (seats.Count > constraints.MaxTargets)
        {
            return RuleResult.Disallowed(
                RuleErrorCode.NoLegalOptions,
                messageKey: "validation.targets.tooMany",
                details: new { constraints.MaxTargets, Actual = seats.Count });
        }

        // Basic structural validation only; actual legality (range, alive, etc.)
        // is handled by rule services and resolvers.
        return RuleResult.Allowed;
    }

    private static RuleResult ValidateSelectCards(ChoiceRequest request, ChoiceResult result)
    {
        var allowed = request.AllowedCards ?? Array.Empty<Card>();
        var selectedIds = result.SelectedCardIds ?? Array.Empty<int>();

        if (selectedIds.Count == 0)
        {
            // It is legal to pass by not selecting any card in many response
            // contexts, so we do not treat this as an error here.
            return RuleResult.Allowed;
        }

        if (allowed.Count == 0)
        {
            return RuleResult.Disallowed(
                RuleErrorCode.NoLegalOptions,
                messageKey: "validation.cards.notAllowed");
        }

        var allowedIds = new HashSet<int>(allowed.Select(c => c.Id));
        foreach (var id in selectedIds)
        {
            if (!allowedIds.Contains(id))
            {
                return RuleResult.Disallowed(
                    RuleErrorCode.CardNotOwned,
                    messageKey: "validation.cards.notInAllowed",
                    details: new { CardId = id });
            }
        }

        return RuleResult.Allowed;
    }

    private static RuleResult ValidateConfirm(ChoiceResult result)
    {
        if (result.Confirmed is null)
        {
            return RuleResult.Disallowed(
                RuleErrorCode.NoLegalOptions,
                messageKey: "validation.confirm.missing");
        }

        return RuleResult.Allowed;
    }

    private static RuleResult ValidateSelectOption(ChoiceResult result)
    {
        if (string.IsNullOrEmpty(result.SelectedOptionId))
        {
            return RuleResult.Disallowed(
                RuleErrorCode.NoLegalOptions,
                messageKey: "validation.option.missing");
        }

        return RuleResult.Allowed;
    }
}
