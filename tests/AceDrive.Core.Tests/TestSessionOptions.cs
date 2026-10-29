using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The knobs of a headless session. Every property names a collaborator or a
/// fake the assertions may read, so a test replaces exactly what it cares
/// about instead of rebuilding the graph.
/// </summary>
internal sealed class TestSessionOptions
{
    public FakeClock? Clock { get; init; }
    public FakeTelemetry? Physics { get; init; }
    public float SpeedKmh { get; init; } = 90f;
    public OnboardingState? Progress { get; init; }
    public DeliveryCatalog? Catalog { get; init; }
    public HubCatalog? Hubs { get; init; }
    public ILog? Log { get; init; }
    public DeliveryCareer? Career { get; init; }
    public RoadTestLedger? Roads { get; init; }
    public IGameClockStore? Clocks { get; init; }
    public int StartWeek { get; init; } = 1;
    public DateTime? GameStart { get; init; }
    public IRoadTestStore? RoadTestStore { get; init; }
    public CityEconomy? City { get; init; }
    public ICityStore? CityStore { get; init; }
    public DriverWallet? Wallet { get; init; }
    public IDriverStore? DriverStore { get; init; }
    public IFriendshipStore? Friendships { get; init; }
    public IReadOnlyList<CarSpec>? Cars { get; init; }
    public string? CarKsId { get; init; }
    public string? CarPreset { get; init; }
    public IBrandMileageStore? BrandMileage { get; init; }
    public IRivalsService? Rivals { get; init; }
    public DeliveryFareTable? DeliveryFares { get; init; }
    public DriverState? SavedDriver { get; init; }
    public Hunger? Hunger { get; init; }
    public IHungerStateStore? HungerStore { get; init; }
    public Garage? Garage { get; init; }
    public IGarageStore? GarageStore { get; init; }
    public BreakdownStrategy? Breakdowns { get; init; }
    public Random? Rng { get; init; }
    public OnboardingContent? Content { get; init; }
    public PlayerProfile? Profile { get; init; }
    public IProfileStore? ProfileStore { get; init; }
    public AddressMap? AddressMap { get; init; }
    public string? DetectedName { get; init; } = "ElGee";
    public IPreferencesStore? PreferencesStore { get; init; }
    public PlayerPreferences? Preferences { get; init; }
    public ICultureService? Culture { get; init; }
    public PoiGenerator? Pois { get; init; }
    public ITelemetryHud? Hud { get; init; }
    public FakeVoiceListener? Voice { get; init; }
    public FakeCarBrandSource? CarBrands { get; init; }
    public Trunk? Trunk { get; init; }
    public IReadOnlyList<IPoiArrivalHandler>? PoiHandlers { get; init; }
    public PoiDirectory? PoiDirectory { get; init; }
    public IPoiDirectoryStore? PoiStore { get; init; }
    public PoiSpawner? PoiSpawner { get; init; }
    public JobLedger? Jobs { get; init; }
    public DestinationPins? Pins { get; init; }
    public VisitLedger? Visits { get; init; }
    public ITodoProgressStore? TodoStore { get; init; }
    public TodoTable? Todos { get; init; }
    public FakeOnboardingStore? Onboarding { get; init; }
    public TaxiRules? TaxiRules { get; init; }
    public ICustomerRequestRoller? Requests { get; init; }
    public CustomerRequestTable? RequestTable { get; init; }
    public PolicePatrolPlanner? Police { get; init; }
    public ICarRaritySource? Rarity { get; init; }
    public IPlayerTelemetry? Telemetry { get; init; }
    public ShiftRecord? Shifts { get; init; }
    public IShiftStandings? ShiftStandings { get; init; }
    // The operating-zone board the expansion page renders (PORTPLAN 58).
    public IZoneStandings? ZoneStandings { get; init; }
    public IPlayerNameRegistry? PlayerNames { get; init; }
    public TaxiShift? Shift { get; init; }
    public IShiftStore? ShiftStore { get; init; }
    public FakeSaveDataResetter? SaveResets { get; init; }
    public FakeAppRestarter? Restarter { get; init; }
    public StoreCatalog? StoreCatalog { get; init; }
    // The weekly valuation list the flow reads and pushes to (PORTPLAN 49).
    public IValuationBoard? ValuationBoard { get; init; }
    // The valuation's own draw stream: a suite that pins an opponent or a venue
    // hands its seeded instance in instead of sharing the other draws'.
    public Random? ValuationRng { get; init; }
    // The Cento Miglia page's tier table: empty by default, so only the suites
    // that drive the event page hand content in (production loads the JSON).
    public IReadOnlyList<CentoTier>? CentoTiers { get; init; }
    // The weekly missing-person run (ToWhereIsWilly.md §5): the resident table
    // its elder draw reads, its balance and its own seeded stream. Production
    // shares one resident book; a suite that pins an elder hands one in.
    public ResidentBook? Residents { get; init; }
    public WhereIsWillyRules? WillyRules { get; init; }
    public Random? WillyRng { get; init; }
    // The per-car road-test board the test centre page reads and pushes to,
    // and the flow's own seeded stream for Unity's reaction hold (PORTPLAN 49).
    public IRoadTestStandings? RoadTestStandings { get; init; }
    public Random? RoadTestRng { get; init; }
    // TEMPORARY (testing): opens every calendar event's gates, so a suite can
    // drive an off-day or replayed event. Shipping sessions keep the gates.
    public bool IgnoreEventGates { get; init; }
}
