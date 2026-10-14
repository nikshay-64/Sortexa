using System.Windows;
using Sortexa.App.ViewModels;

namespace Sortexa.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}