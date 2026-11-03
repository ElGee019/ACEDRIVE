namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// A flow-authored task the companion shows in the task-text slot with an
/// action of its own, outside the destination/arrival rule: the missing-person
/// run's "Well that's a reliefâ€¦" news and its DRIVE button (ToWhereIsWilly.md
/// Â§3.4). The sentence travels as a resource key plus the values it formats, so
/// a language switch repaints it from the same table as every other line
/// (AGENTS.md: no hardcoded copy). <see cref="ActionKey"/> names the button's
/// label; pressing it reports <see cref="ITelemetryHud.TaskActionRequested"/>,
/// and the flow that put the prompt up owns what the tap means.
/// <see cref="Notes"/> adds the reward-card sentences under the main line (a
/// shield that kept a friendship, a repair card that paid the bill), formatted
/// from their own keys so they repaint with the prompt.
/// </summary>
public sealed record HudTaskPrompt(
    string TextKey,
    IReadOnlyList<string>? Args,
    string ActionKey,
    IReadOnlyList<HudPromptNote>? Notes = null);
