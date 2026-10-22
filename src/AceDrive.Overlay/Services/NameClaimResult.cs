namespace AceDrive.Overlay.Services;

/// <summary>Result of the global display-name reservation.</summary>
public enum NameClaimResult
{
    /// <summary>The name was free and is now owned by this uid.</summary>
    Claimed,

    /// <summary>The same uid already owns the name (save wipe / restart).</summary>
    Owned,

    /// <summary>Another player owns the normalized key.</summary>
    Taken,

    /// <summary>The name does not satisfy the local rules.</summary>
    Invalid,

    /// <summary>Online layer unavailable; single-player may proceed locally.</summary>
    Unavailable,
}
