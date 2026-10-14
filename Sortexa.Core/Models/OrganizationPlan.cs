using System;
using System.Collections.Generic;
using System.Linq;

namespace Sortexa.Core.Models;

public sealed record OrganizationPlan
{
    public required string SourceDirectory { get; init; }
    public OrganizationMethod Method { get; init; }
    public required MethodOptions Options { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required IReadOnlyList<PlannedOperation> Operations { get; init; }

    public int TotalFiles => Operations.Count;
    public int EligibleFiles => Operations.Count(o => o.IsEligible);
    public int SkippedFiles => Operations.Count(o => !o.IsEligible);
    public long TotalBytesEligible => Operations.Where(o => o.IsEligible).Sum(o => o.FileSizeBytes);

    public IReadOnlyDictionary<string, int> CategoryCounts =>
        Operations
            .Where(o => o.IsEligible)
            .GroupBy(o => o.MatchedCategoryOrRule)
            .ToDictionary(g => g.Key, g => g.Count());
}
