using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class CategoryItemViewModel : ViewModelBase
{
    private bool _isEnabled;
    private string _displayName;
    private string _description;

    public CategoryItemViewModel(CategoryDefinition definition)
    {
        Id = definition.Id;
        _displayName = definition.DisplayName;
        _description = definition.Description;
        IconKey = definition.IconKey;
        AccentColor = definition.AccentColor;
        _isEnabled = definition.IsEnabled;
        Extensions = new ObservableCollection<string>(definition.Extensions);
    }

    public string Id { get; }
    public string IconKey { get; }
    public string AccentColor { get; }
    public ObservableCollection<string> Extensions { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public CategoryDefinition ToDefinition()
    {
        return new CategoryDefinition
        {
            Id = Id,
            DisplayName = DisplayName,
            Description = Description,
            IconKey = IconKey,
            AccentColor = AccentColor,
            IsEnabled = IsEnabled,
            Extensions = Extensions.ToList()
        };
    }
}

public sealed class CategoriesViewModel : ViewModelBase
{
    private readonly IClassificationRegistry _classificationRegistry;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IDialogService _dialogService;
    private readonly ILoggerService? _logger;

    private CategoryItemViewModel? _selectedCategory;
    private string _newExtensionText = string.Empty;
    private string _statusFeedback = string.Empty;

    public CategoriesViewModel(
        IClassificationRegistry classificationRegistry,
        ISettingsRepository settingsRepository,
        IDialogService dialogService,
        ILoggerService? logger = null)
    {
        _classificationRegistry = classificationRegistry;
        _settingsRepository = settingsRepository;
        _dialogService = dialogService;
        _logger = logger;

        Categories = new ObservableCollection<CategoryItemViewModel>();
        LoadCategories();

        AddExtensionCommand = new RelayCommand(AddExtension, _ => SelectedCategory != null && !string.IsNullOrWhiteSpace(NewExtensionText));
        RemoveExtensionCommand = new RelayCommand(p =>
        {
            if (p is string ext && SelectedCategory != null)
            {
                SelectedCategory.Extensions.Remove(ext);
                StatusFeedback = $"Removed {ext} from {SelectedCategory.DisplayName}. Remember to save.";
            }
        });
        SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync);
        RestoreDefaultsCommand = new AsyncRelayCommand(RestoreDefaultsAsync);
    }

    public ObservableCollection<CategoryItemViewModel> Categories { get; }

    public CategoryItemViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public string NewExtensionText
    {
        get => _newExtensionText;
        set => SetProperty(ref _newExtensionText, value);
    }

    public string StatusFeedback
    {
        get => _statusFeedback;
        private set => SetProperty(ref _statusFeedback, value);
    }

    public ICommand AddExtensionCommand { get; }
    public ICommand RemoveExtensionCommand { get; }
    public ICommand SaveChangesCommand { get; }
    public ICommand RestoreDefaultsCommand { get; }

    private void LoadCategories()
    {
        Categories.Clear();
        foreach (var cat in _classificationRegistry.GetCategories())
        {
            Categories.Add(new CategoryItemViewModel(cat));
        }

        SelectedCategory = Categories.FirstOrDefault();
    }

    private void AddExtension()
    {
        if (SelectedCategory == null || string.IsNullOrWhiteSpace(NewExtensionText))
            return;

        string ext = NewExtensionText.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        if (!SelectedCategory.Extensions.Contains(ext))
        {
            SelectedCategory.Extensions.Add(ext);
            StatusFeedback = $"Added {ext} to {SelectedCategory.DisplayName}. Click Save to apply.";
            NewExtensionText = string.Empty;
        }
        else
        {
            StatusFeedback = $"{ext} is already assigned to {SelectedCategory.DisplayName}.";
        }
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            var defs = Categories.Select(c => c.ToDefinition()).ToList();
            _classificationRegistry.UpdateCategories(defs);

            var settings = await _settingsRepository.LoadSettingsAsync();
            settings.CustomCategories = defs;
            await _settingsRepository.SaveSettingsAsync(settings);

            StatusFeedback = "Categories saved and classification registry updated!";
            _dialogService.ShowInformation("Categories Saved", "Your category rules and extension mappings have been successfully saved.");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error saving categories: {ex.Message}", ex);
            StatusFeedback = $"Failed to save categories: {ex.Message}";
        }
    }

    private async Task RestoreDefaultsAsync()
    {
        bool confirm = _dialogService.ShowConfirmation(
            "Restore Defaults",
            "Are you sure you want to restore default categories and extension mappings? Any custom additions will be reset.");

        if (!confirm)
            return;

        try
        {
            _classificationRegistry.ResetToDefaults();
            var settings = await _settingsRepository.LoadSettingsAsync();
            settings.CustomCategories = null;
            await _settingsRepository.SaveSettingsAsync(settings);

            LoadCategories();
            StatusFeedback = "Restored default categories and mappings.";
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error restoring defaults: {ex.Message}", ex);
            StatusFeedback = $"Failed to restore defaults: {ex.Message}";
        }
    }
}
