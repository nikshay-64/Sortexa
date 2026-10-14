using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class MethodCardItem : ViewModelBase
{
    private bool _isSelected;

    public OrganizationMethod Method { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string ExampleStructure { get; init; }
    public required string IconKey { get; init; }
    public required string AccentColor { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class MethodSelectionViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly Func<DashboardViewModel> _dashboardFactory;
    private readonly Func<MethodConfigViewModel> _configFactory;
    private readonly Func<PreviewViewModel> _previewFactory;
    private readonly IOrganizationPlanner _planner;
    private readonly IClassificationRegistry _classificationRegistry;

    private string _sourceDirectory = string.Empty;
    private IReadOnlyList<FileItem> _files = Array.Empty<FileItem>();
    private MethodCardItem? _selectedCard;

    public MethodSelectionViewModel(
        INavigationService navigationService,
        Func<DashboardViewModel> dashboardFactory,
        Func<MethodConfigViewModel> configFactory,
        Func<PreviewViewModel> previewFactory,
        IOrganizationPlanner planner,
        IClassificationRegistry classificationRegistry)
    {
        _navigationService = navigationService;
        _dashboardFactory = dashboardFactory;
        _configFactory = configFactory;
        _previewFactory = previewFactory;
        _planner = planner;
        _classificationRegistry = classificationRegistry;

        Methods = new ObservableCollection<MethodCardItem>(CreateMethodCards());
        SelectMethodCommand = new RelayCommand(p =>
        {
            if (p is MethodCardItem item)
            {
                SelectCard(item);
            }
        });

        BackCommand = new RelayCommand(() => _navigationService.NavigateTo(_dashboardFactory()));
        ContinueCommand = new RelayCommand(Continue, _ => SelectedCard != null);

        // Default to Smart Categories
        var defaultCard = Methods.FirstOrDefault(m => m.Method == OrganizationMethod.SmartCategories) ?? Methods.First();
        SelectCard(defaultCard);
    }

    public ObservableCollection<MethodCardItem> Methods { get; }

    public MethodCardItem? SelectedCard
    {
        get => _selectedCard;
        private set => SetProperty(ref _selectedCard, value);
    }

    public string SourceDirectory => _sourceDirectory;
    public int FileCount => _files.Count;

    public ICommand SelectMethodCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ContinueCommand { get; }

    public void Initialize(string sourceDirectory, IReadOnlyList<FileItem> files)
    {
        _sourceDirectory = sourceDirectory;
        _files = files;
        OnPropertyChanged(nameof(SourceDirectory));
        OnPropertyChanged(nameof(FileCount));
    }

    private void SelectCard(MethodCardItem item)
    {
        foreach (var m in Methods)
        {
            m.IsSelected = false;
        }

        item.IsSelected = true;
        SelectedCard = item;
    }

    private void Continue()
    {
        if (SelectedCard == null)
            return;

        // Methods that require or benefit from configuration:
        // Date Created, Last Modified, File Extensions, Custom Rules
        // Or user can configure any method. Let's provide MethodConfigView for full transparency!
        var configVm = _configFactory();
        configVm.Initialize(_sourceDirectory, _files, SelectedCard.Method);
        _navigationService.NavigateTo(configVm);
    }

    private static List<MethodCardItem> CreateMethodCards()
    {
        return new List<MethodCardItem>
        {
            new()
            {
                Method = OrganizationMethod.SmartCategories,
                Title = "Smart Categories",
                Description = "Intelligently classifies files into semantic category folders (Documents, Images, Archives, Code, etc.).",
                ExampleStructure = "Documents\\report.docx, Images\\photo.png",
                IconKey = "Layers",
                AccentColor = "#3987FF"
            },
            new()
            {
                Method = OrganizationMethod.FileExtensions,
                Title = "File Extensions",
                Description = "Sorts files into dedicated folders named after their normalized uppercase file extensions.",
                ExampleStructure = "PDF\\manual.pdf, DOCX\\notes.docx, ZIP\\data.zip",
                IconKey = "Hash",
                AccentColor = "#20D9F3"
            },
            new()
            {
                Method = OrganizationMethod.DateCreated,
                Title = "Date Created",
                Description = "Groups files by when they were originally created on the filesystem.",
                ExampleStructure = "2026\\10 - October\\receipt.pdf",
                IconKey = "Calendar",
                AccentColor = "#635BFF"
            },
            new()
            {
                Method = OrganizationMethod.LastModified,
                Title = "Last Modified",
                Description = "Arranges files chronologically by their most recent modification timestamp.",
                ExampleStructure = "2026\\10 - October\\14\\draft.txt",
                IconKey = "Clock",
                AccentColor = "#A855F7"
            },
            new()
            {
                Method = OrganizationMethod.FileSize,
                Title = "File Size",
                Description = "Segments files into clearly delineated volume bands from Tiny to Very Large.",
                ExampleStructure = "Medium (1 MB - 100 MB)\\video_clip.mp4",
                IconKey = "BarChart2",
                AccentColor = "#20C997"
            },
            new()
            {
                Method = OrganizationMethod.FilePurpose,
                Title = "File Purpose",
                Description = "Organizes files by overarching functional role: Applications, Media, Development, etc.",
                ExampleStructure = "Development and Source Code\\program.cs",
                IconKey = "Target",
                AccentColor = "#EC4899"
            },
            new()
            {
                Method = OrganizationMethod.CustomRules,
                Title = "Custom Rules",
                Description = "Evaluates your custom keyword, prefix, suffix, regex, and extension match criteria.",
                ExampleStructure = "Finance\\2026_Invoice_01.pdf",
                IconKey = "Sliders",
                AccentColor = "#FFB547"
            }
        };
    }
}
