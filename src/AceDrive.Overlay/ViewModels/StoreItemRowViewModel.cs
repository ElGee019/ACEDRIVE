using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One row in the shop's available-items list (PORTPLAN item 26, Unity
/// ItemForList): the name/hunger/price copy plus the selection command. A
/// sold-out row keeps its hunger/price information but cannot be selected,
/// exactly like Unity's MaxsScreen.SelectItem guard.
/// </summary>
public sealed class StoreItemRowViewModel : ViewModelBase
{
    private readonly Action<StoreItemRowViewModel> _select;
    private bool _isSelected;

    public StoreItemRowViewModel(
        StoreItem item, int stock, Action<StoreItemRowViewModel> select)
    {
        Item = item;
        _select = select;
        IsSoldOut = stock <= 0;
        Name = IsSoldOut ? Strings.Get("StoreSoldOut") : item.Name;
        HungerLine = Strings.Format("StoreHunger", item.Hunger);
        PriceLine = Strings.Format("StoreCost", item.Price);
        SelectCommand = new RelayCommand(Select);
    }

    public StoreItem Item { get; }

    /// <summary>Item name, or the sold-out label when the shelf is empty.</summary>
    public string Name { get; }

    /// <summary>"hunger -N", shown even on a sold-out row.</summary>
    public string HungerLine { get; }

    /// <summary>"CRx.xx", shown even on a sold-out row.</summary>
    public string PriceLine { get; }

    public bool IsSoldOut { get; }

    /// <summary>True on the one row the selection panel is editing.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public RelayCommand SelectCommand { get; }

    private void Select()
    {
        if (!IsSoldOut)
        {
            _select(this);
        }
    }
}
