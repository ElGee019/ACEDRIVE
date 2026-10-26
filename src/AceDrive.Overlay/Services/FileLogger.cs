using System.Globalization;
using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Dated file sink (%AppData%\AceDrive\logs\acedrive.log.MM-dd,
/// PitVox-style). Lock-guarded, rolls daily, never throws.
/// A sink that cannot be opened — a second overlay holding the file, a
/// read-only %AppData% — relocates once to the temp dir instead of dropping
/// every line: a mute log is what made a failed game reset undiagnosable.
/// </summary>
public sealed class FileLogger : ILog, IDisposable
{
    private readonly string _dir;
    private readonly string _fallbackDir;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private string _sinkDir;
    private string _openDay = "";
    private bool _relocated;
    private bool _dead;
    private bool _disposed;

    public FileLogger()
        : this(DefaultDirectory(), FallbackDirectory())
    { }

    /// <summary>Both sinks explicit, so a test never touches the real ones.</summary>
    public FileLogger(string dir, string fallbackDir)
    {
        _dir = dir;
        _fallbackDir = fallbackDir;
        _sinkDir = dir;
    }

    public void LogInfo(string message) => Write("INFO", message);
    public void LogWarning(string message) => Write("WARN", message);
    public void LogError(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            if (_disposed || _dead) return;
            if (TryWrite(level, message)) return;

            // WHY one relocation only: a second instance used to lose the whole
            // session's trace silently. One move to a writable sink keeps it,
            // and retrying a dead sink forever would only churn per line.
            if (_relocated)
            {
                _dead = true;
                return;
            }

            _relocated = true;
            CloseWriter();
            _sinkDir = _fallbackDir;
            if (!TryWrite("WARN", $"log sink {_dir} is unavailable; writing to {_fallbackDir}")
                || !TryWrite(level, message))
            {
                _dead = true;
            }
        }
    }

    private bool TryWrite(string level, string message)
    {
        try
        {
            string day = DateTime.Now.ToString("MM-dd", CultureInfo.InvariantCulture);
            if (_writer is null || day != _openDay)
            {
                CloseWriter();
                Directory.CreateDirectory(_sinkDir);
                _writer = new StreamWriter(
                    Path.Combine(_sinkDir, $"acedrive.log.{day}"),
                    append: true)
                { AutoFlush = true };
                _openDay = day;
            }

            _writer.WriteLine($"{DateTime.Now:MM-ddTHH:mm:ss} {level,-5} {message}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only genuine sink failures (locked/corrupt log dir) are swallowed
            // here; anything else is a bug worth surfacing (AGENTS.md: no bare
            // catch).
            CloseWriter();
            return false;
        }
    }

    private void CloseWriter()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (IOException)
        {
            // WHY: a flush that fails on the way out must not take the logger,
            // or the caller, down — the handle is being dropped either way.
        }

        _writer = null;
        _openDay = "";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            CloseWriter();
        }
    }

    // WHY DataPaths and not a literal: a demo build keeps its trace in its own
    // profile folder, so running the demo never writes into a real player's log.
    private static string DefaultDirectory() => DataPaths.UserLogs;

    // WHY temp: it is the one folder a per-user process can almost always
    // write, so the trace survives a locked or read-only %AppData%.
    private static string FallbackDirectory() => Path.Combine(
        Path.GetTempPath(), "AceDrive", "logs");
}
