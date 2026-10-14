using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class ProgressViewModel : ViewModelBase
{
    private readonly IExecutionEngine _executionEngine;
    private readonly INavigationService _navigationService;
    private readonly Func<CompletionViewModel> _completionFactory;
    private readonly ILoggerService? _logger;

    private CancellationTokenSource? _cts;
    private double _percentage;
    private string _currentFile = string.Empty;
    private int _processedCount;
    private int _totalCount;
    private int _movedCount;
    private int _skippedCount;
    private int _failedCount;
    private string _statusMessage = "Preparing to organize files...";
    private bool _isCancellationRequested;

    public ProgressViewModel(
        IExecutionEngine executionEngine,
        INavigationService navigationService,
        Func<CompletionViewModel> completionFactory,
        ILoggerService? logger = null)
    {
        _executionEngine = executionEngine;
        _navigationService = navigationService;
        _completionFactory = completionFactory;
        _logger = logger;

        CancelCommand = new RelayCommand(RequestCancel, _ => !IsCancellationRequested);
    }

    /// <summary>
    /// Read-only progress percentage. In XAML, this must ALWAYS be bound using Mode=OneWay
    /// to prevent System.InvalidOperationException crashes on read-only ViewModel properties.
    /// </summary>
    public double Percentage => _percentage;

    public string CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    public int ProcessedCount
    {
        get => _processedCount;
        private set => SetProperty(ref _processedCount, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int MovedCount
    {
        get => _movedCount;
        private set => SetProperty(ref _movedCount, value);
    }

    public int SkippedCount
    {
        get => _skippedCount;
        private set => SetProperty(ref _skippedCount, value);
    }

    public int FailedCount
    {
        get => _failedCount;
        private set => SetProperty(ref _failedCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsCancellationRequested
    {
        get => _isCancellationRequested;
        private set => SetProperty(ref _isCancellationRequested, value);
    }

    public ICommand CancelCommand { get; }

    public void RequestCancel()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            IsCancellationRequested = true;
            StatusMessage = "Cancelling operation... Completing current file safely.";
            _cts.Cancel();
        }
    }

    public async Task StartExecutionAsync(OrganizationPlan plan)
    {
        _cts = new CancellationTokenSource();
        IsCancellationRequested = false;
        TotalCount = plan.EligibleFiles;

        var progress = new Progress<ExecutionProgress>(ReportProgress);

        try
        {
            var result = await Task.Run(() => _executionEngine.ExecutePlanAsync(plan, progress, _cts.Token));

            // Safely navigate on the UI thread
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NavigateToCompletion(result);
                });
            }
            else
            {
                NavigateToCompletion(result);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during execution workflow: {ex.Message}", ex);
            var failureResult = new ExecutionResult
            {
                OperationId = Guid.NewGuid(),
                IsSuccess = false,
                FailureReason = ex.Message,
                ExaminedCount = plan.TotalFiles,
                MovedCount = MovedCount,
                SkippedCount = SkippedCount,
                FailedCount = FailedCount + 1
            };

            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NavigateToCompletion(failureResult);
                });
            }
            else
            {
                NavigateToCompletion(failureResult);
            }
        }
    }

    private void ReportProgress(ExecutionProgress p)
    {
        _percentage = p.Percentage;
        OnPropertyChanged(nameof(Percentage));

        CurrentFile = p.CurrentFile;
        ProcessedCount = p.ProcessedCount;
        TotalCount = p.TotalCount;
        MovedCount = p.MovedCount;
        SkippedCount = p.SkippedCount;
        FailedCount = p.FailedCount;
        StatusMessage = p.StatusMessage;
    }

    private void NavigateToCompletion(ExecutionResult result)
    {
        var completionVm = _completionFactory();
        completionVm.Initialize(result);
        _navigationService.NavigateTo(completionVm);
    }
}
