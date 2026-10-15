// Game-clock persistence (PORTPLAN item 51 groundwork): the game moment plus
// the display week, so date, time and week number survive restarts.
namespace AceDrive.Overlay.Services;

/// <summary>Saved game moment. Small DTO owned by the store interface.</summary>
public sealed record GameClockState(DateTime GameNow, int Week);

public interface IGameClockStore
{
    GameClockState? Load();
    void Save(GameClockState state);
}
