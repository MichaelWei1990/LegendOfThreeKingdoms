namespace LegendOfThreeKingdoms.Core.Model;

/// <summary>
/// Card categories used by the core engine.
/// </summary>
public enum CardType
{
    Basic,
    Trick,
    Equip
}

/// <summary>
/// Fine-grained card subtypes (e.g. Slash, Dodge, Peach, Weapon, Armor, offensive/defensive horse).
/// This enum is intentionally incomplete and will be extended as content is added.
/// </summary>
public enum CardSubType
{
    Unknown = 0,
    Slash,
    Dodge,
    Peach,
    Weapon,
    Armor,

    /// <summary>
    /// 进攻马（-1 距离）。
    /// </summary>
    OffensiveHorse,

    /// <summary>
    /// 防御马（+1 距离）。
    /// </summary>
    DefensiveHorse
}

/// <summary>
/// Standard playing card suits.
/// </summary>
public enum Suit
{
    Spade,
    Heart,
    Club,
    Diamond
}

/// <summary>
/// Phases of a player's turn.
/// </summary>
public enum Phase
{
    None = 0,
    Start,
    Judge,
    Draw,
    Play,
    Discard,
    End
}

/// <summary>
/// High level camp categories; concrete semantics are still determined by the active game mode.
/// </summary>
public enum Camp
{
    Unknown = 0,
    Identity,
    Kingdom
}
