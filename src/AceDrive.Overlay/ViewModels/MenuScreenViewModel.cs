using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The menu page: the entry a call (hotkey or wake word) opens now. The Jobs
/// tile leads into the career — the main control page with every job door —
/// by click or by voice. The other tiles are still artwork only. Marked as a
/// jobs-side screen so the onboarding gate treats it like every other
/// post-introduction page.
/// </summary>
public sealed class MenuScreenViewModel : ViewModelBase, IJobsScreen, IVoiceScreen
{
    // The words are baked into the tile artwork, so the spoken keys are the
    // drawn English labels, not resx copy: a translated page would need new
    // artwork and its own keywords. "job" covers the mic's singular reading;
    // MatchLabel still answers any phrase containing the whole label.
    private static readonly string[] JobsKeywords = ["JOBS", "JOB"];

    public MenuScreenViewModel(ICommand jobs)
    {
        JobsCommand = jobs;
        var actions = new Dictionary<string, Action>();
        foreach (string keyword in JobsKeywords)
        {
            actions[keyword] = OpenJobs;
        }
        VoiceActions = actions;
    }

    /// <summary>Opens the control page (all job doors).</summary>
    public ICommand JobsCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    private void OpenJobs() => JobsCommand.Execute(null);
}
