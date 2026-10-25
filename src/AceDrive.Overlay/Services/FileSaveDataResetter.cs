using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Deletes the save files under %AppData%\AceDrive, together with the backup
/// and temp companions the JSON boundary writes beside each one. Explicitly
/// listed, never a directory sweep: the same folder holds the player's settings
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
        "todos_state.json",
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

    public SaveResetReport DeleteAll()
    {
        // The first survivor is the one the player has to unblock, so it names
        // the report; the loop still tries the rest so a stuck file costs one
        // record instead of the whole wipe.
        string? blockedFile = null;
        SaveResetBlock block = SaveResetBlock.None;
        foreach (string name in SaveFiles)
        {
            // The backup holds the same career, and the read path recovers from
            // it: deleting the primary while the backup survives would let the
            // next boot resurrect the career the player just wiped. A backup
            // that will not go therefore skips its whole save.
            if (!TryDelete(name + JsonFile.BackupSuffix, ref blockedFile, ref block))
            {
                continue;
            }

            // A half-written temp is never read, so it cannot resurrect a
            // career; a failure here is litter, not a surviving save.
            TryDeleteTemp(name + JsonFile.TempSuffix);
            TryDelete(name, ref blockedFile, ref block);
        }

        return new SaveResetReport(blockedFile, block);
    }

    // Deletes one file. Returns false when it stayed, recording the first
    // survivor so the caller can name the file the player has to unblock.
    private bool TryDelete(
        string name, ref string? blockedFile, ref SaveResetBlock block)
    {
        string file = Path.Combine(_directory, name);
        try
        {
            if (!File.Exists(file)) return true;
            File.Delete(file);
            _log.LogInfo($"reset removed {name}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked or protected save must not take the overlay down;
            // report the partial wipe so the caller keeps playing instead of
            // restarting onto a half-deleted career.
            if (blockedFile is null)
            {
                blockedFile = name;
                block = ex is IOException ? SaveResetBlock.InUse : SaveResetBlock.Denied;
            }

            _log.LogWarning($"reset could not remove {name} ({ex.GetType().Name})");
            return false;
        }
    }

    private void TryDeleteTemp(string name)
    {
        string file = Path.Combine(_directory, name);
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // WHY ignored: the temp file is never read, so it cannot bring the
            // career back; reporting it would make a clean wipe look blocked.
            _log.LogWarning($"reset could not remove {name} ({ex.GetType().Name})");
        }
    }
}
