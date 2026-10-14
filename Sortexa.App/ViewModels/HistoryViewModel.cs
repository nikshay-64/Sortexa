using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly IJournalRepository _journalRepository;
    private readonly IUndoEngine _undoEngine;
    private readonly IDialogService _dialogService;
    private readonly ILoggerService? _logger;

    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public HistoryViewModel(
        IJournalRepository journalRepository,
        IUndoEngine undoEngine,
        IDialogService dialogService,
        ILoggerService? logger = null)
    {
        _journalRepository = journalRepository;
        _undoEngine = undoEngine;
        _dialogService = dialogService;
        _logger = logger;

        Entries = new ObservableCollection<JournalEntry>();
        RefreshCommand = new AsyncRelayCommand(LoadHistoryAsync);
        UndoEntryCommand = new AsyncRelayCommand(UndoEntryAsync);

        _ = LoadHistoryAsync();
    }

    public ObservableCollection<JournalEntry> Entries { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand UndoEntryCommand { get; }

    public async Task LoadHistoryAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading history...";

        try
        {
            var list = await _journalRepository.GetAllEntriesAsync();
            Entries.Clear();
            foreach (var item in list)
            {
                Entries.Add(item);
            }

            StatusMessage = Entries.Count == 0 ? "No previous operations recorded." : $"{Entries.Count} operation(s) recorded.";
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error loading history: {ex.Message}", ex);
            StatusMessage = "Failed to load operation history.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UndoEntryAsync(object? parameter)
    {
        if (parameter is not JournalEntry entry)
            return;

        if (entry.IsUndone)
        {
            _dialogService.ShowInformation("Already Undone", "This operation has already been undone.");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation(
            "Confirm Undo",
            $"Do you want to reverse this operation and restore {entry.CompletedMoves.Count} file(s) to:\n{entry.SourceDirectory}?");

        if (!confirm)
            return;

        try
        {
            var result = await _undoEngine.UndoOperationAsync(entry);
            if (result.IsSuccess)
            {
                _dialogService.ShowInformation("Undo Complete", $"Successfully restored {result.RestoredCount} file(s).");
            }
            else
            {
                _dialogService.ShowWarning("Undo Incomplete", $"Restored {result.RestoredCount} file(s), but {result.FailedCount} failed.");
            }

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during history undo: {ex.Message}", ex);
            _dialogService.ShowError("Undo Error", ex.Message);
        }
    }
}
