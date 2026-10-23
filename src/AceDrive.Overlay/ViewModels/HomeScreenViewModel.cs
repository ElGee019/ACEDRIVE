using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The home page (Unity's YouScreen): the player's own doors rather than the
/// job board. The garage (PORTPLAN item 30) and the addresses page (the
/// discovered POIs behind the marker-network stand-in) live here because
/// Unity opens them from the You menu, not from the job list; later You-menu
/// entries (trunk, settings, statistics) join the same page as they land. The
/// page owns only navigation and its voice acknowledgment; the host builds
/// the screen on every visit.
/// </summary>
public sealed class HomeScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly ICommand _garageCommand;
    private readonly ICommand _addressesCommand;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public HomeScreenViewModel(
        ICommand garageCommand, ICommand addressesCommand, ICommand backCommand,
        VoiceBeat beat)
    {
        _garageCommand = garageCommand;
        _addressesCommand = addressesCommand;
        _backCommand = backCommand;
        _beat = beat;
        GarageCommand = new RelayCommand(Garage);
        AddressesCommand = new RelayCommand(Addresses);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>Opens the garage page (owned cars + stats).</summary>
    public ICommand GarageCommand { get; }

    /// <summary>Opens the addresses page (discovered POIs, alphabetical).</summary>
    public ICommand AddressesCommand { get; }

    /// <summary>The page's way back to the menu.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating GARAGE tap.</summary>
    public bool GarageFlash { get => _garageFlash; private set => SetField(ref _garageFlash, value); }
    private bool _garageFlash;

    /// <summary>Teal-glow acknowledgment for the navigating ADDRESSES tap.</summary>
    public bool AddressesFlash { get => _addressesFlash; private set => SetField(ref _addressesFlash, value); }
    private bool _addressesFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // A click acts at once and drops any voice tap still waiting, so one
    // utterance cannot flip the page after the player already moved on.
    private void Garage()
    {
        _beat.Cancel();
        ClearFlash();
        _garageCommand.Execute(null);
    }

    private void Addresses()
    {
        _beat.Cancel();
        ClearFlash();
        _addressesCommand.Execute(null);
    }

    private void Back()
    {
        _beat.Cancel();
        ClearFlash();
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // Both buttons navigate: glow first, act at the end of the shared
            // beat, and clear the glow on the way out (bank-page precedent).
            [Strings.Get("HomeGarage")] = () => FireVoice("garage", _garageCommand),
            [Strings.Get("HomeAddresses")] = () => FireVoice("addresses", _addressesCommand),
            [Strings.Get("HomeBack")] = () => FireVoice("back", _backCommand),
        };
    }

    private void FireVoice(string target, ICommand command)
    {
        GarageFlash = target == "garage";
        AddressesFlash = target == "addresses";
        BackFlash = target == "back";
        _beat.Defer(() =>
        {
            ClearFlash();
            command.Execute(null);
        });
    }

    private void ClearFlash()
    {
        GarageFlash = false;
        AddressesFlash = false;
        BackFlash = false;
    }
}
