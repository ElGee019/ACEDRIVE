// Which shop doors the player has entered (PORTPLAN item 46, Unity
// PlayerData.hasEnteredCarWash / hasEnteredCarDealer /
// hasEnteredInsuranceBroker). The bank's own flag stays on DriverWallet
// (Unity PlayerData.hasEnteredBank, stamped by the bank screen). The fuel
// station joins them with no door to stamp: EVO's pump is a forecourt, so a
// settled fill is the visit. Persisted through the driver save so a one-time
// visit tip retires for good.
namespace AceDrive.Core;

/// <summary>The one-time business visits the ToDo list asks for.</summary>
public sealed class VisitLedger
{
    public VisitLedger(DriverState? saved = null)
    {
        CarWash = saved?.VisitedCarWash ?? false;
        CarDealer = saved?.VisitedCarDealer ?? false;
        InsuranceBroker = saved?.VisitedInsurance ?? false;
        FuelStation = saved?.VisitedFuelStation ?? false;
    }

    public bool CarWash { get; private set; }
    public bool CarDealer { get; private set; }
    public bool InsuranceBroker { get; private set; }

    /// <summary>True once the player has filled the tank at a pump.</summary>
    public bool FuelStation { get; private set; }

    public void MarkCarWash() => CarWash = true;
    public void MarkCarDealer() => CarDealer = true;
    public void MarkInsuranceBroker() => InsuranceBroker = true;
    public void MarkFuelStation() => FuelStation = true;
}
