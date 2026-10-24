using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The trunk page (PORTPLAN item 26, port of Unity YouScreen's TrunkScreen):
/// what the driven car carries, measured against the current car's trunk, the
/// appetite reading and an EAT tap per stack. The free litres, the meter and
/// the carried goods all come from the shared Core state; this page owns only
/// presentation.
/// Unity's page also lists the glove compartment and the reward-card slots;
/// neither exists in this port yet (PORTPLAN 61), so the page renders the
/// trunk alone instead of an invented empty section.
/// Voice parity with the other pages: BACK navigates, so its voice tap glows
/// teal first and the page waits out the shared beat. EAT has no voice entry —
/// several rows answer the same label, so there is no unambiguous target.
/// </summary>
public sealed class TrunkScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly Trunk _trunk;
    private readonly Hunger _hunger;
    private readonly Garage _garage;
    private readonly StoreCatalog _catalog;
    private readonly ICommand _backCommand;
    private readonly Action _onEaten;
    private readonly VoiceBeat _beat;

    public TrunkScreenViewModel(
        Trunk trunk, Hunger hunger, Garage garage, StoreCatalog catalog,
        ICommand backCommand, Action onEaten, VoiceBeat beat)
    {
        _trunk = trunk;
        _hunger = hunger;
        _garage = garage;
        _catalog = catalog;
        _backCommand = backCommand;
        _onEaten = onEaten;
        _beat = beat;

        EatCommand = new ParameterizedCommand<TrunkItemRow>(Eat);
        BackCommand = new RelayCommand(Back);
        Rebuild();
        RebuildVoice();
    }

    /// <summary>One EAT tap: consumes the row's stack one unit at a time.</summary>
    public ICommand EatCommand { get; }

    public ICommand BackCommand { get; }

    public IReadOnlyList<TrunkItemRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<TrunkItemRow> _rows = [];

    /// <summary>Unity's "Free space: {remaining:F2}/{max}L".</summary>
    public string FreeSpaceLine { get => _freeSpaceLine; private set => SetField(ref _freeSpaceLine, value); }
    private string _freeSpaceLine = "";

    /// <summary>Unity's "HUNGER {h}/36" against the rules' own cap, repainted
    /// after every bite.</summary>
    public string HungerLine { get => _hungerLine; private set => SetField(ref _hungerLine, value); }
    private string _hungerLine = "";

    /// <summary>True inside Unity's amber hunger band (the label's orange).</summary>
    public bool HungerAmber { get => _hungerAmber; private set => SetField(ref _hungerAmber, value); }
    private bool _hungerAmber;

    /// <summary>A trunk with nothing in it: the hint keeps the page from reading as broken.</summary>
    public bool IsEmpty => Rows.Count == 0;

    public string EmptyLine { get; } = Strings.Get("TrunkEmpty");

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // Unity's RebuildTrunkScreen runs on every visit and after every bite, so
    // the page never shows a stack the previous tap already consumed.
    private void Rebuild()
    {
        var rows = new List<TrunkItemRow>(_trunk.Items.Count);
        foreach (TrunkItem item in _trunk.Items)
        {
            StoreItem? def = _catalog.Find(item.Name);
            rows.Add(new TrunkItemRow(
                item.Name,
                Strings.Format("TrunkQuantityLine", item.Quantity),
                def is null ? string.Empty : Strings.Format("TrunkHungerLine", def.Hunger),
                def is not null));
        }
        Rows = rows;
        FreeSpaceLine = Strings.Format("TrunkFree", _trunk.Remaining(TrunkCapacity), TrunkCapacity);
        HungerLine = Strings.Format("TrunkHunger", _hunger.Value, _hunger.Max);
        HungerAmber = _hunger.IsAmber;
        OnPropertyChanged(nameof(IsEmpty));
    }

    // One bite: the Core rule decides whether the stack and the catalogue row
    // agree, then the host banks the trunk and the appetite together.
    private void Eat(TrunkItemRow? row)
    {
        if (row is null || !TrunkEat.Eat(_trunk, _hunger, _catalog, row.Name))
        {
            return;
        }
        Rebuild();
        _onEaten();
    }

    // The same capacity the shop's projection uses: the catalogue row behind
    // the car driven now, never a copy taken at purchase time.
    private double TrunkCapacity =>
        _garage.Current is { } car ? _garage.SpecOf(car)?.TrunkLitres ?? 0 : 0;

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("TrunkBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
