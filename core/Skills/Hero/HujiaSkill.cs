using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Hujia (护驾) skill: When you need to use or play Dodge (闪),
/// you can ask other Wei faction players to play Dodge on your behalf.
/// This is a Lord skill, only available when the owner is the Lord.
/// </summary>
public sealed class HujiaSkill : BaseSkill, ILordSkill, IResponseAssistanceSkill
{
    /// <inheritdoc />
    public override string Id => "hujia";

    /// <inheritdoc />
    public override string Name => "护驾";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <inheritdoc />
    public bool CanProvideAssistance(Game game, Player owner, ResponseType responseType, object? sourceEvent)
    {
        if (game is null || owner is null)
            return false;

        // Check if skill is active
        if (!IsActive(game, owner))
            return false;

        // Check if owner is Lord
        if (!owner.Flags.TryGetValue("IsLord", out var isLord) || isLord is not true)
            return false;

        // Hujia only works for Dodge responses (Jink against Slash or Wanjianqifa)
        if (responseType != ResponseType.JinkAgainstSlash && responseType != ResponseType.JinkAgainstWanjianqifa)
            return false;

        // Check if there are any Wei faction assistants available
        var assistants = GetAssistants(game, owner);
        return assistants.Count > 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<Player> GetAssistants(Game game, Player owner)
    {
        if (game is null || owner is null)
            return Array.Empty<Player>();

        // Get all alive Wei faction players except the owner
        var assistants = game.Players
            .Where(p => p.IsAlive 
                && p.FactionId == "wei" 
                && p.Seat != owner.Seat)
            .OrderBy(p => p.Seat)
            .ToList();

        return assistants;
    }

    /// <inheritdoc />
    public bool ShouldActivate(Game game, Player owner, Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        if (game is null || owner is null || getPlayerChoice is null)
            return false;

        // Check if skill can provide assistance
        if (!CanProvideAssistance(game, owner, ResponseType.JinkAgainstSlash, null))
            return false;

        // Ask owner if they want to use Hujia
        var request = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true // Owner can choose not to use Hujia
        );

        try
        {
            var result = getPlayerChoice(request);
            return result?.Confirmed == true;
        }
        catch
        {
            // If choice fails, don't activate
            return false;
        }
    }
}

/// <summary>
/// Factory for creating HujiaSkill instances.
/// </summary>
public sealed class HujiaSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new HujiaSkill();
    }
}

