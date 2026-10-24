using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the shop item database and store layout from
/// Data/store_items.json next to the exe. Missing or invalid files degrade
/// to <see cref="StoreCatalog.Default"/> with a warning — a content file must
/// never block the shop page, and the fallback still carries Maple Syrup.
/// </summary>
public sealed class JsonStoreItemStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonStoreItemStore(ILog log)
        : this(DataPaths.GameData("store_items.json"), log)
    { }

    public JsonStoreItemStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public StoreCatalog Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "store_items.json", "using built-in store catalog",
                IsValid, out StoreCatalog? catalog))
        {
            return StoreCatalog.Default;
        }

        _log.LogInfo($"store catalog loaded: {catalog.Items.Count} item(s)");
        return catalog;
    }

    private static bool IsValid(StoreCatalog catalog)
    {
        // JSON can legally carry null where the record expects a list; treat
        // that like any other corrupt row instead of throwing at startup.
        if (catalog.DefaultStock <= 0 || catalog.Items is null || catalog.Items.Count == 0)
        {
            return false;
        }

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (StoreItem item in catalog.Items)
        {
            if (item is null
                || string.IsNullOrWhiteSpace(item.Name)
                || !names.Add(item.Name)
                || !double.IsFinite(item.Price) || item.Price < 0.0
                || !double.IsFinite(item.Volume) || item.Volume <= 0.0
                || item.Hunger < 0
                || string.IsNullOrWhiteSpace(item.Category))
            {
                return false;
            }
        }

        if (catalog.Layouts is not { } layouts)
        {
            return true;
        }

        foreach ((string storeCategory, IReadOnlyDictionary<string, int> sources) in layouts)
        {
            if (string.IsNullOrWhiteSpace(storeCategory) || sources is null)
            {
                return false;
            }
            foreach ((string source, int count) in sources)
            {
                if (string.IsNullOrWhiteSpace(source) || count < 0)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
