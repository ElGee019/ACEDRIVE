// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11.
namespace AceDrive.Core;

/// <summary>
/// Draws the special request for one accepted job. The caller passes the
/// customer (the suffix triggers the passive tier), the quiz car and the
/// per-car asked-category history; null means "no request this ride".
/// </summary>
public interface ICustomerRequestRoller
{
    CustomerRequest? Roll(
        ProceduralName customer,
        CarSpec? car,
        int gameYear,
        IReadOnlyList<string> askedCategories);
}
