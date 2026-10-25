// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity TaxiJobGenerator special requests).
namespace AceDrive.Core;

/// <summary>
/// The tiered request roll: a suffix match first (passive), then the speed
/// challenge chance, then the car-stat quiz chance. Own seeded stream so the
/// proposal/resident draws and their pinned tests never shift when a request
/// is rolled. A tier that lands on a quiz but has no usable car/category
/// yields no request (WhatCar, the Unity fallback, is deferred).
/// </summary>
public sealed class CustomerRequestRoller : ICustomerRequestRoller
{
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly CustomerRequestTable _table;
    private readonly Random _rng;

    public CustomerRequestRoller(
        IReadOnlyList<CarSpec> cars, CustomerRequestTable table, int seed)
    {
        _cars = cars;
        _table = table;
        _rng = new Random(seed);
    }

    public CustomerRequest? Roll(
        ProceduralName customer,
        CarSpec? car,
        int gameYear,
        IReadOnlyList<string> askedCategories)
    {
        // The Unity line wraps the full group identity ("Max Weber and some
        // boxing partners are ..."): [name] is replaced here, once, so no
        // format-brace hazard reaches the page.
        if (customer.Suffix.Length > 0
            && _table.SuffixRequests.TryGetValue(customer.Suffix, out SuffixRequest? line))
        {
            return new CustomerRequest(
                CustomerRequestKind.Passive,
                line.Text.Replace("[name]", customer.Full, StringComparison.Ordinal),
                Condition: line.Condition);
        }

        if (_rng.NextDouble() < _table.Tier2Chance)
        {
            return new CustomerRequest(CustomerRequestKind.SpeedChallenge, _table.SpeedChallengeText);
        }

        if (_rng.NextDouble() >= _table.Tier3Chance || car is null)
        {
            return null;
        }

        IReadOnlyList<CarStatCategory> available =
            CarStatQuiz.AvailableCategories(_table.QuestionCategories, askedCategories);
        if (available.Count == 0)
        {
            return null;
        }

        CarStatCategory category = available[_rng.Next(available.Count)];
        CarStatQuestion? question = CarStatQuiz.TryCreate(category, car, _cars, _rng);
        return question is null
            ? null
            : new CustomerRequest(CustomerRequestKind.CarQuiz, question.Text, question);
    }
}
