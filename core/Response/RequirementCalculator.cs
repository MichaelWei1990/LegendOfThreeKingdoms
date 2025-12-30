using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Calculates the required number of response units for a response window.
/// </summary>
public static class ResponseRequirementCalculator
{
    /// <summary>
    /// Calculates the required number of Jink units for a Slash.
    /// </summary>
    public static int CalculateJinkRequirementForSlash(
        Game game,
        Player slashSource,
        Player target,
        Card slashCard,
        SkillManager? skillManager)
    {
        int requiredCount = 1; // Default requirement

        if (skillManager is not null)
        {
            var sourceSkills = skillManager.GetActiveSkills(game, slashSource);
            foreach (var skill in sourceSkills.OfType<IResponseRequirementModifyingSkill>())
            {
                var modified = skill.ModifyJinkRequirementForSlash(requiredCount, game, slashSource, target, slashCard);
                if (modified.HasValue)
                {
                    requiredCount = modified.Value;
                }
            }
        }

        return requiredCount;
    }

    /// <summary>
    /// Calculates the required number of Slash units for a Duel.
    /// </summary>
    public static int CalculateSlashRequirementForDuel(
        Game game,
        Player playerToRespond,
        Player opposingPlayer,
        Card? duelCard,
        SkillManager? skillManager)
    {
        int requiredCount = 1; // Default requirement

        if (skillManager is not null)
        {
            var opposingSkills = skillManager.GetActiveSkills(game, opposingPlayer);
            foreach (var skill in opposingSkills.OfType<IResponseRequirementModifyingSkill>())
            {
                var modified = skill.ModifySlashRequirementForDuel(requiredCount, game, playerToRespond, opposingPlayer, duelCard);
                if (modified.HasValue)
                {
                    requiredCount = modified.Value;
                }
            }
        }

        return requiredCount;
    }
}

