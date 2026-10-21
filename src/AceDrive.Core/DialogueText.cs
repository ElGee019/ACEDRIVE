// Fills the content tokens in dialogue pages (PORTPLAN 5/27): the errand
// business and its random map address. Pure string logic so the same rule can
// move to the Lua port.
namespace AceDrive.Core;

/// <summary>
/// Replaces the <c>[POI]</c>, <c>[STREET]</c> and <c>[YOU]</c> tokens in
/// authored dialogue. Lines without a token keep their original instance, so
/// an ordinary page is never copied.
/// </summary>
public static class DialogueText
{
    public const string PoiToken = "[POI]";
    public const string AddressToken = "[STREET]";
    public const string PlayerToken = "[YOU]";

    public static IReadOnlyList<DialogueLine> Fill(
        IReadOnlyList<DialogueLine> lines, string poi, string address,
        string playerName = "")
    {
        if (lines.Count == 0)
        {
            return lines;
        }

        DialogueLine[] filled = new DialogueLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            filled[i] = Fill(lines[i], poi, address, playerName);
        }
        return filled;
    }

    private static DialogueLine Fill(
        DialogueLine line, string poi, string address, string playerName)
    {
        if (!line.Text.Contains(PoiToken, StringComparison.Ordinal)
            && !line.Text.Contains(AddressToken, StringComparison.Ordinal)
            && !line.Text.Contains(PlayerToken, StringComparison.Ordinal))
        {
            return line;
        }

        // A save without a profile still reads naturally: the token becomes
        // the pronoun instead of leaking an authored placeholder.
        string name = string.IsNullOrWhiteSpace(playerName) ? "you" : playerName;
        return line with
        {
            Text = line.Text
                .Replace(PoiToken, poi, StringComparison.Ordinal)
                .Replace(AddressToken, address, StringComparison.Ordinal)
                .Replace(PlayerToken, name, StringComparison.Ordinal),
        };
    }
}
