namespace AceDrive.Core.Tests;

/// <summary>A Random with a pinned answer, so a test can drive a rule that
/// rolls (the Cento Miglia field shuffle and its Star of the Show roll) without
/// searching for a seed. <c>Next</c> answers the same bounded value and
/// <c>NextDouble</c> the pinned sample.</summary>
internal sealed class FixedRandom : Random
{
    private readonly int _next;
    private readonly double _nextDouble;

    public FixedRandom(int next = 0, double nextDouble = 0.0)
    {
        _next = next;
        _nextDouble = nextDouble;
    }

    public override int Next(int maxValue) =>
        maxValue <= 1 ? 0 : Math.Clamp(_next, 0, maxValue - 1);

    public override double NextDouble() => _nextDouble;
}
