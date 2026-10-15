using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Shared road-test pool so the calendar and ViewModel tests pin the same picks.</summary>
internal static class TestRoadCars
{
    internal static readonly IReadOnlyList<RoadTestCar> Pool =
    [
        new("ks_mazda_mx5_na", 1990, "Mazda", "MX-5 NA", 1),
        new("ks_abarth_695_biposto", 2014, "Abarth", "695 Biposto", 1),
        new("ks_datsun_240z_fairlady", 1970, "Datsun", "240Z", 1),
    ];

    internal static RoadTestLedger Ledger(int seed = 7, RoadTestSnapshot? saved = null) =>
        new(Pool, seed, saved);

    /// <summary>Button copy for a pick, as the control page shows it.</summary>
    internal static string Button(RoadTestCar car) =>
        "Road test: " + $"{car.Year} {car.Brand} {car.Model}".ToUpperInvariant();
}
