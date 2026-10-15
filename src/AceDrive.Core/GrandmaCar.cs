// Onboarding data (PORTPLAN Career Start items 2-4): favorite brand ->
// grandma's car -> birthday dialogue. Pure data records, no behavior.
namespace AceDrive.Core;

/// <summary>One of grandma's starter cars. ImagePath is null when no thumbnail ships.</summary>
public sealed record GrandmaCar(string Id, string Name, int Year, int Price, string? ImagePath);
