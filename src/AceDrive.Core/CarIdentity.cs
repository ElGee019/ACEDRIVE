// Identity of the car the game announced: content folder id plus the
// mechanical preset (showroom variant) spawned with it. EVO keeps variants
// as per-car *.mechanicalcarpreset files, not extra ks_ folders, so the
// folder id alone cannot tell an M2 Coupe from an M2 M Performance. The
// preset half is null when the announcing log line carried no preset path.
namespace AceDrive.Core;

/// <summary>One announced car: ks folder id plus optional mechanical preset id.</summary>
public sealed record CarIdentity(string KsId, string? MechPreset)
{
    /// <summary>
    /// The pool row this car drives: exact variant row first, else the folder
    /// (null-preset) row, else the row alone in the pool. A multi-variant pool
    /// with no preset half to match resolves to nothing on purpose: guessing
    /// a variant would key another car's specs to the driven one.
    /// </summary>
    public CarSpec? FindIn(IReadOnlyList<CarSpec> pool)
    {
        CarSpec? exact = null, folder = null, only = null;
        int rows = 0;
        foreach (CarSpec car in pool)
        {
            if (!string.Equals(car.Id, KsId, StringComparison.Ordinal))
            {
                continue;
            }

            rows++;
            only ??= car;
            if (MechPreset is not null && string.Equals(car.Preset, MechPreset, StringComparison.Ordinal))
            {
                exact ??= car;
            }

            if (car.Preset is null)
            {
                folder ??= car;
            }
        }

        return exact ?? folder ?? (rows == 1 ? only : null);
    }

    /// <summary>
    /// Identity after the next announcement: a different folder replaces it,
    /// an id-only line ("Loaded car" echo) for the same car keeps the known
    /// preset, and any line announcing a preset updates it. EVO emits the
    /// preset-carrying spawn line first and the echo after, so a naive last-
    /// line-wins rule would strip the variant right after learning it.
    /// </summary>
    public CarIdentity RefinedBy(CarIdentity next) =>
        next.KsId != KsId || next.MechPreset is not null ? next : this;

    /// <summary>
    /// True when this announced car is the catalogue <paramref name="row"/>.
    /// A variant row requires the announced preset to match it. A base row is
    /// the folder's standard car: it stands in for a preset the catalogue does
    /// not model, but a sibling variant the pool knows is a different car (the
    /// 992 GT3 RS and its Weissach package), not this one. A preset-less
    /// announcement matches the whole folder because the log only knew the
    /// folder.
    /// </summary>
    public bool Matches(CarSpec row, IReadOnlyList<CarSpec> pool)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(pool);

        if (KsId.Length == 0 || !string.Equals(row.Id, KsId, StringComparison.Ordinal))
        {
            return false;
        }

        if (MechPreset is null)
        {
            return true;
        }

        if (row.Preset is not null)
        {
            return string.Equals(row.Preset, MechPreset, StringComparison.Ordinal);
        }

        return !HasVariantRow(pool, row.Id, MechPreset);
    }

    // A variant row for the announced preset means the pool models that
    // variant separately, so the folder row must not claim it too.
    private static bool HasVariantRow(
        IReadOnlyList<CarSpec> pool, string ksId, string preset)
    {
        foreach (CarSpec car in pool)
        {
            if (string.Equals(car.Id, ksId, StringComparison.Ordinal)
                && string.Equals(car.Preset, preset, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
