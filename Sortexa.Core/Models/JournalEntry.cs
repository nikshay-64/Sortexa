using System;
using System.Collections.Generic;

namespace Sortexa.Core.Models;

public sealed record JournalEntry
{
    public Guid OperationId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string SourceDirectory { get; init; }
    public OrganizationMethod Method { get; init; }
    public string MethodName { get; init; } = string.Empty;
    public List<OperationOutcome> CompletedMoves { get; init; } = new();
    public int ExaminedCount { get; set; }
    public int MovedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsUndone { get; set; }
    public DateTime? UndoneAt { get; set; }
}
