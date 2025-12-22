using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Bagua Array skill: allows the owner to perform a judgement when responding to Slash.
/// If the judgement result is red, it is treated as playing a Jink (Dodge).
/// </summary>
public sealed class BaguaFormation : BaseSkill, IResponseEnhancementSkill
{
    /// <inheritdoc />
    public override string Id => "bagua_array";

    /// <inheritdoc />
    public override string Name => "八卦阵";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public int Priority => 0; // High priority: checked before legal cards

    /// <inheritdoc />
    public bool CanProvideResponse(Game game, Player owner, ResponseType responseType, object? sourceEvent)
    {
        // Only works for Jink against Slash
        if (responseType != ResponseType.JinkAgainstSlash)
            return false;

        // Skill must be active
        if (!IsActive(game, owner))
            return false;

        // Check if armor is ignored by attacker
        if (IsArmorIgnored(game, owner, sourceEvent))
            return false;

        return true;
    }

    /// <inheritdoc />
    public bool ExecuteAlternativeResponse(
        Game game,
        Player owner,
        ResponseType responseType,
        object? sourceEvent,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice,
        IJudgementService judgementService,
        ICardMoveService cardMoveService)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));
        if (judgementService is null) throw new ArgumentNullException(nameof(judgementService));
        if (cardMoveService is null) throw new ArgumentNullException(nameof(cardMoveService));

        // Check if can provide response
        if (!CanProvideResponse(game, owner, responseType, sourceEvent))
            return false;

        // Ask player if they want to activate Bagua Array
        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true
        );

        var choice = getPlayerChoice(choiceRequest);

        // If player chose not to activate, return false
        if (choice is null || !choice.Confirmed.HasValue || !choice.Confirmed.Value)
            return false;

        // Create judgement request
        var effectSource = new BaguaFormationEffectSource();
        var request = new JudgementRequest(
            JudgementId: Guid.NewGuid(),
            JudgeOwnerSeat: owner.Seat,
            Reason: JudgementReason.Armor,
            Source: effectSource,
            Rule: new RedJudgementRule(),
            AllowModify: true
        );

        // Execute judgement
        var result = judgementService.ExecuteJudgement(game, owner, request, cardMoveService);

        // Complete judgement (move card to discard pile)
        judgementService.CompleteJudgement(game, owner, result.FinalCard, cardMoveService);

        // Return true if judgement succeeded (red card)
        return result.IsSuccess;
    }

    /// <summary>
    /// Checks if armor effects are ignored by the attacker.
    /// This method will be called from response window which has SkillManager access.
    /// For now, we return false and let the response window handle the check.
    /// </summary>
    private bool IsArmorIgnored(Game game, Player owner, object? sourceEvent)
    {
        // This check requires SkillManager access to check attacker's skills.
        // Since CanProvideResponse doesn't have SkillManager, we'll do a basic check here
        // and rely on the response window to provide proper SkillManager context.
        // For now, return false (armor not ignored) - the actual check will be done
        // in ExecuteAlternativeResponse if needed, or handled at response window level.
        return false;
    }

    /// <summary>
    /// Effect source for Bagua Array judgement.
    /// </summary>
    private sealed class BaguaFormationEffectSource : IEffectSource
    {
        public string SourceId => "bagua_array";
        public string SourceType => "Equipment";
        public string? DisplayName => "八卦阵";
    }
}

/// <summary>
/// Factory for creating BaguaFormation instances.
/// </summary>
public sealed class BaguaFormationFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new BaguaFormation();
    }
}
