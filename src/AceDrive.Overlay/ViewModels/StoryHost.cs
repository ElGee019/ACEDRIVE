using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The introduction walk's reporting host: page swaps through the navigator,
/// panel reveals through <see cref="PanelPresenter"/>, and the one shared
/// driver save a purchase moves cash + trunk through. Built by the composition
/// root, so the walk no longer needs the coordinator to exist first.
/// </summary>
public sealed class StoryHost : IStoryHost
{
    private readonly PanelPresenter _panel;
    private readonly Navigation _nav;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly TodoBoard _todos;

    public StoryHost(
        PanelPresenter panel, Navigation navigation,
        GameStateSaver saver, IRivalsService rivals, TodoBoard todos)
    {
        _panel = panel;
        _nav = navigation;
        _saver = saver;
        _rivals = rivals;
        _todos = todos;
    }

    public object? CurrentScreen => _panel.CurrentScreen;

    public bool IsShown => _panel.IsShown;

    public void ShowScreen(object? screen) => _nav.ShowScreen(screen);

    public void Present(object screen) => _panel.Present(screen);

    public void Hide() => _panel.Hide();

    public object ControlScreen() => _nav.ControlPage();

    public object MenuScreen() => _nav.MenuPage();

    public void ShowMenu() => _panel.ShowMenu();

    /// <summary>
    /// Banks the shared driver file; the standings ride along from the last
    /// refresh, like every other save path.
    /// </summary>
    public void SaveDriver() => _saver.SaveDriver(_rivals.LastResult);

    /// <summary>
    /// The ToDo list re-reads the career after a shop page stamped its visit
    /// flag; the flag rides the same driver save the purchase paths use.
    /// </summary>
    public void PoiEntered()
    {
        if (_todos.Sync().Changed)
        {
            SaveDriver();
        }
    }
}
