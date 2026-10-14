using System;
using System.IO;
using System.Text.RegularExpressions;
using Sortexa.Core.Interfaces;

namespace Sortexa.Infrastructure.Logging;

public sealed class LoggerService : ILoggerService
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LoggerService(string? customLogPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customLogPath))
        {
            _logFilePath = customLogPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDir = Path.Combine(appData, "Sortexa", "logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, "sortexa.log");
        }
    }

    public void LogInfo(string message) => WriteEntry("INFO", message);
    public void LogWarning(string message) => WriteEntry("WARN", message);
    public void LogError(string message, Exception? exception = null)
    {
        string fullMessage = exception == null
            ? message
            : $"{message} | Exception: {exception.GetType().Name}: {exception.Message}";
        WriteEntry("ERROR", fullMessage);
    }

    public string RedactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            // Mask username in user profiles: C:\Users\username -> C:\Users\***
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                string rel = path.Substring(userProfile.Length).TrimStart('\\', '/');
                return Path.Combine("~", RedactSensitiveTokens(rel));
            }

            return RedactSensitiveTokens(path);
        }
        catch
        {
            return "[Path]";
        }
    }

    private static string RedactSensitiveTokens(string input)
    {
        // Redact potential certificates, keys, or credential tokens
        string redacted = Regex.Replace(input, @"(id_rsa|.*\.pem|.*\.pfx|.*\.key|.*token.*|.*password.*)", "[REDACTED]", RegexOptions.IgnoreCase);
        return redacted;
    }

    private void WriteEntry(string level, string message)
    {
        try
        {
            string safeMessage = RedactSensitiveTokens(message);
            string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{level}] {safeMessage}{Environment.NewLine}";

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, line);
            }
        }
        catch
        {
            // Logging should never crash the app
        }
    }
}
