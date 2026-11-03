using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The call input without Win32: the page's capture answers with whatever the
/// test hands it, and the armer records what the preferences write asked for.
/// </summary>
internal sealed class FakeCallInput : ICallBinder, ICallArmer
{
    private Action<CallBinding?>? _captured;

    /// <summary>Every binding the flow armed, in order (null = the default chord).</summary>
    public List<CallBinding?> Armed { get; } = [];

    /// <summary>True while the page is listening for an input.</summary>
    public bool Capturing { get; private set; }

    /// <summary>The label of the armed input, as the menu reads it live.</summary>
    public string Label { get; private set; } = CallBinding.Default.Label;

    public void BeginCapture(Action<CallBinding?> captured)
    {
        Capturing = true;
        _captured = captured;
    }

    public void CancelCapture() => Finish(null);

    /// <summary>Simulates the platform answering the capture with an input.</summary>
    public void Deliver(CallBinding? binding) => Finish(binding);

    public void Arm(CallBinding? binding)
    {
        Armed.Add(binding);
        Label = CallBinding.OrDefault(binding).Label;
    }

    private void Finish(CallBinding? binding)
    {
        Action<CallBinding?>? captured = _captured;
        _captured = null;
        Capturing = false;
        captured?.Invoke(binding);
    }
}
