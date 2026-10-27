// Pure gameplay data. The jury verdict for one entry draw: who won, by how
// many score points and the XP the result pays.
namespace AceDrive.Core;

/// <summary>One valuation draw's result (Unity ValuationScreen.ShowVerdict).</summary>
public sealed record ValuationVerdict(bool Won, double Gap, double Xp);
