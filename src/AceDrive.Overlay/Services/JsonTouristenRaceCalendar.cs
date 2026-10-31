using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the real-world Nürburgring calendar (Data/touristenweekends.json): the
/// events whose days title the Touristenfahrten briefing and pay its bonus. A
/// missing or unusable file degrades to <see cref="TouristenRaceCalendar.None"/>
/// with a warning — the run still works, it just never names an event or pays
/// a bonus.
/// </summary>
public sealed class JsonTouristenRaceCalendar
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonTouristenRaceCalendar(ILog log)
        : this(DataPaths.GameData("touristenweekends.json"), log)
    { }

    public JsonTouristenRaceCalendar(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TouristenRaceCalendar Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "touristenweekends.json", "using no race weekend",
                calendar => calendar.IsUsable, out TouristenRaceCalendar? calendar))
        {
            return TouristenRaceCalendar.None;
        }

        _log.LogInfo(
            $"touristen weekend calendar loaded: {calendar.Weekends.Count} event(s), " +
            $"x{calendar.BonusMultiplier:0.##} payout");
        return calendar;
    }
}
