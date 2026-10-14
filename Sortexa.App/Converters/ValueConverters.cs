using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Sortexa.Core.Models;

namespace Sortexa.App.Converters;

public sealed class EnumBooleanConverter : IValueConverter
{
    public object? TargetValue { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? targetString = TargetValue?.ToString() ?? parameter?.ToString();
        if (value == null || targetString == null)
            return false;

        return string.Equals(value.ToString(), targetString, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? targetString = TargetValue?.ToString() ?? parameter?.ToString();
        if (value is bool b && b && targetString != null)
        {
            return Enum.Parse(targetType, targetString, true);
        }

        return Binding.DoNothing;
    }
}

public sealed class BytesFormattedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
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

        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string status = value?.ToString() ?? string.Empty;
        return status switch
        {
            "Eligible" or "Moved" or "Success" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#20C997")),
            "Skipped" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB547")),
            "Failed" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B81")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3987FF"))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public static readonly EmptyStringToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int len)
            return len == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (value is string s)
            return string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;

        return true;
    }
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
            return !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;
        if (value is int count)
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class SettingsDisplayConverter : IValueConverter
{
    public static readonly SettingsDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        return value switch
        {
            OrganizationMethod.SmartCategories => "Smart Categories",
            OrganizationMethod.FileExtensions => "File Extensions",
            OrganizationMethod.DateCreated => "Date Created",
            OrganizationMethod.LastModified => "Date Modified",
            OrganizationMethod.FileSize => "File Size",
            OrganizationMethod.FilePurpose => "File Purpose",
            OrganizationMethod.CustomRules => "Custom Rules",

            DuplicateResolutionPolicy.RenameWithCounter => "Rename with Counter",
            DuplicateResolutionPolicy.Skip => "Skip Duplicate",

            "System" => "System Default",
            "Dark" => "Dark Theme",
            "Light" => "Light Theme",

            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
