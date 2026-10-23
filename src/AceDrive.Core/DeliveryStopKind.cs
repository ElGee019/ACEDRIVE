// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobHandler).
namespace AceDrive.Core;

/// <summary>
/// One leg of a delivery run. Unity's handler distinguishes the wholesale
/// hub pickup (PICK UP THE GOODS) from the store drop-off (DELIVER ITEMS);
/// the shared task-text machinery then paints a different action per leg.
/// </summary>
public enum DeliveryStopKind
{
    /// <summary>A wholesale hub's pickup leg (PICK UP THE GOODS).</summary>
    Hub,

    /// <summary>A store's drop-off leg (DELIVER ITEMS).</summary>
    Store,
}
