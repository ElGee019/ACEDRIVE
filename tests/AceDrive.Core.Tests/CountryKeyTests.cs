using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Country aggregate doc keys.</summary>
public sealed class CountryKeyTests
{
    [Theory]
    [InlineData("Germany", "germany")]
    [InlineData("United States", "united-states")]
    [InlineData("  Belgium ", "belgium")]
    [InlineData("!!!", "unknown")]
    [InlineData("", "unknown")]
    public void Normalize_ProducesTheDocKey(string country, string expected) =>
        Assert.Equal(expected, CountryKey.Normalize(country));
}
