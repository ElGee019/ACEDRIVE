namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One car on this week's dealer floor. The whole row is the pick button and its
/// text is the car's name; the price shows below once the car is picked, so the
/// row itself carries no money. The label the buy button shows comes from the
/// ViewModel (BUY IT / CAN'T AFFORD), so the view holds no text.
/// </summary>
public sealed record DealerRow(
    string Key,
    string Title,
    bool IsSelected);
