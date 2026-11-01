namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Which block of the broker's screen is up (Unity InsuranceScreen's three
/// views on one screen): the landing page with MANAGE and COLLECT, the cover
/// rows behind MANAGE, and the escrow behind COLLECT. Unity swaps the panels
/// on the same screen instead of pushing pages, so BACK closes a panel first.
/// </summary>
public enum InsuranceView
{
    /// <summary>The broker's own page: MANAGE, COLLECT and BACK.</summary>
    Landing = 0,

    /// <summary>Every owned car with its ADD/REMOVE cover and daily premium.</summary>
    Manage,

    /// <summary>What the insurance owes the player, and the COLLECT IT button.</summary>
    Collect,
}
