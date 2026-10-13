using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Starter ToDo table: Max's grocery first, all targets reachable.</summary>
public sealed class GrandmaTodoTests
{
    [Fact]
    public void Default_StartsWithMaxGrocery()
    {
        Assert.True(GrandmaTodo.IsKnown(GrandmaTodo.MaxGrocery));
        Assert.Equal(GrandmaTodo.MaxGrocery, TodoTable.Default.Entries[0].SubjectId);
        Assert.Equal(1, TodoTable.Default.Entries[0].Target);
    }

    [Fact]
    public void Default_AllTargetsReachable()
    {
        Assert.NotEmpty(TodoTable.Default.Entries);
        foreach (TodoEntry entry in TodoTable.Default.Entries)
        {
            Assert.True(GrandmaTodo.IsKnown(entry.SubjectId));
            Assert.True(entry.Target >= 1);
        }
    }

    [Fact]
    public void IsKnown_RejectsUnknown()
    {
        Assert.False(GrandmaTodo.IsKnown(null));
        Assert.False(GrandmaTodo.IsKnown("CarHaul"));
    }
}
