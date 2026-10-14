using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sortexa.App;
using Sortexa.App.Services;
using Sortexa.App.ViewModels;
using Sortexa.App.Views;
using Sortexa.Core.Classification;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;
using Sortexa.Core.Planning;
using Sortexa.Core.Validation;
using Sortexa.Infrastructure.Engine;
using Sortexa.Infrastructure.Filesystem;
using Sortexa.Infrastructure.Logging;
using Sortexa.Infrastructure.Persistence;
using Xunit;

namespace Sortexa.Tests.Integration;

public sealed class ScreenshotGeneratorTests
{
    private static readonly string ScreenshotOutputDir = @"C:\Users\niksh\screenshots";
    private static readonly string BrainScreenshotDir = @"C:\Users\niksh\.gemini\antigravity-cli\brain\ff528250-daf7-4eb6-8b66-a91dadd2b75b\screenshots";

    private static void EnsureWpfApplication(string themeResource = "Themes/DarkTheme.xaml")
    {
        if (Application.Current == null)
        {
            try
            {
                new Application();
            }
            catch (InvalidOperationException)
            {
                // Already created on another STA thread
            }
        }

        Application.Current!.Resources.MergedDictionaries.Clear();
        var dictionaries = new[]
        {
            "Styles/Colors.xaml",
            "Styles/Gradients.xaml",
            "Styles/Icons.xaml",
            "Styles/Typography.xaml",
            "Styles/Buttons.xaml",
            "Styles/Cards.xaml",
            "Styles/Controls.xaml",
            "Styles/Animations.xaml",
            themeResource
        };

        foreach (var dict in dictionaries)
        {
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Sortexa;component/{dict}", UriKind.RelativeOrAbsolute)
            });
        }

        Application.Current.Resources["BoolToVis"] = new System.Windows.Controls.BooleanToVisibilityConverter();
        Application.Current.Resources["StringToVis"] = Sortexa.App.Converters.StringToVisibilityConverter.Instance;
        Application.Current.Resources["InverseBoolConverter"] = Sortexa.App.Converters.InverseBooleanConverter.Instance;
        Application.Current.Resources["BytesConverter"] = new Sortexa.App.Converters.BytesFormattedConverter();
        Application.Current.Resources["StatusColorConverter"] = new Sortexa.App.Converters.StatusColorConverter();
    }

    private static void SaveVisualToPng(Visual visual, int width, int height, string filename)
    {
        Directory.CreateDirectory(ScreenshotOutputDir);
        Directory.CreateDirectory(BrainScreenshotDir);

        if (visual is FrameworkElement fe)
        {
            fe.Width = width;
            fe.Height = height;
            fe.Measure(new Size(width, height));
            fe.Arrange(new Rect(0, 0, width, height));
            fe.UpdateLayout();
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        string outPath1 = Path.Combine(ScreenshotOutputDir, filename);
        using (var fs1 = File.Create(outPath1))
        {
            encoder.Save(fs1);
        }

        string outPath2 = Path.Combine(BrainScreenshotDir, filename);
        File.Copy(outPath1, outPath2, overwrite: true);
    }

    [Fact]
    public void Generate_AllProductionScreenshots_EndToEnd()
    {
        Sortexa.Tests.Common.StaTestRunner.Run(() =>
        {
            EnsureWpfApplication("Themes/DarkTheme.xaml");

            var logger = new LoggerService();
            var registry = new ClassificationRegistry();
            var validator = new PathValidator();
            var planner = new OrganizationPlanner(validator);
            var scanner = new FileScanner();
            var journalRepo = new JournalRepository();
            var settingsRepo = new SettingsRepository();
            var engine = new ExecutionEngine(journalRepo);
            var undoEngine = new UndoEngine(journalRepo);
            var fakeDialog = new FakeDialogService();
            var themeService = new ThemeService();

            // 1. Splash Screen Window
            var splashWindow = new SplashScreenWindow();
            if (splashWindow.Content is FrameworkElement splashContent)
            {
                if (splashContent.FindName("AmbientGlow") is UIElement glow) glow.Opacity = 0.8;
                if (splashContent.FindName("LogoContainer") is UIElement logo) logo.Opacity = 1.0;
                if (splashContent.FindName("WordmarkPanel") is UIElement wm) wm.Opacity = 1.0;
                if (splashContent.FindName("TaglineText") is UIElement tl) tl.Opacity = 1.0;
                if (splashContent.FindName("AttributionPanel") is UIElement att) att.Opacity = 1.0;
                if (splashContent.FindName("SplashProgressBar") is ProgressBar pb) pb.Value = 75;

                SaveVisualToPng(splashContent, 580, 360, "01_SplashScreen.png");
            }

            // Create sample files
            var sampleFiles = new List<FileItem>
            {
                new() { FullPath = @"C:\Users\Nikshay\Downloads\financial_report_q3.pdf", FileName = "financial_report_q3.pdf", Extension = ".pdf", SizeBytes = 2540000, DateCreated = DateTime.Now, DateModified = DateTime.Now },
                new() { FullPath = @"C:\Users\Nikshay\Downloads\campus_photo_hdr.jpg", FileName = "campus_photo_hdr.jpg", Extension = ".jpg", SizeBytes = 4850000, DateCreated = DateTime.Now, DateModified = DateTime.Now },
                new() { FullPath = @"C:\Users\Nikshay\Downloads\Sortexa-Setup.exe", FileName = "Sortexa-Setup.exe", Extension = ".exe", SizeBytes = 43900000, DateCreated = DateTime.Now, DateModified = DateTime.Now },
                new() { FullPath = @"C:\Users\Nikshay\Downloads\dsp_lab_assignment.py", FileName = "dsp_lab_assignment.py", Extension = ".py", SizeBytes = 18500, DateCreated = DateTime.Now, DateModified = DateTime.Now },
                new() { FullPath = @"C:\Users\Nikshay\Downloads\telecom_project_dataset.zip", FileName = "telecom_project_dataset.zip", Extension = ".zip", SizeBytes = 145000000, DateCreated = DateTime.Now, DateModified = DateTime.Now },
                new() { FullPath = @"C:\Users\Nikshay\Downloads\circuit_schematic (1).png", FileName = "circuit_schematic (1).png", Extension = ".png", SizeBytes = 3200000, DateCreated = DateTime.Now, DateModified = DateTime.Now },
                new() { FullPath = @"C:\Users\Nikshay\Downloads\lecture_notes.docx", FileName = "lecture_notes.docx", Extension = ".docx", SizeBytes = 850000, DateCreated = DateTime.Now, DateModified = DateTime.Now }
            };

            // Setup Navigation and Factories
            DashboardViewModel dashboardVm = null!;
            MethodSelectionViewModel methodSelectVm = null!;
            MethodConfigViewModel configVm = null!;
            PreviewViewModel previewVm = null!;
            ProgressViewModel progressVm = null!;
            CompletionViewModel completionVm = null!;
            CategoriesViewModel categoriesVm = null!;
            HistoryViewModel historyVm = null!;
            SettingsViewModel settingsVm = null!;
            AboutViewModel aboutVm = null!;

            var navService = new NavigationService(type =>
            {
                if (type == typeof(DashboardViewModel)) return dashboardVm;
                if (type == typeof(MethodSelectionViewModel)) return methodSelectVm;
                if (type == typeof(MethodConfigViewModel)) return configVm;
                if (type == typeof(PreviewViewModel)) return previewVm;
                if (type == typeof(ProgressViewModel)) return progressVm;
                if (type == typeof(CompletionViewModel)) return completionVm;
                if (type == typeof(CategoriesViewModel)) return categoriesVm;
                if (type == typeof(HistoryViewModel)) return historyVm;
                if (type == typeof(SettingsViewModel)) return settingsVm;
                if (type == typeof(AboutViewModel)) return aboutVm;
                return Activator.CreateInstance(type)!;
            });

            dashboardVm = new DashboardViewModel(scanner, validator, fakeDialog, navService, () => methodSelectVm, logger);
            methodSelectVm = new MethodSelectionViewModel(navService, () => dashboardVm, () => configVm, () => previewVm, planner, registry);
            configVm = new MethodConfigViewModel(navService, () => methodSelectVm, () => previewVm, planner, registry, settingsRepo);
            previewVm = new PreviewViewModel(navService, fakeDialog, () => dashboardVm, () => configVm, () => progressVm);
            progressVm = new ProgressViewModel(engine, navService, () => completionVm, logger);
            completionVm = new CompletionViewModel(navService, fakeDialog, undoEngine, () => dashboardVm, logger);
            categoriesVm = new CategoriesViewModel(registry, settingsRepo, fakeDialog, logger);
            historyVm = new HistoryViewModel(journalRepo, undoEngine, fakeDialog, logger);
            settingsVm = new SettingsViewModel(settingsRepo, themeService, fakeDialog, logger);
            aboutVm = new AboutViewModel();

            var mainVm = new MainViewModel(
                navService,
                themeService,
                () => dashboardVm,
                () => historyVm,
                () => categoriesVm,
                () => settingsVm,
                () => aboutVm);

            var mainWindow = new MainWindow(mainVm);
            var rootVisual = (UIElement)mainWindow.Content;

            // 2. Dashboard View
            dashboardVm.SelectedFolderPath = @"C:\Users\Nikshay\Downloads";
            typeof(DashboardViewModel).GetProperty("FileCount", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(dashboardVm, new object[] { 160 });
            typeof(DashboardViewModel).GetProperty("TotalSizeBytes", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(dashboardVm, new object[] { 5177000000L });
            typeof(DashboardViewModel).GetProperty("FormattedSize", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(dashboardVm, new object[] { "4.82 GB" });
            typeof(DashboardViewModel).GetProperty("StatusMessage", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(dashboardVm, new object[] { "160 files detected · 4.82 GB · Ready for organization" });

            navService.NavigateTo(dashboardVm);
            SaveVisualToPng(rootVisual, 1200, 800, "02_Dashboard.png");

            // 3. Method Selection View
            methodSelectVm.Initialize(@"C:\Users\Nikshay\Downloads", sampleFiles);
            navService.NavigateTo(methodSelectVm);
            SaveVisualToPng(rootVisual, 1200, 800, "03_MethodSelection.png");

            // 4. Method Configuration View
            configVm.Initialize(@"C:\Users\Nikshay\Downloads", sampleFiles, OrganizationMethod.SmartCategories);
            navService.NavigateTo(configVm);
            SaveVisualToPng(rootVisual, 1200, 800, "04_MethodConfig.png");

            // 5. Preview View
            var plannedOps = new List<PlannedOperation>
            {
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\financial_report_q3.pdf",
                    OriginalFileName = "financial_report_q3.pdf",
                    DestinationRelativePath = @"Documents\financial_report_q3.pdf",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Documents\financial_report_q3.pdf",
                    MatchedCategoryOrRule = "Documents",
                    Status = OperationStatus.Eligible,
                    FileSizeBytes = 2540000,
                    DateModified = DateTime.Now
                },
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\campus_photo_hdr.jpg",
                    OriginalFileName = "campus_photo_hdr.jpg",
                    DestinationRelativePath = @"Images\campus_photo_hdr.jpg",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Images\campus_photo_hdr.jpg",
                    MatchedCategoryOrRule = "Images",
                    Status = OperationStatus.Eligible,
                    FileSizeBytes = 4850000,
                    DateModified = DateTime.Now
                },
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\Sortexa-Setup.exe",
                    OriginalFileName = "Sortexa-Setup.exe",
                    DestinationRelativePath = @"Executables\Sortexa-Setup.exe",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Executables\Sortexa-Setup.exe",
                    MatchedCategoryOrRule = "Executables",
                    Status = OperationStatus.Eligible,
                    FileSizeBytes = 43900000,
                    DateModified = DateTime.Now
                },
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\dsp_lab_assignment.py",
                    OriginalFileName = "dsp_lab_assignment.py",
                    DestinationRelativePath = @"Code\dsp_lab_assignment.py",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Code\dsp_lab_assignment.py",
                    MatchedCategoryOrRule = "Code",
                    Status = OperationStatus.Eligible,
                    FileSizeBytes = 18500,
                    DateModified = DateTime.Now
                },
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\telecom_project_dataset.zip",
                    OriginalFileName = "telecom_project_dataset.zip",
                    DestinationRelativePath = @"Archives\telecom_project_dataset.zip",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Archives\telecom_project_dataset.zip",
                    MatchedCategoryOrRule = "Archives",
                    Status = OperationStatus.Eligible,
                    FileSizeBytes = 145000000,
                    DateModified = DateTime.Now
                },
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\circuit_schematic (1).png",
                    OriginalFileName = "circuit_schematic (1).png",
                    DestinationRelativePath = @"Images\circuit_schematic (1) (1).png",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Images\circuit_schematic (1) (1).png",
                    MatchedCategoryOrRule = "Images",
                    Status = OperationStatus.Eligible,
                    SkipOrWarningReason = "Destination collision resolved by counter rename",
                    FileSizeBytes = 3200000,
                    DateModified = DateTime.Now
                },
                new()
                {
                    SourcePath = @"C:\Users\Nikshay\Downloads\lecture_notes.docx",
                    OriginalFileName = "lecture_notes.docx",
                    DestinationRelativePath = @"Documents\lecture_notes.docx",
                    ProposedDestinationPath = @"C:\Users\Nikshay\Downloads\Documents\lecture_notes.docx",
                    MatchedCategoryOrRule = "Documents",
                    Status = OperationStatus.Eligible,
                    FileSizeBytes = 850000,
                    DateModified = DateTime.Now
                }
            };

            var plan = new OrganizationPlan
            {
                SourceDirectory = @"C:\Users\Nikshay\Downloads",
                Method = OrganizationMethod.SmartCategories,
                Options = new MethodOptions(),
                Operations = plannedOps
            };
            previewVm.Initialize(plan);
            navService.NavigateTo(previewVm);
            SaveVisualToPng(rootVisual, 1200, 800, "05_Preview.png");

            // 6. Progress View
            typeof(ProgressViewModel).GetProperty("Percentage", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(progressVm, new object[] { 68.0 });
            typeof(ProgressViewModel).GetProperty("ProcessedCount", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(progressVm, new object[] { 108 });
            typeof(ProgressViewModel).GetProperty("TotalCount", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(progressVm, new object[] { 160 });
            typeof(ProgressViewModel).GetProperty("MovedCount", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(progressVm, new object[] { 108 });
            typeof(ProgressViewModel).GetProperty("CurrentFile", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(progressVm, new object[] { @"C:\Users\Nikshay\Downloads\dsp_lab_assignment.py" });
            typeof(ProgressViewModel).GetProperty("StatusMessage", BindingFlags.Public | BindingFlags.Instance)!
                .GetSetMethod(true)?.Invoke(progressVm, new object[] { "Moving and verifying SHA-256 integrity: 108 / 160 items completed" });

            navService.NavigateTo(progressVm);
            SaveVisualToPng(rootVisual, 1200, 800, "06_Progress.png");

            // 7. Completion View
            var execResult = new ExecutionResult
            {
                OperationId = Guid.NewGuid(),
                IsSuccess = true,
                ExaminedCount = 160,
                MovedCount = 160,
                SkippedCount = 0,
                FailedCount = 0,
                Duration = TimeSpan.FromSeconds(1.42),
                Journal = new JournalEntry
                {
                    SourceDirectory = @"C:\Users\Nikshay\Downloads",
                    Method = OrganizationMethod.SmartCategories,
                    MethodName = "Smart Categories",
                    ExaminedCount = 160,
                    MovedCount = 160,
                    SkippedCount = 0,
                    FailedCount = 0,
                    Duration = TimeSpan.FromSeconds(1.42)
                }
            };
            completionVm.Initialize(execResult);
            navService.NavigateTo(completionVm);
            SaveVisualToPng(rootVisual, 1200, 800, "07_Completion.png");

            // 8. Categories View
            navService.NavigateTo(categoriesVm);
            SaveVisualToPng(rootVisual, 1200, 800, "08_Categories.png");

            // 9. History View
            historyVm.Entries.Clear();
            historyVm.Entries.Add(new JournalEntry
            {
                SourceDirectory = @"C:\Users\Nikshay\Downloads",
                Method = OrganizationMethod.SmartCategories,
                MethodName = "Smart Categories",
                ExaminedCount = 160,
                MovedCount = 160,
                SkippedCount = 0,
                FailedCount = 0,
                Timestamp = DateTime.UtcNow.AddMinutes(-15),
                Duration = TimeSpan.FromMilliseconds(1420)
            });
            historyVm.Entries.Add(new JournalEntry
            {
                SourceDirectory = @"C:\Users\Nikshay\Documents\LabProjects",
                Method = OrganizationMethod.FileExtensions,
                MethodName = "File Extensions",
                ExaminedCount = 48,
                MovedCount = 48,
                SkippedCount = 0,
                FailedCount = 0,
                Timestamp = DateTime.UtcNow.AddHours(-2),
                Duration = TimeSpan.FromMilliseconds(380)
            });
            navService.NavigateTo(historyVm);
            SaveVisualToPng(rootVisual, 1200, 800, "09_History.png");

            // 10. Settings View
            navService.NavigateTo(settingsVm);
            SaveVisualToPng(rootVisual, 1200, 800, "10_Settings.png");

            // 11. About View
            navService.NavigateTo(aboutVm);
            SaveVisualToPng(rootVisual, 1200, 800, "11_About.png");

            // 12. Dashboard View - Light Theme
            EnsureWpfApplication("Themes/LightTheme.xaml");
            navService.NavigateTo(dashboardVm);
            SaveVisualToPng(rootVisual, 1200, 800, "12_Dashboard_LightTheme.png");
        });

        // Assert all screenshots were successfully written to disk
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "01_SplashScreen.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "02_Dashboard.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "03_MethodSelection.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "04_MethodConfig.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "05_Preview.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "06_Progress.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "07_Completion.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "08_Categories.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "09_History.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "10_Settings.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "11_About.png")));
        Assert.True(File.Exists(Path.Combine(ScreenshotOutputDir, "12_Dashboard_LightTheme.png")));
    }

    [Fact]
    public void Generate_BrandedIcon_MatchesUiLogo()
    {
        Sortexa.Tests.Common.StaTestRunner.Run(() =>
        {
            EnsureWpfApplication("Themes/DarkTheme.xaml");

            var logoGeometry = (Geometry)Application.Current.Resources["GeoSortexaLogo"];
            var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
            var pngFrames = new List<byte[]>();

            foreach (int size in sizes)
            {
                var grid = new Grid
                {
                    Width = size,
                    Height = size,
                    Background = Brushes.Transparent
                };

                double borderSize = Math.Round(size * 0.90);
                double cornerRadius = Math.Max(2, Math.Round(size * 0.22));
                double iconSize = Math.Round(size * 0.52);

                var border = new Border
                {
                    Width = borderSize,
                    Height = borderSize,
                    CornerRadius = new CornerRadius(cornerRadius),
                    Background = new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new GradientStop((Color)ColorConverter.ConvertFromString("#3987FF"), 0.0),
                            new GradientStop((Color)ColorConverter.ConvertFromString("#635BFF"), 0.5),
                            new GradientStop((Color)ColorConverter.ConvertFromString("#A855F7"), 1.0)
                        },
                        new Point(0, 0),
                        new Point(1, 1)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (size >= 32)
                {
                    border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#3987FF"),
                        BlurRadius = Math.Max(3, size * 0.10),
                        Opacity = 0.40,
                        ShadowDepth = 0
                    };
                }

                var path = new System.Windows.Shapes.Path
                {
                    Data = logoGeometry,
                    Fill = Brushes.White,
                    Stretch = Stretch.Uniform,
                    Width = iconSize,
                    Height = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                border.Child = path;
                grid.Children.Add(border);

                grid.Measure(new Size(size, size));
                grid.Arrange(new Rect(0, 0, size, size));
                grid.UpdateLayout();

                var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(grid);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                pngFrames.Add(ms.ToArray());

                if (size == 256)
                {
                    string previewPng = Path.Combine(ScreenshotOutputDir, "icon_preview_256.png");
                    File.WriteAllBytes(previewPng, ms.ToArray());
                }
            }

            void WriteIco(string targetPath)
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var fs = File.Create(targetPath);
                using var bw = new BinaryWriter(fs);

                bw.Write((ushort)0); // idReserved
                bw.Write((ushort)1); // idType = 1 (icon)
                bw.Write((ushort)pngFrames.Count); // idCount

                int offset = 6 + (16 * pngFrames.Count);
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    byte bDim = s >= 256 ? (byte)0 : (byte)s;
                    byte[] png = pngFrames[i];

                    bw.Write(bDim); // bWidth
                    bw.Write(bDim); // bHeight
                    bw.Write((byte)0); // bColorCount
                    bw.Write((byte)0); // bReserved
                    bw.Write((ushort)1); // wPlanes
                    bw.Write((ushort)32); // wBitCount
                    bw.Write((uint)png.Length); // dwBytesInRes
                    bw.Write((uint)offset); // dwImageOffset

                    offset += png.Length;
                }

                foreach (var png in pngFrames)
                {
                    bw.Write(png);
                }
            }

            WriteIco(@"C:\Users\niksh\Sortexa.App\Assets\Sortexa.ico");
            WriteIco(@"C:\Users\niksh\sortexa app\Sortexa.App\Assets\Sortexa.ico");
        });

        Assert.True(File.Exists(@"C:\Users\niksh\Sortexa.App\Assets\Sortexa.ico"));
        Assert.True(File.Exists(@"C:\Users\niksh\sortexa app\Sortexa.App\Assets\Sortexa.ico"));
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? PickFolder(string? title = null, string? initialPath = null) => @"C:\Users\Nikshay\Downloads";
        public bool ShowConfirmation(string title, string message) => true;
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public void OpenFolderInExplorer(string folderPath) { }
    }
}
