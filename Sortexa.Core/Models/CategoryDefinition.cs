using System.Collections.Generic;

namespace Sortexa.Core.Models;

public sealed record CategoryDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public string IconKey { get; init; } = "Folder";
    public string AccentColor { get; init; } = "#3987FF";
    public List<string> Extensions { get; init; } = new();
    public bool IsEnabled { get; init; } = true;
    public bool IsDefault { get; init; } = false;
}
