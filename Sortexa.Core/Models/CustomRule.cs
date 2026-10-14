using System;

namespace Sortexa.Core.Models;

public sealed record CustomRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string RuleName { get; init; } = "New Custom Rule";
    public string TargetSubfolder { get; init; } = "Custom";
    public RuleConditionField Field { get; init; } = RuleConditionField.Extension;
    public RuleConditionOperator Operator { get; init; } = RuleConditionOperator.Equals;
    public string Value { get; init; } = string.Empty;
    public bool IsCaseSensitive { get; init; } = false;
    public int Priority { get; init; } = 100; // Lower number = higher priority
    public bool IsEnabled { get; init; } = true;
}
