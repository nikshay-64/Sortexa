using System;
using System.Collections.Generic;
using System.Linq;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.Core.Classification;

public sealed class ClassificationRegistry : IClassificationRegistry
{
    private readonly object _lock = new();
    private List<CategoryDefinition> _categories = new();
    private Dictionary<string, CategoryDefinition> _extensionMap = new(StringComparer.OrdinalIgnoreCase);

    public ClassificationRegistry()
    {
        ResetToDefaults();
    }

    public IReadOnlyList<CategoryDefinition> GetCategories()
    {
        lock (_lock)
        {
            return _categories.Select(c => c with { Extensions = new List<string>(c.Extensions) }).ToList();
        }
    }

    public CategoryDefinition? FindCategoryForExtension(string extension)
    {
        string norm = ExtensionHelper.GetNormalizedExtension("file" + (extension.StartsWith('.') ? extension : "." + extension));
        lock (_lock)
        {
            if (_extensionMap.TryGetValue(norm, out var cat) && cat.IsEnabled)
            {
                return cat;
            }
            return null;
        }
    }

    public string GetCategoryNameForFile(string fileName, string extension)
    {
        string norm = ExtensionHelper.GetNormalizedExtension(fileName);
        if (string.IsNullOrEmpty(norm) && !string.IsNullOrEmpty(extension))
        {
            norm = ExtensionHelper.GetNormalizedExtension("file" + (extension.StartsWith('.') ? extension : "." + extension));
        }

        lock (_lock)
        {
            if (_extensionMap.TryGetValue(norm, out var cat) && cat.IsEnabled)
            {
                return cat.DisplayName;
            }
            return "Other Files";
        }
    }

    public string GetPurposeCategoryNameForFile(string fileName, string extension)
    {
        string norm = ExtensionHelper.GetNormalizedExtension(fileName);
        if (string.IsNullOrEmpty(norm) && !string.IsNullOrEmpty(extension))
        {
            norm = ExtensionHelper.GetNormalizedExtension("file" + (extension.StartsWith('.') ? extension : "." + extension));
        }

        // Map extension or category to Purpose groups specified in Part 7 Method F:
        // 1. Applications and Installers
        // 2. Documents and Office Files
        // 3. Media
        // 4. Development and Source Code
        // 5. Archives and Backups
        // 6. Disk Images and Virtual Machines
        // 7. Data and Databases
        // 8. Creative and Design Assets

        lock (_lock)
        {
            if (_extensionMap.TryGetValue(norm, out var cat) && cat.IsEnabled)
            {
                switch (cat.Id)
                {
                    case "applications":
                        return "Applications and Installers";
                    case "documents":
                    case "spreadsheets":
                    case "presentations":
                    case "pdfs":
                    case "ebooks":
                    case "email":
                        return "Documents and Office Files";
                    case "images":
                    case "videos":
                    case "audio":
                        return "Media";
                    case "code":
                        return "Development and Source Code";
                    case "archives":
                    case "backups":
                        return "Archives and Backups";
                    case "diskimag":
                        return "Disk Images and Virtual Machines";
                    case "databases":
                        return "Data and Databases";
                    case "models3d":
                    case "fonts":
                    case "design":
                        return "Creative and Design Assets";
                    case "shortcuts":
                        return "Shortcuts and Utilities";
                    default:
                        return "Other Purposes";
                }
            }

            return "Other Purposes";
        }
    }

    public void UpdateCategories(IEnumerable<CategoryDefinition> categories)
    {
        lock (_lock)
        {
            _categories = categories.Select(c => c with { Extensions = c.Extensions.Select(e => ExtensionHelper.GetNormalizedExtension("file." + e.TrimStart('.'))).Distinct().ToList() }).ToList();
            RebuildIndex();
        }
    }

