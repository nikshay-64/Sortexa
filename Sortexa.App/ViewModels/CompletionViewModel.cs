using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class CompletionViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly IUndoEngine _undoEngine;
    private readonly Func<DashboardViewModel> _dashboardFactory;
    private readonly ILoggerService? _logger;

    private ExecutionResult? _result;
    private string _statusTitle = "Completed";
    private string _statusSubtitle = string.Empty;
    private string _statusBadgeColor = "#20C997"; // Emerald default
    private bool _isUndoing;
    private bool _isUndone;
    private string _undoStatusText = string.Empty;

    public CompletionViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IUndoEngine undoEngine,
        Func<DashboardViewModel> dashboardFactory,
        ILoggerService? logger = null)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _undoEngine = undoEngine;
        _dashboardFactory = dashboardFactory;
        _logger = logger;

        OpenDestinationCommand = new RelayCommand(OpenDestinationFolder, _ => !string.IsNullOrEmpty(DestinationDirectory));
        UndoCommand = new AsyncRelayCommand(UndoAsync, _ => CanUndo);
        ReturnDashboardCommand = new RelayCommand(() => _navigationService.NavigateTo(_dashboardFactory()));
    }

    public ExecutionResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(ExaminedCount));
                OnPropertyChanged(nameof(MovedCount));
                OnPropertyChanged(nameof(SkippedCount));
                OnPropertyChanged(nameof(FailedCount));
                OnPropertyChanged(nameof(DurationFormatted));
                OnPropertyChanged(nameof(DestinationDirectory));
                OnPropertyChanged(nameof(CanUndo));
            }
        }
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusSubtitle
    {
        get => _statusSubtitle;
        private set => SetProperty(ref _statusSubtitle, value);
    }

    public string StatusBadgeColor
    {
        get => _statusBadgeColor;
        private set => SetProperty(ref _statusBadgeColor, value);
    }

    public bool IsUndoing
    {
        get => _isUndoing;
        private set => SetProperty(ref _isUndoing, value);
    }

    public bool IsUndone
    {
        get => _isUndone;
        private set
        {
            if (SetProperty(ref _isUndone, value))
            {
                OnPropertyChanged(nameof(CanUndo));
            }
        }
    }

    public string UndoStatusText
    {
        get => _undoStatusText;
        private set => SetProperty(ref _undoStatusText, value);
    }

    public int ExaminedCount => Result?.ExaminedCount ?? 0;
    public int MovedCount => Result?.MovedCount ?? 0;
    public int SkippedCount => Result?.SkippedCount ?? 0;
    public int FailedCount => Result?.FailedCount ?? 0;
    public string DurationFormatted => Result != null ? $"{Result.Duration.TotalSeconds:F1}s" : "0.0s";
    public string DestinationDirectory => Result?.Journal?.SourceDirectory ?? string.Empty;
    public bool CanUndo => Result?.Journal != null && Result.Journal.CompletedMoves.Count > 0 && !IsUndone && !IsUndoing;

    public ICommand OpenDestinationCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand ReturnDashboardCommand { get; }

    public void Initialize(ExecutionResult result)
    {
        Result = result;
        IsUndone = false;
        UndoStatusText = string.Empty;

        if (result.IsCancelled)
        {
            StatusTitle = "Operation Cancelled";
            StatusSubtitle = $"Organization stopped early. {result.MovedCount} files were organized safely.";
            StatusBadgeColor = "#FFB547"; // Amber
        }
        else if (result.FailedCount > 0 && result.MovedCount > 0)
        {
            StatusTitle = "Partially Completed";
            StatusSubtitle = $"{result.MovedCount} files were organized, but {result.FailedCount} file(s) failed.";
            StatusBadgeColor = "#FF6B81"; // Coral
        }
        else if (result.FailedCount > 0 && result.MovedCount == 0)
        {
            StatusTitle = "Operation Failed";
            StatusSubtitle = result.FailureReason ?? "Failed to organize files.";
            StatusBadgeColor = "#FF6B81"; // Coral
        }
        else
        {
            StatusTitle = "Organization Complete!";
            StatusSubtitle = $"Successfully organized {result.MovedCount} files in {DurationFormatted}.";
            StatusBadgeColor = "#20C997"; // Emerald
        }
    }

    private void OpenDestinationFolder()
    {
        if (!string.IsNullOrWhiteSpace(DestinationDirectory))
        {
            _dialogService.OpenFolderInExplorer(DestinationDirectory);
        }
    }

    private async Task UndoAsync()
    {
        if (Result?.Journal == null || !CanUndo)
            return;

        bool confirm = _dialogService.ShowConfirmation(
            "Confirm Undo",
            $"Do you want to restore all {Result.Journal.CompletedMoves.Count} organized files back to their original locations?");

        if (!confirm)
            return;

        IsUndoing = true;
        UndoStatusText = "Restoring files...";

        try
        {
            var undoResult = await _undoEngine.UndoOperationAsync(Result.Journal);
            if (undoResult.IsSuccess)
            {
                IsUndone = true;
                UndoStatusText = $"Successfully restored {undoResult.RestoredCount} files!";
                _dialogService.ShowInformation("Undo Complete", $"All {undoResult.RestoredCount} files have been safely restored.");
            }
            else
            {
                UndoStatusText = $"Partial restore: {undoResult.RestoredCount} restored, {undoResult.FailedCount} failed.";
                _dialogService.ShowWarning("Undo Incomplete", $"Restored {undoResult.RestoredCount} files, but {undoResult.FailedCount} files could not be restored.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error in UndoAsync: {ex.Message}", ex);
            UndoStatusText = $"Undo failed: {ex.Message}";
            _dialogService.ShowError("Undo Error", ex.Message);
        }
        finally
        {
            IsUndoing = false;
        }
    }
}
