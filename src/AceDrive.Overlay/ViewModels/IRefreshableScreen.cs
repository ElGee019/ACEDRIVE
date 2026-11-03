namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// A page that paints a formatted snapshot of shared state — the wallet's CR,
/// the mechanic's dents — instead of binding it live. The panel hands an
/// interrupted page back as the same instance, and a crash repair can charge
/// the wallet or clear the dents while that page is away, so the presenter
/// re-reads it through this hook before showing it again. A page whose labels
/// are rebuilt on every visit does not need it.
/// </summary>
public interface IRefreshableScreen
{
    /// <summary>
    /// Re-reads what the page paints and raises the change notifications for
    /// the labels that moved. Called on a hand-back, never per tick, so it may
    /// format text; it must not throw.
    /// </summary>
    void Refresh();
}
