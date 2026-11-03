namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One extra sentence a flow attaches under a prompt (a reward card that paid
/// for the event), kept as a resource key plus its values so the HUD repaints
/// it with the language like the prompt's own sentence. Co-located with
/// <see cref="HudTaskPrompt"/> because the prompt is its only owner.
/// </summary>
public sealed record HudPromptNote(string TextKey, IReadOnlyList<string>? Args = null);
