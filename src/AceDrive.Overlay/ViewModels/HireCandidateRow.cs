namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One resident the community centre offers for hire this week: the name the
/// row's HIRE button passes to the roster and the familiarity line under it.
/// The page only paints these; the roster owns who is actually hireable.
/// </summary>
public sealed record HireCandidateRow(string Name, string FriendshipLine);
