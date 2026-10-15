using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One car button: label plus optional thumbnail.</summary>
public sealed record CarOption(GrandmaCar Car, string Label, bool HasImage);
