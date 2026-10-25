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
    // WHY the garbles: in the car the model usually returns one of these
    // two-word fragments for a clean "pick up", and only sometimes the clean
    // transcript — logged 2026-10-21..25 ("the gop" x6, "it up" x5,
    // "the top"/"the up" x2, plus "he got", "you go", "the cup", "the good",
    // "the poop", "the google"), each one dead air the player had to repeat.
    // They are whole-word runs like every other keyword, so a radio transcript
    // containing one can fire too; that is accepted because the button is only
    // up parked on its own door, where this action is the only thing to do.
    private static readonly string[] PickUp =
    [
        "enter", "pick up", "pickup",
        "the gop", "it up", "the up", "the top",
        "he got", "you go", "the cup", "the good", "the poop", "the google",
    ];
    private static readonly string[] DropOff = ["enter", "drop off", "dropoff"];
    // Delivery's two actions: loading the freight and unloading it at a store.
    private static readonly string[] Deliver = ["enter", "deliver", "deliver items"];
    // Grandma's arrival button prints PANCAKES!; the plural, the singular and
    // the model's split two-word reading all answer because the trailing
    // punctuation is dropped before matching.
    private static readonly string[] Pancakes =
        ["enter", "pancakes", "pancake", "pan cakes", "pan cake"];

    /// <summary>Keywords for one action resource key; unknown keys answer "enter".</summary>
    public static IReadOnlyList<string> For(string actionKey) => actionKey switch
    {
        "HudPickUp" or "HudJobPickupAction" or "HudDeliveryHubAction" => PickUp,
        "HudJobDropoffAction" => DropOff,
        "HudDeliveryStoreAction" => Deliver,
        "HudPancakes" => Pancakes,
        _ => Enter,
    };
}
