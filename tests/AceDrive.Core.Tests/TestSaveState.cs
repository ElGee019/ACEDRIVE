using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// A real <see cref="GameStateSaver"/> over in-memory stores. The flows take the
/// saver itself (it assembles the snapshot, so there is nothing to fake), and a
/// collaborator test still needs every write to land somewhere harmless.
/// The wallet and garage arrive from the caller: they are the state the test
/// asserts on, so the saver must see the same instances.
/// </summary>
internal static class TestSaveState
{
    public static GameStateSaver Saver(
        IClock clock, DriverWallet wallet, Garage garage, IDriverStore? drivers = null,
        ValuationLedger? valuation = null) =>
        new(clock, new GameClock(clock, clock.UtcNow),
            new FakeGameClockStore(), drivers ?? new FakeDriverStore(),
            new FakeGarageStore(), new FakeCityStore(),
            new FakeHungerStore(), new FakeRoadTestStore(),
            new FakeShiftStore(),
            wallet, new DailyStreak(clock, null), new OperatingZone(null, seed: 0, AddressMap.Default), new Trunk(),
            garage, new CityEconomy([], CityRules.Default), new Hunger(HungerRules.Default),
            new RoadTestLedger([], seed: 0), new TaxiShift(), new ShiftRecord(null), new JobLedger(),
            new DestinationPins(), new VisitLedger(), valuation ?? new ValuationLedger(),
            new WhereIsWillyLedger());
}
