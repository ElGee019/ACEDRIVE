using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Observes a fire-and-forget task without ever letting it escape as an
/// unobserved exception. Services keep their write paths non-blocking; a
/// failure is a log line, never a crash.
/// </summary>
internal static class SafeTask
{
    public static void Forget(Task task, ILog log, string operation)
    {
        _ = task.ContinueWith(
            completed =>
            {
                Exception? error = completed.Exception?.GetBaseException();
                if (error is not null)
                {
                    log.LogWarning($"{operation}: {error.GetType().Name}: {error.Message}");
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
