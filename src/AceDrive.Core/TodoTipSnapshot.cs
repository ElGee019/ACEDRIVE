// The tip a completed subject showed when it completed (PORTPLAN item 46,
// Unity TodoManager.completedTipTexts). The threshold advances at completion,
// so the live tip would name the NEXT target; the popup must show the one the
// player just met. Copy stays a key plus its values, never a sentence.
namespace AceDrive.Core;

/// <summary>The copy key and the two values a completed tip renders with.</summary>
public sealed record TodoTipSnapshot(string CopyKey, double Target, double Current);
