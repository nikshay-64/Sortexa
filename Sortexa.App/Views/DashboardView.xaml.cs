using System.IO;
using System.Windows;
using System.Windows.Controls;
using Sortexa.App.ViewModels;

namespace Sortexa.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropZone.Opacity = 0.8;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.Opacity = 1.0;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.Opacity = 1.0;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string path = files[0];
                if (Directory.Exists(path) && DataContext is DashboardViewModel vm)
                {
                    await vm.HandleDropFolderAsync(path);
                }
            }
        }
    }
}
