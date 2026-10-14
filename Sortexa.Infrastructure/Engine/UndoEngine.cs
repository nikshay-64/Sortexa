using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;
using Sortexa.Infrastructure.Filesystem;

namespace Sortexa.Infrastructure.Engine;

public sealed class UndoEngine : IUndoEngine
{
    private readonly SafeFileMover _fileMover;
    private readonly IJournalRepository _journalRepository;
    private readonly ILoggerService? _logger;

    public UndoEngine(
        IJournalRepository journalRepository,
        ILoggerService? logger = null,
        SafeFileMover? fileMover = null)
    {
        _journalRepository = journalRepository;
        _logger = logger;
        _fileMover = fileMover ?? new SafeFileMover(logger);
    }

    public async Task<UndoResult> UndoOperationAsync(
        JournalEntry journalEntry,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (journalEntry == null || journalEntry.CompletedMoves.Count == 0)
        {
            return new UndoResult
            {
                OperationId = journalEntry?.OperationId ?? Guid.Empty,
                IsSuccess = false,
                FailureReason = "No recorded moves to reverse."
            };
        }

        if (journalEntry.IsUndone)
        {
            return new UndoResult
            {
                OperationId = journalEntry.OperationId,
                IsSuccess = false,
                FailureReason = "This operation has already been undone."
            };
        }

        int total = journalEntry.CompletedMoves.Count;
        int restored = 0;
        int failed = 0;

        _logger?.LogInfo($"Starting undo for operation {journalEntry.OperationId}: {total} moves to reverse.");

        // Reverse in LIFO order
        var movesToReverse = journalEntry.CompletedMoves.AsEnumerable().Reverse().ToList();

        for (int i = 0; i < movesToReverse.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var move = movesToReverse[i];
            string currentPath = move.DestinationPath;
            string originalPath = move.SourcePath;
            string fileName = Path.GetFileName(originalPath);

            double percent = (double)i / total * 100.0;
            progress?.Report(new ExecutionProgress
            {
                CurrentFile = fileName,
                Percentage = Math.Clamp(percent, 0.0, 100.0),
                ProcessedCount = i,
                TotalCount = total,
                MovedCount = restored,
                SkippedCount = 0,
                FailedCount = failed,
                StatusMessage = $"Restoring {fileName}..."
            });

            if (!File.Exists(currentPath))
            {
                _logger?.LogWarning($"Undo skipped missing file: {currentPath}");
                failed++;
                continue;
            }

            // Restore back to original path, using rename with counter if original already has a new file
            var outcome = await _fileMover.MoveFileSafelyAsync(
                currentPath,
                originalPath,
                DuplicateResolutionPolicy.RenameWithCounter,
                cancellationToken);

            if (outcome.Status == OperationStatus.Moved)
            {
                restored++;
            }
            else
            {
                failed++;
                _logger?.LogWarning($"Undo failed for {currentPath}: {outcome.ErrorMessage}");
            }
        }

        // Clean up empty destination subdirectories if applicable
        CleanEmptyDirectories(journalEntry.CompletedMoves.Select(m => Path.GetDirectoryName(m.DestinationPath)).Distinct());

        // Mark journal entry as undone if at least partially restored
        if (restored > 0)
        {
            journalEntry.IsUndone = true;
            journalEntry.UndoneAt = DateTime.UtcNow;
            try
            {
                await _journalRepository.UpdateJournalEntryAsync(journalEntry);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to update journal entry after undo: {ex.Message}", ex);
            }
        }

        progress?.Report(new ExecutionProgress
        {
            CurrentFile = "Restoration Complete",
            Percentage = 100.0,
            ProcessedCount = total,
            TotalCount = total,
            MovedCount = restored,
            SkippedCount = 0,
            FailedCount = failed,
            StatusMessage = $"Undo finished: {restored} restored, {failed} failed."
        });

        return new UndoResult
        {
            OperationId = journalEntry.OperationId,
            IsSuccess = restored > 0 && failed == 0,
            RestoredCount = restored,
            FailedCount = failed,
            TotalCount = total
        };
    }

    private static void CleanEmptyDirectories(System.Collections.Generic.IEnumerable<string?> directories)
    {
        foreach (var dir in directories)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                continue;

            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
            catch
            {
                // Non-critical cleanup
            }
        }
    }
}
