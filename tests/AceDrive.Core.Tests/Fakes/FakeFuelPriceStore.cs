using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeFuelPriceStore : IFuelPriceStore
{
    public FakeFuelPriceStore(FuelPriceState? seed = null) => Saved = seed;

    /// <summary>The last banked market, so a suite can assert the daily drift
    /// was persisted (and how many rolls wrote).</summary>
    public FuelPriceState? Saved { get; private set; }

    public int Saves { get; private set; }

    public FuelPriceState? Load() => Saved;

    public void Save(FuelPriceState state)
    {
        Saved = state;
        Saves++;
    }
}
