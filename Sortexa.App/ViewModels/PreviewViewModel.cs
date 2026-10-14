using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class PreviewViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly Func<DashboardViewModel> _dashboardFactory;
    private readonly Func<MethodConfigViewModel> _configFactory;
    private readonly Func<ProgressViewModel> _progressFactory;

    private OrganizationPlan? _plan;
    private string _searchFilter = string.Empty;
    private ObservableCollection<PlannedOperation> _displayedOperations = new();

    public PreviewViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        Func<DashboardViewModel> dashboardFactory,
        Func<MethodConfigViewModel> configFactory,
        Func<ProgressViewModel> progressFactory)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _dashboardFactory = dashboardFactory;
        _configFactory = configFactory;
        _progressFactory = progressFactory;

        BackCommand = new RelayCommand(GoBack);
        CancelCommand = new RelayCommand(Cancel);
        ConfirmAndOrganizeCommand = new RelayCommand(ConfirmAndOrganize, _ => Plan != null && EligibleCount > 0);
    }

    public OrganizationPlan? Plan
    {
        get => _plan;
        private set
        {
            if (SetProperty(ref _plan, value))
            {
                OnPropertyChanged(nameof(SourceDirectory));
                OnPropertyChanged(nameof(MethodName));
                OnPropertyChanged(nameof(TotalFiles));
                OnPropertyChanged(nameof(EligibleCount));
                OnPropertyChanged(nameof(SkippedCount));
                OnPropertyChanged(nameof(TotalSizeFormatted));
                ApplyFilter();
            }
        }
    }

    public ObservableCollection<PlannedOperation> DisplayedOperations => _displayedOperations;

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SourceDirectory => Plan?.SourceDirectory ?? string.Empty;
    public string MethodName => Plan?.Method.ToString() ?? string.Empty;
    public int TotalFiles => Plan?.TotalFiles ?? 0;
    public int EligibleCount => Plan?.EligibleFiles ?? 0;
    public int SkippedCount => Plan?.SkippedFiles ?? 0;
    public string TotalSizeFormatted => FormatBytes(Plan?.TotalBytesEligible ?? 0);

    public ICommand BackCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmAndOrganizeCommand { get; }

    public void Initialize(OrganizationPlan plan)
    {
        Plan = plan;
    }

    private void ApplyFilter()
    {
        _displayedOperations.Clear();
        if (_plan == null)
            return;

        var query = _plan.Operations.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            string filter = _searchFilter.Trim();
            query = query.Where(o =>
                o.OriginalFileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                o.MatchedCategoryOrRule.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                o.DestinationRelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var op in query)
        {
            _displayedOperations.Add(op);
        }
    }

    private void GoBack()
    {
        if (_plan == null)
        {
            _navigationService.NavigateTo(_dashboardFactory());
            return;
        }

        // Return to config screen
        var configVm = _configFactory();
        configVm.Initialize(_plan.SourceDirectory, _plan.Operations.Select(o => new FileItem
        {
            FullPath = o.SourcePath,
            FileName = o.OriginalFileName,
            Extension = System.IO.Path.GetExtension(o.OriginalFileName),
            SizeBytes = o.FileSizeBytes,
            DateModified = o.DateModified
        }).ToList(), _plan.Method);

        _navigationService.NavigateTo(configVm);
    }

    private void Cancel()
    {
        _navigationService.NavigateTo(_dashboardFactory());
    }

    private void ConfirmAndOrganize()
    {
        if (_plan == null || EligibleCount == 0)
            return;

        // Strict Rule: Never move files without explicit user confirmation
        bool confirmed = _dialogService.ShowConfirmation(
            "Confirm Organization",
            $"Are you sure you want to organize {EligibleCount} file{(EligibleCount == 1 ? "" : "s")} in:\n\n{_plan.SourceDirectory}\n\nAll actions are safely recorded and can be reversed via Undo.");

        if (!confirmed)
            return;

        var progressVm = _progressFactory();
        _navigationService.NavigateTo(progressVm);
        _ = progressVm.StartExecutionAsync(_plan);
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
            if (counter >= suffixes.Length - 1)
                break;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
