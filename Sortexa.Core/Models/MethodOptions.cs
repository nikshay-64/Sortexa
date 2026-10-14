namespace Sortexa.Core.Models;

public sealed class MethodOptions
{
    // Date grouping options
    public DateGroupingFormat DateFormat { get; set; } = DateGroupingFormat.YearMonth;

    // Extension options
    public int ExtensionMinimumCountThreshold { get; set; } = 0; // 0 = disabled
    public bool GroupExtensionlessAsNoExtension { get; set; } = true;

    // File size bands (in bytes)
    public long TinyThresholdBytes { get; set; } = 100 * 1024; // 100 KB
    public long SmallThresholdBytes { get; set; } = 1024 * 1024; // 1 MB
    public long MediumThresholdBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
    public long LargeThresholdBytes { get; set; } = 1024 * 1024 * 1024; // 1 GB

    // Fallback directory names
    public string FallbackFolder { get; set; } = "Other Files";
    public string UnknownDateFolder { get; set; } = "Unknown Date";
    public string NoExtensionFolder { get; set; } = "No Extension";
    public string OtherExtensionsFolder { get; set; } = "Other Extensions";
}
