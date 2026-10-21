// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity ProceduralQuestionService +
// CustomerRequestScreen option building).
namespace AceDrive.Core;

/// <summary>
/// Builds and grades the car-stat quiz. Values come from the catalogue row,
/// never from the telemetry: the quiz asks about the garage's current car,
/// like Unity. Decoys prefer same-year cars, then any car, and pad with the
/// correct value +/- 10..50 only when the roster cannot supply two.
/// </summary>
public static class CarStatQuiz
{
    /// <summary>Reads one quiz stat, null when the catalogue row leaves it empty.</summary>
    public static double? Read(CarSpec? car, CarStat stat)
    {
        if (car is null)
        {
            return null;
        }

        double value = stat switch
        {
            CarStat.MassKg => car.MassKg,
            CarStat.Horsepower => car.Horsepower,
            CarStat.TopSpeedKmh => car.TopSpeedKmh,
            CarStat.EngineCc => car.EngineCc,
            CarStat.FuelPer100Km => car.FuelPer100Km,
            CarStat.Price => car.Price,
            _ => 0.0,
        };
        // A zero/missing column is not a question (Unity's empty designer rule,
        // generalised to every stat); negative or NaN data is broken too.
        return double.IsFinite(value) && value > 0.0 ? value : null;
    }

    /// <summary>Categories not asked on this car yet, in table order.</summary>
    public static IReadOnlyList<CarStatCategory> AvailableCategories(
        IReadOnlyList<CarStatCategory> categories, IReadOnlyList<string> asked)
    {
        List<CarStatCategory> available = [];
        foreach (CarStatCategory category in categories)
        {
            if (category.Bands.Count > 0 && !Contains(asked, category.Key))
            {
                available.Add(category);
            }
        }
        return available;
    }

    /// <summary>
    /// Appends the asked category and clears the history once every category
    /// has been asked. Unity resets at 7 while only 6 categories are askable
    /// with an empty designer; resetting at the table count fixes that drift.
    /// </summary>
    public static IReadOnlyList<string> MarkAsked(
        IReadOnlyList<string> asked, string key, int categoryCount)
    {
        List<string> next = [];
        foreach (string existing in asked)
        {
            string cleaned = (existing ?? "").Trim();
            if (cleaned.Length > 0 && !Contains(next, cleaned))
            {
                next.Add(cleaned);
            }
        }

        string askedKey = (key ?? "").Trim();
        if (askedKey.Length > 0 && !Contains(next, askedKey))
        {
            next.Add(askedKey);
        }

        if (categoryCount > 0 && next.Count >= categoryCount)
        {
            next.Clear();
        }
        return next;
    }

    /// <summary>
    /// Builds one question: two decoys plus the correct value, shuffled, with
    /// the correct index resolved after the shuffle.
    /// </summary>
    public static CarStatQuestion? TryCreate(
        CarStatCategory category,
        CarSpec car,
        IReadOnlyList<CarSpec> pool,
        Random rng)
    {
        if (Read(car, category.Stat) is not { } correct)
        {
            return null;
        }

        CarStatBand? band = category.BandFor(correct);
        if (band is not { Variants.Count: > 0 })
        {
            return null;
        }

        List<double> options = [correct];
        AddDecoys(options, correct, category.Stat, car, pool, sameYear: true);
        AddDecoys(options, correct, category.Stat, car, pool, sameYear: false);
        while (options.Count < 3)
        {
            PadDecoy(options, correct, rng);
        }

        Shuffle(options, rng);
        int correctIndex = options.IndexOf(correct);
        if (correctIndex < 0)
        {
            // Defensive: the correct value is always option 0 before the
            // shuffle, so this only guards a future edit.
            options[0] = correct;
            correctIndex = 0;
        }

        string text = band.Variants[rng.Next(band.Variants.Count)];
        return new CarStatQuestion(category, text, options, correctIndex);
    }

    private static void AddDecoys(
        List<double> options, double correct, CarStat stat, CarSpec car,
        IReadOnlyList<CarSpec> pool, bool sameYear)
    {
        foreach (CarSpec other in pool)
        {
            if (options.Count >= 3)
            {
                return;
            }
            if (other.Key == car.Key || (other.Year == car.Year) != sameYear)
            {
                continue;
            }
            if (Read(other, stat) is not { } value
                || value == correct || options.Contains(value))
            {
                continue;
            }
            options.Add(value);
        }
    }

    private static void PadDecoy(List<double> options, double correct, Random rng)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            double offset = rng.Next(10, 50);
            double candidate = attempt % 2 == 0 ? correct + offset : correct - offset;
            if (candidate <= 0.0 || candidate == correct || options.Contains(candidate))
            {
                continue;
            }
            options.Add(candidate);
            return;
        }

        // A tiny value can exhaust the low-side attempts; the high side always
        // has room above zero.
        double fallback = correct + rng.Next(10, 50);
        while (options.Contains(fallback))
        {
            fallback += 1.0;
        }
        options.Add(fallback);
    }

    private static void Shuffle(List<double> options, Random rng)
    {
        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (options[i], options[j]) = (options[j], options[i]);
        }
    }

    private static bool Contains(IReadOnlyList<string> values, string key)
    {
        foreach (string value in values)
        {
            if (string.Equals(value, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
