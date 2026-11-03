using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The call binding record: the shipped chord, what a save may keep, and the
/// "same input" test the listener uses (a re-capture must still match).
/// </summary>
public sealed class CallBindingTests
{
    [Fact]
    public void Default_IsTheShippedChord()
    {
        Assert.Equal(CallBindingKind.Keyboard, CallBinding.Default.Kind);
        Assert.Equal("CTRL+Shift+C", CallBinding.Default.Label);
        Assert.Equal(
            CallModifiers.Control | CallModifiers.Shift, CallBinding.Default.Modifiers);
        Assert.True(CallBinding.Default.IsUsable);
    }

    [Fact]
    public void NoBinding_AnswersTheDefault()
    {
        Assert.Same(CallBinding.Default, CallBinding.OrDefault(null));
    }

    [Theory]
    [InlineData(CallBindingKind.HidButton, 3, "Button 3", true)]
    [InlineData(CallBindingKind.PadButton, 13, "A", true)]
    [InlineData(CallBindingKind.Keyboard, 0, "C", false)]
    [InlineData(CallBindingKind.Keyboard, 0x43, "", false)]
    [InlineData((CallBindingKind)9, 3, "Button 3", false)]
    public void IsUsable_RefusesASaveTheListenerCannotArm(
        CallBindingKind kind, int code, string label, bool usable)
    {
        var binding = new CallBinding(kind, code, CallModifiers.None, label, null);

        Assert.Equal(usable, binding.IsUsable);
    }

    [Fact]
    public void Matches_IgnoresTheLabelButNotTheDevice()
    {
        var recorded = new CallBinding(
            CallBindingKind.HidButton, 3, CallModifiers.None, "Button 3", "VID_046D&PID_C24F");
        var recaptured = new CallBinding(
            CallBindingKind.HidButton, 3, CallModifiers.None, "Button 3", "vid_046d&pid_c24f");

        Assert.True(recorded.Matches(recaptured));
        Assert.False(recorded.Matches(recaptured with { Device = "VID_1234&PID_5678" }));
        Assert.False(recorded.Matches(recaptured with { Code = 4 }));
    }
}
