using System.Collections.Generic;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface IOrganizationPlanner
{
    OrganizationPlan CreatePlan(
        string sourceDirectory,
        IReadOnlyList<FileItem> files,
        OrganizationMethod method,
        MethodOptions options,
        IClassificationRegistry registry,
        IEnumerable<CustomRule>? customRules = null);
}
