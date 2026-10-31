using System.Text.Json;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The wake grammar (voice dial-in, 2026-10-30): the JSON Vosk's constrained
/// decoder is built with. The model's vocabulary decides what survives, and a
/// phrase that would lose a word is dropped whole â€” Kaldi skips unknown words
/// silently, which would leave a bare "hey" that anything can fire.
/// </summary>
public sealed class VoskWakeGrammarTests
{
    [Fact]
    public void Build_KeepsEveryKnownPhraseAndAddsTheUnknownToken()
    {
        WakeGrammar? grammar = VoskWakeGrammar.Build(
            ["hey audi", "hey nissan"], _ => true);

        Assert.NotNull(grammar);
        Assert.Equal(2, grammar!.Kept);
        Assert.Equal(0, grammar.Dropped);
        Assert.Equal(
            ["hey audi", "hey nissan", "[unk]"],
            JsonSerializer.Deserialize<string[]>(grammar.Json) ?? []);
    }

    [Fact]
    public void Build_DropsAPhraseWhole_WhenOneWordIsMissing()
    {
        WakeGrammar? grammar = VoskWakeGrammar.Build(
            ["hey datsun", "hey dotson"], word => word != "datsun");

        Assert.NotNull(grammar);
        Assert.Equal(1, grammar!.Kept);
        Assert.Equal(1, grammar.Dropped);
        Assert.DoesNotContain("datsun", grammar.Json);
        Assert.Contains("hey dotson", grammar.Json);
    }

    [Fact]
    public void Build_NoUsablePhrase_ReturnsNull_SoTheFullDecoderKeepsWaking()
    {
        Assert.Null(VoskWakeGrammar.Build(["hey datsun"], _ => false));
        Assert.Null(VoskWakeGrammar.Build([], _ => true));
    }

    [Fact]
    public void Build_BlankPhrase_IsDropped()
    {
        WakeGrammar? grammar = VoskWakeGrammar.Build(["  "], _ => true);

        Assert.Null(grammar);
    }
}
