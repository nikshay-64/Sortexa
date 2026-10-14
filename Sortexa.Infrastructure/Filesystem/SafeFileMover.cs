using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Classification;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.Infrastructure.Filesystem;

public sealed class SafeFileMover
{
    private readonly ILoggerService? _logger;

    public SafeFileMover(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file.
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        using var sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Resolves collision by finding an unused filename in the target directory (e.g. file (1).ext).
    /// </summary>
    public static string ResolveCollision(string targetFilePath)
    {
        if (!File.Exists(targetFilePath))
            return targetFilePath;

        string directory = Path.GetDirectoryName(targetFilePath) ?? string.Empty;
        string fileName = Path.GetFileName(targetFilePath);
        string compoundExt = ExtensionHelper.GetNormalizedExtension(fileName);

        string baseName;
        string extension;

        if (!string.IsNullOrEmpty(compoundExt) && fileName.EndsWith(compoundExt, StringComparison.OrdinalIgnoreCase))
        {
            baseName = fileName.Substring(0, fileName.Length - compoundExt.Length);
            extension = compoundExt;
        }
        else
        {
            baseName = Path.GetFileNameWithoutExtension(fileName);
            extension = Path.GetExtension(fileName);
        }

        int counter = 1;
        while (true)
        {
            string candidateName = $"{baseName} ({counter}){extension}";
            string candidatePath = Path.Combine(directory, candidateName);

            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            counter++;
            if (counter > 10000)
            {
                // Fallback with GUID if extreme collision count
                return Path.Combine(directory, $"{baseName} ({Guid.NewGuid():N}){extension}");
            }
        }
    }

    /// <summary>
    /// Checks whether source and destination are on different volumes.
    /// </summary>
    public static bool IsCrossVolume(string sourcePath, string destinationPath)
    {
        try
        {
            string? sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
            string? destRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath));

            return !string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely moves a file from source to resolved destination.
    /// Supports both same-volume and cross-volume moves with SHA-256 verification.
    /// </summary>
    public async Task<OperationOutcome> MoveFileSafelyAsync(
        string sourcePath,
        string desiredDestinationPath,
        DuplicateResolutionPolicy duplicatePolicy = DuplicateResolutionPolicy.RenameWithCounter,
        CancellationToken cancellationToken = default)
    {
        // Safety check 1: Source must exist and not be a directory
        if (Directory.Exists(sourcePath))
        {
            return new OperationOutcome
            {
                SourcePath = sourcePath,
                DestinationPath = desiredDestinationPath,
                Status = OperationStatus.Skipped,
                ErrorMessage = "Target is a directory and cannot be moved as a file."
            };
        }

        if (!File.Exists(sourcePath))
        {
            return new OperationOutcome
            {
                SourcePath = sourcePath,
                DestinationPath = desiredDestinationPath,
                Status = OperationStatus.Failed,
                ErrorMessage = "Source file no longer exists."
            };
        }

        // Ensure target directory exists
        string? destDir = Path.GetDirectoryName(desiredDestinationPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Safety check 2: Collision handling
        string resolvedDestination = desiredDestinationPath;
        if (File.Exists(desiredDestinationPath))
        {
            if (duplicatePolicy == DuplicateResolutionPolicy.Skip)
            {
                return new OperationOutcome
                {
                    SourcePath = sourcePath,
                    DestinationPath = desiredDestinationPath,
                    Status = OperationStatus.Skipped,
                    ErrorMessage = "Destination file already exists (duplicate policy: Skip)."
                };
            }

            resolvedDestination = ResolveCollision(desiredDestinationPath);
        }

        long fileSizeBytes = new FileInfo(sourcePath).Length;
        bool crossVolume = IsCrossVolume(sourcePath, resolvedDestination);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!crossVolume)
            {
                // Same-volume move: atomic filesystem move
                File.Move(sourcePath, resolvedDestination);

                return new OperationOutcome
                {
                    SourcePath = sourcePath,
                    DestinationPath = resolvedDestination,
                    Status = OperationStatus.Moved,
                    IsCrossVolume = false,
                    FileSizeBytes = fileSizeBytes,
                    ExecutedAt = DateTime.UtcNow
                };
            }
            else
            {
                // Cross-volume safe operation:
                // 1. Copy to temporary destination
                string tempDest = resolvedDestination + $".sortexa_tmp_{Guid.NewGuid():N}";

                try
                {
                    using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                    using (var tempStream = new FileStream(tempDest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                    {
                        await sourceStream.CopyToAsync(tempStream, 65536, cancellationToken);
                    }

                    // 2. Verify copied byte count
                    var tempInfo = new FileInfo(tempDest);
                    if (tempInfo.Length != fileSizeBytes)
                    {
                        File.Delete(tempDest);
                        return new OperationOutcome
                        {
                            SourcePath = sourcePath,
                            DestinationPath = resolvedDestination,
                            Status = OperationStatus.Failed,
                            ErrorMessage = $"Cross-volume copy size mismatch (expected {fileSizeBytes}, got {tempInfo.Length}). Source preserved.",
                            IsCrossVolume = true,
                            FileSizeBytes = fileSizeBytes
                        };
                    }

                    // 3. Compute and compare SHA-256 hashes
                    string sourceHash = await ComputeSha256Async(sourcePath, cancellationToken);
                    string destHash = await ComputeSha256Async(tempDest, cancellationToken);

                    if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempDest);
                        return new OperationOutcome
                        {
                            SourcePath = sourcePath,
                            DestinationPath = resolvedDestination,
                            Status = OperationStatus.Failed,
                            ErrorMessage = "Cross-volume SHA-256 integrity check failed. Source file preserved.",
                            IsCrossVolume = true,
                            SourceSha256 = sourceHash,
                            DestinationSha256 = destHash,
                            FileSizeBytes = fileSizeBytes
                        };
                    }

                    // 4. Promote verified temporary file to final destination
                    File.Move(tempDest, resolvedDestination);

                    // 5. Only then delete source file
                    File.Delete(sourcePath);

                    return new OperationOutcome
                    {
                        SourcePath = sourcePath,
                        DestinationPath = resolvedDestination,
                        Status = OperationStatus.Moved,
                        IsCrossVolume = true,
                        SourceSha256 = sourceHash,
                        DestinationSha256 = destHash,
                        FileSizeBytes = fileSizeBytes,
                        ExecutedAt = DateTime.UtcNow
                    };
                }
                catch
                {
                    // Clean up temp file on failure
                    if (File.Exists(tempDest))
                    {
                        try { File.Delete(tempDest); } catch { }
                    }
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error moving file {sourcePath} to {resolvedDestination}: {ex.Message}", ex);
            return new OperationOutcome
            {
                SourcePath = sourcePath,
                DestinationPath = resolvedDestination,
                Status = OperationStatus.Failed,
                ErrorMessage = ex.Message,
                IsCrossVolume = crossVolume,
                FileSizeBytes = fileSizeBytes
            };
        }
    }
}
