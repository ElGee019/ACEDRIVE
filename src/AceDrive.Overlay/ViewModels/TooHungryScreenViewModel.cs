using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Unity's HungerBlockPopup (PORTPLAN items 26/52): the page a job tap lands on
/// once the appetite reaches the block band. The job entries refuse rather than
/// opening, so the tap still has to answer — this page says why, names the two
/// ways out (the trunk or a shop) and hands the player to the trunk page in one
/// tap. The reading is printed so the player can see how close the meter is.
///
/// It keeps the panel open (<see cref="IKeepPanelOpen"/>): the player was
/// mid-menu when they tapped, and a page that hid itself after 15 s would leave
/// the refusal unread and the job entry looking broken.
/// </summary>
public sealed class TooHungryScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly ICommand _toTrunkCommand;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public TooHungryScreenViewModel(
        Hunger hunger, ICommand toTrunkCommand, ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(hunger);
        ArgumentNullException.ThrowIfNull(toTrunkCommand);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        Title = Strings.Get("TooHungryTitle");
        Body = Strings.Get("TooHungryBody");
        HungerLine = Strings.Format("TrunkHunger", hunger.Value, hunger.Max);
        HungerAmber = hunger.IsAmber;
        ToTrunkLabel = Strings.Get("HungerIntroToTrunk");
        BackLabel = Strings.Get("DeliveryBack");
        _toTrunkCommand = toTrunkCommand;
        _backCommand = backCommand;
        _beat = beat;
        ToTrunkCommand = new RelayCommand(ToTrunk);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>"TOO HUNGRY".</summary>
    public string Title { get; }

    /// <summary>Why the job refused and the two ways to eat.</summary>
    public string Body { get; }

    /// <summary>Unity's "HUNGER {h}/36", the reading that refused the job.</summary>
    public string HungerLine { get; }

    /// <summary>True inside Unity's amber hunger band (the label's orange).</summary>
    public bool HungerAmber { get; }

    /// <summary>TO TRUNK's label, spoken as well as painted.</summary>
    public string ToTrunkLabel { get; }

    /// <summary>BACK's label, spoken as well as painted.</summary>
    public string BackLabel { get; }

    /// <summary>Opens the trunk page, where the carried food is eaten.</summary>
    public ICommand ToTrunkCommand { get; }

    /// <summary>Returns to the page the refused tap came from.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating TO TRUNK tap.</summary>
    public bool ToTrunkFlash { get => _toTrunkFlash; private set => SetField(ref _toTrunkFlash, value); }
    private bool _toTrunkFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    /// <summary>Both buttons answer their labels.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void ToTrunk()
    {
        ToTrunkFlash = false;
        BackFlash = false;
        _beat.Cancel();
        _toTrunkCommand.Execute(null);
    }

    private void Back()
    {
        ToTrunkFlash = false;
        BackFlash = false;
        _beat.Cancel();
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        // Both verbs navigate: glow first, act at the end of the shared beat,
        // like every other page's buttons.
        VoiceActions = new Dictionary<string, Action>
        {
            [ToTrunkLabel] = () =>
            {
                ToTrunkFlash = true;
                _beat.Defer(ToTrunk);
            },
            [BackLabel] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
