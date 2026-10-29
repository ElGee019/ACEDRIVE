using System.Globalization;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One painted row of the road-test board (Unity StandingsRow: position, name,
/// result), plus the separator row Unity inserts before a player run that fell
/// outside the visible top slice.
/// </summary>
public sealed record RoadTestRow(
    int Position, string Name, string Result, bool IsOwn, bool IsSeparator = false)
{
    /// <summary>The rank cell; the separator has no rank of its own.</summary>
    public string PositionText => IsSeparator
        ? Strings.Get("RoadTestSeparatorRank")
        : Position.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Unity's "..." / "---" row keeps the board's column rhythm. WHY a factory
    /// and not a cached instance: the name is resolved in the current UI
    /// language, so a page built after a language switch must not repaint an
    /// older translation.
    /// </summary>
    public static RoadTestRow CreateSeparator() =>
        new(0, Strings.Get("RoadTestSeparatorName"), string.Empty, false, IsSeparator: true);
}
