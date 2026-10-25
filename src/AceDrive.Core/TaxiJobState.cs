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
    /// isCustomerHomePickup roll): the home door becomes the pickup and the
    /// second address the X residence, or the other way around.</summary>
    public bool HomeIsPickup { get; set; }

    /// <summary>The customer's home door, null when the map cannot supply two
    /// addresses and the page falls back to the placeholder copy.</summary>
    public AddressLocation? CustomerHome { get; set; }

    /// <summary>The structured pickup door (the home or the second residence
    /// after the flip); the future driving slice routes to it.</summary>
    public AddressLocation? PickupAddress { get; set; }

    /// <summary>The structured drop-off door.</summary>
    public AddressLocation? DropoffAddress { get; set; }

    public string PickupName { get; set; } = "";
    public string DropoffName { get; set; } = "";
    public double DistanceKm { get; set; }
    public double Fare { get; set; }
    public double Tip { get; set; }
    /// <summary>Brand sponsorship on the settled receipt (Unity's
    /// brandLoyaltyBonus, 0 until the mileage store knows the brand).</summary>
    public double BrandBonus { get; set; }
    /// <summary>Business starter bonus on the settled receipt (Unity's
    /// starterBonus, 0 once the zone/car decay has eaten it).</summary>
    public double StarterBonus { get; set; }
    /// <summary>Speed-fine deduction on the settled receipt (0 until dropoff).</summary>
    public double Fine { get; set; }
    public double Total => Fare + BrandBonus + Tip + StarterBonus - Fine;

    /// <summary>The special request drawn at the pickup, null when this ride
    /// asks nothing. Kept with the job so the receipt and a later save can
    /// replay the same ask.</summary>
    public CustomerRequest? Request { get; set; }

    /// <summary>True when the customer will tip (Unity's isTipEligible): only
    /// an accepted passive/speed ask or a correct quiz sets it. A police fine
    /// clears it; a correct quiz tip survives through its own flag.</summary>
    public bool TipEligible { get; set; }

    /// <summary>True only when the car-stat quiz was answered correctly
    /// (Unity's quizAnsweredCorrectly). Tips on its own, even after a fine.</summary>
    public bool QuizAnsweredCorrectly { get; set; }

    /// <summary>
    /// True until an accepted passive ask's live promise is broken (a brusque
    /// input, a rev or speed limit) or its delivery deadline passes. A broken
    /// promise removes the ordinary tip; the quiz's own tip is not affected.
    /// Reset to true for every new job.
    /// </summary>
    public bool ConditionsMet { get; set; } = true;

    /// <summary>
    /// When the accepted passive ask's delivery deadline falls, null when that
    /// ask set none or the map could not supply a route. Separate from the
    /// speed challenge's deadline: a ride has at most one of the two, and only
    /// the challenge pays a cornering award.
    /// </summary>
    public DateTime? RequestDeadline { get; set; }

    /// <summary>True when the speed challenge was accepted; the police
    /// patrols and fine checks are armed for the rest of the ride.</summary>
    public bool SpeedChallengeAgreed { get; set; }

    /// <summary>How many speed-challenge fines this ride collected.</summary>
    public int SpeedChallengeFines { get; set; }

    /// <summary>
    /// When the accepted speed challenge must reach the drop-off. Null when
    /// the address map cannot supply a route (no deadline is invented) or
    /// when this ride has no speed challenge.
    /// </summary>
    public DateTime? SpeedChallengeDeadline { get; set; }

    /// <summary>The patrols drawn when the speed challenge was accepted;
    /// empty for every other request and after the job ends.</summary>
    public IReadOnlyList<PolicePatrol> Patrols { get; set; } = [];

    /// <summary>Patrol ids that already charged this ride, so one patrol can
    /// fine once even if the car circles back through it.</summary>
    public HashSet<int> FinedPatrolIds { get; } = [];

    /// <summary>Patrol ids whose HUD warning already showed, so the notice
    /// stays once per patrol instead of repainting every tick.</summary>
    public HashSet<int> WarnedPatrolIds { get; } = [];
}
