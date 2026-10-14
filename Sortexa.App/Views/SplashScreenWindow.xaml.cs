using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Sortexa.App.Views;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
    }

    public async Task PlaySplashAsync(int minDurationMs = 2000)
    {
        var storyboard = (Storyboard)Resources["StoryboardSplashSequence"];
        storyboard.Begin(this);

        // Keep splash presentation active for the specified duration
        await Task.Delay(minDurationMs);
    }
}
