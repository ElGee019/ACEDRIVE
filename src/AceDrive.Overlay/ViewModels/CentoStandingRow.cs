// One formatted row of the Cento Miglia receipt's RESULTS block (PORTPLAN item
// 49): the page builds the sentence, the view paints it and highlights the
// player's line.
namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// A standings row ready to print: the already-formatted line and whether it is
/// the player's own, which the receipt paints teal like the other own-row
/// boards. Formatting happens once when the page is built, not per binding.
/// </summary>
public sealed record CentoStandingRow(string Text, bool IsPlayer);
