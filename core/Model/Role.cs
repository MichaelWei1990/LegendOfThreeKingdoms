namespace LegendOfThreeKingdoms.Core.Model;

/// <summary>
/// Role types in identity mode (身份局).
/// </summary>
public enum Role
{
    /// <summary>
    /// Lord (主公) - The emperor who must be protected.
    /// </summary>
    Lord,

    /// <summary>
    /// Loyalist (忠臣) - Loyal to the Lord, wins with the Lord.
    /// </summary>
    Loyalist,

    /// <summary>
    /// Rebel (反贼) - Opposes the Lord, wins when the Lord dies.
    /// </summary>
    Rebel,

    /// <summary>
    /// Renegade (内奸) - Wins by being the sole survivor.
    /// </summary>
    Renegade
}

/// <summary>
/// Constants for role identifiers used in Player.CampId.
/// </summary>
public static class RoleConstants
{
    /// <summary>
    /// Lord role identifier.
    /// </summary>
    public const string Lord = "Lord";

    /// <summary>
    /// Loyalist role identifier.
    /// </summary>
    public const string Loyalist = "Loyalist";

    /// <summary>
    /// Rebel role identifier.
    /// </summary>
    public const string Rebel = "Rebel";

    /// <summary>
    /// Renegade role identifier.
    /// </summary>
    public const string Renegade = "Renegade";
}

