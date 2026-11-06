using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The fuel-out repair page (PORTPLAN item 38, port of Unity's RepairScreen
/// with <c>OnFuelOutDetected</c>): the exact dry-tank sentence, the car that
/// ran dry, the mechanic's bill (a full tank at the pump's price plus the
/// service fee), the cash before/after line and one REPAIR button. Paying the
/// bill is the flow's job, not the page's: the command arrives already wired
/// to the charge, the job failure and the prepaid fill.
/// </summary>
public sealed class FuelOutScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly DriverWallet _wallet;
    private readonly VoiceBeat _beat;
    private readonly ICommand _repairCommand;

    public FuelOutScreenViewModel(
        FuelOutage outage, string carTitle, DriverWallet wallet,
        VoiceBeat beat, ICommand repairCommand, bool freeRepair = false)
    {
        ArgumentNullException.ThrowIfNull(outage);
        ArgumentNullException.ThrowIfNull(carTitle);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(repairCommand);
        Outage = outage;
        _wallet = wallet;
        _beat = beat;
        _repairCommand = repairCommand;
        FreeRepair = freeRepair;
        CarLine = carTitle;
        RepairCommand = new RelayCommand(Repair);
        RebuildVoice();
    }

    /// <summary>The quoted bill, kept as data for tests and callers.</summary>
    public FuelOutage Outage { get; }

    /// <summary>True when a Free Car Repair card covers the bill (Unity
    /// RepairScreen's free branch); the card is consumed on REPAIR, not here.</summary>
    public bool FreeRepair { get; }

    /// <summary>The bill the wallet will actually take, 0 on a free repair.</summary>
    public double EffectiveCost => FreeRepair ? 0.0 : Outage.RepairCost;

    /// <summary>Unity's screen header.</summary>
    public string Title { get; } = Strings.Get("FuelOutTitle");

    /// <summary>The car that ran dry (Unity RepairScreen's <c>_textCarName</c>).</summary>
    public string CarLine { get; }

    /// <summary>Unity's damage line for this branch: "Your car ran out of fuel."</summary>
    public string Message { get; } = Strings.Get("FuelOutMessage");

    /// <summary>Unity RepairScreen's "Repair cost: CR{0:F2}", or the free-card
    /// sentence when a reward covers it.</summary>
    public string RepairCostLine => FreeRepair
        ? Strings.Get("FuelOutFreeRepair")
        : Strings.Format("FuelOutRepairCost", Outage.RepairCost);

    /// <summary>Unity RepairScreen's "Cash before: CR{0:F2} | Cash after: CR{1:F2}".</summary>
    public string CashLine => Strings.Format(
        "FuelOutCash", _wallet.Cash, _wallet.Cash - EffectiveCost);

    public ICommand RepairCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating REPAIR tap.</summary>
    public bool RepairFlash
    {
        get => _repairFlash;
        private set => SetField(ref _repairFlash, value);
    }
    private bool _repairFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // The one path out: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way to the resume.
    private void Repair()
    {
        RepairFlash = false;
        _repairCommand.Execute(null);
    }

    // REPAIR navigates (it resumes the page the interruption took), so the tap
    // glows first and acts at the end of the shared beat like every other
    // navigating voice button.
    private void RebuildVoice() =>
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("FuelOutRepair")] = () =>
            {
                RepairFlash = true;
                _beat.Defer(Repair);
            },
        };
}
