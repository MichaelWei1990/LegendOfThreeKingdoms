using LegendOfThreeKingdoms.Core.Abstractions;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Base class for untargeted mass trick resolvers.
/// These tricks affect all players (or multiple players) without explicit targeting,
/// and cannot be nullified per individual target (单体无懈无效).
/// Examples: Taoyuan Jieyi (桃园结义), Wanjian Qifa (万箭齐发), Nanman Rushin (南蛮入侵).
/// </summary>
public abstract class UntargetedMassTrickResolverBase : IResolver
{
    /// <summary>
    /// Gets a value indicating whether this trick can be nullified per individual target.
    /// For untargeted mass tricks, this is typically false, meaning nullification
    /// cannot be used to cancel the effect on a single target.
    /// However, some mass tricks (like Wanjian Qifa and Nanman Rushin) can be nullified
    /// per target during their resolution phase.
    /// </summary>
    protected virtual bool CanBeNullifiedPerTarget => false;

    /// <inheritdoc />
    public abstract ResolutionResult Resolve(ResolutionContext context);
}
