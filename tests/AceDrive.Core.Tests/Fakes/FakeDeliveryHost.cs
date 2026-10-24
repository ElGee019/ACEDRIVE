using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shell a delivery run reports to: records every page, pin and settle, and
/// answers the one question the run asks — whether a leg still has a door.
/// </summary>
internal sealed class FakeDeliveryHost : IDeliveryHost
{
    private readonly object _controlPage = new();

    /// <summary>Legs whose door is gone (a hand-edited catalogue row).</summary>
    public HashSet<string> Doorless { get; } = new(StringComparer.Ordinal);

    public List<object> Shown { get; } = [];
    public List<object> Revealed { get; } = [];
    public List<DeliveryStop> Pinned { get; } = [];
    public List<DeliveryRun> Settled { get; } = [];
    public int Hides { get; private set; }
    public int Clears { get; private set; }
    public int HubsOpened { get; private set; }

    public void ShowPage(object screen) => Shown.Add(screen);

    public void RevealPage(object screen) => Revealed.Add(screen);

    public void HidePanel() => Hides++;

    public bool PinStop(DeliveryStop leg)
    {
        if (Doorless.Contains(leg.Key))
        {
            return false;
        }
        Pinned.Add(leg);
        return true;
    }

    public void ClearGuidance() => Clears++;

    public object ControlPage() => _controlPage;

    public void OpenHub() => HubsOpened++;

    public void Settle(DeliveryRun run) => Settled.Add(run);
}
