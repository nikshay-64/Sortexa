using System;

namespace Sortexa.Core.Models;

public sealed record ExecutionProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public double Percentage { get; init; }
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
    public int MovedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed record ExecutionResult
{
    public Guid OperationId { get; init; }
    public bool IsSuccess { get; init; }
    public bool IsPartialSuccess => MovedCount > 0 && (FailedCount > 0 || IsCancelled);
    public bool IsCancelled { get; init; }
    public int ExaminedCount { get; init; }
    public int MovedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public TimeSpan Duration { get; init; }
    public string? FailureReason { get; init; }
    public JournalEntry? Journal { get; init; }
}

public sealed record UndoResult
{
    public Guid OperationId { get; init; }
    public bool IsSuccess { get; init; }
    public int RestoredCount { get; init; }
    public int FailedCount { get; init; }
    public int TotalCount { get; init; }
    public string? FailureReason { get; init; }
}
