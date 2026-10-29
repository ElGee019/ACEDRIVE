using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the Touristenfahrten content table (Data/touristenfahrten.json): the
/// pickup shop kinds with their company names and order ranges, the dropoff
/// places and the run's draw bounds. A missing or unusable file degrades to
/// <see cref="TouristenMenu.Default"/> with a warning — the overlay must never
/// fail to start over a data file.
/// </summary>
public sealed class JsonTouristenMenu
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonTouristenMenu(ILog log)
        : this(DataPaths.GameData("touristenfahrten.json"), log)
    { }

    public JsonTouristenMenu(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TouristenMenu Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "touristenfahrten.json", "using built-in defaults",
                menu => menu.IsUsable, out TouristenMenu? menu))
        {
            return TouristenMenu.Default;
        }

        _log.LogInfo(
            $"touristenfahrten loaded: {menu.Pickups.Count} pickup kinds, " +
            $"{menu.Dropoffs.Count} dropoffs, {menu.MinDropoffs}-{menu.MaxDropoffs} per run");
        return menu;
    }
}
