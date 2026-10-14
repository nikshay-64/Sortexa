using System;

namespace Sortexa.Core.Models;

public sealed record OperationOutcome
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public OperationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsCrossVolume { get; init; }
    public string? SourceSha256 { get; init; }
    public string? DestinationSha256 { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
    public long FileSizeBytes { get; init; }
}
