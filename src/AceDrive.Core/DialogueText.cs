// Fills the content tokens in dialogue pages (PORTPLAN 5/27): the errand
// business and its random map address. Pure string logic so the same rule can
// move to the Lua port.
namespace AceDrive.Core;

/// <summary>
/// Replaces the <c>[POI]</c> and <c>[STREET]</c> tokens in authored dialogue.
/// Lines without a token keep their original instance, so an ordinary page is
/// never copied.
/// </summary>
public static class DialogueText
{
    public const string PoiToken = "[POI]";
    public const string AddressToken = "[STREET]";

    public static IReadOnlyList<DialogueLine> Fill(
        IReadOnlyList<DialogueLine> lines, string poi, string address)
    {
        if (lines.Count == 0)
        {
            return lines;
        }

        DialogueLine[] filled = new DialogueLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            filled[i] = Fill(lines[i], poi, address);
        }
        return filled;
    }

    private static DialogueLine Fill(DialogueLine line, string poi, string address)
    {
        if (!line.Text.Contains(PoiToken, StringComparison.Ordinal)
            && !line.Text.Contains(AddressToken, StringComparison.Ordinal))
        {
            return line;
        }

        return line with
        {
            Text = line.Text
                .Replace(PoiToken, poi, StringComparison.Ordinal)
                .Replace(AddressToken, address, StringComparison.Ordinal),
        };
    }
}
