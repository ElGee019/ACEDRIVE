using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Maps a maintenance part's stable data key (maintenance_rules.json) to its
/// resx copy. Display names never ride the data file (AGENTS.md: no hardcoded
/// copy, and the key is what the balance table owns), so this is the one place
/// the mapping is spelled — the ControlCalendar event-key precedent. An unknown
/// key falls back to itself so a new part still reads as something.
/// </summary>
public static class MechanicPartNames
{
    public static string Of(string key) => Strings.Get(key switch
    {
        "oil_change" => "MechanicPartOilChange",
        "wipers" => "MechanicPartWipers",
        "air_filter" => "MechanicPartAirFilter",
        "brake_pads" => "MechanicPartBrakePads",
        "tires" => "MechanicPartTires",
        "brake_fluid" => "MechanicPartBrakeFluid",
        "transmission_fluid" => "MechanicPartTransmissionFluid",
        "car_battery" => "MechanicPartCarBattery",
        _ => key,
    });
}
