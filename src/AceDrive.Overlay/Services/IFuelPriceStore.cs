using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Persists the fuel market (PORTPLAN item 37): the CR/L the daily drift last
/// rolled to and the game day it rolled on. Small on purpose so a fake
/// substitutes cleanly in tests; the JSON file owns persistence.
/// </summary>
public interface IFuelPriceStore
{
    /// <summary>The saved market, or null on a first run (Unity's 1.65 seed).</summary>
    FuelPriceState? Load();

    /// <summary>Banks a rolled price. Never throws (the file sink swallows IO).</summary>
    void Save(FuelPriceState state);
}
