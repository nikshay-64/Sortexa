using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sortexa.App.Services;
using Sortexa.App.ViewModels;
using Sortexa.App.Views;
using Sortexa.Core.Classification;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Planning;
using Sortexa.Core.Validation;
using Sortexa.Infrastructure.Engine;
using Sortexa.Infrastructure.Filesystem;
using Sortexa.Infrastructure.Logging;
using Sortexa.Infrastructure.Persistence;

namespace Sortexa.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private ILoggerService? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global Exception Handlers
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                _logger?.LogError($"AppDomain unhandled exception: {ex.Message}", ex);
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            _logger?.LogError($"Dispatcher unhandled exception: {args.Exception.Message}", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "Sortexa Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILoggerService>();
        _logger.LogInfo("Sortexa v1.0.0 initializing...");

        // Load saved theme
        var themeService = _serviceProvider.GetRequiredService<IThemeService>();
        var settingsRepo = _serviceProvider.GetRequiredService<ISettingsRepository>();
        try
        {
            var settings = await settingsRepo.LoadSettingsAsync();
            themeService.ApplyTheme(settings.Theme);
        }
        catch
        {
            themeService.ApplyTheme("System");
        }

        // Show Branded Startup Splash Screen
        var splash = new SplashScreenWindow();
        splash.Show();

        // Run the splash sequence (approx 2s duration)
        try
        {
            await splash.PlaySplashAsync(2000);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Splash playback error: {ex.Message}", ex);
        }

        // Initialize Main Window
        try
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            splash.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to launch main window: {ex.Message}", ex);
            splash.Close();
            MessageBox.Show(
                $"Failed to launch Sortexa:\n\n{ex.Message}",
                "Sortexa Startup Failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core & Infrastructure
        services.AddSingleton<ILoggerService, LoggerService>();
        services.AddSingleton<IPathValidator, PathValidator>();
        services.AddSingleton<IClassificationRegistry, ClassificationRegistry>();
        services.AddSingleton<IOrganizationPlanner, OrganizationPlanner>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IJournalRepository, JournalRepository>();
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<SafeFileMover>();
        services.AddSingleton<IExecutionEngine, ExecutionEngine>();
        services.AddSingleton<IUndoEngine, UndoEngine>();

        // Application Services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();

        // Navigation Service with factory delegate
        services.AddSingleton<INavigationService>(sp =>
            new NavigationService(type => sp.GetRequiredService(type)));

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<MethodSelectionViewModel>();
        services.AddTransient<MethodConfigViewModel>();
        services.AddTransient<PreviewViewModel>();
        services.AddTransient<ProgressViewModel>();
        services.AddTransient<CompletionViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        // ViewModel Factory Delegates
        services.AddSingleton<Func<DashboardViewModel>>(sp => () => sp.GetRequiredService<DashboardViewModel>());
        services.AddSingleton<Func<MethodSelectionViewModel>>(sp => () => sp.GetRequiredService<MethodSelectionViewModel>());
        services.AddSingleton<Func<MethodConfigViewModel>>(sp => () => sp.GetRequiredService<MethodConfigViewModel>());
        services.AddSingleton<Func<PreviewViewModel>>(sp => () => sp.GetRequiredService<PreviewViewModel>());
        services.AddSingleton<Func<ProgressViewModel>>(sp => () => sp.GetRequiredService<ProgressViewModel>());
        services.AddSingleton<Func<CompletionViewModel>>(sp => () => sp.GetRequiredService<CompletionViewModel>());
        services.AddSingleton<Func<HistoryViewModel>>(sp => () => sp.GetRequiredService<HistoryViewModel>());
        services.AddSingleton<Func<CategoriesViewModel>>(sp => () => sp.GetRequiredService<CategoriesViewModel>());
        services.AddSingleton<Func<SettingsViewModel>>(sp => () => sp.GetRequiredService<SettingsViewModel>());
        services.AddSingleton<Func<AboutViewModel>>(sp => () => sp.GetRequiredService<AboutViewModel>());

        // Views
        services.AddTransient<MainWindow>();
    }
}
