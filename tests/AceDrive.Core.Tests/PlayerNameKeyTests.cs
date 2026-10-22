using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Opening-screen name rules and the normalized reservation key.</summary>
public sealed class PlayerNameKeyTests
{
    [Theory]
    [InlineData("ElGee", true)]
    [InlineData("Jo", true)]
    [InlineData("A", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("12345678901234567", false)]
    [InlineData("!!", false)]
    public void IsValid_EnforcesTheTwoToSixteenRule(string name, bool expected) =>
        Assert.Equal(expected, PlayerNameKey.IsValid(name));

    [Fact]
    public void IsValid_TrimsBeforeCounting()
    {
        Assert.True(PlayerNameKey.IsValid("  ElGee  "));
        Assert.False(PlayerNameKey.IsValid(" A "));
    }

    [Theory]
    [InlineData("ElGee", "elgee")]
    [InlineData("Max Müller", "max-m-ller")]
    [InlineData("  AC  EVO  ", "ac-evo")]
    [InlineData("!!!", "")]
    public void Normalize_ProducesTheDocKey(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Normalize(name));
}
