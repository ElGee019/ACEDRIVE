namespace AceDrive.Overlay.Services;

/// <summary>
/// A built wake grammar: the JSON the constrained decoder listens with, and how
/// many of the offered phrases survived the model's vocabulary (the rest are
/// dropped with the words the model cannot pronounce, see
/// <see cref="VoskWakeGrammar"/>).
/// </summary>
public sealed record WakeGrammar(string Json, int Kept, int Dropped);
