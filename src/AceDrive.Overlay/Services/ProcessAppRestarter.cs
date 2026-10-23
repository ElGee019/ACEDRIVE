using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Restarts the overlay by spawning the same executable again, then ending
/// this process. The hard exit is deliberate: the window's close path banks
/// the live session, which would recreate the save files the reset just
/// deleted.
/// </summary>
public sealed class ProcessAppRestarter : IAppRestarter
{
    private readonly ILog _log;

    public ProcessAppRestarter(ILog log) => _log = log;

    public bool Restart()
    {
        string? executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            _log.LogError("restart failed: the process path is unknown");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            // WHY recoverable: the reset already deleted the save files, so
            // failing to spawn must leave this window alive (and the failure
            // logged) rather than dropping the player to the desktop.
            _log.LogError($"restart failed: {ex.Message}");
            return false;
        }

        // WHY Environment.Exit: a graceful Shutdown would run the window's
        // close handler and bank the live session back to disk. The return is
        // unreachable at runtime; the compiler still wants the bool path.
        Environment.Exit(0);
        return true;
    }
}
