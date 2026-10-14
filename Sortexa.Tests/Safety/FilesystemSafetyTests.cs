using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sortexa.Core.Models;
using Sortexa.Core.Validation;
using Sortexa.Infrastructure.Filesystem;
using Xunit;

namespace Sortexa.Tests.Safety;

public sealed class FilesystemSafetyTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SafeFileMover _mover = new();
    private readonly PathValidator _validator = new();

    public FilesystemSafetyTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Sortexa_SafetyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
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
    public async Task SameVolumeMove_MovesFileAndVerifiesContent()
    {
        string source = Path.Combine(_testRoot, "test.txt");
        string dest = Path.Combine(_testRoot, "subfolder", "test.txt");
        await File.WriteAllTextAsync(source, "Hello Sortexa Safety Test");

        var outcome = await _mover.MoveFileSafelyAsync(source, dest);

        Assert.Equal(OperationStatus.Moved, outcome.Status);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(dest));
        Assert.Equal("Hello Sortexa Safety Test", await File.ReadAllTextAsync(dest));
    }

    [Fact]
    public async Task CollisionResolution_NeverOverwritesExistingFile()
    {
        string subfolder = Path.Combine(_testRoot, "collision");
        Directory.CreateDirectory(subfolder);

        string existingFile = Path.Combine(subfolder, "document.pdf");
        await File.WriteAllTextAsync(existingFile, "Original Content");

        string sourceFile = Path.Combine(_testRoot, "document.pdf");
        await File.WriteAllTextAsync(sourceFile, "New Content");

        var outcome = await _mover.MoveFileSafelyAsync(sourceFile, existingFile, DuplicateResolutionPolicy.RenameWithCounter);

        Assert.Equal(OperationStatus.Moved, outcome.Status);
        // Original file must NOT be modified
        Assert.Equal("Original Content", await File.ReadAllTextAsync(existingFile));

        // New file must be saved under collision resolved path: document (1).pdf
        string expectedCollisionPath = Path.Combine(subfolder, "document (1).pdf");
        Assert.True(File.Exists(expectedCollisionPath));
        Assert.Equal("New Content", await File.ReadAllTextAsync(expectedCollisionPath));
    }

    [Fact]
    public void CollisionResolution_HandlesCompoundExtensionsProperly()
    {
        string subfolder = Path.Combine(_testRoot, "compound");
        Directory.CreateDirectory(subfolder);

        string existing = Path.Combine(subfolder, "archive.tar.gz");
        File.WriteAllText(existing, "Archive 1");

        string resolved = SafeFileMover.ResolveCollision(existing);
        Assert.Equal(Path.Combine(subfolder, "archive (1).tar.gz"), resolved);
    }

    [Fact]
    public async Task ComputesSha256Deterministically()
    {
        string file = Path.Combine(_testRoot, "hash.bin");
        byte[] bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        await File.WriteAllBytesAsync(file, bytes);

        string hash1 = await SafeFileMover.ComputeSha256Async(file);
        string hash2 = await SafeFileMover.ComputeSha256Async(file);

        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Validator_RejectsProtectedSystemDirectories()
    {
        string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        bool isValid = _validator.IsValidTargetDirectory(windowsPath, out string? error);

        Assert.False(isValid);
        Assert.Contains("protected", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_RejectsApplicationInstallationDirectory()
    {
        string appDir = AppContext.BaseDirectory;
        bool isValid = _validator.IsValidTargetDirectory(appDir, out string? error);

        Assert.False(isValid);
        Assert.Contains("Sortexa cannot organize its own", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_RejectsPathTraversalOutsideRoot()
    {
        string root = Path.Combine(_testRoot, "UserFiles");
        Directory.CreateDirectory(root);

        string badPath = Path.Combine(root, @"..\..\..\Windows\evil.txt");
        bool isWithin = _validator.IsPathWithinRoot(root, badPath);

        Assert.False(isWithin);
    }

    [Fact]
    public async Task DirectoryAsSource_IsSafelyRejected()
    {
        string dirAsSource = Path.Combine(_testRoot, "MyFolder");
        Directory.CreateDirectory(dirAsSource);
        string dest = Path.Combine(_testRoot, "DestFolder", "MyFolder");

        var outcome = await _mover.MoveFileSafelyAsync(dirAsSource, dest);

        Assert.Equal(OperationStatus.Skipped, outcome.Status);
        Assert.Contains("directory", outcome.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingSourceFile_FailsGracefullyWithoutCrashing()
    {
        string ghostFile = Path.Combine(_testRoot, "does_not_exist.txt");
        string dest = Path.Combine(_testRoot, "dest", "does_not_exist.txt");

        var outcome = await _mover.MoveFileSafelyAsync(ghostFile, dest);

        Assert.Equal(OperationStatus.Failed, outcome.Status);
        Assert.Contains("no longer exists", outcome.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
