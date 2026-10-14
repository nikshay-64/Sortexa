using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Sortexa.Core.Models;
using Sortexa.Infrastructure.Engine;
using Sortexa.Infrastructure.Filesystem;
using Sortexa.Infrastructure.Persistence;
using Xunit;

namespace Sortexa.Tests.Safety;

public sealed class JournalAndUndoTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _journalDir;
    private readonly JournalRepository _journalRepo;
    private readonly UndoEngine _undoEngine;

    public JournalAndUndoTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Sortexa_JournalTests_" + Guid.NewGuid().ToString("N"));
        _journalDir = Path.Combine(_testRoot, "journal");
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_journalDir);

        _journalRepo = new JournalRepository(_journalDir);
        _undoEngine = new UndoEngine(_journalRepo);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task JournalPersistence_SavesAndRetrievesEntries()
    {
        var entry = new JournalEntry
        {
            SourceDirectory = _testRoot,
            Method = OrganizationMethod.SmartCategories,
            MethodName = "Smart Categories",
            ExaminedCount = 5,
            MovedCount = 4,
            SkippedCount = 1,
            FailedCount = 0,
            Duration = TimeSpan.FromSeconds(1.5),
            CompletedMoves = new List<OperationOutcome>
            {
                new()
                {
                    SourcePath = Path.Combine(_testRoot, "doc.pdf"),
                    DestinationPath = Path.Combine(_testRoot, "PDFs", "doc.pdf"),
                    Status = OperationStatus.Moved,
                    FileSizeBytes = 1024
                }
            }
        };

        await _journalRepo.SaveJournalEntryAsync(entry);

        var retrieved = await _journalRepo.GetEntryByIdAsync(entry.OperationId);
        Assert.NotNull(retrieved);
        Assert.Equal(entry.OperationId, retrieved.OperationId);
        Assert.Equal(4, retrieved.MovedCount);
        Assert.Single(retrieved.CompletedMoves);

        var all = await _journalRepo.GetAllEntriesAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task UndoOperation_RestoresFilesToOriginalLocation()
    {
        string originalFile = Path.Combine(_testRoot, "memo.txt");
        string organizedDir = Path.Combine(_testRoot, "Documents");
        Directory.CreateDirectory(organizedDir);
        string organizedFile = Path.Combine(organizedDir, "memo.txt");

        // Simulate file has been moved to organized folder
        await File.WriteAllTextAsync(organizedFile, "Secret memo contents");

        var journal = new JournalEntry
        {
            SourceDirectory = _testRoot,
            Method = OrganizationMethod.SmartCategories,
            MethodName = "Smart Categories",
            CompletedMoves = new List<OperationOutcome>
            {
                new()
                {
                    SourcePath = originalFile,
                    DestinationPath = organizedFile,
                    Status = OperationStatus.Moved,
                    FileSizeBytes = 20
                }
            }
        };

        await _journalRepo.SaveJournalEntryAsync(journal);

        // Run Undo
        var undoResult = await _undoEngine.UndoOperationAsync(journal);

        Assert.True(undoResult.IsSuccess);
        Assert.Equal(1, undoResult.RestoredCount);
        Assert.Equal(0, undoResult.FailedCount);

        // Verify file is back at original location and removed from organized directory
        Assert.True(File.Exists(originalFile));
        Assert.False(File.Exists(organizedFile));
        Assert.Equal("Secret memo contents", await File.ReadAllTextAsync(originalFile));

        // Verify journal marked as undone
        var updatedJournal = await _journalRepo.GetEntryByIdAsync(journal.OperationId);
        Assert.NotNull(updatedJournal);
        Assert.True(updatedJournal.IsUndone);
    }

    [Fact]
    public async Task UndoOperation_HandlesCollisionIfOriginalExists()
    {
        string originalPath = Path.Combine(_testRoot, "existing.txt");
        await File.WriteAllTextAsync(originalPath, "New conflicting file");

        string organizedDir = Path.Combine(_testRoot, "Documents");
        Directory.CreateDirectory(organizedDir);
        string organizedPath = Path.Combine(organizedDir, "existing.txt");
        await File.WriteAllTextAsync(organizedPath, "Restored content");

        var journal = new JournalEntry
        {
            SourceDirectory = _testRoot,
            Method = OrganizationMethod.SmartCategories,
            CompletedMoves = new List<OperationOutcome>
            {
                new()
                {
                    SourcePath = originalPath,
                    DestinationPath = organizedPath,
                    Status = OperationStatus.Moved,
                    FileSizeBytes = 16
                }
            }
        };

        var undoResult = await _undoEngine.UndoOperationAsync(journal);

        Assert.True(undoResult.IsSuccess);
        // Original conflicting file must remain untouched
        Assert.Equal("New conflicting file", await File.ReadAllTextAsync(originalPath));

        // Restored file must be placed with collision counter
        string collisionPath = Path.Combine(_testRoot, "existing (1).txt");
        Assert.True(File.Exists(collisionPath));
        Assert.Equal("Restored content", await File.ReadAllTextAsync(collisionPath));
    }
}
