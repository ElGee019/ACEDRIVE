using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class PlayerProfileTests
{
    [Fact]
    public void CompleteProfile_IsComplete()
    {
        var profile = new PlayerProfile("ElGee", "Germany", "Brands", "street-1", 1);
        Assert.True(profile.IsComplete);
    }

    [Fact]
    public void BlankName_IsIncomplete()
    {
        var profile = new PlayerProfile("  ", "Germany", "Brands", "street-1", 1);
        Assert.False(profile.IsComplete);
    }

    [Fact]
    public void MissingHouseNumber_IsIncomplete()
    {
        var profile = new PlayerProfile("ElGee", "Germany", "Brands", "street-1", 0);
        Assert.False(profile.IsComplete);
    }
}
