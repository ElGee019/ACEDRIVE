// How a generated business POI's count grows (PORTPLAN item 24, port of
// Unity CarWash/Bank/Insurance/Dealer/Mechanic generators). Kept apart from
// the definition so the data file names the curve and the spawner stays free
// of per-business branches.
namespace AceDrive.Core;

/// <summary>The pacing curve behind one generated business family.</summary>
public enum PoiPacing
{
    /// <summary>Never generated: story and fixed POIs use this.</summary>
    None = 0,

    /// <summary>First at <see cref="PoiSpawnRule.FirstStreets"/>, then one
    /// more every <see cref="PoiSpawnRule.IntervalStreets"/> streets (Unity's
    /// car wash, bank and insurance broker).</summary>
    Streets,

    /// <summary>One only after <see cref="PoiSpawnRule.FirstJobs"/> settled
    /// jobs, then one more every <see cref="PoiSpawnRule.IntervalStreets"/>
    /// streets beyond <see cref="PoiSpawnRule.FirstStreets"/> (Unity's car
    /// mechanic).</summary>
    JobsFirstThenStreets,

    /// <summary>Whichever comes first: the job-gated opening or the street
    /// curve (Unity's car dealer).</summary>
    Either,

    /// <summary>Only while a hub of the definition's category is owned; one
    /// immediately, then one per <see cref="PoiSpawnRule.IntervalStreets"/>
    /// streets (Unity's grocery and coffee store backfill).</summary>
    OwnedCategory,
}
