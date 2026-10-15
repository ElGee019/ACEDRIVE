// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11-13.
namespace AceDrive.Core;

/// <summary>Plain data for one taxi job. All money in EUR.</summary>
public sealed class TaxiJobState
{
    public JobPhase Phase { get; set; } = JobPhase.None;

    /// <summary>The drawn passenger identity (proposal generation), data parts
    /// kept separate so pages compose them per culture (PORTPLAN 11/61).</summary>
    public ProceduralName Customer { get; set; } = new();

    /// <summary>True when the customer's home is the pickup leg (Unity's
    /// isCustomerHomePickup roll). With placeholder addresses it says which
    /// of the two is "home"; the other one is the X residence.</summary>
    public bool HomeIsPickup { get; set; }

    public string PickupName { get; set; } = "";
    public string DropoffName { get; set; } = "";
    public double DistanceKm { get; set; }
    public double Fare { get; set; }
    public double Tip { get; set; }
    /// <summary>Speed-fine deduction on the settled receipt (0 until dropoff).</summary>
    public double Fine { get; set; }
    public double Total => Fare + Tip - Fine;
}
