namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One row of a first-run dropdown: the stored value plus the label the
/// player reads. Generic so the unit and the language picker share one shape
/// instead of two near-identical record types.
/// </summary>
public sealed record PreferenceOption<T>(T Value, string Label);
