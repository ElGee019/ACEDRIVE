// The odometer is banked in kilometres (EVO's unit); this converts it to what
// the player reads. Pure maths so the future Lua port shares the same number.
namespace AceDrive.Core;

/// <summary>Kilometre &lt;-&gt; player-unit conversion for the odometer readouts.</summary>
public static class Mileage
{
    /// <summary>The international mile, exactly 1.609344 km by definition.</summary>
    public const double KilometresPerMile = 1.609344;

    /// <summary>Kilometres expressed in the player's chosen unit.</summary>
    public static double FromKilometres(double kilometres, MileageUnit unit) =>
        unit == MileageUnit.Miles ? kilometres / KilometresPerMile : kilometres;
}
