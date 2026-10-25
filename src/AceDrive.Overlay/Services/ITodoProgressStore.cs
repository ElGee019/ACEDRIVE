// ToDo progress persistence (PORTPLAN item 46): the ledger's own file, kept
// apart from driver.json because Unity keeps TodoManager state in its own
// PlayerPrefs entry and because a corrupt todo save must never cost the
// player their money. The store swallows its own I/O (AGENTS.md).
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>The ToDo ledger's own save file.</summary>
public interface ITodoProgressStore
{
    /// <summary>The saved career, or null when there is none / it is unreadable.</summary>
    TodoLedgerState? Load();

    /// <summary>Banks the career; never throws.</summary>
    void Save(TodoLedgerState state);
}
