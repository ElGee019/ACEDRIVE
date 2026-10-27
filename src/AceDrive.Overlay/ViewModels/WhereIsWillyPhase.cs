namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Where the weekly missing-person run is (ToWhereIsWilly.md §3.3). Session
/// state only: a restart inside a phase loses the run exactly like Unity, and
/// only the receipt's completion stamp survives.
///
/// <see cref="AtHome"/> and <see cref="WrongAddress"/> exist so the 10 Hz poll
/// cannot act on the same parked car twice: it keeps reporting
/// <c>ParkedAtTarget</c> until the player drives off, and a re-awarded
/// friendship (or fee) must be impossible.
/// </summary>
public enum WhereIsWillyPhase
{
    /// <summary>No run: the calendar row can start one.</summary>
    Idle,

    /// <summary>The elder is missing: the player is visiting coffee shops.</summary>
    Searching,

    /// <summary>The hiding shop was found; the elder is waiting to be driven home.</summary>
    Found,

    /// <summary>The elder is home: the HUD holds DROP OFF, which pays and stamps.</summary>
    AtHome,

    /// <summary>The player parked at the wrong door: CONTINUE clears the run unpaid.</summary>
    WrongAddress,
}
