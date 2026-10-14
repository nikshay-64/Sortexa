using System.Collections.Generic;

namespace Sortexa.Core.Models;

public sealed class SortexaSettings
{
    public string Theme { get; set; } = "System"; // "System", "Dark", "Light"
    public OrganizationMethod PreferredMethod { get; set; } = OrganizationMethod.SmartCategories;
    public DuplicateResolutionPolicy DuplicatePolicy { get; set; } = DuplicateResolutionPolicy.RenameWithCounter;
    public string FallbackCategoryName { get; set; } = "Other Files";
    public bool EnableLogging { get; set; } = true;

    // Optional customized category overrides
    public List<CategoryDefinition>? CustomCategories { get; set; }

    // User custom rules
    public List<CustomRule> CustomRules { get; set; } = new();
}
