// Pure gameplay data. No WPF / Windows / backend references: the board
// service maps Firestore documents into this record and the valuation flow
// compares entries through the pure rules.
namespace AceDrive.Core;

/// <summary>
/// One car entered in a week's market valuation (Unity ValuationEntry): the
/// player behind it, the car, the jobs done with that car, the engine state at
/// entry time and the score fixed for the whole week.
/// </summary>
public sealed record ValuationEntry(
    string Uid,
    string PlayerName,
    string CarKey,
    int JobsWithCar,
    double EngineBrokenPercent,
    double Score,
    DateTime? EnteredAt = null);