    public void ResetToDefaults()
    {
        lock (_lock)
        {
            _categories = GetDefaultCategories();
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        _extensionMap = new Dictionary<string, CategoryDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var cat in _categories)
        {
            foreach (var ext in cat.Extensions)
            {
                string norm = ExtensionHelper.GetNormalizedExtension("file" + (ext.StartsWith('.') ? ext : "." + ext));
                if (!_extensionMap.ContainsKey(norm))
                {
                    _extensionMap[norm] = cat;
                }
            }
        }
    }

    private static List<CategoryDefinition> GetDefaultCategories()
    {
        return new List<CategoryDefinition>
        {
            new()
            {
                Id = "documents",
                DisplayName = "Documents",
                Description = "Word documents, text files, markdown, and rich text",
                IconKey = "FileText",
                AccentColor = "#3987FF",
                Extensions = new() { ".doc", ".docx", ".txt", ".rtf", ".odt", ".pages", ".wpd", ".tex", ".md", ".markdown", ".rst" },
                IsDefault = true
            },
            new()
            {
                Id = "spreadsheets",
                DisplayName = "Spreadsheets and Data",
                Description = "Worksheets, tabular datasets, and comma-separated values",
                IconKey = "Table",
                AccentColor = "#20C997",
                Extensions = new() { ".xls", ".xlsx", ".xlsm", ".csv", ".tsv", ".ods", ".numbers", ".parquet" },
                IsDefault = true
            },
            new()
            {
                Id = "presentations",
                DisplayName = "Presentations",
                Description = "Slide decks and conference presentations",
                IconKey = "Presentation",
                AccentColor = "#FFB547",
                Extensions = new() { ".ppt", ".pptx", ".odp", ".key", ".pps", ".ppsx" },
                IsDefault = true
            },
            new()
            {
                Id = "pdfs",
                DisplayName = "PDFs",
                Description = "Portable Document Format publications and forms",
                IconKey = "FilePdf",
                AccentColor = "#FF6B81",
                Extensions = new() { ".pdf" },
                IsDefault = true
            },
            new()
            {
                Id = "images",
                DisplayName = "Images and Photos",
                Description = "Photographs, raster graphics, vector illustrations, and icons",
                IconKey = "Image",
                AccentColor = "#20D9F3",
                Extensions = new() { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".svg", ".raw", ".cr2", ".nef", ".heic", ".heif", ".ico" },
                IsDefault = true
            },
            new()
            {
                Id = "videos",
                DisplayName = "Videos",
                Description = "Movies, clips, screen recordings, and animations",
                IconKey = "Video",
                AccentColor = "#A855F7",
                Extensions = new() { ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".3gp", ".ts", ".vob" },
                IsDefault = true
            },
            new()
            {
                Id = "audio",
                DisplayName = "Audio and Music",
                Description = "Soundtracks, vocal recordings, audio stems, and music",
                IconKey = "Music",
                AccentColor = "#EC4899",
                Extensions = new() { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".aiff", ".alac", ".mid", ".midi" },
                IsDefault = true
            },
            new()
            {
                Id = "archives",
                DisplayName = "Archives and Compressed Files",
                Description = "Zip archives, tarballs, 7z packs, and compressed volumes",
                IconKey = "Archive",
                AccentColor = "#635BFF",
                Extensions = new() { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst", ".cab" },
                IsDefault = true
            },
            new()
            {
                Id = "ebooks",
                DisplayName = "E-books and Literature",
                Description = "Digital books, manuals, and comic publications",
                IconKey = "BookOpen",
                AccentColor = "#F59E0B",
                Extensions = new() { ".epub", ".mobi", ".azw", ".azw3", ".fb2", ".djvu", ".cbr", ".cbz" },
                IsDefault = true
            },
            new()
            {
                Id = "code",
                DisplayName = "Code and Development",
                Description = "Source code, scripts, configuration files, and schemas",
                IconKey = "Code",
                AccentColor = "#3B82F6",
                Extensions = new() { ".c", ".h", ".cpp", ".hpp", ".cs", ".py", ".js", ".ts", ".d.ts", ".jsx", ".tsx", ".html", ".htm", ".css", ".scss", ".sass", ".json", ".xml", ".yaml", ".yml", ".sql", ".java", ".kt", ".rs", ".go", ".php", ".rb", ".sh", ".bat", ".ps1", ".cmd", ".lua", ".swift" },
                IsDefault = true
            },
            new()
            {
                Id = "applications",
                DisplayName = "Applications and Installers",
                Description = "Executable binaries, setup packages, and installation bundles",
                IconKey = "Package",
                AccentColor = "#10B981",
                Extensions = new() { ".exe", ".msi", ".msix", ".appx", ".apk", ".dmg", ".pkg", ".deb", ".rpm" },
                IsDefault = true
            },
            new()
            {
                Id = "diskimag",
                DisplayName = "Disk Images and Virtual Disks",
                Description = "ISO disk images, virtual hard disks, and machine images",
                IconKey = "Disc",
                AccentColor = "#8B5CF6",
                Extensions = new() { ".iso", ".img", ".vhd", ".vhdx", ".vmdk", ".qcow2" },
                IsDefault = true
            },
            new()
            {
                Id = "databases",
                DisplayName = "Databases and Data Stores",
                Description = "Local database files and storage records",
                IconKey = "Database",
                AccentColor = "#06B6D4",
                Extensions = new() { ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb", ".mdf", ".sdf" },
                IsDefault = true
            },
            new()
            {
                Id = "models3d",
                DisplayName = "3D Models and CAD",
                Description = "Polygonal meshes, CAD designs, and 3D scenes",
                IconKey = "Box",
                AccentColor = "#F97316",
                Extensions = new() { ".stl", ".obj", ".fbx", ".glb", ".gltf", ".blend", ".step", ".stp", ".iges", ".igs", ".dwg", ".dxf", ".3ds" },
                IsDefault = true
            },
            new()
            {
                Id = "fonts",
                DisplayName = "Fonts",
                Description = "OpenType, TrueType, and web font typefaces",
                IconKey = "Type",
                AccentColor = "#64748B",
                Extensions = new() { ".ttf", ".otf", ".woff", ".woff2", ".eot", ".fon" },
                IsDefault = true
            },
            new()
            {
                Id = "design",
                DisplayName = "Design and Creative Assets",
                Description = "Layered compositions, vector project files, and design workspaces",
                IconKey = "PenTool",
                AccentColor = "#D946EF",
                Extensions = new() { ".psd", ".ai", ".xd", ".sketch", ".fig", ".cdr", ".eps", ".indd" },
                IsDefault = true
            },
            new()
            {
                Id = "email",
                DisplayName = "Email and Mail Archives",
                Description = "Saved messages and mailbox storage files",
                IconKey = "Mail",
                AccentColor = "#14B8A6",
                Extensions = new() { ".eml", ".msg", ".pst", ".ost", ".mbox" },
                IsDefault = true
            },
            new()
            {
                Id = "backups",
                DisplayName = "Backups and Export Files",
                Description = "Backup archives and system snapshots",
                IconKey = "Save",
                AccentColor = "#84CC16",
                Extensions = new() { ".bak", ".backup", ".dump", ".old", ".orig" },
                IsDefault = true
            },
            new()
            {
                Id = "shortcuts",
                DisplayName = "Shortcuts and Links",
                Description = "Windows shortcuts and web bookmarks",
                IconKey = "Link",
                AccentColor = "#94A3B8",
                Extensions = new() { ".lnk", ".url" },
                IsDefault = true
            },
            new()
            {
                Id = "other",
                DisplayName = "Other Files",
                Description = "Uncategorized or generic files",
                IconKey = "HelpCircle",
                AccentColor = "#6B7280",
                Extensions = new(),
                IsDefault = true
            }
        };
    }
}
