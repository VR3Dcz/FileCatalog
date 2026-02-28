using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileCatalog.Services.Core;

public class AppLogger
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AppLogger(PathProvider pathProvider)
    {
        _logFilePath = Path.Combine(pathProvider.AppDataDirectory, "scan_errors.log");
    }

    public async Task LogInfoAsync(string message) => await LogAsync("INFO", message);

    public async Task LogErrorAsync(string message, Exception? ex = null)
    {
        string errorDetails = ex != null ? $" | Výjimka: {ex.Message}" : "";
        await LogAsync("ERROR", $"{message}{errorDetails}");
    }

    private async Task LogAsync(string level, string message)
    {
        await _semaphore.WaitAsync();
        try
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(_logFilePath, logLine);
        }
        catch { /* Kritická ochrana: Pád logování nesmí shodit aplikaci */ }
        finally
        {
            _semaphore.Release();
        }
    }
}