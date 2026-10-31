using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The shell the car-haul flow talks to in the headless suite: it
/// records the pages, the pinned leg, the receipt and the notice instead of
/// touching a window.</summary>
internal sealed class FakeCarHaulHost : ICarHaulHost
{
    public List<object> Pages { get; } = [];

    public object? Receipt { get; private set; }

    public HudJobGuidance? Guidance { get; private set; }

    public string? Notice { get; private set; }

    public int Refreshes { get; private set; }

    /// <summary>The control page the flow hands back when no offer can be drawn.</summary>
    public object ControlPage { get; } = new();

    public void ShowPage(object page) => Pages.Add(page);

    public void ShowReceipt(object page) => Receipt = page;

    public void RefreshControl() => Refreshes++;

    public void PinTarget(HudJobGuidance? guidance) => Guidance = guidance;

    public void ShowNotice(string text) => Notice = text;
}
