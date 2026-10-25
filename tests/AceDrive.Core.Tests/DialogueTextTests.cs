using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The authored tokens turn dialogue into a concrete address and speaker.</summary>
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
    public void Fill_ReplacesThePlayerToken_WithTheNameOrPronoun()
    {
        DialogueLine[] lines = [new("GRANDMA", "[YOU], dinner is ready.")];

        Assert.Equal(
            "Ace, dinner is ready.",
            Assert.Single(DialogueText.Fill(lines, "", "", "Ace")).Text);
        Assert.Equal(
            "you, dinner is ready.",
            Assert.Single(DialogueText.Fill(lines, "", "")).Text);
    }

    [Fact]
    public void Fill_ReplacesTheCarToken_WithTheBrandOrMachine()
    {
        // Unity's MaxDialogueScreen swaps "machine" for the current car brand;
        // a save without an announced car keeps the authored noun.
        DialogueLine[] lines = [new("MAX", "That fine [CAR] of yours.")];

        Assert.Equal(
            "That fine Mazda of yours.",
            Assert.Single(DialogueText.Fill(lines, "", "", "", "Mazda")).Text);
        Assert.Equal(
            "That fine machine of yours.",
            Assert.Single(DialogueText.Fill(lines, "", "", "")).Text);
    }

    [Fact]
    public void Fill_ReplacesTheNoteTokens_WithTheSameValues()
    {
        // The note is authored beside the spoken page, so a hint that names
        // the car must not leak the [CAR] placeholder either.
        DialogueLine[] lines =
            [new("MAX", "Interested?", "Open JOBS from your [CAR].")];

        DialogueLine page = Assert.Single(
            DialogueText.Fill(lines, "", "", "", "Mazda"));

        Assert.Equal("Interested?", page.Text);
        Assert.Equal("Open JOBS from your Mazda.", page.Note);
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
