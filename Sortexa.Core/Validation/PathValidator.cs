using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sortexa.Core.Interfaces;

namespace Sortexa.Core.Validation;

public sealed class PathValidator : IPathValidator
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly char[] InvalidPathChars = Path.GetInvalidFileNameChars()
        .Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
        .Distinct()
        .ToArray();

    public bool IsValidTargetDirectory(string directoryPath, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            errorMessage = "Directory path cannot be empty.";
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(directoryPath);

            if (!Directory.Exists(fullPath))
            {
                errorMessage = "The specified directory does not exist.";
                return false;
            }

            // Check if directory is a root drive directly (e.g. C:\)
            var dirInfo = new DirectoryInfo(fullPath);
            if (dirInfo.Parent == null)
            {
                errorMessage = "Organizing a drive root directly is not permitted for safety.";
                return false;
            }

            if (IsProtectedSystemDirectory(fullPath))
            {
                errorMessage = "Windows system and program directories are protected and cannot be organized.";
                return false;
            }

            if (IsApplicationDirectory(fullPath))
            {
                errorMessage = "Sortexa cannot organize its own application installation directory.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid directory path: {ex.Message}";
            return false;
        }
    }

    public bool IsSafeSubfolderName(string subfolderName, out string? sanitizedName)
    {
        sanitizedName = SanitizePathComponent(subfolderName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "Folder";
            return false;
        }

        return string.Equals(subfolderName.Trim(), sanitizedName, StringComparison.Ordinal);
    }

    public bool IsPathWithinRoot(string rootDirectory, string candidatePath)
    {
        try
        {
            string normalizedRoot = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidatePath);

            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool IsProtectedSystemDirectory(string directoryPath)
    {
        try
        {
            string norm = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string[] protectedPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
            };

            foreach (var p in protectedPaths)
            {
                if (!string.IsNullOrEmpty(p))
                {
                    string target = Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(norm, target, StringComparison.OrdinalIgnoreCase) ||
                        norm.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return true; // fail safe
        }
    }

    public bool IsApplicationDirectory(string directoryPath)
    {
        try
        {
            string appDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetDir = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(appDir, targetDir, StringComparison.OrdinalIgnoreCase) ||
                   targetDir.StartsWith(appDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true; // fail safe
        }
    }

    public string SanitizePathComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
            return "Item";

        // Remove illegal characters
        string sanitized = component;
        foreach (char c in InvalidPathChars)
        {
            sanitized = sanitized.Replace(c.ToString(), "_");
        }

        // Collapse consecutive underscores or spaces
        sanitized = sanitized.Trim(' ', '.');

        // Check reserved device names
        string checkName = sanitized;
        int dotIndex = checkName.IndexOf('.');
        if (dotIndex >= 0)
        {
            checkName = checkName.Substring(0, dotIndex);
        }

        if (ReservedDeviceNames.Contains(checkName))
        {
            sanitized = "_" + sanitized;
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Item";
        }

        return sanitized;
    }
}
