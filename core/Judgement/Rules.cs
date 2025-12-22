using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Judgement;

/// <summary>
/// Judgement rule that succeeds when the card is red (Heart or Diamond).
/// </summary>
public sealed class RedJudgementRule : IJudgementRule
{
    /// <inheritdoc />
    public string Description => "红色判定成功";

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return card.Suit.IsRed();
    }
}

/// <summary>
/// Judgement rule that succeeds when the card is black (Spade or Club).
/// </summary>
public sealed class BlackJudgementRule : IJudgementRule
{
    /// <inheritdoc />
    public string Description => "黑色判定成功";

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return card.Suit.IsBlack();
    }
}

/// <summary>
/// Judgement rule that succeeds when the card matches a specific suit.
/// </summary>
public sealed class SuitJudgementRule : IJudgementRule
{
    private readonly Suit _requiredSuit;

    /// <summary>
    /// Creates a new suit judgement rule.
    /// </summary>
    /// <param name="requiredSuit">The suit that must match for the judgement to succeed.</param>
    public SuitJudgementRule(Suit requiredSuit)
    {
        _requiredSuit = requiredSuit;
    }

    /// <inheritdoc />
    public string Description => $"{_requiredSuit}判定成功";

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return card.Suit == _requiredSuit;
    }
}

/// <summary>
/// Judgement rule that succeeds when the card matches a specific rank.
/// </summary>
public sealed class RankJudgementRule : IJudgementRule
{
    private readonly int _requiredRank;

    /// <summary>
    /// Creates a new rank judgement rule.
    /// </summary>
    /// <param name="requiredRank">The rank that must match for the judgement to succeed.</param>
    public RankJudgementRule(int requiredRank)
    {
        if (requiredRank < 1 || requiredRank > 13)
            throw new ArgumentOutOfRangeException(nameof(requiredRank), "Rank must be between 1 and 13.");
        _requiredRank = requiredRank;
    }

    /// <inheritdoc />
    public string Description => $"{_requiredRank}点判定成功";

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return card.Rank == _requiredRank;
    }
}

/// <summary>
/// Judgement rule that succeeds when the card rank is within a range.
/// </summary>
public sealed class RankRangeJudgementRule : IJudgementRule
{
    private readonly int _minRank;
    private readonly int _maxRank;

    /// <summary>
    /// Creates a new rank range judgement rule.
    /// </summary>
    /// <param name="minRank">The minimum rank (inclusive).</param>
    /// <param name="maxRank">The maximum rank (inclusive).</param>
    public RankRangeJudgementRule(int minRank, int maxRank)
    {
        if (minRank < 1 || minRank > 13)
            throw new ArgumentOutOfRangeException(nameof(minRank), "Min rank must be between 1 and 13.");
        if (maxRank < 1 || maxRank > 13)
            throw new ArgumentOutOfRangeException(nameof(maxRank), "Max rank must be between 1 and 13.");
        if (minRank > maxRank)
            throw new ArgumentException("Min rank must be less than or equal to max rank.");

        _minRank = minRank;
        _maxRank = maxRank;
    }

    /// <inheritdoc />
    public string Description => $"{_minRank}-{_maxRank}点判定成功";

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return card.Rank >= _minRank && card.Rank <= _maxRank;
    }
}

/// <summary>
/// Logical operator for combining judgement rules.
/// </summary>
public enum JudgementRuleOperator
{
    /// <summary>
    /// All rules must succeed (AND).
    /// </summary>
    And,

    /// <summary>
    /// At least one rule must succeed (OR).
    /// </summary>
    Or
}

/// <summary>
/// Composite judgement rule that combines multiple rules with a logical operator.
/// </summary>
public sealed class CompositeJudgementRule : IJudgementRule
{
    private readonly IReadOnlyList<IJudgementRule> _rules;
    private readonly JudgementRuleOperator _operator;

    /// <summary>
    /// Creates a new composite judgement rule.
    /// </summary>
    /// <param name="rules">The rules to combine.</param>
    /// <param name="op">The logical operator (And or Or).</param>
    public CompositeJudgementRule(IReadOnlyList<IJudgementRule> rules, JudgementRuleOperator op)
    {
        if (rules is null || rules.Count == 0)
            throw new ArgumentException("Rules list cannot be null or empty.", nameof(rules));

        _rules = rules;
        _operator = op;
    }

    /// <inheritdoc />
    public string Description
    {
        get
        {
            var ruleDescriptions = _rules.Select(r => r.Description);
            var opStr = _operator == JudgementRuleOperator.And ? "且" : "或";
            return string.Join($" {opStr} ", ruleDescriptions);
        }
    }

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return _operator switch
        {
            JudgementRuleOperator.And => _rules.All(rule => rule.Evaluate(card)),
            JudgementRuleOperator.Or => _rules.Any(rule => rule.Evaluate(card)),
            _ => throw new InvalidOperationException($"Unknown operator: {_operator}")
        };
    }
}

/// <summary>
/// Judgement rule that succeeds when the card does NOT match the specified rule (negation).
/// </summary>
public sealed class NegatedJudgementRule : IJudgementRule
{
    private readonly IJudgementRule _innerRule;

    /// <summary>
    /// Creates a new negated judgement rule.
    /// </summary>
    /// <param name="innerRule">The rule to negate.</param>
    public NegatedJudgementRule(IJudgementRule innerRule)
    {
        _innerRule = innerRule ?? throw new ArgumentNullException(nameof(innerRule));
    }

    /// <inheritdoc />
    public string Description => $"非({_innerRule.Description})";

    /// <inheritdoc />
    public bool Evaluate(Card card)
    {
        return !_innerRule.Evaluate(card);
    }
}
