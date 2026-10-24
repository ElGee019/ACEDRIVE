using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Deletes the save files under %AppData%\AceDrive. Explicitly listed, never a
/// directory sweep: the same folder holds the player's settings
/// (preferences.json, window.json, hud.json) and firebase.json, and wiping
/// those would reset the chosen language or break the online layer. A new
/// persistent store must add its file here.
/// </summary>
public sealed class FileSaveDataResetter : ISaveDataResetter
{
    // The career: onboarding walk + profile, wallet/trunk/streak/zone,
    // garage, town, appetite, clock/week, road test, taxi shift, familiarity,
    // brand mileage, the resident book drawn for taxi jobs and the discovered
    // POI addresses.
    private static readonly string[] SaveFiles =
    [
        "onboarding.json",
        "profile.json",
        "driver.json",
        "garage.json",
        "city.json",
        "hunger.json",
        "gameclock.json",
        "roadtest.json",
        "shift.json",
        "friendship.json",
        "brand_mileage.json",
        "residents.json",
        "addresses.json",
    ];

    private readonly string _directory;
    private readonly ILog _log;

    public FileSaveDataResetter(ILog log)
        : this(DataPaths.UserDirectory, log)
    { }

    public FileSaveDataResetter(string directory, ILog log)
    {
        _directory = directory;
        _log = log;
    }

    public bool DeleteAll()
    {
        bool complete = true;
        foreach (string name in SaveFiles)
        {
            string file = Path.Combine(_directory, name);
            try
            {
                if (!File.Exists(file)) continue;
                File.Delete(file);
                _log.LogInfo($"reset removed {name}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A locked or protected save must not take the overlay down;
                // report the partial wipe so the caller keeps playing instead
                // of restarting onto a half-deleted career.
                complete = false;
                _log.LogWarning($"reset could not remove {name} ({ex.GetType().Name})");
            }
        }

        return complete;
    }
}
