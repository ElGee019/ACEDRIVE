using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One store row, preformatted: the view stays dumb (Unity StoreListView).</summary>
public sealed record DeliveryRow(
    DeliveryStore Store,
    string NumberWord,
    string NameLine,
    string OwnerLine,
    string UnitsLine,
    string GrowthLine,
    string PercentLine,
    double Percent,
    StockLevel Level,
    bool IsUrgent,
    bool IsConfirmed,
    string ConfirmedLine);
