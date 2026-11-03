using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The name draw a test hands to a service the composition root wires with the
/// shared generator. The default pool on a fixed seed keeps a test that reads
/// an owner identity replayed the same way every run, while the draw itself
/// stays the production rule (weighted pool, then first name and surname).
/// </summary>
internal static class TestNames
{
    public static ProceduralNameGenerator For(int seed = 0) => new(NamePoolTable.Default, seed);
}
