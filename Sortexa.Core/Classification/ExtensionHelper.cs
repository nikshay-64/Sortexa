using System;
using System.IO;

namespace Sortexa.Core.Classification;

public static class ExtensionHelper
{
    private static readonly string[] KnownCompoundExtensions = new[]
    {
        ".tar.gz",
        ".tar.bz2",
        ".tar.xz",
        ".tar.zst",
        ".user.js",
        ".d.ts"
    };

    /// <summary>
    /// Normalizes and extracts extension, checking for compound extensions like .tar.gz first.
    /// Returns lowercase extension with leading dot, or empty string if no extension.
    /// </summary>
    public static string GetNormalizedExtension(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
            return string.Empty;

        string fileName = Path.GetFileName(fileNameOrPath).Trim().ToLowerInvariant();

        foreach (var compound in KnownCompoundExtensions)
        {
            if (fileName.EndsWith(compound, StringComparison.OrdinalIgnoreCase))
            {
                return compound;
            }
        }

        string ext = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(ext) ? string.Empty : ext.ToLowerInvariant();
    }
}
