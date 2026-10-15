using System.Globalization;

namespace AceDrive.Overlay.Services;

/// <summary>AC gear convention shared by all consumers.</summary>
public static class TelemetryConventions
{
    public static string GearDisplay(int gear) => gear switch
    {
        0 => "R",
        1 => "N",
        _ => (gear - 1).ToString(CultureInfo.InvariantCulture),
    };
}
