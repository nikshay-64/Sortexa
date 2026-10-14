using System;

namespace Sortexa.Core.Interfaces;

public interface ILoggerService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    string RedactPath(string? path);
}
