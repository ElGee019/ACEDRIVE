using System.IO;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonResidentStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "residents.json"), log)
    { }

    public JsonResidentStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<Resident> Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                return [];
            }

            List<Resident>? residents = JsonSerializer.Deserialize<List<Resident>>(
                File.ReadAllText(_file), JsonOptions);
            return residents ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt save degrades to a
            // fresh resident table, never takes the overlay down.
            _log.LogWarning(
                $"residents.json unreadable at {_file} ({ex.GetType().Name}); starting empty");
            return [];
        }
    }

    public void Save(IReadOnlyList<Resident> residents)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_file, JsonSerializer.Serialize(residents, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed bank must never break gameplay; the book keeps the
            // resident in memory and the next creation retries the write.
            _log.LogWarning(
                $"residents.json unwritable at {_file} ({ex.GetType().Name}); residents kept in memory");
        }
    }
}
