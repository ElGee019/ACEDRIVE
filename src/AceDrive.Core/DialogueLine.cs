// Onboarding data (PORTPLAN Career Start items 2-4): favorite brand ->
// grandma's car -> birthday dialogue. Pure data records, no behavior.
namespace AceDrive.Core;

/// <summary>One birthday dialogue page. Speaker is "GRANDMA", "YOU", or empty.</summary>
public sealed record DialogueLine(string Speaker, string Text);
