// Cento Miglia registration verification (PORTPLAN item 49, port of Unity
// MilleMigliaManager.SubmitRegNumberAttempt): buffer the typed digits until the
// number's digit count is reached, compare, and close the entry on the second
// wrong number. Mutable cold-path state holder, like the job state records.
namespace AceDrive.Core;

/// <summary>
/// One entry's registration check. The digit count grows with the tier's
/// number (Unity's <see cref="CentoMiglia.RegistrationDigits"/>), so the screen
/// waits for exactly as many digits as the number has before judging. Unity
/// keys the attempt count on the week's history entry; this entry owns it,
/// because the port has no history gate yet.
/// </summary>
public sealed class CentoRegistrationCheck
{
    /// <summary>
    /// Wrong entries the player may make before the entry closes (Unity's
    /// <c>attempts &gt;= 2</c> rule: the second wrong number ends it).
    /// </summary>
    public const int MaxAttempts = 2;

    private readonly int _registration;
    private readonly int _digitCount;
    private readonly List<int> _digits = [];

    public CentoRegistrationCheck(int registration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(registration);
        _registration = registration;
        _digitCount = DigitCountOf(registration);
    }

    /// <summary>
    /// The number the player selected so far, oldest first: the digits still
    /// pending, or the attempt just judged. The judged attempt stays in the
    /// buffer until the next digit starts a new one, because the screen paints
    /// the keypad selection and the "selected" line from it — clearing it the
    /// instant the last digit landed would blank the number the player just
    /// chose.
    /// </summary>
    public IReadOnlyList<int> EnteredDigits => _digits;

    /// <summary>How many digits the registration has (1 to 4, tier-dependent).</summary>
    public int DigitCount => _digitCount;

    /// <summary>Wrong numbers entered so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>True once the right number was entered; the race may start.</summary>
    public bool IsSolved { get; private set; }

    /// <summary>True once too many wrong numbers closed the entry for good.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// Adds one typed digit (0-9) and answers what it means. A closed entry
    /// stays closed, a solved one stays solved, and a complete buffer is judged
    /// as one number exactly like Unity's digit loop.
    /// </summary>
    public CentoRegistrationResult Submit(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);

        if (IsClosed)
        {
            return CentoRegistrationResult.Closed;
        }

        if (IsSolved)
        {
            return CentoRegistrationResult.Accepted;
        }

        // A judged number is still on the page; the next digit starts the new
        // attempt from an empty buffer (the selection display reads this).
        if (_digits.Count >= _digitCount)
        {
            _digits.Clear();
        }

        _digits.Add(digit);
        if (_digits.Count < _digitCount)
        {
            return CentoRegistrationResult.DigitPending;
        }

        int entered = 0;
        foreach (int buffered in _digits)
        {
            entered = (entered * 10) + buffered;
        }

        if (entered == _registration)
        {
            IsSolved = true;
            return CentoRegistrationResult.Accepted;
        }

        Attempts++;
        if (Attempts >= MaxAttempts)
        {
            IsClosed = true;
            return CentoRegistrationResult.Closed;
        }

        return CentoRegistrationResult.WrongAttempt;
    }

    // Unity's DigitCountOf: 0 is one digit (the first tier may draw 0).
    private static int DigitCountOf(int number)
    {
        int digits = 1;
        while (number >= 10)
        {
            number /= 10;
            digits++;
        }
        return digits;
    }
}
