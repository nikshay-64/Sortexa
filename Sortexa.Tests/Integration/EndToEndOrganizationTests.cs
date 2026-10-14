using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sortexa.Core.Classification;
using Sortexa.Core.Models;
using Sortexa.Core.Planning;
using Sortexa.Core.Validation;
using Sortexa.Infrastructure.Engine;
using Sortexa.Infrastructure.Filesystem;
using Sortexa.Infrastructure.Persistence;
using Xunit;

namespace Sortexa.Tests.Integration;

public sealed class EndToEndOrganizationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _journalDir;
    private readonly FileScanner _scanner;
    private readonly ClassificationRegistry _registry;
    private readonly OrganizationPlanner _planner;
    private readonly JournalRepository _journalRepo;
    private readonly ExecutionEngine _engine;
    private readonly UndoEngine _undoEngine;

    public EndToEndOrganizationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Sortexa_E2E_" + Guid.NewGuid().ToString("N"));
        _journalDir = Path.Combine(_testRoot, "_journal");
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_journalDir);

        _scanner = new FileScanner();
        _registry = new ClassificationRegistry();
        _planner = new OrganizationPlanner(new PathValidator());
        _journalRepo = new JournalRepository(_journalDir);
        _engine = new ExecutionEngine(_journalRepo);
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
    public async Task CompleteWorkflow_Organizes150Files_AndRestoresViaUndo()
    {
        // 1. Generate 160 representative test files across categories
        var sampleExtensions = new[]
        {
            "docx", "txt", "rtf", "md",
            "pdf",
            "xlsx", "csv", "tsv",
            "pptx",
            "jpg", "png", "svg", "gif", "webp",
            "mp4", "mkv",
            "mp3", "wav", "flac",
            "zip", "7z", "tar.gz",
            "epub",
            "cs", "py", "js", "html", "json", "sql",
            "exe", "msi",
            "iso",
            "db", "sqlite",
            "stl", "obj",
            "ttf", "otf",
            "psd",
            "bak",
            "lnk",
            "customext"
        };

        var createdFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalToCreate = 160;

        for (int i = 1; i <= totalToCreate; i++)
        {
            string ext = sampleExtensions[(i - 1) % sampleExtensions.Length];
            string fileName = $"test_sample_{i:D3}.{ext}";
            string filePath = Path.Combine(_testRoot, fileName);

            // Write distinct test payload
            string payload = $"Test Payload for file #{i} ({fileName}) created at {DateTime.UtcNow:O}";
            await File.WriteAllTextAsync(filePath, payload);
            createdFiles.Add(fileName);
        }

        Assert.Equal(totalToCreate, createdFiles.Count);

        // 2. Scan directory
        var scannedFiles = await _scanner.ScanDirectoryAsync(_testRoot);
        Assert.Equal(totalToCreate, scannedFiles.Count);

        // 3. Generate organization plan using Smart Categories
        var plan = _planner.CreatePlan(_testRoot, scannedFiles, OrganizationMethod.SmartCategories, new MethodOptions(), _registry);

        Assert.Equal(totalToCreate, plan.TotalFiles);
        Assert.Equal(totalToCreate, plan.EligibleFiles);
        Assert.Equal(0, plan.SkippedFiles);

        // 4. Execute the confirmed operation
        var progress = new Progress<ExecutionProgress>();
        var result = await _engine.ExecutePlanAsync(plan, progress);

        // 5. Verify actual destinations and execution result
        Assert.True(result.IsSuccess, $"Execution failed: {result.FailureReason}");
        Assert.Equal(totalToCreate, result.MovedCount);
        Assert.Equal(0, result.FailedCount);

        // Verify root directory no longer contains the test files
        foreach (var fileName in createdFiles)
        {
            string rootPath = Path.Combine(_testRoot, fileName);
            Assert.False(File.Exists(rootPath), $"File {fileName} should have been moved from root.");
        }

        // Verify files exist in expected organized subfolders
        int foundInSubfolders = 0;
        foreach (var dir in Directory.GetDirectories(_testRoot))
        {
            if (Path.GetFileName(dir) == "_journal")
                continue;

            foundInSubfolders += Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
        }

        Assert.Equal(totalToCreate, foundInSubfolders);

        // 6. Undo the operation using the recorded journal
        Assert.NotNull(result.Journal);
        var undoResult = await _undoEngine.UndoOperationAsync(result.Journal);

        Assert.True(undoResult.IsSuccess, $"Undo failed: {undoResult.FailureReason}");
        Assert.Equal(totalToCreate, undoResult.RestoredCount);
        Assert.Equal(0, undoResult.FailedCount);

        // 7. Verify all files are restored back to the original root folder
        foreach (var fileName in createdFiles)
        {
            string restoredPath = Path.Combine(_testRoot, fileName);
            Assert.True(File.Exists(restoredPath), $"Restored file {fileName} must exist in root.");
        }
    }
}
