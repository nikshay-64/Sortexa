using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sortexa.Core.Classification;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;
using Sortexa.Core.Validation;

namespace Sortexa.Core.Planning;

public sealed class OrganizationPlanner : IOrganizationPlanner
{
    private readonly IPathValidator _pathValidator;

    public OrganizationPlanner(IPathValidator? pathValidator = null)
    {
        _pathValidator = pathValidator ?? new PathValidator();
    }

    public OrganizationPlan CreatePlan(
        string sourceDirectory,
        IReadOnlyList<FileItem> files,
        OrganizationMethod method,
        MethodOptions options,
        IClassificationRegistry registry,
        IEnumerable<CustomRule>? customRules = null)
    {
        string normalizedSource = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var operations = new List<PlannedOperation>();

        // Precompute counts for Method B if threshold is enabled
        Dictionary<string, int>? extensionCounts = null;
        if (method == OrganizationMethod.FileExtensions && options.ExtensionMinimumCountThreshold > 0)
        {
            extensionCounts = files
                .Where(f => !f.IsDirectory)
                .GroupBy(f => GetCleanExtension(f.FileName, f.Extension))
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        }

        var activeRules = (customRules ?? Enumerable.Empty<CustomRule>())
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ToList();

        foreach (var file in files)
        {
            if (file.IsDirectory)
            {
                operations.Add(new PlannedOperation
                {
                    SourcePath = file.FullPath,
                    OriginalFileName = file.FileName,
                    DestinationRelativePath = string.Empty,
                    ProposedDestinationPath = file.FullPath,
                    MatchedCategoryOrRule = "Directory",
                    Status = OperationStatus.Skipped,
                    SkipOrWarningReason = "Directories cannot be moved as ordinary files.",
                    FileSizeBytes = 0,
                    DateModified = file.DateModified
                });
                continue;
            }

            // Determine target subfolder & matched category name
            var (targetSubfolder, matchedRuleName) = ResolveDestination(file, method, options, registry, activeRules, extensionCounts);

            // Sanitize subfolder name to prevent path traversal or reserved device names
            string sanitizedSubfolder = SanitizeSubfolderPath(targetSubfolder);

            string proposedDestination = Path.Combine(normalizedSource, sanitizedSubfolder, file.FileName);

            // Safety validation: Ensure destination remains strictly within source directory
            if (!_pathValidator.IsPathWithinRoot(normalizedSource, proposedDestination))
            {
                operations.Add(new PlannedOperation
                {
                    SourcePath = file.FullPath,
                    OriginalFileName = file.FileName,
                    DestinationRelativePath = sanitizedSubfolder,
                    ProposedDestinationPath = proposedDestination,
                    MatchedCategoryOrRule = matchedRuleName,
                    Status = OperationStatus.Skipped,
                    SkipOrWarningReason = "Destination path escapes the root directory (path traversal blocked).",
                    FileSizeBytes = file.SizeBytes,
                    DateModified = file.DateModified
                });
                continue;
            }

            // Check if file is already in the target location
            if (string.Equals(Path.GetFullPath(file.FullPath), Path.GetFullPath(proposedDestination), StringComparison.OrdinalIgnoreCase))
            {
                operations.Add(new PlannedOperation
                {
                    SourcePath = file.FullPath,
                    OriginalFileName = file.FileName,
                    DestinationRelativePath = sanitizedSubfolder,
                    ProposedDestinationPath = proposedDestination,
                    MatchedCategoryOrRule = matchedRuleName,
                    Status = OperationStatus.Skipped,
                    SkipOrWarningReason = "File is already in proposed category destination.",
                    FileSizeBytes = file.SizeBytes,
                    DateModified = file.DateModified
                });
                continue;
            }

            operations.Add(new PlannedOperation
            {
                SourcePath = file.FullPath,
                OriginalFileName = file.FileName,
                DestinationRelativePath = sanitizedSubfolder,
                ProposedDestinationPath = proposedDestination,
                MatchedCategoryOrRule = matchedRuleName,
                Status = OperationStatus.Eligible,
                SkipOrWarningReason = null,
                FileSizeBytes = file.SizeBytes,
                DateModified = file.DateModified
            });
        }

        return new OrganizationPlan
        {
            SourceDirectory = normalizedSource,
            Method = method,
            Options = options,
            CreatedAt = DateTime.UtcNow,
            Operations = operations
        };
    }

