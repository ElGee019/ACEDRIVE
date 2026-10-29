namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell a live Touristenfahrten run needs: hiding the panel
/// so the companion drives, showing the per-dropoff receipt, handing the player
/// back to the calendar once the run is done, and the standings tail the
/// receipt reads. The flow owns the run and the doors; only the shell knows
/// which page is showing and where the saves live.
/// </summary>
public interface ITouristenfahrtenHost
{
    /// <summary>Hides the panel: the HUD drives every leg of the run.</summary>
    void HidePanel();

    /// <summary>Reveals the panel on a receipt page.</summary>
    void ShowReceipt(object screen);

    /// <summary>Opens the calendar page the run was started from.</summary>
    void GoToCalendar();

    /// <summary>Refreshes and pushes the XP standings, banking the driver file.</summary>
    void RefreshStandings();
}
