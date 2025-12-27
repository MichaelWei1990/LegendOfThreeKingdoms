using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Tuxi (突袭) skill: obtains up to 2 hand cards from other players.
/// This resolver handles the execution of Tuxi after the player chooses to replace draw phase.
/// </summary>
public sealed class TuxiResolver : IResolver
{
    private readonly IReadOnlyList<int> _targetSeats;

    /// <summary>
    /// Creates a new TuxiResolver.
    /// </summary>
    /// <param name="targetSeats">The seats of the target players (up to 2).</param>
    public TuxiResolver(IReadOnlyList<int> targetSeats)
    {
        _targetSeats = targetSeats ?? throw new ArgumentNullException(nameof(targetSeats));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var cardMoveService = context.CardMoveService;

        // Process each target
        foreach (var targetSeat in _targetSeats)
        {
            var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);
            if (target is null || !target.IsAlive)
                continue; // Skip invalid or dead targets

            // Check if target has hand cards
            if (target.HandZone.Cards.Count == 0)
                continue; // Skip targets with no hand cards

            // Get card by index from target's hand collection (index 0 = first card)
            var handCards = target.HandZone.Cards;
            var cardIndex = 0; // Get the first card (index 0)
            var stolenCard = handCards[cardIndex];

            // Move card from target's hand to source player's hand
            try
            {
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: target.HandZone,
                    TargetZone: sourcePlayer.HandZone,
                    Cards: new[] { stolenCard },
                    Reason: CardMoveReason.Draw, // Using Draw reason for obtaining cards (similar to drawing)
                    Ordering: CardMoveOrdering.ToTop,
                    Game: game
                );

                cardMoveService.MoveSingle(moveDescriptor);

                // CardMovedEvent will be automatically published by the card move service
            }
            catch (Exception)
            {
                // If moving fails, log but continue with other targets
                // In production, we might want to log this error
                continue;
            }
        }

        return ResolutionResult.SuccessResult;
    }
}

