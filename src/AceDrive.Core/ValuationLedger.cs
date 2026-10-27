// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The weekly valuation's persisted half (Unity PlayerData.valuation* fields):
// the venue the player is driving to, the week's own entry, the settled uid
// set and the event counter. The flow owns the timing; this class owns the
// state that survives a restart.
namespace AceDrive.Core;

/// <summary>
/// State one restart must reopen for the weekly market valuation (PORTPLAN
/// item 49): the preloaded venue, the in-flight drive, the week's entry
/// (score + car + week), the seen-uid set the live/offline comparison pays
/// from, the lifetime completed-event count and the last reset week.
/// </summary>
public sealed class ValuationLedger
{
    /// <summary>Sentinel for "no week yet": no entry and no reset recorded.</summary>
    public const int NoWeek = -1;

    private readonly List<string> _seen;

    public ValuationLedger(DriverState? saved = null)
    {
        VenueStreetId = string.IsNullOrWhiteSpace(saved?.ValuationVenueStreetId)
            ? null
            : saved.ValuationVenueStreetId;
        VenueHouseNumber = Math.Max(0, saved?.ValuationVenueHouseNumber ?? 0);
        Driving = saved?.ValuationDriving ?? false;
        EventsCompleted = Math.Max(0, saved?.ValuationEventsCompleted ?? 0);
        EntryWeek = saved?.ValuationEntryWeek ?? NoWeek;
        EntryCarKey = string.IsNullOrWhiteSpace(saved?.ValuationEntryCarKey)
            ? null
            : saved.ValuationEntryCarKey;
        OwnScore = double.IsFinite(saved?.ValuationOwnScore ?? 0.0)
            ? Math.Max(0.0, saved?.ValuationOwnScore ?? 0.0)
            : 0.0;
        LastResetWeek = saved?.LastValuationResetWeek ?? NoWeek;
        _seen = CleanSeen(saved?.ValuationSeenUids);
    }

    /// <summary>Street id of this week's venue, null when none is drawn.</summary>
    public string? VenueStreetId { get; private set; }

    /// <summary>House number of this week's venue, 0 when none is drawn.</summary>
    public int VenueHouseNumber { get; private set; }

    /// <summary>True while the player is on the leg to the venue (Unity valuationDriving).</summary>
    public bool Driving { get; private set; }

    /// <summary>Lifetime completed showcase events (Unity valuationEventsCompleted).</summary>
    public int EventsCompleted { get; private set; }

    /// <summary>Week key the own entry belongs to, <see cref="NoWeek"/> when none.</summary>
    public int EntryWeek { get; private set; }

    /// <summary>Catalogue key of the car entered this week, null when none.</summary>
    public string? EntryCarKey { get; private set; }

    /// <summary>The score pushed with this week's entry, fixed until the reset.</summary>
    public double OwnScore { get; private set; }

    /// <summary>Every uid settled for the current week's entry.</summary>
    public IReadOnlyList<string> SeenUids => _seen;

    /// <summary>Week key of the last weekly reset, <see cref="NoWeek"/> before the first.</summary>
    public int LastResetWeek { get; private set; }

    /// <summary>True when the given week has an entry (the live/offline comparison gate).</summary>
    public bool HasEntry(int week) => EntryWeek == week && !string.IsNullOrEmpty(EntryCarKey);

    /// <summary>True when a venue is stored (Unity IsPreloaded).</summary>
    public bool HasVenue =>
        VenueHouseNumber > 0 && !string.IsNullOrWhiteSpace(VenueStreetId);

    /// <summary>Stores the drawn venue (Unity DiscoverAsync's save).</summary>
    public void SetVenue(ValuationVenue venue)
    {
        ArgumentNullException.ThrowIfNull(venue);
        VenueStreetId = venue.StreetId;
        VenueHouseNumber = Math.Max(0, venue.HouseNumber);
    }

    /// <summary>Consumes the venue: the arrival opened the showcase.</summary>
    public void ClearVenue()
    {
        VenueStreetId = null;
        VenueHouseNumber = 0;
        Driving = false;
    }

    /// <summary>The DRIVE tap started the leg (Unity player.valuationDriving = true).</summary>
    public void BeginDrive() => Driving = true;

    /// <summary>The leg was abandoned or completed without an arrival.</summary>
    public void EndDrive() => Driving = false;

    /// <summary>The arrival completed the event (Unity's counter and consume tail).</summary>
    public void CompleteEvent()
    {
        EventsCompleted++;
        ClearVenue();
    }

    /// <summary>Records this week's entry and its fixed score.</summary>
    public void MarkEntry(string carKey, double score, int week)
    {
        EntryCarKey = string.IsNullOrWhiteSpace(carKey) ? null : carKey;
        OwnScore = double.IsFinite(score) ? Math.Max(0.0, score) : 0.0;
        EntryWeek = week;
    }

    /// <summary>
    /// Drops the entry (its doc vanished). The settled set deliberately stays:
    /// the flow re-seeds it with the entrant list it just read, so a later
    /// snapshot cannot pay a newcomer the vanished entry never met. Only
    /// <see cref="MarkReset"/> clears both together.
    /// </summary>
    public void ClearEntry()
    {
        EntryCarKey = null;
        OwnScore = 0.0;
        EntryWeek = NoWeek;
    }

    /// <summary>Adds uids to the settled set (union, never shrink).</summary>
    public void RecordSeen(IEnumerable<string>? uids)
    {
        if (uids is null)
        {
            return;
        }
        foreach (string uid in uids)
        {
            if (!string.IsNullOrEmpty(uid) && !_seen.Contains(uid, StringComparer.Ordinal))
            {
                _seen.Add(uid);
            }
        }
    }

    /// <summary>
    /// The Monday-midnight reset: the week's entry, its settled set and the
    /// venue go, and the new week is stamped so the reset runs once.
    /// </summary>
    public void MarkReset(int week)
    {
        ClearEntry();
        ClearVenue();
        _seen.Clear();
        LastResetWeek = week;
    }

    private static List<string> CleanSeen(IReadOnlyList<string>? saved)
    {
        List<string> seen = [];
        if (saved is null)
        {
            return seen;
        }
        foreach (string uid in saved)
        {
            if (!string.IsNullOrEmpty(uid) && !seen.Contains(uid, StringComparer.Ordinal))
            {
                seen.Add(uid);
            }
        }
        return seen;
    }
}
