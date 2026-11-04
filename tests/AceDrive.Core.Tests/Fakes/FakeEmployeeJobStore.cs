using AceDrive.Core;

namespace AceDrive.Core.Tests;

internal sealed class FakeEmployeeJobStore : IEmployeeJobStore
{
    public EmployeeJobState State { get; set; } = new();

    /// <summary>Every banked state, so a test can assert the write happened.</summary>
    public List<EmployeeJobState> Saves { get; } = [];

    /// <summary>How many guarded reads the owner paid, so a test can prove the cache.</summary>
    public int Loads { get; private set; }

    public EmployeeJobState Load()
    {
        Loads++;
        return State;
    }

    public void Save(EmployeeJobState state)
    {
        State = state;
        Saves.Add(state);
    }
}
