// Why a shop purchase did or did not happen (PORTPLAN item 26, port of
// Unity MaxsScreen.OnBuyItem's gates). The page disables BUY on any non-
// success quote; Buy re-checks the same quote before moving anything.
namespace AceDrive.Core;

/// <summary>Outcome of one shop checkout.</summary>
public enum StorePurchaseStatus
{
    /// <summary>No outcome was computed (default value only).</summary>
    None = 0,

    /// <summary>The purchase is legal and has been applied.</summary>
    Success,

    /// <summary>Quantity is zero or negative.</summary>
    InvalidQuantity,

    /// <summary>The shelf cannot cover the requested quantity.</summary>
    NotEnoughStock,

    /// <summary>The wallet cannot cover the total price.</summary>
    NotEnoughCash,

    /// <summary>The projected volume does not fit the trunk.</summary>
    TrunkFull,
}
