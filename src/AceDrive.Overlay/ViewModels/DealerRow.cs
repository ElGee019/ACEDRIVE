namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One car on this week's dealer floor or in the dealer's sell list. The whole
/// row is the pick button and its text is the car's name; the price shows below
/// once the car is picked, so the row itself carries no money. The label the buy
/// or sell button shows comes from the ViewModel (BUY / CAN'T AFFORD THIS CAR /
/// SELL), so the view holds no text.
/// </summary>
public sealed record DealerRow(
    string Key,
    string Title,
    bool IsSelected);
