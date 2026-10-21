using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>residents.json: missing/corrupt files mean a fresh resident table,
/// never a crash; a round trip keeps every door and identity.</summary>
public sealed class JsonResidentStoreTests
{
    private static readonly Resident[] Sample =
    [
        new(new AddressLocation("Street 1", 4, HouseSide.Left),
            new ProceduralName("Anna", "Abt", "alone")),
        new(new AddressLocation("Street 2", 17, HouseSide.Right),
            new ProceduralName("Bert", "Bach", "together")),
    ];

    [Fact]
    public void MissingFile_AnswersAnEmptyTable()
    {
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N"), "residents.json");
        var log = new TestLog();

        Assert.Empty(new JsonResidentStore(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void RoundTrip_KeepsEveryResident()
    {
        string file = Path.GetTempFileName();
        try
        {
            new JsonResidentStore(file, new TestLog()).Save(Sample);

            Assert.Equal(Sample, new JsonResidentStore(file, new TestLog()).Load());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CorruptFile_FallsBackToAnEmptyTableAndWarns()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();

            Assert.Empty(new JsonResidentStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
