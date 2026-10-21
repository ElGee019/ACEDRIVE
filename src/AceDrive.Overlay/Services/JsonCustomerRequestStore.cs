using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the customer-request content from Data/customer_requests.json next to
/// the exe. Missing, unreadable or structurally invalid content degrades to an
/// empty table (no requests) so the overlay always starts; the log names the
/// reason. The data carries the request lines and quiz variants, like the
/// dialogue JSON; only chrome and units live in resx.
/// </summary>
public sealed class JsonCustomerRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonCustomerRequestStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "customer_requests.json"), log)
    { }

    public JsonCustomerRequestStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public CustomerRequestTable Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"customer_requests.json missing at {_file}; no customer requests");
                return CustomerRequestTable.Empty;
            }

            CustomerRequestTable? table = JsonSerializer.Deserialize<CustomerRequestTable>(
                File.ReadAllText(_file), JsonOptions);
            if (table is null || !IsValid(table))
            {
                _log.LogWarning($"customer_requests.json invalid at {_file}; no customer requests");
                return CustomerRequestTable.Empty;
            }

            _log.LogInfo(
                $"customer requests loaded: suffixes={table.SuffixRequests.Count} " +
                $"categories={table.QuestionCategories.Count} timer={table.TimerSeconds}s");
            return table;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning(
                $"customer_requests.json unreadable at {_file} ({ex.GetType().Name}); no customer requests");
            return CustomerRequestTable.Empty;
        }
    }

    private static bool IsValid(CustomerRequestTable table)
    {
        if (table.TimerSeconds <= 0
            || !IsChance(table.Tier2Chance)
            || !IsChance(table.Tier3Chance)
            || string.IsNullOrWhiteSpace(table.SpeedChallengeText)
            || table.SuffixRequests is not { Count: > 0 }
            || table.QuestionCategories is not { Count: > 0 })
        {
            return false;
        }

        foreach ((string suffix, string text) in table.SuffixRequests)
        {
            if (string.IsNullOrWhiteSpace(suffix) || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
        }

        // Every category must be askable: one of the six numeric stats, a unit
        // key, and at least one band with at least one authored variant. A
        // duplicated stat means the file left a stat out (the enum default),
        // which would make two categories ask the same question.
        var stats = new HashSet<CarStat>();
        foreach (CarStatCategory category in table.QuestionCategories)
        {
            if (string.IsNullOrWhiteSpace(category.Key)
                || string.IsNullOrWhiteSpace(category.UnitKey)
                || category.Bands is not { Count: > 0 }
                || !stats.Add(category.Stat))
            {
                return false;
            }

            foreach (CarStatBand band in category.Bands)
            {
                if (band.Variants is not { Count: > 0 }
                    || (band.Min is { } min && !double.IsFinite(min))
                    || (band.Max is { } max && !double.IsFinite(max)))
                {
                    return false;
                }

                foreach (string variant in band.Variants)
                {
                    if (string.IsNullOrWhiteSpace(variant))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool IsChance(double value) =>
        double.IsFinite(value) && value is >= 0.0 and <= 1.0;
}
