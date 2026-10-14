using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Sortexa.App.Services;
using Sortexa.App.ViewModels;
using Sortexa.App.Views;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;
using Xunit;

namespace Sortexa.Tests.Unit;

public sealed class WpfRegressionTests
{
    private static void RunInSta(Action action)
    {
        Sortexa.Tests.Common.StaTestRunner.Run(action);
    }

    [Fact]
    public void Regression_ProgressViewModel_PercentageProperty_IsStrictlyReadOnly()
    {
        var prop = typeof(ProgressViewModel).GetProperty("Percentage");
        Assert.NotNull(prop);
        Assert.True(prop.CanRead, "Percentage must be readable");
        Assert.False(prop.CanWrite && prop.SetMethod != null && prop.SetMethod.IsPublic,
            "Percentage property must NOT have a public setter to prevent unintended mutations.");
    }

    [Fact]
    public void Regression_ProgressView_XAML_BindsPercentage_WithExplicitOneWayMode()
    {
        // Audit the actual production XAML view to guarantee Mode=OneWay is configured
        string appDir = Path.GetFullPath(AppContext.BaseDirectory);
        // Find Sortexa.App source path
        string? projectRoot = appDir;
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "Sortexa.sln")))
        {
            projectRoot = Path.GetDirectoryName(projectRoot);
        }

        Assert.NotNull(projectRoot);
        string xamlPath = Path.Combine(projectRoot, "Sortexa.App", "Views", "ProgressView.xaml");
        Assert.True(File.Exists(xamlPath), $"ProgressView.xaml must exist at {xamlPath}");

        string xamlContent = File.ReadAllText(xamlPath);

        // Verify ProgressBar.Value binds Percentage with Mode=OneWay
        Assert.Contains("Value=\"{Binding Percentage, Mode=OneWay}\"", xamlContent);
    }

    [Fact]
    public void Regression_ProgressView_CanInitialize_WithoutBindingException()
    {
        RunInSta(() =>
        {
            Sortexa.Tests.Common.StaTestRunner.EnsureResources("Themes/DarkTheme.xaml");

            var fakeEngine = new FakeExecutionEngine();
            var navService = new NavigationService(t => new object());
            var vm = new ProgressViewModel(fakeEngine, navService, () => null!);

            var view = new ProgressView
            {
                DataContext = vm
            };

            Assert.NotNull(view);
            Assert.Equal(0, vm.Percentage);
        });
    }

    [Fact]
    public void NavigationService_UpdatesCurrentViewAndNotifies()
    {
        var navService = new NavigationService(t => Activator.CreateInstance(t)!);
        object? recordedView = null;
        navService.CurrentViewChanged += v => recordedView = v;

        var dummy = new object();
        navService.NavigateTo(dummy);

        Assert.Same(dummy, navService.CurrentView);
        Assert.Same(dummy, recordedView);
    }

    private sealed class FakeExecutionEngine : IExecutionEngine
    {
        public Task<ExecutionResult> ExecutePlanAsync(OrganizationPlan plan, IProgress<ExecutionProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExecutionResult());
        }
    }

    private sealed class FakeUndoEngine : IUndoEngine
    {
        public Task<UndoResult> UndoOperationAsync(JournalEntry journalEntry, IProgress<ExecutionProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UndoResult());
        }
    }

    private sealed class FakeScanner : IFileScanner
    {
        public Task<System.Collections.Generic.IReadOnlyList<FileItem>> ScanDirectoryAsync(string path, CancellationToken token = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<FileItem>>(Array.Empty<FileItem>());
        }
    }

    private sealed class FakeValidator : IPathValidator
    {
        public bool IsValidTargetDirectory(string path, out string? error) { error = null; return true; }
        public bool IsSafeSubfolderName(string name, out string? san) { san = name; return true; }
        public bool IsPathWithinRoot(string root, string cand) => true;
        public bool IsProtectedSystemDirectory(string path) => false;
        public bool IsApplicationDirectory(string path) => false;
        public string SanitizePathComponent(string comp) => comp;
    }

    [Fact]
    public void GenerateAndVerifySortexaIco()
    {
        RunInSta(() =>
        {
            string appDir = Path.GetFullPath(AppContext.BaseDirectory);
            string? projectRoot = appDir;
            while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "Sortexa.sln")))
            {
                projectRoot = Path.GetDirectoryName(projectRoot);
            }
            Assert.NotNull(projectRoot);

            string icoPath = Path.Combine(projectRoot, "Sortexa.App", "Assets", "Sortexa.ico");
            int[] sizes = [256, 128, 64, 48, 32, 16];
            var pngBuffers = new System.Collections.Generic.List<byte[]>();

            string pathData = "M 18,3 L 8.5,3 C 5,3 2.5,5.5 2.5,9 C 2.5,12.5 5,14 8,14 L 13.5,9.5 L 9.5,9.5 C 8,9.5 6.8,8.8 6.8,7.5 C 6.8,6.2 8,5.2 9.5,5.2 L 18,5.2 C 18.6,5.2 19,4.7 19,4.1 C 19,3.5 18.6,3 18,3 Z M 6,21 L 15.5,21 C 19,21 21.5,18.5 21.5,15 C 21.5,11.5 19,10 16,10 L 10.5,14.5 L 14.5,14.5 C 16,14.5 17.2,15.2 17.2,16.5 C 17.2,17.8 16,18.8 14.5,18.8 L 6,18.8 C 5.4,18.8 5,19.3 5,19.9 C 5,20.5 5.4,21 6,21 Z";

            foreach (int size in sizes)
            {
                var grid = new System.Windows.Controls.Grid
                {
                    Width = size,
                    Height = size,
                    Background = System.Windows.Media.Brushes.Transparent
                };

                double cornerRadius = Math.Max(3, size * 0.22);
                var border = new System.Windows.Controls.Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new System.Windows.CornerRadius(cornerRadius),
                    Background = new System.Windows.Media.LinearGradientBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B1020"),
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#17233B"),
                        45),
                    BorderBrush = new System.Windows.Media.LinearGradientBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3987FF"),
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#20D9F3"),
                        45),
                    BorderThickness = new System.Windows.Thickness(Math.Max(1.0, size * 0.035))
                };

                double iconSize = size * 0.62;
                var path = new System.Windows.Shapes.Path
                {
                    Data = System.Windows.Media.Geometry.Parse(pathData),
                    Fill = new System.Windows.Media.LinearGradientBrush(
                        new System.Windows.Media.GradientStopCollection
                        {
                            new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#20D9F3"), 0.0),
                            new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3987FF"), 0.5),
                            new System.Windows.Media.GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A855F7"), 1.0)
                        },
                        new System.Windows.Point(0, 0),
                        new System.Windows.Point(1, 1)),
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Width = iconSize,
                    Height = iconSize,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                border.Child = path;
                grid.Children.Add(border);

                grid.Measure(new System.Windows.Size(size, size));
                grid.Arrange(new System.Windows.Rect(0, 0, size, size));
                grid.UpdateLayout();

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(grid);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using var ms = new MemoryStream();
                enc.Save(ms);
                pngBuffers.Add(ms.ToArray());
            }

            using (var fs = File.Create(icoPath))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)sizes.Length);

                int offset = 6 + (16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write((uint)pngBuffers[i].Length);
                    bw.Write((uint)offset);
                    offset += pngBuffers[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngBuffers[i]);
                }
                fs.Flush();
            }

            Assert.True(File.Exists(icoPath), "Sortexa.ico must exist");
            var fi = new FileInfo(icoPath);
            Assert.True(fi.Length > 2000, "Sortexa.ico must contain multi-res frames");
        });
    }
}

