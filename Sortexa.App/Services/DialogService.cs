using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Sortexa.App.Services;

public interface IDialogService
{
    string? PickFolder(string? title = null, string? initialPath = null);
    bool ShowConfirmation(string title, string message);
    void ShowInformation(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    void OpenFolderInExplorer(string folderPath);
}

public sealed class DialogService : IDialogService
{
    public string? PickFolder(string? title = null, string? initialPath = null)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = title ?? "Select Folder to Organize",
                Multiselect = false
            };

            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                dialog.InitialDirectory = initialPath;
            }

            bool? result = dialog.ShowDialog();
            if (result == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return dialog.FolderName;
            }
        }
        catch (Exception ex)
        {
            ShowError("Folder Selection", $"Could not open folder picker: {ex.Message}");
        }

        return null;
    }

    public bool ShowConfirmation(string title, string message)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public void ShowInformation(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void OpenFolderInExplorer(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently ignore explorer spawn errors
        }
    }
}
