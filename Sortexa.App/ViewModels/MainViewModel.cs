using System;
using System.Windows.Input;
using Sortexa.App.Services;

namespace Sortexa.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly Func<DashboardViewModel> _dashboardFactory;
    private readonly Func<HistoryViewModel> _historyFactory;
    private readonly Func<CategoriesViewModel> _categoriesFactory;
    private readonly Func<SettingsViewModel> _settingsFactory;
    private readonly Func<AboutViewModel> _aboutFactory;

    private object? _currentView;
    private string _activeTab = "Dashboard";

    public MainViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        Func<DashboardViewModel> dashboardFactory,
        Func<HistoryViewModel> historyFactory,
        Func<CategoriesViewModel> categoriesFactory,
        Func<SettingsViewModel> settingsFactory,
        Func<AboutViewModel> aboutFactory)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _dashboardFactory = dashboardFactory;
        _historyFactory = historyFactory;
        _categoriesFactory = categoriesFactory;
        _settingsFactory = settingsFactory;
        _aboutFactory = aboutFactory;

        _navigationService.CurrentViewChanged += view =>
        {
            CurrentView = view;
            UpdateActiveTabForView(view);
        };

        NavigateDashboardCommand = new RelayCommand(() => _navigationService.NavigateTo(_dashboardFactory()));
        NavigateHistoryCommand = new RelayCommand(() => _navigationService.NavigateTo(_historyFactory()));
        NavigateCategoriesCommand = new RelayCommand(() => _navigationService.NavigateTo(_categoriesFactory()));
        NavigateSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo(_settingsFactory()));
        NavigateAboutCommand = new RelayCommand(() => _navigationService.NavigateTo(_aboutFactory()));
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

        // Start on Dashboard
        _navigationService.NavigateTo(_dashboardFactory());
    }

    public object? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public string ActiveTab
    {
        get => _activeTab;
        private set => SetProperty(ref _activeTab, value);
    }

    public string CurrentTheme => _themeService.CurrentTheme;

    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigateHistoryCommand { get; }
    public ICommand NavigateCategoriesCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateAboutCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    private void ToggleTheme()
    {
        string nextTheme = _themeService.CurrentTheme switch
        {
            "Dark" => "Light",
            "Light" => "Dark",
            _ => "Dark"
        };

        _themeService.ApplyTheme(nextTheme);
        OnPropertyChanged(nameof(CurrentTheme));
    }

    private void UpdateActiveTabForView(object view)
    {
        ActiveTab = view switch
        {
            DashboardViewModel => "Dashboard",
            MethodSelectionViewModel => "Dashboard",
            MethodConfigViewModel => "Dashboard",
            PreviewViewModel => "Dashboard",
            ProgressViewModel => "Dashboard",
            CompletionViewModel => "Dashboard",
            HistoryViewModel => "History",
            CategoriesViewModel => "Categories",
            SettingsViewModel => "Settings",
            AboutViewModel => "About",
            _ => "Dashboard"
        };
    }
}
