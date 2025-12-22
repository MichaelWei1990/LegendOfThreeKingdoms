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
    DefensiveHorse,

    // Trick card categories
    /// <summary>
    /// Immediate trick card (即时锦囊).
    /// </summary>
    ImmediateTrick,

    /// <summary>
    /// Delayed trick card (延时锦囊).
    /// </summary>
    DelayedTrick,

    // Specific immediate tricks
    /// <summary>
    /// 无中生有 (Wuzhong Shengyou).
    /// </summary>
    WuzhongShengyou,

    // Specific delayed tricks
    /// <summary>
    /// 乐不思蜀 (Le Bu Si Shu).
    /// </summary>
    Lebusishu,

    /// <summary>
    /// 闪电 (Shandian).
    /// </summary>
    Shandian
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

/// <summary>
/// Types of skills in the game.
/// </summary>
public enum SkillType
{
    /// <summary>
    /// Active skill: requires player to actively choose to activate.
    /// </summary>
    Active,
    
    /// <summary>
    /// Trigger skill: automatically triggers or asks player when specific events occur.
    /// </summary>
    Trigger,
    
    /// <summary>
    /// Locked skill: continuously active, no need to activate manually.
    /// </summary>
    Locked
}

/// <summary>
/// Capabilities that a skill can provide.
/// Used to express what a skill can do.
/// </summary>
[Flags]
public enum SkillCapability
{
    None = 0,
    
    /// <summary>
    /// Provides additional actions (e.g., extra play phase).
    /// </summary>
    ProvidesActions = 1 << 0,
    
    /// <summary>
    /// Modifies rule judgments (e.g., modifies usage count, target range).
    /// </summary>
    ModifiesRules = 1 << 1,
    
    /// <summary>
    /// Intervenes in resolution (e.g., modifies damage value, replaces effects).
    /// </summary>
    IntervenesResolution = 1 << 2,
    
    /// <summary>
    /// Initiates choice requests (e.g., select cards, select targets).
    /// </summary>
    InitiatesChoices = 1 << 3
}
