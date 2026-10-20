using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Opening screen (PORTPLAN Career Start Screen 1): the player confirms the
/// name AC EVO/Steam supplies plus a home address on the placeholder map.
/// Country and city are fixed data, the street and house-number dropdowns
/// cascade, and CONTINUE stays disabled until the address is complete.
/// Mouse-only like the brand and grandma-car pages: typing a name is not a
/// voice action.
/// </summary>
public sealed class OpeningScreenViewModel : ViewModelBase
{
    private readonly AddressMap _map;
    private readonly Action<PlayerProfile> _confirmed;
    private string _displayName;
    private AddressStreet? _selectedStreet;
    private int _selectedHouseNumber;
    private IReadOnlyList<int> _houseNumbers = [];

    public OpeningScreenViewModel(
        AddressMap map,
        PlayerProfile? saved,
        string? detectedName,
        Action<PlayerProfile> confirmed)
    {
        _map = map;
        _confirmed = confirmed;
        Country = map.Country;
        City = map.City;
        Streets = map.Streets;
        _displayName = FirstNonEmpty(saved?.DisplayName, detectedName);
        _selectedStreet = FindStreet(saved?.StreetId) ?? FirstStreet();
        _houseNumbers = HouseNumbersFor(_selectedStreet);
        _selectedHouseNumber = PickHouseNumber(saved?.HouseNumber);
        ContinueCommand = new RelayCommand(Confirm, () => CanContinue);
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            // Keep the raw text while typing; Confirm trims the saved value.
            if (SetField(ref _displayName, value)) RefreshContinue();
        }
    }

    public string Country { get; }
    public string City { get; }
    public IReadOnlyList<AddressStreet> Streets { get; }

    public AddressStreet? SelectedStreet
    {
        get => _selectedStreet;
        set
        {
            if (!SetField(ref _selectedStreet, value)) return;
            // PORTPLAN rule: changing the street resets the house number.
            HouseNumbers = HouseNumbersFor(value);
            SelectedHouseNumber = HouseNumbers.Count > 0 ? HouseNumbers[0] : 0;
            RefreshContinue();
        }
    }

    public IReadOnlyList<int> HouseNumbers
    {
        get => _houseNumbers;
        private set => SetField(ref _houseNumbers, value);
    }

    public int SelectedHouseNumber
    {
        get => _selectedHouseNumber;
        set
        {
            if (SetField(ref _selectedHouseNumber, value)) RefreshContinue();
        }
    }

    public bool CanContinue =>
        !string.IsNullOrWhiteSpace(DisplayName)
        && !string.IsNullOrWhiteSpace(Country)
        && !string.IsNullOrWhiteSpace(City)
        && SelectedStreet is not null
        && SelectedHouseNumber > 0;

    public ICommand ContinueCommand { get; }

    private void Confirm()
    {
        if (!CanContinue || SelectedStreet is null) return;
        _confirmed(new PlayerProfile(
            DisplayName.Trim(), Country, City,
            SelectedStreet.Id, SelectedHouseNumber));
    }

    private void RefreshContinue()
    {
        OnPropertyChanged(nameof(CanContinue));
        ((RelayCommand)ContinueCommand).RaiseCanExecuteChanged();
    }

    private IReadOnlyList<int> HouseNumbersFor(AddressStreet? street) =>
        street is null ? [] : HouseNumberPlan.Numbers(street, _map);

    private int PickHouseNumber(int? saved)
    {
        if (saved is { } number && HouseNumbers.Contains(number))
        {
            return number;
        }
        return HouseNumbers.Count > 0 ? HouseNumbers[0] : 0;
    }

    private AddressStreet? FindStreet(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (AddressStreet street in Streets)
        {
            if (string.Equals(street.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return street;
            }
        }
        return null;
    }

    private AddressStreet? FirstStreet() => Streets.Count > 0 ? Streets[0] : null;

    private static string FirstNonEmpty(string? saved, string? detected)
    {
        if (!string.IsNullOrWhiteSpace(saved)) return saved.Trim();
        return detected?.Trim() ?? string.Empty;
    }
}
