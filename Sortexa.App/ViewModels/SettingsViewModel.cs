using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly ILoggerService? _logger;

    private string _selectedTheme = "System";
    private OrganizationMethod _preferredMethod = OrganizationMethod.SmartCategories;
    private DuplicateResolutionPolicy _duplicatePolicy = DuplicateResolutionPolicy.RenameWithCounter;
    private string _fallbackCategoryName = "Other Files";
    private bool _enableLogging = true;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IThemeService themeService,
        IDialogService dialogService,
        ILoggerService? logger = null)
    {
        _settingsRepository = settingsRepository;
        _themeService = themeService;
        _dialogService = dialogService;
        _logger = logger;

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);

        _ = LoadSettingsAsync();
    }

    public string[] AvailableThemes => new[] { "System", "Dark", "Light" };

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _themeService.ApplyTheme(value);
            }
        }
    }

    public OrganizationMethod[] AvailableMethods => (OrganizationMethod[])Enum.GetValues(typeof(OrganizationMethod));

    public OrganizationMethod PreferredMethod
    {
        get => _preferredMethod;
        set => SetProperty(ref _preferredMethod, value);
    }

    public DuplicateResolutionPolicy[] AvailablePolicies => (DuplicateResolutionPolicy[])Enum.GetValues(typeof(DuplicateResolutionPolicy));

    public DuplicateResolutionPolicy DuplicatePolicy
    {
        get => _duplicatePolicy;
        set => SetProperty(ref _duplicatePolicy, value);
    }

    public string FallbackCategoryName
    {
        get => _fallbackCategoryName;
        set => SetProperty(ref _fallbackCategoryName, value);
    }

    public bool EnableLogging
    {
        get => _enableLogging;
        set => SetProperty(ref _enableLogging, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsRepository.LoadSettingsAsync();
            _selectedTheme = settings.Theme;
            _preferredMethod = settings.PreferredMethod;
            _duplicatePolicy = settings.DuplicatePolicy;
            _fallbackCategoryName = settings.FallbackCategoryName;
            _enableLogging = settings.EnableLogging;

            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(PreferredMethod));
            OnPropertyChanged(nameof(DuplicatePolicy));
            OnPropertyChanged(nameof(FallbackCategoryName));
            OnPropertyChanged(nameof(EnableLogging));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error loading settings: {ex.Message}", ex);
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = await _settingsRepository.LoadSettingsAsync();
            settings.Theme = SelectedTheme;
            settings.PreferredMethod = PreferredMethod;
            settings.DuplicatePolicy = DuplicatePolicy;
            settings.FallbackCategoryName = string.IsNullOrWhiteSpace(FallbackCategoryName) ? "Other Files" : FallbackCategoryName.Trim();
            settings.EnableLogging = EnableLogging;

            await _settingsRepository.SaveSettingsAsync(settings);
            StatusMessage = "Settings saved successfully!";
            _dialogService.ShowInformation("Settings Saved", "Your configuration has been updated.");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error saving settings: {ex.Message}", ex);
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
    }

    private void OpenLogsFolder()
    {
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sortexa", "logs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
        _dialogService.OpenFolderInExplorer(logDir);
    }
}
