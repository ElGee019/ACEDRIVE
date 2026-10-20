namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One line of the receipt's earnings block. The view renders the pair as
/// two left-aligned columns, so the ViewModel hands explanation and amount
/// over separately instead of gluing them into one sentence.
/// </summary>
public sealed record ReceiptLine(string Explanation, string Amount);
