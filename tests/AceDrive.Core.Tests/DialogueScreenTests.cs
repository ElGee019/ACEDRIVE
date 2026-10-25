using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The dialogue page carries an optional aside under its rule: guidance
/// beside the spoken line that the speaker never says (Max's JOBS pointer).</summary>
public sealed class DialogueScreenTests
{
    private static DialogueScreenViewModel New(
        IReadOnlyList<DialogueLine> lines, Action? onFinished = null) =>
        new(lines, "A NEW TURN", new VoiceBeat(new FakeClock()), onFinished ?? (() => { }));

    [Fact]
    public void Note_RidesItsOwnPage_AndClearsOnTheNext()
    {
        var vm = New(
        [
            new DialogueLine("MAX", "Easy job, fast cash.", "Open JOBS."),
            new DialogueLine("YOU", "Thanks!"),
        ]);

        Assert.True(vm.HasNote);
        Assert.Equal("Open JOBS.", vm.Note);

        vm.ContinueCommand.Execute(null);

        Assert.False(vm.HasNote);
        Assert.Equal("", vm.Note);
    }

    [Fact]
    public void Note_EmptyPage_StaysHidden()
    {
        var vm = New([new DialogueLine("YOU", "Thanks!")]);

        Assert.False(vm.HasNote);
        Assert.Equal("", vm.Note);
    }
}
