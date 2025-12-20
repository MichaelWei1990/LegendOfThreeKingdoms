using System;
using System.Collections.Generic;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Basic, extensible implementation of <see cref="IActionResolutionMapper"/>.
/// It maintains an internal mapping from action identifiers to handler delegates
/// so that resolvers can be registered without changing the core engine code.
/// 
/// This class deliberately does not know about concrete resolver types yet;
/// those will be provided by higher-level modules once the resolution pipeline
/// is introduced.
/// </summary>
public sealed class ActionResolutionMapper : IActionResolutionMapper
{
    /// <summary>
    /// Delegate that performs the actual resolution logic for a given action.
    /// </summary>
    public delegate void ActionHandler(RuleContext context, ActionDescriptor action, ChoiceRequest? originalRequest, ChoiceResult? playerChoice);

    private readonly Dictionary<string, ActionHandler> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers or replaces the handler for the given action identifier.
    /// </summary>
    public void Register(string actionId, ActionHandler handler)
    {
        if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("Action id must be non-empty.", nameof(actionId));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        _handlers[actionId] = handler;
    }

    /// <summary>
    /// Attempts to remove a handler for the given action identifier.
    /// </summary>
    public bool Unregister(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("Action id must be non-empty.", nameof(actionId));
        return _handlers.Remove(actionId);
    }

    /// <inheritdoc />
    public void Resolve(RuleContext context, ActionDescriptor action, ChoiceRequest? originalRequest, ChoiceResult? playerChoice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (!_handlers.TryGetValue(action.ActionId, out var handler))
        {
            throw new InvalidOperationException(
                $"No action resolution handler registered for action id '{action.ActionId}'.");
        }

        handler(context, action, originalRequest, playerChoice);
    }
}
