using System.Text.Json;
using System.Text.Json.Serialization;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the care-package reward catalogue from Data/reward_cards.json next to
/// the exe (PORTPLAN item 61). Missing or invalid files degrade to
/// <see cref="RewardTable.Default"/> with a warning — the overlay must never
/// fail to start over a data file, and the delivered page must still be able to
/// fill its two cards.
/// </summary>
public sealed class JsonRewardCardStore
{
    // WHY the converter: the card kind is authored as a word ("Card",
    // "CashBonus"), so a data file stays readable and an inserted card cannot
    // shift every following row's meaning the way a bare enum index would.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonRewardCardStore(ILog log)
        : this(DataPaths.GameData("reward_cards.json"), log)
    { }

    public JsonRewardCardStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public RewardTable Load()
    {
        JsonReadStatus status = JsonFile.TryRead(
            _file, out RewardTable? table, out string? error, Options);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"reward_cards.json missing at {_file}; using built-in defaults");
            return RewardTable.Default;
        }

        // A corrupt or locked data file must not take the overlay down
        // (AGENTS.md: Try* at every external boundary).
        if (table is null || !IsValid(table))
        {
            _log.LogWarning(
                $"reward_cards.json {JsonFile.Describe(error)} at {_file}; using built-in defaults");
            return RewardTable.Default;
        }

        _log.LogInfo(
            $"reward cards loaded: {table.Cards.Count} cards, " +
            $"{table.DefaultCapacity} glovebox slots, {table.CashBonus:0} CR bonus");
        return table;
    }

    // The delivered page needs two distinct rewards to fill its cards, a
    // glovebox needs at least one slot and a title is what a carried card is
    // matched by, so a blank or duplicated title would make one card
    // unclaimable. WHY the file's own shape is checked first: a catalogue with
    // no Cards block deserializes to a null list, which must degrade to the
    // defaults rather than throw.
    private static bool IsValid(RewardTable table)
    {
        if (table.Cards is not { Count: >= 2 } cards
            || table.DefaultCapacity < 1
            || !double.IsFinite(table.CashBonus) || table.CashBonus < 0.0)
        {
            return false;
        }

        var titles = new HashSet<string>(StringComparer.Ordinal);
        foreach (RewardCard? card in cards)
        {
            if (card is null
                || string.IsNullOrWhiteSpace(card.Title)
                || string.IsNullOrWhiteSpace(card.Description)
                || !titles.Add(card.Title))
            {
                return false;
            }
        }

        return true;
    }
}