    private (string Subfolder, string MatchedRule) ResolveDestination(
        FileItem file,
        OrganizationMethod method,
        MethodOptions options,
        IClassificationRegistry registry,
        IReadOnlyList<CustomRule> activeRules,
        Dictionary<string, int>? extensionCounts)
    {
        switch (method)
        {
            case OrganizationMethod.SmartCategories:
                {
                    string category = registry.GetCategoryNameForFile(file.FileName, file.Extension);
                    return (category, category);
                }

            case OrganizationMethod.FileExtensions:
                {
                    string ext = GetCleanExtension(file.FileName, file.Extension);
                    if (string.IsNullOrEmpty(ext))
                    {
                        string fallback = options.GroupExtensionlessAsNoExtension ? options.NoExtensionFolder : options.FallbackFolder;
                        return (fallback, fallback);
                    }

                    if (extensionCounts != null &&
                        extensionCounts.TryGetValue(ext, out int count) &&
                        count < options.ExtensionMinimumCountThreshold)
                    {
                        return (options.OtherExtensionsFolder, options.OtherExtensionsFolder);
                    }

                    string folder = ext.ToUpperInvariant();
                    return (folder, folder);
                }

            case OrganizationMethod.DateCreated:
                {
                    if (!file.DateCreated.HasValue)
                        return (options.UnknownDateFolder, options.UnknownDateFolder);

                    return FormatDateSubfolder(file.DateCreated.Value, options.DateFormat);
                }

            case OrganizationMethod.LastModified:
                {
                    if (!file.DateModified.HasValue)
                        return (options.UnknownDateFolder, options.UnknownDateFolder);

                    return FormatDateSubfolder(file.DateModified.Value, options.DateFormat);
                }

            case OrganizationMethod.FileSize:
                {
                    long bytes = file.SizeBytes;
                    if (bytes < options.TinyThresholdBytes)
                    {
                        string band = "Tiny (under 100 KB)";
                        return (band, band);
                    }
                    if (bytes < options.SmallThresholdBytes)
                    {
                        string band = "Small (100 KB - 1 MB)";
                        return (band, band);
                    }
                    if (bytes < options.MediumThresholdBytes)
                    {
                        string band = "Medium (1 MB - 100 MB)";
                        return (band, band);
                    }
                    if (bytes < options.LargeThresholdBytes)
                    {
                        string band = "Large (100 MB - 1 GB)";
                        return (band, band);
                    }
                    string veryLarge = "Very Large (1 GB and above)";
                    return (veryLarge, veryLarge);
                }

            case OrganizationMethod.FilePurpose:
                {
                    string purpose = registry.GetPurposeCategoryNameForFile(file.FileName, file.Extension);
                    return (purpose, purpose);
                }

            case OrganizationMethod.CustomRules:
                {
                    foreach (var rule in activeRules)
                    {
                        if (MatchesRule(file, rule))
                        {
                            return (rule.TargetSubfolder, rule.RuleName);
                        }
                    }
                    return (options.FallbackFolder, $"{options.FallbackFolder} (Default)");
                }

            default:
                return (options.FallbackFolder, options.FallbackFolder);
        }
    }

    private static (string Subfolder, string MatchedRule) FormatDateSubfolder(DateTime date, DateGroupingFormat format)
    {
        string year = date.ToString("yyyy");
        string month = date.ToString("MM - MMMM");
        string day = date.ToString("dd");

        switch (format)
        {
            case DateGroupingFormat.YearOnly:
                return (year, year);
            case DateGroupingFormat.YearMonthDay:
                string fullDate = Path.Combine(year, month, day);
                return (fullDate, $"{year}/{month}/{day}");
            case DateGroupingFormat.YearMonth:
            default:
                string yearMonth = Path.Combine(year, month);
                return (yearMonth, $"{year}/{month}");
        }
    }

    private static string GetCleanExtension(string fileName, string extension)
    {
        string norm = ExtensionHelper.GetNormalizedExtension(fileName);
        if (string.IsNullOrEmpty(norm) && !string.IsNullOrEmpty(extension))
        {
            norm = ExtensionHelper.GetNormalizedExtension("file" + (extension.StartsWith('.') ? extension : "." + extension));
        }

        return norm.TrimStart('.');
    }

    private static bool MatchesRule(FileItem file, CustomRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Value))
            return false;

        StringComparison comp = rule.IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        string targetValue;
        switch (rule.Field)
        {
            case RuleConditionField.Extension:
                targetValue = GetCleanExtension(file.FileName, file.Extension);
                break;
            case RuleConditionField.FileNamePrefix:
            case RuleConditionField.FileNameSuffix:
            case RuleConditionField.FileNameContains:
            case RuleConditionField.FileNameRegex:
            default:
                targetValue = Path.GetFileNameWithoutExtension(file.FileName);
                break;
        }

        string pattern = rule.Value.Trim();
        if (rule.Field == RuleConditionField.Extension)
        {
            pattern = pattern.TrimStart('.');
        }

        switch (rule.Operator)
        {
            case RuleConditionOperator.Equals:
                return string.Equals(targetValue, pattern, comp);
            case RuleConditionOperator.StartsWith:
                return targetValue.StartsWith(pattern, comp);
            case RuleConditionOperator.EndsWith:
                return targetValue.EndsWith(pattern, comp);
            case RuleConditionOperator.Contains:
                return targetValue.IndexOf(pattern, comp) >= 0;
            case RuleConditionOperator.MatchesRegex:
                try
                {
                    var regexOpts = rule.IsCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    return Regex.IsMatch(file.FileName, pattern, regexOpts, TimeSpan.FromMilliseconds(250));
                }
                catch
                {
                    return false;
                }
            default:
                return false;
        }
    }

    private string SanitizeSubfolderPath(string subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
            return "Organized";

        // Handle possible sub-path components (e.g. "2026\10 - October")
        string[] parts = subfolder.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var sanitizedParts = new List<string>();

        foreach (var part in parts)
        {
            if (part == ".." || part == ".")
                continue;

            string sanitized = _pathValidator.SanitizePathComponent(part);
            sanitizedParts.Add(sanitized);
        }

        if (sanitizedParts.Count == 0)
            return "Organized";

        return string.Join(Path.DirectorySeparatorChar.ToString(), sanitizedParts);
    }
}
