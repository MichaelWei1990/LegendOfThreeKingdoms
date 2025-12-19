using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Core interface for all resolvers in the resolution pipeline.
/// Resolvers are responsible for executing game state changes based on player actions.
/// </summary>
public interface IResolver
{
    /// <summary>
    /// Executes the resolution logic for this resolver.
    /// </summary>
    /// <param name="context">The resolution context containing game state and dependencies.</param>
    /// <returns>The result of the resolution, indicating success or failure with error details.</returns>
    ResolutionResult Resolve(ResolutionContext context);
}

/// <summary>
/// Context object passed to resolvers during execution.
/// Contains all necessary dependencies and state for resolution.
/// </summary>
public sealed record ResolutionContext(
    Game Game,
    Player SourcePlayer,
    ActionDescriptor? Action,
    ChoiceResult? Choice,
    IResolutionStack Stack,
    ICardMoveService CardMoveService,
    IRuleService RuleService
);

/// <summary>
/// Result of a resolver execution.
/// </summary>
public sealed record ResolutionResult(
    bool Success,
    ResolutionErrorCode? ErrorCode = null,
    string? MessageKey = null,
    object? Details = null
)
{
    /// <summary>
    /// A successful resolution result.
    /// </summary>
    public static readonly ResolutionResult SuccessResult = new(true);

    /// <summary>
    /// Creates a failure result with the specified error code.
    /// </summary>
    public static ResolutionResult Failure(ResolutionErrorCode code, string? messageKey = null, object? details = null) =>
        new(false, code, messageKey, details);
}

/// <summary>
/// Error codes for resolution failures.
/// </summary>
public enum ResolutionErrorCode
{
    None = 0,
    InvalidTarget,
    CardNotFound,
    TargetNotAlive,
    InvalidState,
    RuleValidationFailed
}

/// <summary>
/// Interface for managing the resolution stack during execution.
/// The stack tracks active resolvers and maintains execution history.
/// </summary>
public interface IResolutionStack
{
    /// <summary>
    /// Pushes a new resolver onto the stack for execution.
    /// </summary>
    /// <param name="resolver">The resolver to execute.</param>
    /// <param name="context">The context for the resolver execution.</param>
    void Push(IResolver resolver, ResolutionContext context);

    /// <summary>
    /// Pops and executes the next resolver from the stack.
    /// </summary>
    /// <returns>The result of the resolver execution.</returns>
    ResolutionResult Pop();

    /// <summary>
    /// Gets the execution history of completed resolvers.
    /// </summary>
    /// <returns>A read-only list of resolution records.</returns>
    IReadOnlyList<ResolutionRecord> GetHistory();

    /// <summary>
    /// Checks whether the stack is empty.
    /// </summary>
    bool IsEmpty { get; }
}

/// <summary>
/// Alias for IResolutionStack to match the plan naming.
/// </summary>
public interface ResolutionStack : IResolutionStack
{
}

/// <summary>
/// Record of a completed resolver execution.
/// Used for debugging, logging, and replay.
/// </summary>
public sealed record ResolutionRecord(
    Type ResolverType,
    ResolutionContext ContextSnapshot,
    ResolutionResult Result
);
