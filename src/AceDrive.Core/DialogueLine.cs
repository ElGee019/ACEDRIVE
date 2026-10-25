// Onboarding data (PORTPLAN Career Start items 2-4): favorite brand ->
// voice info -> birthday dialogue -> grandma's car. Pure data records, no
// behavior.
namespace AceDrive.Core;

/// <summary>
/// One birthday dialogue page. Speaker is "GRANDMA", "YOU", or empty. The
/// optional note is an aside the screen prints under a rule: guidance that
/// belongs beside the page but is not part of what the speaker says.
/// </summary>
public sealed record DialogueLine(string Speaker, string Text, string? Note = null);
