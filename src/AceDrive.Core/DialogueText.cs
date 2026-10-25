// Fills the content tokens in dialogue pages (PORTPLAN 5/27): the errand
// business and its random map address. Pure string logic so the same rule can
// move to the Lua port.
namespace AceDrive.Core;

/// <summary>
/// Replaces the <c>[POI]</c>, <c>[STREET]</c>, <c>[YOU]</c> and <c>[CAR]</c>
/// tokens in authored dialogue pages and their notes. Lines without a token
/// keep their original instance, so an ordinary page is never copied.
/// </summary>
public static class DialogueText
{
    public const string PoiToken = "[POI]";
    public const string AddressToken = "[STREET]";
    public const string PlayerToken = "[YOU]";
    // Unity's MaxDialogueScreen swaps the word "machine" for the current car
    // brand; the token keeps that flavor in data instead of the screen code.
    public const string CarToken = "[CAR]";

    public static IReadOnlyList<DialogueLine> Fill(
        IReadOnlyList<DialogueLine> lines, string poi, string address,
        string playerName = "", string carBrand = "")
    {
        if (lines.Count == 0)
        {
            return lines;
        }

        DialogueLine[] filled = new DialogueLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            filled[i] = Fill(lines[i], poi, address, playerName, carBrand);
        }
        return filled;
    }

    private static DialogueLine Fill(
        DialogueLine line, string poi, string address, string playerName,
        string carBrand)
    {
        // The note is authored beside the spoken page, so it earns the same
        // tokens; a page whose note is the only carrier still gets copied.
        if (!NeedsFill(line.Text) && !NeedsFill(line.Note))
        {
            return line;
        }

        // A save without a profile still reads naturally: the token becomes
        // the pronoun instead of leaking an authored placeholder.
        string name = string.IsNullOrWhiteSpace(playerName) ? "you" : playerName;
        // Same idea for the car: no announced brand keeps Unity's noun.
        string car = string.IsNullOrWhiteSpace(carBrand) ? "machine" : carBrand;
        return line with
        {
            Text = FillTokens(line.Text, poi, address, name, car),
            Note = line.Note is null
                ? null
                : FillTokens(line.Note, poi, address, name, car),
        };
    }

    private static bool NeedsFill(string? text) =>
        text is not null
        && (text.Contains(PoiToken, StringComparison.Ordinal)
            || text.Contains(AddressToken, StringComparison.Ordinal)
            || text.Contains(PlayerToken, StringComparison.Ordinal)
            || text.Contains(CarToken, StringComparison.Ordinal));

    private static string FillTokens(
        string text, string poi, string address, string name, string car) =>
        text.Replace(PoiToken, poi, StringComparison.Ordinal)
            .Replace(AddressToken, address, StringComparison.Ordinal)
            .Replace(PlayerToken, name, StringComparison.Ordinal)
            .Replace(CarToken, car, StringComparison.Ordinal);
}
