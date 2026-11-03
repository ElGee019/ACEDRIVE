using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The setup page's "press the call you want" seam: it puts the platform in
/// listening mode for the next input (a chord, or any wheel/pad button) and
/// answers exactly once. A cancel answers null, so the page can put the armed
/// call back on its button instead of leaving the prompt up.
/// </summary>
public interface ICallBinder
{
    /// <summary>Listens for the next input; <paramref name="captured"/> gets the result.</summary>
    void BeginCapture(Action<CallBinding?> captured);

    /// <summary>Stops listening without a result (Escape, or a second tap).</summary>
    void CancelCapture();
}
