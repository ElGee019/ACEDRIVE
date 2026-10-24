using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the taxi residents under %AppData%\AceDrive\residents.json.
/// Never throws: a missing or corrupt file means every address is new again,
/// and a failed write must never break gameplay or shutdown. Street matching
/// is case-insensitive in ResidentBook, like Unity's owner lookup.
/// </summary>
public sealed class JsonResidentStore : IResidentStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonResidentStore(ILog log)
        : this(DataPaths.UserSave("residents.json"), log)
    { }

    public JsonResidentStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<Resident> Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out List<Resident>? residents, out string? error);
        if (status == JsonReadStatus.Unreadable)
        {
            // A corrupt save degrades to a fresh resident table, never takes
            // the overlay down.
            _log.LogWarning(
                $"residents.json unreadable at {_file} ({error}); starting empty");
        }

        return residents ?? [];
    }

    public void Save(IReadOnlyList<Resident> residents)
    {
        // A failed bank must never break gameplay; the book keeps the
        // resident in memory and the next creation retries the write.
        if (!JsonFile.TryWrite(_file, residents, out string? error))
        {
            _log.LogWarning(
                $"residents.json unwritable at {_file} ({error}); residents kept in memory");
        }
    }
}
