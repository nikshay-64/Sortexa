using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sortexa.Core.Classification;
using Sortexa.Core.Models;
using Sortexa.Core.Planning;
using Sortexa.Core.Validation;
using Xunit;

namespace Sortexa.Tests.Unit;

public sealed class PlanningTests
{
    private readonly OrganizationPlanner _planner;
    private readonly ClassificationRegistry _registry = new();
    private readonly string _dummyRoot = @"C:\Users\test\Downloads";

    public PlanningTests()
    {
        _planner = new OrganizationPlanner(new PathValidator());
    }

    [Fact]
    public void MethodA_SmartCategories_GeneratesAccuratePlan()
    {
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "report.pdf"), FileName = "report.pdf", Extension = ".pdf", SizeBytes = 1024 },
            new() { FullPath = Path.Combine(_dummyRoot, "photo.jpg"), FileName = "photo.jpg", Extension = ".jpg", SizeBytes = 2048 },
            new() { FullPath = Path.Combine(_dummyRoot, "archive.zip"), FileName = "archive.zip", Extension = ".zip", SizeBytes = 4096 }
        };

        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.SmartCategories, new MethodOptions(), _registry);

        Assert.Equal(3, plan.TotalFiles);
        Assert.Equal(3, plan.EligibleFiles);
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "report.pdf" && o.DestinationRelativePath == "PDFs");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "photo.jpg" && o.DestinationRelativePath == "Images and Photos");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "archive.zip" && o.DestinationRelativePath == "Archives and Compressed Files");
    }

    [Fact]
    public void MethodB_FileExtensions_GeneratesUppercaseExtensionFolders()
    {
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "manual.pdf"), FileName = "manual.pdf", Extension = ".pdf", SizeBytes = 100 },
            new() { FullPath = Path.Combine(_dummyRoot, "data.CSV"), FileName = "data.CSV", Extension = ".csv", SizeBytes = 100 },
            new() { FullPath = Path.Combine(_dummyRoot, "archive.tar.gz"), FileName = "archive.tar.gz", Extension = ".tar.gz", SizeBytes = 100 },
            new() { FullPath = Path.Combine(_dummyRoot, "NO_EXT"), FileName = "NO_EXT", Extension = "", SizeBytes = 100 }
        };

        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.FileExtensions, new MethodOptions(), _registry);

        Assert.Equal(4, plan.EligibleFiles);
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "manual.pdf" && o.DestinationRelativePath == "PDF");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "data.CSV" && o.DestinationRelativePath == "CSV");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "archive.tar.gz" && o.DestinationRelativePath == "TAR.GZ");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "NO_EXT" && o.DestinationRelativePath == "No Extension");
    }

    [Fact]
    public void MethodB_FileExtensions_ThresholdGroupsRareExtensions()
    {
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "doc1.txt"), FileName = "doc1.txt", Extension = ".txt", SizeBytes = 10 },
            new() { FullPath = Path.Combine(_dummyRoot, "doc2.txt"), FileName = "doc2.txt", Extension = ".txt", SizeBytes = 10 },
            new() { FullPath = Path.Combine(_dummyRoot, "doc3.txt"), FileName = "doc3.txt", Extension = ".txt", SizeBytes = 10 },
            new() { FullPath = Path.Combine(_dummyRoot, "rare.xyz"), FileName = "rare.xyz", Extension = ".xyz", SizeBytes = 10 }
        };

        var options = new MethodOptions { ExtensionMinimumCountThreshold = 2 };
        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.FileExtensions, options, _registry);

        Assert.Contains(plan.Operations, o => o.OriginalFileName == "doc1.txt" && o.DestinationRelativePath == "TXT");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "rare.xyz" && o.DestinationRelativePath == "Other Extensions");
    }

    [Theory]
    [InlineData(DateGroupingFormat.YearOnly, "2026")]
    [InlineData(DateGroupingFormat.YearMonth, @"2026\10 - October")]
    [InlineData(DateGroupingFormat.YearMonthDay, @"2026\10 - October\14")]
    public void MethodC_DateCreated_RespectsFormattingTiers(DateGroupingFormat format, string expectedSubfolder)
    {
        var date = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "test.txt"), FileName = "test.txt", Extension = ".txt", DateCreated = date, SizeBytes = 50 }
        };

        var options = new MethodOptions { DateFormat = format };
        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.DateCreated, options, _registry);

        Assert.Equal(expectedSubfolder, plan.Operations[0].DestinationRelativePath);
    }

    [Fact]
    public void MethodE_FileSize_ClassifiesBoundaryTiersCorrectly()
    {
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "tiny.txt"), FileName = "tiny.txt", Extension = ".txt", SizeBytes = 50 * 1024 }, // 50 KB -> Tiny
            new() { FullPath = Path.Combine(_dummyRoot, "small.dat"), FileName = "small.dat", Extension = ".dat", SizeBytes = 500 * 1024 }, // 500 KB -> Small
            new() { FullPath = Path.Combine(_dummyRoot, "med.zip"), FileName = "med.zip", Extension = ".zip", SizeBytes = 50 * 1024 * 1024 }, // 50 MB -> Medium
            new() { FullPath = Path.Combine(_dummyRoot, "large.iso"), FileName = "large.iso", Extension = ".iso", SizeBytes = 500 * 1024 * 1024 }, // 500 MB -> Large
            new() { FullPath = Path.Combine(_dummyRoot, "huge.vmdk"), FileName = "huge.vmdk", Extension = ".vmdk", SizeBytes = 2L * 1024 * 1024 * 1024 } // 2 GB -> Very Large
        };

        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.FileSize, new MethodOptions(), _registry);

        Assert.Contains(plan.Operations, o => o.OriginalFileName == "tiny.txt" && o.DestinationRelativePath.StartsWith("Tiny"));
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "small.dat" && o.DestinationRelativePath.StartsWith("Small"));
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "med.zip" && o.DestinationRelativePath.StartsWith("Medium"));
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "large.iso" && o.DestinationRelativePath.StartsWith("Large"));
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "huge.vmdk" && o.DestinationRelativePath.StartsWith("Very Large"));
    }

    [Fact]
    public void MethodF_FilePurpose_GroupsCorrectly()
    {
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "code.cs"), FileName = "code.cs", Extension = ".cs", SizeBytes = 100 },
            new() { FullPath = Path.Combine(_dummyRoot, "app.exe"), FileName = "app.exe", Extension = ".exe", SizeBytes = 100 }
        };

        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.FilePurpose, new MethodOptions(), _registry);

        Assert.Contains(plan.Operations, o => o.OriginalFileName == "code.cs" && o.DestinationRelativePath == "Development and Source Code");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "app.exe" && o.DestinationRelativePath == "Applications and Installers");
    }

    [Fact]
    public void MethodG_CustomRules_HonorsPriorityAndMatching()
    {
        var rules = new List<CustomRule>
        {
            new() { RuleName = "Invoices", Field = RuleConditionField.FileNameContains, Operator = RuleConditionOperator.Contains, Value = "Invoice", TargetSubfolder = "Finances", Priority = 10 },
            new() { RuleName = "2026 Reports", Field = RuleConditionField.FileNamePrefix, Operator = RuleConditionOperator.StartsWith, Value = "2026", TargetSubfolder = "Annual", Priority = 5 }
        };

        var files = new List<FileItem>
        {
            // Matches both rules, but Priority 5 (Annual) wins over Priority 10 (Finances)
            new() { FullPath = Path.Combine(_dummyRoot, "2026_Invoice_Q1.pdf"), FileName = "2026_Invoice_Q1.pdf", Extension = ".pdf", SizeBytes = 100 },
            // Matches only Invoice rule
            new() { FullPath = Path.Combine(_dummyRoot, "MyInvoice.pdf"), FileName = "MyInvoice.pdf", Extension = ".pdf", SizeBytes = 100 },
            // Matches neither rule
            new() { FullPath = Path.Combine(_dummyRoot, "Random.txt"), FileName = "Random.txt", Extension = ".txt", SizeBytes = 100 }
        };

        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.CustomRules, new MethodOptions(), _registry, rules);

        Assert.Contains(plan.Operations, o => o.OriginalFileName == "2026_Invoice_Q1.pdf" && o.DestinationRelativePath == "Annual");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "MyInvoice.pdf" && o.DestinationRelativePath == "Finances");
        Assert.Contains(plan.Operations, o => o.OriginalFileName == "Random.txt" && o.DestinationRelativePath == "Other Files");
    }

    [Fact]
    public void SkipsDirectoriesAndPreventsPathTraversal()
    {
        var files = new List<FileItem>
        {
            new() { FullPath = Path.Combine(_dummyRoot, "Subfolder"), FileName = "Subfolder", Extension = "", IsDirectory = true, SizeBytes = 0 },
            new() { FullPath = Path.Combine(_dummyRoot, "normal.txt"), FileName = "normal.txt", Extension = ".txt", IsDirectory = false, SizeBytes = 100 }
        };

        var plan = _planner.CreatePlan(_dummyRoot, files, OrganizationMethod.SmartCategories, new MethodOptions(), _registry);

        Assert.Equal(2, plan.TotalFiles);
        Assert.Equal(1, plan.EligibleFiles);
        Assert.Equal(1, plan.SkippedFiles);

        var dirOp = plan.Operations.First(o => o.OriginalFileName == "Subfolder");
        Assert.Equal(OperationStatus.Skipped, dirOp.Status);
        Assert.Contains("Directories cannot be moved", dirOp.SkipOrWarningReason);
    }
}
