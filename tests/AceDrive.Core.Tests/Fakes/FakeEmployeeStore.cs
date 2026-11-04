using AceDrive.Core;

namespace AceDrive.Core.Tests;

internal sealed class FakeEmployeeStore : IEmployeeStore
{
    public EmployeeState State { get; set; } = new();

    /// <summary>Every banked state, so a test can assert the write happened.</summary>
    public List<EmployeeState> Saves { get; } = [];

    public EmployeeState Load() => State;

    public void Save(EmployeeState state)
    {
        State = state;
        Saves.Add(state);
    }
}
