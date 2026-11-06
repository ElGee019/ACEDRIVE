using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The pancake visit's host: records the page it was handed and the two page
/// swaps the flow owns, so a test reads exactly what the coordinator would have
/// shown without a panel or a navigator. A page shown through the navigator can
/// be routed into a real shell by handing in a presenter.
/// </summary>
internal sealed class FakePancakeHost : IPancakeHost
{
    private readonly Action<object>? _present;

    public FakePancakeHost(Action<object>? present = null) => _present = present;

    /// <summary>The last page the flow presented, or null.</summary>
    public object? Page { get; private set; }

    public int Presented { get; private set; }

    public int HidPanel { get; private set; }

    public int CalendarShown { get; private set; }

    public int MenuShown { get; private set; }

    public void Present(object page)
    {
        Page = page;
        Presented++;
        _present?.Invoke(page);
    }

    public void HidePanel() => HidPanel++;

    public void ShowCalendar() => CalendarShown++;

    public void ShowMenu() => MenuShown++;
}
