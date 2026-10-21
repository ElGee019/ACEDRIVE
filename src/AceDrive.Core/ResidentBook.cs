// The address-keyed resident table (PORTPLAN items 11/24/45, port of Unity
// TaxiJobResidentService). A proposal's two addresses resolve to the same
// homeowner every time they come up again, so the customer's identity — and
// the familiarity keyed on it — survives decline/relaunch like Unity's
// persisted resident POIs. Pure state: the store owns the file.
namespace AceDrive.Core;

/// <summary>
/// One resident per address, loaded from the store and banked on first
/// encounter. Names draw from a seeded generator owned here, independent of
/// the proposal's address/flip stream, exactly like Unity's global name
/// manager.
/// </summary>
public sealed class ResidentBook
{
    private readonly Dictionary<string, Resident> _byAddress =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ProceduralNameGenerator _names;
    private readonly IResidentStore _store;
    private readonly ILog _log;

    public ResidentBook(
        IResidentStore store, ProceduralNameGenerator names, ILog log)
    {
        _store = store;
        _names = names;
        _log = log;

        foreach (Resident? saved in store.Load())
        {
            if (saved is not null && IsValid(saved))
            {
                _byAddress[KeyOf(saved.Address)] = saved;
            }
        }
    }

    /// <summary>
    /// The homeowner at the address, drawn and persisted the first time the
    /// address is used. Saves immediately (the JsonFriendshipStore pattern),
    /// because a ready resident is progress the next launch must reopen.
    /// </summary>
    public Resident GetOrCreate(AddressLocation address)
    {
        string key = KeyOf(address);
        if (_byAddress.TryGetValue(key, out Resident? existing))
        {
            return existing;
        }

        var resident = new Resident(address, _names.Next());
        _byAddress[key] = resident;
        _store.Save([.. _byAddress.Values]);
        _log.LogInfo(
            $"resident created at {address.StreetName} No. {address.HouseNumber}: {resident.Name.Full}");
        return resident;
    }

    /// <summary>
    /// An unplaced name for the data-broken fallback (the map cannot supply
    /// two streets). Not persisted: there is no address to remember it by.
    /// </summary>
    public ProceduralName DrawName() => _names.Next();

    private static string KeyOf(AddressLocation address) =>
        $"{address.StreetName.Trim()}|{address.HouseNumber}";

    private static bool IsValid(Resident resident) =>
        !string.IsNullOrWhiteSpace(resident.Address.StreetName)
        && resident.Address.HouseNumber > 0
        && resident.Name is not null
        && !string.IsNullOrWhiteSpace(resident.Name.FirstName)
        && !string.IsNullOrWhiteSpace(resident.Name.Surname);
}
