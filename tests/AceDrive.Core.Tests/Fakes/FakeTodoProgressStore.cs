using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>In-memory ToDo progress: the ledger's own file without disk I/O.</summary>
internal sealed class FakeTodoProgressStore : ITodoProgressStore
{
    public TodoLedgerState? Saved { get; private set; }

    public int Saves { get; private set; }

    public TodoLedgerState? Load() => Saved;

    public void Save(TodoLedgerState state)
    {
        Saved = state;
        Saves++;
    }
}
