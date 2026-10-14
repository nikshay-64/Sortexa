using System;
using System.Collections.Generic;
using System.Linq;
using Sortexa.Core.Classification;
using Sortexa.Core.Models;
using Xunit;

namespace Sortexa.Tests.Unit;

public sealed class ClassificationTests
{
    private readonly ClassificationRegistry _registry = new();

    [Theory]
    [InlineData("document.docx", "Documents")]
    [InlineData("notes.txt", "Documents")]
    [InlineData("readme.md", "Documents")]
    [InlineData("manual.pdf", "PDFs")]
    [InlineData("data.xlsx", "Spreadsheets and Data")]
    [InlineData("report.csv", "Spreadsheets and Data")]
    [InlineData("slides.pptx", "Presentations")]
    [InlineData("photo.jpg", "Images and Photos")]
    [InlineData("vector.svg", "Images and Photos")]
    [InlineData("clip.mp4", "Videos")]
    [InlineData("recording.wav", "Audio and Music")]
    [InlineData("archive.zip", "Archives and Compressed Files")]
    [InlineData("pack.7z", "Archives and Compressed Files")]
    [InlineData("book.epub", "E-books and Literature")]
    [InlineData("Program.cs", "Code and Development")]
    [InlineData("script.py", "Code and Development")]
    [InlineData("Setup.exe", "Applications and Installers")]
    [InlineData("installer.msi", "Applications and Installers")]
    [InlineData("disk.iso", "Disk Images and Virtual Disks")]
    [InlineData("store.sqlite", "Databases and Data Stores")]
    [InlineData("mesh.stl", "3D Models and CAD")]
    [InlineData("font.ttf", "Fonts")]
    [InlineData("artwork.psd", "Design and Creative Assets")]
    [InlineData("letter.eml", "Email and Mail Archives")]
    [InlineData("system.bak", "Backups and Export Files")]
    [InlineData("shortcut.lnk", "Shortcuts and Links")]
    public void ClassifiesCommonExtensionsCorrectly(string fileName, string expectedCategory)
    {
        string category = _registry.GetCategoryNameForFile(fileName, System.IO.Path.GetExtension(fileName));
        Assert.Equal(expectedCategory, category);
    }

    [Theory]
    [InlineData("bundle.tar.gz", ".tar.gz", "Archives and Compressed Files")]
    [InlineData("dist.tar.bz2", ".tar.bz2", "Archives and Compressed Files")]
    [InlineData("archive.tar.xz", ".tar.xz", "Archives and Compressed Files")]
    [InlineData("types.d.ts", ".d.ts", "Code and Development")]
    public void DetectsCompoundExtensionsCorrectly(string fileName, string expectedExt, string expectedCategory)
    {
        string detectedExt = ExtensionHelper.GetNormalizedExtension(fileName);
        Assert.Equal(expectedExt, detectedExt);

        string category = _registry.GetCategoryNameForFile(fileName, detectedExt);
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public void NormalizesCaseAndWhitespace()
    {
        string cat1 = _registry.GetCategoryNameForFile("PHOTO.PNG", ".PNG");
        string cat2 = _registry.GetCategoryNameForFile("photo.png", ".png");
        string cat3 = _registry.GetCategoryNameForFile("PHOTO.PnG", " .PNG ");

        Assert.Equal("Images and Photos", cat1);
        Assert.Equal(cat1, cat2);
        Assert.Equal(cat2, cat3);
    }

    [Fact]
    public void UnrecognizedExtensionsFallbackToOtherFiles()
    {
        string category = _registry.GetCategoryNameForFile("file.xyz123custom", ".xyz123custom");
        Assert.Equal("Other Files", category);
    }

    [Fact]
    public void ExtensionlessFilesFallbackToOtherFiles()
    {
        string category = _registry.GetCategoryNameForFile("LICENSE", "");
        Assert.Equal("Other Files", category);
    }

    [Fact]
    public void CanAddCustomExtensionToExistingCategory()
    {
        var categories = _registry.GetCategories().ToList();
        var codeCategory = categories.First(c => c.Id == "code");
        codeCategory.Extensions.Add(".zig");

        _registry.UpdateCategories(categories);

        string category = _registry.GetCategoryNameForFile("main.zig", ".zig");
        Assert.Equal("Code and Development", category);
    }

    [Fact]
    public void CanDisableCategory()
    {
        var categories = _registry.GetCategories().ToList();
        int idx = categories.FindIndex(c => c.Id == "pdfs");
        categories[idx] = categories[idx] with { IsEnabled = false };

        _registry.UpdateCategories(categories);

        string category = _registry.GetCategoryNameForFile("doc.pdf", ".pdf");
        Assert.Equal("Other Files", category);
    }

    [Fact]
    public void PurposeMappingProducesStandardSemanticGroups()
    {
        Assert.Equal("Applications and Installers", _registry.GetPurposeCategoryNameForFile("app.exe", ".exe"));
        Assert.Equal("Documents and Office Files", _registry.GetPurposeCategoryNameForFile("sheet.xlsx", ".xlsx"));
        Assert.Equal("Documents and Office Files", _registry.GetPurposeCategoryNameForFile("paper.pdf", ".pdf"));
        Assert.Equal("Media", _registry.GetPurposeCategoryNameForFile("picture.png", ".png"));
        Assert.Equal("Media", _registry.GetPurposeCategoryNameForFile("song.flac", ".flac"));
        Assert.Equal("Development and Source Code", _registry.GetPurposeCategoryNameForFile("code.cs", ".cs"));
        Assert.Equal("Archives and Backups", _registry.GetPurposeCategoryNameForFile("backup.zip", ".zip"));
        Assert.Equal("Disk Images and Virtual Machines", _registry.GetPurposeCategoryNameForFile("system.iso", ".iso"));
        Assert.Equal("Data and Databases", _registry.GetPurposeCategoryNameForFile("store.db", ".db"));
        Assert.Equal("Creative and Design Assets", _registry.GetPurposeCategoryNameForFile("design.psd", ".psd"));
    }
}
