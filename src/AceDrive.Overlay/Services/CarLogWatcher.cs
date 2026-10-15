using System.IO;
using System.Text;
using System.Threading;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Watches %USERPROFILE%\Saved Games\ACE\Logs for the player's current car and
/// publishes its spoken brand (wake word "hey {brand}" follows the car you
/// actually drive, per the companion spec — NOT the saved favorite brand).
/// Runs on its own background thread; the UI Poll() loop stays allocation-
/// free. Logs brand transitions only, never per poll.
/// </summary>
public sealed class CarLogWatcher : ICarBrandSource
{
    private const double PollSec = 2.0;
    private static readonly Encoding Utf8 = Encoding.UTF8;

    private readonly ILog _log;
    private readonly DirectoryInfo _logsDir;
    private Thread? _thread;
    private volatile bool _running;
    private string? _watchedFile;
    private long _offset;
    private string _pending = "";
    private string? _brand;
    private string? _ksId;

    public string? CurrentKsId => _ksId;

    public CarLogWatcher(ILog log, DirectoryInfo? logsDir = null)
    {
        _log = log;
        _logsDir = logsDir ?? new DirectoryInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games", "ACE", "Logs"));
    }

    public event EventHandler<CarBrandChangedEventArgs>? BrandChanged;

    public string Prime(string fallback)
    {
        // Newest logs first: the last car line anywhere recent is the current car.
        foreach (FileInfo log in RecentLogs(5))
        {
            try
            {
                string? ks = CarBrand.FindCurrentKs(File.ReadLines(log.FullName).TakeLast(500));
                if (ks is not null && CarBrand.BrandOf(ks) is string brand)
                {
                    _brand = brand;
                    _ksId = ks;
                    return brand;
                }
            }
            catch (IOException) { /* unreadable log; older ones may still answer */ }
            catch (UnauthorizedAccessException) { }
        }

        _brand = fallback;
        return fallback;
    }

    public void StartWatching()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "car-brand-watcher" };
        _thread.Start();
    }

    public void StopWatching() => _running = false;

    public void Dispose() => StopWatching();

    private void Run()
    {
        while (_running)
        {
            try
            {
                PumpLatestLog();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.LogWarning($"car watcher: {ex.GetType().Name}");
            }

            Thread.Sleep(TimeSpan.FromSeconds(PollSec));
        }
    }

    private void PumpLatestLog()
    {
        FileInfo? latest = RecentLogs(1).LastOrDefault();
        if (latest is null) return;

        if (latest.FullName != _watchedFile)
        {
            _watchedFile = latest.FullName;
            _offset = 0;
            _pending = "";
        }

        string chunk;
        using (var fs = new FileStream(_watchedFile, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite))
        {
            if (fs.Length <= _offset) return;
            fs.Seek(_offset, SeekOrigin.Begin);
            byte[] buf = new byte[fs.Length - _offset];
            fs.ReadExactly(buf);
            _offset = fs.Length;
            chunk = _pending + Utf8.GetString(buf);
        }

        // Process whole lines only; a trailing partial line is kept for next pump.
        int cut = chunk.LastIndexOf('\n');
        if (cut < 0)
        {
            _pending = chunk;
            return;
        }

        _pending = chunk[(cut + 1)..];
        foreach (string line in chunk[..cut].Split('\n'))
        {
            HandleLine(line.TrimEnd('\r'));
        }
    }

    private void HandleLine(string line)
    {
        string? ksId = CarBrand.KsIdOfLogLine(line);
        string? brand = ksId is not null ? CarBrand.BrandOf(ksId) : null;
        if (brand is not null && (brand != _brand || ksId != _ksId))
        {
            _brand = brand;
            _ksId = ksId;
            BrandChanged?.Invoke(this, new CarBrandChangedEventArgs(brand, ksId));
        }
    }

    private FileInfo[] RecentLogs(int n)
    {
        try
        {
            if (!_logsDir.Exists) return [];
            return _logsDir.GetFiles("log-*.txt")
                .OrderBy(f => f.LastWriteTimeUtc)
                .TakeLast(n)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning($"car watcher: {ex.GetType().Name}");
            return [];
        }
    }
}
