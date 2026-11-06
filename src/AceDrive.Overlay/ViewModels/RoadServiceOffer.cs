namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// What the control page's stranded band should draw: hidden when the car is
/// not dry, otherwise Unity's text for the flat road-service charge and
/// whether the wallet can take it (<c>cash &lt; 300</c> swaps the label to the
/// refusal and disables the button). A value object keeps the page free of the
/// fuel flow's state and makes the whole band one cached read.
/// </summary>
public sealed record RoadServiceOffer(bool Visible, string Label, bool Enabled)
{
    /// <summary>The no-band state, reused so a parked full tank allocates
    /// nothing on the 10 Hz refresh.</summary>
    public static RoadServiceOffer Hidden { get; } = new(false, string.Empty, false);
}
