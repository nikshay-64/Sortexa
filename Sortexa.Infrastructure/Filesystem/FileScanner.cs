using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Classification;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.Infrastructure.Filesystem;

public sealed class FileScanner : IFileScanner
{
    private readonly ILoggerService? _logger;

    public FileScanner(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<FileItem>> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<FileItem>>(() =>
        {
            var results = new List<FileItem>();

            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return results;
            }

            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);

                // Enumerate top-level files safely
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // Check attributes safely
                        bool isHidden = (file.Attributes & FileAttributes.Hidden) != 0;
                        bool isSystem = (file.Attributes & FileAttributes.System) != 0;

                        // Skip desktop.ini and thumbs.db by default if system
                        if (isSystem && (string.Equals(file.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(file.Name, "thumbs.db", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        DateTime? created = null;
                        DateTime? modified = null;
                        long size = 0;

                        try { created = file.CreationTime; } catch { }
                        try { modified = file.LastWriteTime; } catch { }
                        try { size = file.Length; } catch { }

                        string ext = ExtensionHelper.GetNormalizedExtension(file.Name);

                        results.Add(new FileItem
                        {
                            FullPath = file.FullName,
                            FileName = file.Name,
                            Extension = ext,
                            SizeBytes = size,
                            DateCreated = created,
                            DateModified = modified,
                            IsDirectory = false,
                            IsHidden = isHidden,
                            IsSystem = isSystem
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Skipped unreadable file: {file.Name} ({ex.Message})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error enumerating directory {directoryPath}: {ex.Message}", ex);
            }

            return results;
        }, cancellationToken);
    }
}
