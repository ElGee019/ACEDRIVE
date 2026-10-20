using System.Globalization;
using AceDrive.Overlay.Localization;

namespace AceDrive.Core.Tests;

/// <summary>English ordinal suffix rules from the Unity StringHelper port,
/// including the 11–13 break and the teen-hundred repeats.</summary>
public sealed class OrdinalsTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(-3, "-3")]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(111, "111th")]
    [InlineData(112, "112th")]
    [InlineData(121, "121st")]
    [InlineData(122, "122nd")]
    public void Of_MatchesUnityStringHelper(int value, string expected)
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        Assert.Equal(expected, Ordinals.Of(value));
    }
}
