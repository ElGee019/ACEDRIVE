using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class VoiceMatchTests
{
    [Theory]
    [InlineData("hey mazda", "mazda", true)]
    [InlineData("Hey Mazda", "mazda", true)]
    [InlineData("hey mazda!", "mazda", true)]
    [InlineData("um hey mazda can you hear me", "mazda", true)]
    [InlineData("hey ford", "mazda", false)]
    [InlineData("hey", "mazda", false)]
    [InlineData("mazda", "mazda", false)]
    [InlineData("hey alfa romeo", "alfa romeo", true)]
    [InlineData("hey alfa romeo", "mazda", false)]
    [InlineData("", "mazda", false)]
    public void IsWake_MatchesSpec(string text, string brand, bool expected) =>
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand));

    [Theory]
    [InlineData("hey lancer", "lancia", true, new[] { "lancer" })]
    [InlineData("hey lancia", "lancia", true, new[] { "lancer" })]
    [InlineData("hey lancer", "lancia", false, new string[0])]
    [InlineData("hey lancer", "mazda", false, new string[0])] // no alias leak across brands
    [InlineData("hey ford", "lancia", false, new[] { "lancer" })]
    public void IsWake_AliasesCoverMishearings(string text, string brand, bool expected, string[] aliases) =>
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand, aliases));

    [Theory]
    [InlineData("continue", "Continue")]
    [InlineData("please continue now", "Continue")]
    [InlineData("Continue!", "Continue")]
    [InlineData("hey continue", "Continue")] // wake prefix tolerated, page owns routing
    [InlineData("could you pick up the syrup", "Pick Up")]
    [InlineData("pickup", null)] // partial word is not a whole-word match
    [InlineData("go on", null)]
    [InlineData("", null)]
    public void MatchLabel_MatchesSpec(string text, string? expected) =>
        Assert.Equal(expected, VoiceMatch.MatchLabel(text, ["Continue", "Pick Up"]));

    [Fact]
    public void MatchLabel_EmptyCommands_NoMatch() =>
        Assert.Null(VoiceMatch.MatchLabel("anything", []));

    [Fact]
    public void MatchLabel_LongestWins() =>
        Assert.Equal("Pick Up", VoiceMatch.MatchLabel("pick up", ["Pick", "Pick Up"]));

    [Theory]
    [InlineData("where is willy", "Where is Willy")] // full label beats its keyword
    [InlineData("cento millia", "cento")]           // garbled tail rescued by keyword
    [InlineData("where is willie", "where is")]     // ditto for "Willy"
    public void MatchLabel_KeywordAliases_RescueGarbledNames(string text, string expected) =>
        Assert.Equal(expected, VoiceMatch.MatchLabel(text, ["cento", "Cento Miglia", "where is", "Where is Willy"]));
}
