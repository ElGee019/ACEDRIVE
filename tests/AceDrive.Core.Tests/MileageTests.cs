using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Kilometre &lt;-&gt; player-unit conversion behind the HUD mileage.</summary>
public sealed class MileageTests
{
    [Fact]
    public void Kilometres_PassThrough()
    {
        Assert.Equal(1234.5, Mileage.FromKilometres(1234.5, MileageUnit.Kilometres));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.609344, 1.0)]
    [InlineData(100.0, 62.13711922373339)]
    public void Miles_DivideByTheInternationalMile(double kilometres, double miles)
    {
        Assert.Equal(miles, Mileage.FromKilometres(kilometres, MileageUnit.Miles), precision: 9);
    }
}
