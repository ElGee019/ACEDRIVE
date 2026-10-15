// Road-test pick (PORTPLAN item 49, port of RoadTestService): one car at a
// time, drawn from the Data/cars.json pool (parsed from CARSTATS_EVO.md until
// the full car database lands, PORTPLAN item 28) and kept until its road test
// is completed. Pure data: display caps and selection live with the
// caller/ledger.
namespace AceDrive.Core;

/// <summary>One road-testable car: stable Kunos id plus its build year, brand and model.</summary>
public sealed record RoadTestCar(string Id, int Year, string Brand, string Model);
