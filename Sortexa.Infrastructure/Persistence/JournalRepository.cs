using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.Infrastructure.Persistence;

public sealed class JournalRepository : IJournalRepository
{
    private readonly string _journalDirectory;
    private readonly ILoggerService? _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public JournalRepository(string? customDir = null, ILoggerService? logger = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(customDir))
        {
            _journalDirectory = customDir;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _journalDirectory = Path.Combine(appData, "Sortexa", "journal");
        }

        Directory.CreateDirectory(_journalDirectory);
    }

    public async Task SaveJournalEntryAsync(JournalEntry entry)
    {
        try
        {
            string filePath = GetFilePath(entry.OperationId);
            string tempPath = filePath + ".tmp";

            string json = JsonSerializer.Serialize(entry, JsonOpts);
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempPath, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save journal entry {entry.OperationId}: {ex.Message}", ex);
            throw;
        }
    }

    public async Task<IReadOnlyList<JournalEntry>> GetAllEntriesAsync()
    {
        var list = new List<JournalEntry>();

        try
        {
            if (!Directory.Exists(_journalDirectory))
                return list;

            var files = Directory.GetFiles(_journalDirectory, "journal_*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(file);
                    var entry = JsonSerializer.Deserialize<JournalEntry>(json);
                    if (entry != null)
                    {
                        list.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Skipped corrupted journal file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error reading journal directory: {ex.Message}", ex);
        }

        return list.OrderByDescending(e => e.Timestamp).ToList();
    }

    public async Task<JournalEntry?> GetEntryByIdAsync(Guid operationId)
    {
        string filePath = GetFilePath(operationId);
        if (!File.Exists(filePath))
            return null;

        try
        {
            string json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<JournalEntry>(json);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load journal entry {operationId}: {ex.Message}", ex);
            return null;
        }
    }

    public async Task UpdateJournalEntryAsync(JournalEntry entry)
    {
        await SaveJournalEntryAsync(entry);
    }

    private string GetFilePath(Guid operationId)
    {
        return Path.Combine(_journalDirectory, $"journal_{operationId:N}.json");
    }
}
