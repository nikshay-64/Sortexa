namespace Sortexa.Core.Models;

public enum OrganizationMethod
{
    SmartCategories,
    FileExtensions,
    DateCreated,
    LastModified,
    FileSize,
    FilePurpose,
    CustomRules
}

public enum DateGroupingFormat
{
    YearOnly,       // e.g. "2026"
    YearMonth,      // e.g. "2026\10 - October"
    YearMonthDay    // e.g. "2026\10 - October\14"
}

public enum DuplicateResolutionPolicy
{
    RenameWithCounter, // "report (1).pdf"
    Skip
}

public enum OperationStatus
{
    Pending,
    Eligible,
    Moved,
    Skipped,
    Failed,
    Reversed
}

public enum RuleConditionField
{
    Extension,
    FileNamePrefix,
    FileNameSuffix,
    FileNameContains,
    FileNameRegex
}

public enum RuleConditionOperator
{
    Equals,
    StartsWith,
    EndsWith,
    Contains,
    MatchesRegex
}
