// One reward as the care-package picker shows it (PORTPLAN item 61): the card's
// own title and function text, so the row never carries a second copy of the
// content the reward table already owns.
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One card button: the reward it offers plus the text the page prints.</summary>
public sealed record RewardCardOption(RewardCard Card)
{
    /// <summary>The card's stored title, the page's button label.</summary>
    public string Title => Card.Title;

    /// <summary>The card's function text, the line under the title.</summary>
    public string Description => Card.Description;
}
