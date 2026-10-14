using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;
using Sortexa.App.Services;

namespace Sortexa.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IFileScanner _fileScanner;
    private readonly IPathValidator _pathValidator;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILoggerService? _logger;
    private readonly Func<MethodSelectionViewModel> _methodSelectionFactory;

    private string _selectedFolderPath = string.Empty;
    private string _folderDisplayName = string.Empty;
    private int _fileCount;
    private long _totalSizeBytes;
    private string _formattedSize = "0 B";
    private bool _isFolderSelected;
    private bool _isScanning;
    private string _statusMessage = "Select a folder to begin organizing your files.";
    private IReadOnlyList<FileItem> _scannedFiles = Array.Empty<FileItem>();

    public DashboardViewModel(
        IFileScanner fileScanner,
        IPathValidator pathValidator,
        IDialogService dialogService,
        INavigationService navigationService,
        Func<MethodSelectionViewModel> methodSelectionFactory,
        ILoggerService? logger = null)
    {
        _fileScanner = fileScanner;
        _pathValidator = pathValidator;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _methodSelectionFactory = methodSelectionFactory;
        _logger = logger;

        PickFolderCommand = new AsyncRelayCommand(PickFolderAsync);
        ClearFolderCommand = new RelayCommand(ClearFolder);
        ContinueCommand = new RelayCommand(ContinueToMethodSelection, _ => IsFolderSelected && FileCount > 0 && !IsScanning);
    }

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        set
        {
            if (SetProperty(ref _selectedFolderPath, value))
            {
                IsFolderSelected = !string.IsNullOrWhiteSpace(value);
                FolderDisplayName = string.IsNullOrWhiteSpace(value) ? string.Empty : Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar));
            }
        }
    }

    public string FolderDisplayName
    {
        get => _folderDisplayName;
        private set => SetProperty(ref _folderDisplayName, value);
    }

    public int FileCount
    {
        get => _fileCount;
        private set => SetProperty(ref _fileCount, value);
    }

    public long TotalSizeBytes
    {
        get => _totalSizeBytes;
        private set => SetProperty(ref _totalSizeBytes, value);
    }

    public string FormattedSize
    {
        get => _formattedSize;
        private set => SetProperty(ref _formattedSize, value);
    }

    public bool IsFolderSelected
    {
        get => _isFolderSelected;
        private set => SetProperty(ref _isFolderSelected, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetProperty(ref _isScanning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IReadOnlyList<FileItem> ScannedFiles => _scannedFiles;

    public ICommand PickFolderCommand { get; }
    public ICommand ClearFolderCommand { get; }
    public ICommand ContinueCommand { get; }

    public async Task PickFolderAsync()
    {
        string? folder = _dialogService.PickFolder("Choose Folder to Organize", SelectedFolderPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            await SetFolderAndScanAsync(folder);
        }
    }

    public async Task HandleDropFolderAsync(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            await SetFolderAndScanAsync(folderPath);
        }
    }

    public async Task SetFolderAndScanAsync(string folderPath)
    {
        if (!_pathValidator.IsValidTargetDirectory(folderPath, out string? error))
        {
            _dialogService.ShowWarning("Invalid Folder", error ?? "The selected directory cannot be organized.");
            return;
        }

        SelectedFolderPath = folderPath;
        IsScanning = true;
        StatusMessage = "Scanning directory contents...";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var files = await _fileScanner.ScanDirectoryAsync(folderPath, cts.Token);
            _scannedFiles = files;
            FileCount = files.Count;

            long totalBytes = 0;
            foreach (var f in files)
            {
                totalBytes += f.SizeBytes;
            }
            TotalSizeBytes = totalBytes;
            FormattedSize = FormatBytes(totalBytes);

            if (FileCount == 0)
            {
                StatusMessage = "No top-level files found in this folder.";
            }
            else
            {
                StatusMessage = $"Ready! Found {FileCount} file{(FileCount == 1 ? "" : "s")} ({FormattedSize}).";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error scanning {folderPath}: {ex.Message}", ex);
            StatusMessage = $"Failed to scan folder: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void ClearFolder()
    {
        SelectedFolderPath = string.Empty;
        _scannedFiles = Array.Empty<FileItem>();
        FileCount = 0;
        TotalSizeBytes = 0;
        FormattedSize = "0 B";
        StatusMessage = "Select a folder to begin organizing your files.";
    }

    private void ContinueToMethodSelection()
    {
        if (!IsFolderSelected || FileCount == 0)
            return;

        var methodSelectionVm = _methodSelectionFactory();
        methodSelectionVm.Initialize(SelectedFolderPath, _scannedFiles);
        _navigationService.NavigateTo(methodSelectionVm);
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
