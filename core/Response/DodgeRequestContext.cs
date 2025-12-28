using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Context for tracking a Dodge (闪) request and its resolution.
/// Used to coordinate multiple Dodge providers (e.g., Hujia, Bagua Array, manual Dodge)
/// in a priority chain.
/// </summary>
public sealed class DodgeRequestContext
{
    /// <summary>
    /// The player who needs to provide Dodge.
    /// </summary>
    public Player Defender { get; }

    /// <summary>
    /// The player who triggered the Dodge requirement (e.g., attacker).
    /// </summary>
    public Player? Attacker { get; }

    /// <summary>
    /// The source event that triggered the Dodge requirement (e.g., Slash event).
    /// </summary>
    public object? SourceEvent { get; }

    /// <summary>
    /// Whether the Dodge request has been resolved.
    /// </summary>
    public bool Resolved { get; set; }

    /// <summary>
    /// The player who provided the Dodge (may be different from Defender for assistance skills).
    /// </summary>
    public Player? ProvidedBy { get; set; }

    /// <summary>
    /// The card that was used as Dodge (may be a virtual card from Bagua Array).
    /// </summary>
    public Card? ProvidedCard { get; set; }

    /// <summary>
    /// Whether a high-priority provider (e.g., Response Assistance) has been activated.
    /// When true, lower-priority providers should not execute.
    /// </summary>
    public bool HighPriorityProviderActivated { get; set; }

    /// <summary>
    /// Creates a new DodgeRequestContext.
    /// </summary>
    public DodgeRequestContext(Player defender, Player? attacker, object? sourceEvent)
    {
        Defender = defender ?? throw new ArgumentNullException(nameof(defender));
        Attacker = attacker;
        SourceEvent = sourceEvent;
        Resolved = false;
        ProvidedBy = null;
        ProvidedCard = null;
        HighPriorityProviderActivated = false;
    }
}

