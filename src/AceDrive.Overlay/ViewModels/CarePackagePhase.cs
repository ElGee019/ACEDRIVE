// The care-package run's phase (PORTPLAN item 21, Unity
// CarePackageDeliveryHandler's route-setting/pickup/community-centre legs):
// session state, rebuilt from the ledger on the next DELIVER tap.
namespace AceDrive.Overlay.ViewModels;

/// <summary>Where a care-package delivery run is.</summary>
public enum CarePackagePhase
{
    /// <summary>No run: every kit waits on a store shelf or the button is hidden.</summary>
    Idle,

    /// <summary>Driving to (and picking up at) the run's allocated stores.</summary>
    Pickup,

    /// <summary>Every store leg done: the car carries the kits to the community centre.</summary>
    Dropoff,
}
