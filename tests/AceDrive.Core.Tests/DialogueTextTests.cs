using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The errand tokens turn authored dialogue into a concrete address.</summary>
public sealed class DialogueTextTests
{
    [Fact]
    public void Fill_ReplacesThePoiAndTheAddress()
    {
        DialogueLine[] lines = [new("GRANDMA", "Stop by [POI] on [STREET].")];

        IReadOnlyList<DialogueLine> filled = DialogueText.Fill(
            lines, "Max's Grocery Store", "Street 2 · No. 17");

        DialogueLine page = Assert.Single(filled);
        Assert.Equal("Stop by Max's Grocery Store on Street 2 · No. 17.", page.Text);
        Assert.Equal("GRANDMA", page.Speaker);
    }

    [Fact]
    public void Fill_KeepsLinesWithoutTokens()
    {
        DialogueLine page = new("YOU", "Thanks!");

        IReadOnlyList<DialogueLine> filled = DialogueText.Fill(
            [page], "Max's Grocery Store", "Street 1 · No. 1");

        Assert.Same(page, Assert.Single(filled));
    }

    [Fact]
    public void Fill_EmptyDialogue_StaysEmpty()
    {
        DialogueLine[] lines = [];

        Assert.Same(lines, DialogueText.Fill(lines, "x", "y"));
    }
}
