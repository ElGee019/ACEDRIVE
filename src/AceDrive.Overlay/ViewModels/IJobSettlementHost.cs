using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell a finished job needs: put the receipt on screen,
/// refresh the job line, run the shared checkpoint saves, let the career grow
/// and hand the panel back to the page CONTINUE belongs on.
///
/// The settle prices and banks the job itself; only the shell knows which page
/// is showing and where the state files live, so those stay here. Keeping the
/// list this narrow is what lets the settle be built in the composition root
/// instead of inside the shell that calls it.
/// </summary>
public interface IJobSettlementHost
{
    /// <summary>Reveals the panel on the receipt page.</summary>
    void ShowReceipt(object screen);

    /// <summary>Re-renders the job line, fare and shift summary after the phase moved.</summary>
    void RefreshJob();

    /// <summary>Banks the shared driver file; standings ride along when the caller just refreshed them.</summary>
    void SaveDriver(RivalsResult? standings);

    /// <summary>Banks the shift (its wall-clock stamps make the idle timeout survive a restart).</summary>
    void SaveShift();

    /// <summary>Banks the game moment.</summary>
    void SaveClock();

    /// <summary>Banks the garage (the dirt tick and the odometer ride in it).</summary>
    void SaveGarage();

    /// <summary>
    /// The career tail after progress was banked: the zone expansion at its
    /// threshold and the business doors it unlocks.
    /// </summary>
    void OnProgressBanked();

    /// <summary>Refreshes and pushes the standings, banking the driver file with them.</summary>
    RivalsResult? RefreshStandings();

    /// <summary>CONTINUE on a taxi receipt lands on the control page.</summary>
    void GoToControlPage();

    /// <summary>Re-reads the cached delivery board (filled shelves, new stores).</summary>
    void RefreshDeliveryBoard();

    /// <summary>Drops the finished run and clears the HUD guidance before the receipt.</summary>
    void ClearDeliveryRun();

    /// <summary>CONTINUE on a delivery receipt lands back on the board it came from.</summary>
    void GoToDeliveryBoard();
}
