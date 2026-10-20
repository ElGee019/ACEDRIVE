// House-number rule for the placeholder free-roam map: one number every
// 30 metres along a street, starting at the street's first metre. Pure logic
// so the ViewModel and the future Lua port share the same arithmetic.
namespace AceDrive.Core;

/// <summary>House-number spacing maths for the placeholder address map.</summary>
public static class HouseNumberPlan
{
    /// <summary>Street length in metres, clamped at zero.</summary>
    public static double StreetLengthMeters(AddressStreet street, AddressMap map) =>
        StreetLengthMeters(street, map.TrackLengthMeters);

    /// <summary>
    /// Street length at a runtime track length (EVO's static block), clamped at
    /// zero. The address resolver uses this overload so a track-length change
    /// does not need a new data map.
    /// </summary>
    public static double StreetLengthMeters(AddressStreet street, double trackLengthMeters)
    {
        if (!double.IsFinite(trackLengthMeters) || !double.IsFinite(street.FromFraction)
            || !double.IsFinite(street.ToFraction))
        {
            return 0.0;
        }

        double fractions = street.ToFraction - street.FromFraction;
        return fractions <= 0.0 ? 0.0 : fractions * trackLengthMeters;
    }

    /// <summary>
    /// How many house numbers a street gets. Number 1 sits at the street's
    /// start, so a 964.5 m street with 30 m spacing answers 33 (0, 30, ...,
    /// 960 m). Invalid geometry answers 0 instead of throwing.
    /// </summary>
    public static int Count(AddressStreet street, AddressMap map) =>
        CountAt(street, map.TrackLengthMeters, map.HouseSpacingMeters);

    /// <summary>
    /// <see cref="Count"/> at a runtime track length instead of the map's
    /// stored length.
    /// </summary>
    public static int CountAt(
        AddressStreet street, double trackLengthMeters, double houseSpacingMeters)
    {
        if (!double.IsFinite(houseSpacingMeters) || houseSpacingMeters <= 0.0)
        {
            return 0;
        }

        double length = StreetLengthMeters(street, trackLengthMeters);
        return length <= 0.0
            ? 0
            : (int)Math.Floor(length / houseSpacingMeters) + 1;
    }

    /// <summary>
    /// Which side a house number sits on: even numbers one side, odd the
    /// other, so a street reads like a real one (Unity HouseNumberGenerator's
    /// parity rule). The count is unchanged — only the side differs.
    /// </summary>
    public static HouseSide SideOf(int number) =>
        number % 2 == 0 ? HouseSide.Left : HouseSide.Right;

    /// <summary>The dropdown's contents: 1 through <see cref="Count"/>.</summary>
    public static IReadOnlyList<int> Numbers(AddressStreet street, AddressMap map)
    {
        int count = Count(street, map);
        if (count <= 0)
        {
            return [];
        }

        int[] numbers = new int[count];
        for (int i = 0; i < count; i++)
        {
            numbers[i] = i + 1;
        }
        return numbers;
    }

    /// <summary>
    /// Distance from the street's start for one house number. Answers -1 for
    /// a number outside the street's dropdown.
    /// </summary>
    public static double OffsetMeters(AddressStreet street, int number, AddressMap map)
    {
        if (number < 1 || number > Count(street, map))
        {
            return -1.0;
        }

        return (number - 1) * map.HouseSpacingMeters;
    }
}
