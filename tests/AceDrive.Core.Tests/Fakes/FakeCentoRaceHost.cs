using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Captures what the Cento Miglia flow asks the shell to do: the
/// pinned legs, the reputation readings, the register/receipt pages and the
/// settle. Tests drive the flow headless through it.</summary>
internal sealed class FakeCentoRaceHost : ICentoRaceHost
{
    public object? Screen { get; private set; }
    public object? Revealed { get; private set; }
    public object? RevealedPage => Revealed;
    public int HideCalls { get; private set; }
    public CentoMigliaEvent? StartPinned { get; private set; }
    public CentoMigliaEvent? FinishPinned { get; private set; }
    public List<double?> ReputationReadings { get; } = [];
    public double? Reputation => ReputationReadings.Count == 0 ? null : ReputationReadings[^1];
    public bool ParkedAtTarget { get; set; }
    public int ClearGuidanceCalls { get; private set; }
    public (CentoMigliaEvent Race, double Start, double End)? Settled { get; private set; }
    public object ControlPageObject { get; } = new();

    public void ShowPage(object screen) => Screen = screen;

    public void RevealPage(object screen) => Revealed = screen;

    public void HidePanel() => HideCalls++;

    public object ControlPage() => ControlPageObject;

    public void PinStart(CentoMigliaEvent race) => StartPinned = race;

    public void PinFinish(CentoMigliaEvent race) => FinishPinned = race;

    public void ClearGuidance() => ClearGuidanceCalls++;

    public void SetReputation(double? reputation) => ReputationReadings.Add(reputation);

    public void SettleRace(CentoMigliaEvent race, double startReputation, double endReputation) =>
        Settled = (race, startReputation, endReputation);
}
