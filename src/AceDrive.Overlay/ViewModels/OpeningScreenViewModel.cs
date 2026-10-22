using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Opening screen (PORTPLAN Career Start Screen 1): the player confirms the
/// name AC EVO/Steam supplies plus a home address on the placeholder map.
/// Country and city are fixed data, the street and house-number dropdowns
/// cascade, and CONTINUE stays disabled until the address is complete.
/// The name is reserved in Firestore before the profile is saved, so two
/// players cannot own the same world identity; an unavailable online layer
/// still lets single-player proceed. Mouse-only like the brand and
/// grandma-car pages: typing a name is not a voice action.
/// </summary>
public sealed class OpeningScreenViewModel : ViewModelBase
{
    private readonly AddressMap _map;
    private readonly IPlayerNameRegistry _names;
    private readonly Action<PlayerProfile> _confirmed;
    private string _displayName;
    private string _nameStatus = string.Empty;
    private bool _isClaiming;
    private AddressStreet? _selectedStreet;
    private int _selectedHouseNumber;
    private IReadOnlyList<int> _houseNumbers = [];

    public OpeningScreenViewModel(
        AddressMap map,
        PlayerProfile? saved,
        string? detectedName,
        IPlayerNameRegistry names,
        Action<PlayerProfile> confirmed)
    {
        _map = map;
        _names = names;
        _confirmed = confirmed;
        Country = map.Country;
        City = map.City;
        Streets = map.Streets;
        _displayName = FirstNonEmpty(saved?.DisplayName, detectedName);
        _selectedStreet = FindStreet(saved?.StreetId) ?? FirstStreet();
        _houseNumbers = HouseNumbersFor(_selectedStreet);
        _selectedHouseNumber = PickHouseNumber(saved?.HouseNumber);
        ContinueCommand = new RelayCommand(() => _ = ConfirmAsync(), () => CanContinue);
        ValidateName();
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            // Keep the raw text while typing; Confirm trims the saved value.
            if (!SetField(ref _displayName, value)) return;
            ValidateName();
            RefreshContinue();
        }
    }

    public string Country { get; }
    public string City { get; }
    public IReadOnlyList<AddressStreet> Streets { get; }

    /// <summary>Inline validation/claim message; empty when the form is fine.</summary>
    public string NameStatus
    {
        get => _nameStatus;
        private set => SetField(ref _nameStatus, value);
    }

    /// <summary>True while the Firestore name claim is in flight.</summary>
    public bool IsClaiming
    {
        get => _isClaiming;
        private set
        {
            if (!SetField(ref _isClaiming, value)) return;
            NameStatus = value ? Strings.Get("OpeningNameChecking") : NameError;
            RefreshContinue();
        }
    }

    private string NameError { get; set; } = string.Empty;

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
        !IsClaiming
        && PlayerNameKey.IsValid(DisplayName)
        && !string.IsNullOrWhiteSpace(Country)
        && !string.IsNullOrWhiteSpace(City)
        && SelectedStreet is not null
        && SelectedHouseNumber > 0;

    public ICommand ContinueCommand { get; }

    /// <summary>
    /// Validates, claims the global name, then hands the profile to the walk.
    /// Public so tests can await the async claim instead of racing the
    /// command's fire-and-forget execution. Never throws: the registry
    /// reports Taken/Invalid/Unavailable, and only Taken/Invalid stop here.
    /// </summary>
    public async Task ConfirmAsync()
    {
        if (!CanContinue || SelectedStreet is null) return;

        IsClaiming = true;
        try
        {
            NameClaimResult result = await _names.ClaimAsync(DisplayName.Trim())
                .ConfigureAwait(true);
            if (result == NameClaimResult.Taken)
            {
                NameError = Strings.Get("OpeningNameTaken");
                return;
            }
            if (result == NameClaimResult.Invalid)
            {
                NameError = Strings.Get("OpeningNameInvalid");
                return;
            }
        }
        finally
        {
            IsClaiming = false;
        }

        _confirmed(new PlayerProfile(
            DisplayName.Trim(), Country, City,
            SelectedStreet.Id, SelectedHouseNumber));
    }

    private void ValidateName()
    {
        string trimmed = DisplayName.Trim();
        NameError = trimmed.Length switch
        {
            0 => string.Empty,
            < PlayerNameKey.MinLength => Strings.Get("OpeningNameTooShort"),
            > PlayerNameKey.MaxLength => Strings.Get("OpeningNameTooLong"),
            _ when PlayerNameKey.Normalize(trimmed).Length == 0 =>
                Strings.Get("OpeningNameInvalid"),
            _ => string.Empty,
        };
        if (!IsClaiming)
        {
            NameStatus = NameError;
        }
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
