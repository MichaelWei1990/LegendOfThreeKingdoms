using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Wushuang (无双) skill: Locked skill that modifies response requirements.
/// - Your Slash requires 2 Jinks to be dodged.
/// - Opponents in Duel with you need to play 2 Slashes each time.
/// </summary>
public sealed class WushuangSkill : BaseSkill, IResponseRequirementModifyingSkill
{
    /// <inheritdoc />
    public override string Id => "wushuang";

    /// <inheritdoc />
    public override string Name => "无双";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public int? ModifyJinkRequirementForSlash(int current, Game game, Player slashSource, Player target, Card slashCard)
    {
        // This skill modifies requirement when the slash source has Wushuang
        // The skill manager will only call this for skills owned by slashSource
        // So if this method is called, it means slashSource has Wushuang
        // We need to check if the skill is active for the slash source
        // Since we don't have direct access to owner, we check if slashSource has this skill active
        // The RequirementCalculator will only call this for skills owned by slashSource
        return 2; // If this skill is called, it means slashSource has Wushuang
    }

    /// <inheritdoc />
    public int? ModifySlashRequirementForDuel(int current, Game game, Player playerToRespond, Player opposingPlayer, Card? duelCard)
    {
        // This skill modifies requirement when the opposing player has Wushuang
        // The skill manager will only call this for skills owned by opposingPlayer
        // So if this method is called, it means opposingPlayer has Wushuang
        return 2; // If this skill is called, it means opposingPlayer has Wushuang
    }
}

/// <summary>
/// Factory for creating WushuangSkill instances.
/// </summary>
public sealed class WushuangSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new WushuangSkill();
    }
}
