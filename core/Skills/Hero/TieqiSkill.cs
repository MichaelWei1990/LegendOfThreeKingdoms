using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Tieqi (铁骑) skill: Trigger skill that performs judgement when Slash targets are confirmed.
/// If judgement is red, the target cannot use Dodge to respond to this Slash.
/// </summary>
public sealed class TieqiSkill : BaseSkill, ISlashResponseModifier
{
    /// <inheritdoc />
    public override string Id => "tieqi";

    /// <inheritdoc />
    public override string Name => "铁骑";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public bool ProcessSlashTargetConfirmed(
        Game game,
        Player sourcePlayer,
        Card slashCard,
        Player targetPlayer,
        IJudgementService judgementService,
        ICardMoveService cardMoveService,
        IEventBus? eventBus)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (sourcePlayer is null)
            throw new ArgumentNullException(nameof(sourcePlayer));
        if (slashCard is null)
            throw new ArgumentNullException(nameof(slashCard));
        if (targetPlayer is null)
            throw new ArgumentNullException(nameof(targetPlayer));
        if (judgementService is null)
            throw new ArgumentNullException(nameof(judgementService));
        if (cardMoveService is null)
            throw new ArgumentNullException(nameof(cardMoveService));

        // Check if skill is active
        if (!IsActive(game, sourcePlayer))
            return false;

        // Perform judgement
        var judgementRequest = new JudgementRequest(
            JudgementId: Guid.NewGuid(),
            JudgeOwnerSeat: sourcePlayer.Seat,
            Reason: JudgementReason.Skill,
            Source: new TieqiEffectSource(),
            Rule: new RedJudgementRule(),
            Tags: null,
            AllowModify: false // Tieqi does not allow modification
        );

        try
        {
            var judgementResult = judgementService.ExecuteJudgement(
                game,
                sourcePlayer,
                judgementRequest,
                cardMoveService);

            // Complete the judgement (move card from JudgementZone to discard pile)
            judgementService.CompleteJudgement(game, sourcePlayer, judgementResult.FinalCard, cardMoveService);

            // If judgement is successful (red), return true to prevent target from using Dodge
            return judgementResult.IsSuccess;
        }
        catch (Exception)
        {
            // Judgement failed (e.g., draw pile empty), return false (target can use Dodge)
            return false;
        }
    }
}

/// <summary>
/// Effect source for Tieqi skill judgements.
/// </summary>
internal sealed class TieqiEffectSource : IEffectSource
{
    public string SourceId => "tieqi";
    public string SourceType => "Skill";
    public string? DisplayName => "铁骑";
}

/// <summary>
/// Factory for creating TieqiSkill instances.
/// </summary>
public sealed class TieqiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new TieqiSkill();
    }
}
