using System;

namespace Sortexa.Core.Models;

public sealed record FileItem
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public long SizeBytes { get; init; }
    public DateTime? DateCreated { get; init; }
    public DateTime? DateModified { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsHidden { get; init; }
    public bool IsSystem { get; init; }
}
