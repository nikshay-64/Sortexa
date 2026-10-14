using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;
using Sortexa.Infrastructure.Filesystem;

namespace Sortexa.Infrastructure.Engine;

public sealed class ExecutionEngine : IExecutionEngine
{
    private readonly SafeFileMover _fileMover;
    private readonly IJournalRepository _journalRepository;
    private readonly ILoggerService? _logger;

    public ExecutionEngine(
        IJournalRepository journalRepository,
        ILoggerService? logger = null,
        SafeFileMover? fileMover = null)
    {
        _journalRepository = journalRepository;
        _logger = logger;
        _fileMover = fileMover ?? new SafeFileMover(logger);
    }

    public async Task<ExecutionResult> ExecutePlanAsync(
        OrganizationPlan plan,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var journalEntry = new JournalEntry
        {
            SourceDirectory = plan.SourceDirectory,
            Method = plan.Method,
            MethodName = plan.Method.ToString(),
            ExaminedCount = plan.TotalFiles
        };

        int processed = 0;
        int moved = 0;
        int skipped = 0;
        int failed = 0;
        bool isCancelled = false;
        string? failureReason = null;

        var eligibleOps = plan.Operations.Where(o => o.IsEligible).ToList();
        int totalOps = eligibleOps.Count;

        // Count initially skipped operations from planning
        int preSkipped = plan.Operations.Count(o => !o.IsEligible);
        skipped += preSkipped;

        _logger?.LogInfo($"Starting execution of plan: {totalOps} eligible operations in {plan.SourceDirectory}");

        try
        {
            foreach (var op in eligibleOps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    isCancelled = true;
                    _logger?.LogWarning("Operation execution was cancelled by the user.");
                    break;
                }

                processed++;
                string currentFileName = Path.GetFileName(op.SourcePath);

                // Report progress before starting item
                double percent = totalOps > 0 ? (double)(processed - 1) / totalOps * 100.0 : 0.0;
                progress?.Report(new ExecutionProgress
                {
                    CurrentFile = currentFileName,
                    Percentage = Math.Clamp(percent, 0.0, 100.0),
                    ProcessedCount = processed - 1,
                    TotalCount = totalOps,
                    MovedCount = moved,
                    SkippedCount = skipped,
                    FailedCount = failed,
                    StatusMessage = $"Moving {currentFileName}..."
                });

                var outcome = await _fileMover.MoveFileSafelyAsync(
                    op.SourcePath,
                    op.ProposedDestinationPath,
                    DuplicateResolutionPolicy.RenameWithCounter,
                    cancellationToken);

                if (outcome.Status == OperationStatus.Moved)
                {
                    moved++;
                    journalEntry.CompletedMoves.Add(outcome);
                }
                else if (outcome.Status == OperationStatus.Skipped)
                {
                    skipped++;
                }
                else
                {
                    failed++;
                    _logger?.LogWarning($"File operation failed for {op.SourcePath}: {outcome.ErrorMessage}");
                }

                // Final progress report for this item
                double itemPercent = totalOps > 0 ? (double)processed / totalOps * 100.0 : 100.0;
                progress?.Report(new ExecutionProgress
                {
                    CurrentFile = currentFileName,
                    Percentage = Math.Clamp(itemPercent, 0.0, 100.0),
                    ProcessedCount = processed,
                    TotalCount = totalOps,
                    MovedCount = moved,
                    SkippedCount = skipped,
                    FailedCount = failed,
                    StatusMessage = outcome.Status == OperationStatus.Moved ? $"Organized {currentFileName}" : $"Skipped/Failed {currentFileName}"
                });
            }
        }
        catch (OperationCanceledException)
        {
            isCancelled = true;
        }
        catch (Exception ex)
        {
            failed++;
            failureReason = ex.Message;
            _logger?.LogError($"Execution engine encountered unexpected error: {ex.Message}", ex);
        }
        finally
        {
            stopwatch.Stop();
        }

        journalEntry.Duration = stopwatch.Elapsed;
        journalEntry.ExaminedCount = plan.TotalFiles;
        journalEntry.MovedCount = moved;
        journalEntry.SkippedCount = skipped;
        journalEntry.FailedCount = failed;

        if (moved > 0)
        {
            try
            {
                await _journalRepository.SaveJournalEntryAsync(journalEntry);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to persist journal entry after move: {ex.Message}", ex);
            }
        }

        // Final 100% progress report
        progress?.Report(new ExecutionProgress
        {
            CurrentFile = isCancelled ? "Cancelled" : "Completed",
            Percentage = 100.0,
            ProcessedCount = processed,
            TotalCount = totalOps,
            MovedCount = moved,
            SkippedCount = skipped,
            FailedCount = failed,
            StatusMessage = isCancelled ? "Organization cancelled." : "Organization finished."
        });

        bool isSuccess = !isCancelled && failed == 0 && moved > 0;

        return new ExecutionResult
        {
            OperationId = journalEntry.OperationId,
            IsSuccess = isSuccess,
            IsCancelled = isCancelled,
            ExaminedCount = plan.TotalFiles,
            MovedCount = moved,
            SkippedCount = skipped,
            FailedCount = failed,
            Duration = stopwatch.Elapsed,
            FailureReason = failureReason,
            Journal = journalEntry
        };
    }
}
