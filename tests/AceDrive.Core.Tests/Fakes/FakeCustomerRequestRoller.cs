using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>A request roller whose draw the test sets. The defaults are a
/// no-request ride; the recorded call proves the host passed the drawn
/// customer, the current catalogue row and the car's asked history.</summary>
internal sealed class FakeCustomerRequestRoller : ICustomerRequestRoller
{
    public CustomerRequest? Result { get; set; }
    public int Calls { get; private set; }
    public ProceduralName? LastCustomer { get; private set; }
    public CarSpec? LastCar { get; private set; }
    public int LastGameYear { get; private set; }
    public IReadOnlyList<string> LastAsked { get; private set; } = [];

    public CustomerRequest? Roll(
        ProceduralName customer, CarSpec? car, int gameYear,
        IReadOnlyList<string> askedCategories)
    {
        Calls++;
        LastCustomer = customer;
        LastCar = car;
        LastGameYear = gameYear;
        LastAsked = [.. askedCategories];
        return Result;
    }
}
