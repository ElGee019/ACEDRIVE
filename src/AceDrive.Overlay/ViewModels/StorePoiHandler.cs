using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Grocery-shop arrivals (PORTPLAN items 24/26, Unity MaxsScreen): the store
/// page over this POI's live inventory. Other store families (coffee shops)
/// join by widening <see cref="Handles"/> and their data category — the page
/// itself already reads the category layout.
/// </summary>
public sealed class StorePoiHandler : IPoiArrivalHandler
{
    private readonly StoreNetwork _stores;
    private readonly Trunk _trunk;
    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly IFriendshipStore _friendships;
    private readonly VoiceBeat _beat;

    public StorePoiHandler(
        StoreNetwork stores, Trunk trunk, DriverWallet wallet, Garage garage,
        IFriendshipStore friendships, VoiceBeat beat)
    {
        _stores = stores;
        _trunk = trunk;
        _wallet = wallet;
        _garage = garage;
        _friendships = friendships;
        _beat = beat;
    }

    public bool Owns(PointOfInterest poi) =>
        string.Equals(poi.Type, PoiTypes.Grocery, StringComparison.OrdinalIgnoreCase)
        || string.Equals(poi.Type, PoiTypes.GroceryStore, StringComparison.OrdinalIgnoreCase)
        || string.Equals(poi.Type, PoiTypes.MaxsGrocery, StringComparison.OrdinalIgnoreCase);

    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged) =>
        new StoreScreenViewModel(
            poi, _stores.For(poi), _trunk, _wallet, _garage, _friendships,
            _beat, backCommand, onChanged);
}
