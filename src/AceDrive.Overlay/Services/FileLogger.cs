using System.IO;
using AceDrive.Core;

using System.Globalization;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Dated file sink (%AppData%\AceDrive\logs\acedrive.log.MM-dd,
/// PitVox-style). Lock-guarded, rolls daily, never throws. Year is left
/// out of the date: within the overlay's context it is irrelevant.
/// </summary>
public sealed class FileLogger : ILog, IDisposable
{
    private readonly string _dir;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private string _openDay = "";
    private bool _disposed;

    public FileLogger()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "logs"))
    { }

    public FileLogger(string dir) => _dir = dir;

    public void LogInfo(string message) => Write("INFO", message);
    public void LogWarning(string message) => Write("WARN", message);
    public void LogError(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                string day = DateTime.Now.ToString("MM-dd", CultureInfo.InvariantCulture);
                if (_writer is null || day != _openDay)
                {
                    _writer?.Dispose();
                    Directory.CreateDirectory(_dir);
                    _writer = new StreamWriter(
                        Path.Combine(_dir, $"acedrive.log.{day}"),
                        append: true)
                    { AutoFlush = true };
                    _openDay = day;
                }
                _writer.WriteLine($"{DateTime.Now:MM-ddTHH:mm:ss} {level,-5} {message}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the app, but only genuine sink
            // failures (locked/corrupt log dir) are swallowed here:
            // anything else is a bug worth surfacing (AGENTS.md: no bare catch).
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
