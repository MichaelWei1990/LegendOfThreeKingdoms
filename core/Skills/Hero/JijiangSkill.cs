using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Jijiang (激将) skill: Lord skill, Trigger skill.
/// When you need to use or play Slash (杀), you can ask other Shu faction players
/// to play Slash on your behalf (considered as used/played by you).
/// </summary>
public sealed class JijiangSkill : BaseSkill, IResponseAssistanceSkill
{
    /// <inheritdoc />
    public override string Id => "jijiang";

    /// <inheritdoc />
    public override string Name => "激将";

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

        // Jijiang only works for Slash responses (Slash against Duel or Nanman Rushin)
        if (responseType != ResponseType.SlashAgainstDuel && responseType != ResponseType.SlashAgainstNanmanRushin)
            return false;

        // Check if there are any Shu faction assistants available
        var assistants = GetAssistants(game, owner);
        return assistants.Count > 0;
    }

    /// <summary>
    /// Checks whether this skill can provide assistance for active Slash usage.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>True if the skill can provide assistance for active Slash usage, false otherwise.</returns>
    public bool CanProvideAssistanceForUse(Game game, Player owner)
    {
        if (game is null || owner is null)
            return false;

        // Check if skill is active
        if (!IsActive(game, owner))
            return false;

        // Check if owner is Lord
        if (!owner.Flags.TryGetValue("IsLord", out var isLord) || isLord is not true)
            return false;

        // Check if there are any Shu faction assistants available
        var assistants = GetAssistants(game, owner);
        return assistants.Count > 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<Player> GetAssistants(Game game, Player owner)
    {
        if (game is null || owner is null)
            return Array.Empty<Player>();

        // Get all alive Shu faction players except the owner
        // Order by seat starting from owner's next seat
        var assistants = game.Players
            .Where(p => p.IsAlive 
                && p.FactionId?.ToLowerInvariant() == "shu" 
                && p.Seat != owner.Seat)
            .OrderBy(p => (p.Seat - owner.Seat + game.Players.Count) % game.Players.Count)
            .ToList();

        return assistants;
    }

    /// <inheritdoc />
    public bool ShouldActivate(Game game, Player owner, Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        if (game is null || owner is null || getPlayerChoice is null)
            return false;

        // Check if skill can provide assistance
        // We need to check for both possible response types
        var canProvideForDuel = CanProvideAssistance(game, owner, ResponseType.SlashAgainstDuel, null);
        var canProvideForNanman = CanProvideAssistance(game, owner, ResponseType.SlashAgainstNanmanRushin, null);
        
        if (!canProvideForDuel && !canProvideForNanman)
            return false;

        // Ask owner if they want to use Jijiang
        var request = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true // Owner can choose not to use Jijiang
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
/// Factory for creating JijiangSkill instances.
/// </summary>
public sealed class JijiangSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new JijiangSkill();
    }
}

