using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The post-introduction community centre interior (PORTPLAN item 54): the
/// licensing conversation is the introduction's, so after it is banked the
/// same door becomes the town's hiring hall. This assembler owns the one
/// Unity-shaped adaptation the port needs — the resident table only grows
/// from taxi draws, so the centre seeds enough free-house residents to fill a
/// roster — and keeps the page itself free of map and file work.
///
/// A hire warms Signore Moro's familiarity, like Unity's OnHireClicked; the
/// roster banks the crew, the friendship store banks the level, and this class
/// only logs the result. Cold path: a POI arrival, never the poll.
/// </summary>
public sealed class CommunityCentreInteriors
{
    private readonly EmployeeRoster _roster;
    private readonly ResidentBook _residents;
    private readonly PoiDirectory _directory;
    private readonly OperatingZone _zone;
    private readonly PoiGenerator _generator;
    private readonly IFriendshipStore _friendships;
    private readonly Func<int> _week;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    public CommunityCentreInteriors(
        EmployeeRoster roster, ResidentBook residents, PoiDirectory directory,
        OperatingZone zone, PoiGenerator generator, IFriendshipStore friendships,
        Func<int> week, VoiceBeat beat, ILog log)
    {
        _roster = roster;
        _residents = residents;
        _directory = directory;
        _zone = zone;
        _generator = generator;
        _friendships = friendships;
        _week = week;
        _beat = beat;
        _log = log;
    }

    /// <summary>
    /// The hiring page for this visit. The resident pool is topped up first so
    /// an early career can still offer five candidates, then the week's roster
    /// is refreshed (a new display week re-rolls it) before the page opens on
    /// its button.
    /// </summary>
    public CommunityCentreScreenViewModel Open(ICommand backCommand)
    {
        SeedResidents();
        _roster.RefreshCandidates(ResidentNames(), _week());
        return new CommunityCentreScreenViewModel(
            _roster, ResidentNames, _week, _friendships, backCommand, _beat, OnHired);
    }

    // Unity's town has resident POIs to hire from; the port's resident book is
    // only filled by taxi customers, so a first post-intro visit could open on
    // an empty roster. Draw free houses on the zone's map until the pool can
    // hold a full roster beside the crew already hired. The book saves each
    // resident itself (GetOrCreate), so a relaunch meets the same people.
    private void SeedResidents()
    {
        int target = EmployeeRoster.CandidateCount + _roster.Employees.Count;
        if (_residents.Residents.Count >= target)
        {
            return;
        }

        var taken = new List<AddressLocation>();
        foreach (PointOfInterest poi in _directory.All)
        {
            taken.Add(poi.Address);
        }
        foreach (Resident resident in _residents.Residents)
        {
            taken.Add(resident.Address);
        }

        int created = 0;
        while (_residents.Residents.Count < target)
        {
            if (WhereIsWillyCandidates.PickFreeHouse(
                    _zone.ZoneMap, _generator, taken) is not { } door)
            {
                break;
            }

            int before = _residents.Residents.Count;
            Resident resident = _residents.GetOrCreate(door);
            taken.Add(resident.Address);
            if (_residents.Residents.Count == before)
            {
                // Defensive: a draw that resolves to an already-known house
                // would otherwise spin forever on a hand-edited map.
                break;
            }

            created++;
        }

        if (created > 0)
        {
            _log.LogInfo(
                $"[CommunityCentre] seeded {created} resident(s) for the hiring roster");
        }
    }

    // The names the roster may draw from, in the resident book's stable order
    // so the seeded weekly roll stays deterministic.
    private List<string> ResidentNames()
    {
        var names = new List<string>(_residents.Residents.Count);
        foreach (Resident resident in _residents.Residents)
        {
            string name = resident.Name.BaseName;
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private void OnHired(string name)
    {
        // Unity OnHireClicked: hiring warms the centre owner's familiarity, and
        // the next friendship tier is what the player sees on the row.
        _friendships.AddFamiliarity(StoryCommunityCentre.OwnerName, 1);
        _log.LogInfo(
            $"[CommunityCentre] hired {name} " +
            $"({_roster.Employees.Count} employee(s))");
    }
}
