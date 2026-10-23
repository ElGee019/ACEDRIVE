namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One labelled value in the garage's car-stats block (Price, Engine, Power …).
/// Both halves are already formatted for the view: the label is resx copy, the
/// value carries its unit, so the view only lays the two columns out.
/// </summary>
public sealed record GarageStatRow(string Label, string Value);
