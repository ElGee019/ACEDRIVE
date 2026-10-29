using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Captures what the road-test flow asks the shell to do: the page it
/// reveals, the receipt it shows, the panel it hides and the page it returns to
/// (the control page on a way out before a run, the menu after a completed one).
/// Tests drive the flow headless through it.</summary>
internal sealed class FakeRoadTestHost : IRoadTestHost
{
    public object? Revealed { get; private set; }
    public int RevealCalls { get; private set; }
    public object? Receipt { get; private set; }
    public int ShowReceiptCalls { get; private set; }
    public int HideCalls { get; private set; }
    public int ShowControlCalls { get; private set; }
    public int ShowMenuCalls { get; private set; }

    public void RevealPage(object page)
    {
        RevealCalls++;
        Revealed = page;
    }

    public void ShowReceipt(object screen)
    {
        ShowReceiptCalls++;
        Receipt = screen;
    }

    public void HidePanel() => HideCalls++;

    public void ShowControl() => ShowControlCalls++;

    public void ShowMenu() => ShowMenuCalls++;
}
