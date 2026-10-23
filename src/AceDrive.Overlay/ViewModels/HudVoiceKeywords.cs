namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Spoken words the companion's action button answers to, keyed by the same
/// action resource key that paints its label. The words stay English whatever
/// language the button prints: the Vosk model is English, so a Dutch
/// "AFZETTEN" button still has to answer "drop off". Every action also answers
/// "enter", the one word the model hears reliably, so the old habit keeps
/// working.
/// </summary>
internal static class HudVoiceKeywords
{
    private static readonly string[] Enter = ["enter"];

    // WHY both spellings: the model splits what it hears inconsistently
    // ("pick up" one utterance, "pickup" the next), and each form is a
    // whole-word match of its own, so the table lists both instead of guessing
    // which one the microphone will land.
    private static readonly string[] PickUp = ["enter", "pick up", "pickup"];
    private static readonly string[] DropOff = ["enter", "drop off", "dropoff"];
    // Delivery's two actions: loading the freight and unloading it at a store.
    private static readonly string[] Deliver = ["enter", "deliver", "deliver items"];

    /// <summary>Keywords for one action resource key; unknown keys answer "enter".</summary>
    public static IReadOnlyList<string> For(string actionKey) => actionKey switch
    {
        "HudPickUp" or "HudJobPickupAction" or "HudDeliveryHubAction" => PickUp,
        "HudJobDropoffAction" => DropOff,
        "HudDeliveryStoreAction" => Deliver,
        _ => Enter,
    };
}
