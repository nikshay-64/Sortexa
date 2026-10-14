using System.Collections.Generic;
using Sortexa.Core.Models;

namespace Sortexa.Core.Interfaces;

public interface IClassificationRegistry
{
    IReadOnlyList<CategoryDefinition> GetCategories();
    CategoryDefinition? FindCategoryForExtension(string extension);
    string GetCategoryNameForFile(string fileName, string extension);
    string GetPurposeCategoryNameForFile(string fileName, string extension);
    void UpdateCategories(IEnumerable<CategoryDefinition> categories);
    void ResetToDefaults();
}
