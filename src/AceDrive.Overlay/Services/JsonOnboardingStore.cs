using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Onboarding progress in %AppData%\AceDrive\onboarding.json. Never throws.
/// This is the file that decides whether a restart resumes the walk or replays
/// it, so its load and its failed writes are logged: "starts at point 0" must
/// be diagnosable as a missing save instead of looking like a resume bug.
/// </summary>
public sealed class JsonOnboardingStore : IOnboardingStore
{
    private readonly string _file;
    private readonly ILog _log;
    private readonly object _gate = new();
    private OnboardingState? _current;
    private bool _loaded;

    public JsonOnboardingStore(ILog log)
        : this(DataPaths.UserSave("onboarding.json"), log)
    { }

    public JsonOnboardingStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    // Read once, then cached: the gate is asked from PoiWorld.Grow,
    // TripBreakdowns.Interrupt, ZoneGrowth.Expand, TodoBoard and the boot
    // catch-up, and a file read per ask is the cost this rule exists to
    // avoid (AGENTS.md: cache-first). Save keeps the cache in step.
    public OnboardingState Load()
    {
        lock (_gate)
        {
            if (_loaded) return _current!;
            _loaded = true;

            JsonReadStatus status = JsonFile.TryRead(
                _file, out OnboardingState? state, out string? error, out bool recovered);
            // A corrupt, locked or absent progress file means "start the
            // introduction fresh" instead of blocking startup; the backup is
            // tried first, because a wipe of the primary is not the same thing
            // as a career the player asked to reset.
            _current = state ?? OnboardingState.Fresh;
            if (state is not null)
            {
                // Recovery is a warning, not an info line: the primary file was
                // damaged, and the next reset/deletion would have no backup.
                if (recovered)
                {
                    _log.LogWarning(
                        $"onboarding.json {JsonFile.Describe(error)} at {_file}; " +
                        $"resumed the last good save: {Describe(_current)}");
                }
                else
                {
                    _log.LogInfo($"onboarding progress loaded: {Describe(_current)}");
                }
            }
            else if (status == JsonReadStatus.Unreadable)
            {
                _log.LogWarning(
                    $"onboarding.json {JsonFile.Describe(error)} at {_file}; " +
                    "starting the introduction from the first page");
            }

            return _current;
        }
    }

    public void Save(OnboardingState state)
    {
        // never break shutdown for persistence
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning(
                $"onboarding.json unwritable at {_file} ({error}); progress kept in memory");
        }

        // Invalidate on write: the next Load must see what was just saved,
        // even when the write failed (the in-memory state is the live one).
        lock (_gate)
        {
            _current = state;
            _loaded = true;
        }
    }

    // The state as the log reader needs it: whether the walk is still running
    // and, if so, which page it resumes on.
    private static string Describe(OnboardingState state) =>
        state.IsComplete
            ? $"past the birthday (tour done: {state.CommunityCentreDone})"
            : $"stage={state.Stage}";
}
