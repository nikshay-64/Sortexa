using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.Infrastructure.Persistence;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly string _settingsFilePath;
    private readonly ILoggerService? _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public SettingsRepository(string? customPath = null, ILoggerService? logger = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _settingsFilePath = customPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "Sortexa");
            Directory.CreateDirectory(dir);
            _settingsFilePath = Path.Combine(dir, "settings.json");
        }
    }

    public async Task<SortexaSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<SortexaSettings>(json, JsonOpts);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error reading settings from {_settingsFilePath}: {ex.Message}", ex);
        }

        return new SortexaSettings();
    }

    public async Task SaveSettingsAsync(SortexaSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempPath = _settingsFilePath + ".tmp";
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
            }

            File.Move(tempPath, _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error writing settings to {_settingsFilePath}: {ex.Message}", ex);
        }
    }
}
