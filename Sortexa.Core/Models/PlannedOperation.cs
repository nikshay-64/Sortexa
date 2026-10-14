using System;

namespace Sortexa.Core.Models;

public sealed record PlannedOperation
{
    public required string SourcePath { get; init; }
    public required string OriginalFileName { get; init; }
    public required string DestinationRelativePath { get; init; }
    public required string ProposedDestinationPath { get; init; }
    public required string MatchedCategoryOrRule { get; init; }
    public OperationStatus Status { get; init; } = OperationStatus.Eligible;
    public string? SkipOrWarningReason { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime? DateModified { get; init; }

    public bool IsEligible => Status == OperationStatus.Eligible;
}
