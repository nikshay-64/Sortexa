using System;

namespace Sortexa.Core.Interfaces;

public interface IPathValidator
{
    bool IsValidTargetDirectory(string directoryPath, out string? errorMessage);
    bool IsSafeSubfolderName(string subfolderName, out string? sanitizedName);
    bool IsPathWithinRoot(string rootDirectory, string candidatePath);
    bool IsProtectedSystemDirectory(string directoryPath);
    bool IsApplicationDirectory(string directoryPath);
    string SanitizePathComponent(string component);
}
