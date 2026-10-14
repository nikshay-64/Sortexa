using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Sortexa.Tests.Common;

public static class StaTestRunner
{
    private static readonly Lazy<Dispatcher> _staDispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            if (Application.Current == null)
            {
                try
                {
                    new Application();
                }
                catch (InvalidOperationException)
                {
                }
            }

            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "Sortexa-Wpf-Test-STA-Thread";
        thread.Start();
        ready.Wait();

        return dispatcher!;
    });

    public static void Run(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA &&
            _staDispatcher.IsValueCreated &&
            Thread.CurrentThread == _staDispatcher.Value.Thread)
        {
            action();
            return;
        }

        Exception? caught = null;
        _staDispatcher.Value.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        if (caught != null)
        {
            throw new AggregateException("Exception in STA test runner", caught);
        }
    }

    public static void EnsureResources(string themeResource = "Themes/DarkTheme.xaml")
    {
        if (Application.Current == null)
        {
            try
            {
                new Application();
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (Application.Current!.Resources.MergedDictionaries.Count == 0)
        {
            var dictionaries = new[]
            {
                "Styles/Colors.xaml",
                "Styles/Gradients.xaml",
                "Styles/Icons.xaml",
                "Styles/Typography.xaml",
                "Styles/Buttons.xaml",
                "Styles/Cards.xaml",
                "Styles/Controls.xaml",
                "Styles/Animations.xaml",
                themeResource
            };

            foreach (var dict in dictionaries)
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/Sortexa;component/{dict}", UriKind.RelativeOrAbsolute)
                });
            }

            Application.Current.Resources["BoolToVis"] = new System.Windows.Controls.BooleanToVisibilityConverter();
            Application.Current.Resources["StringToVis"] = Sortexa.App.Converters.StringToVisibilityConverter.Instance;
            Application.Current.Resources["InverseBoolConverter"] = Sortexa.App.Converters.InverseBooleanConverter.Instance;
            Application.Current.Resources["BytesConverter"] = new Sortexa.App.Converters.BytesFormattedConverter();
            Application.Current.Resources["StatusColorConverter"] = new Sortexa.App.Converters.StatusColorConverter();
        }
    }
}
