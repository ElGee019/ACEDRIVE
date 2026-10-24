using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The trip-start breakdown (PORTPLAN 39, Unity's <c>BeginTripSession</c> +
/// <c>BreakdownFlow</c>): the seeded roll, the mechanic's tow fee and the page
/// that reports the interrupted trip.
///
/// The taxi ride and the delivery run start a session the same way, so the roll
/// lives here once instead of as two host members both flows had to be handed.
/// What a cut costs the job (a lost ride, lost cargo, familiarity) belongs to
/// the job that was cut: the caller applies it and puts the page up, this
/// collaborator only prices the interruption.
/// </summary>
public sealed class TripBreakdowns
{
    private readonly BreakdownStrategy _breakdowns;
    private readonly Garage _garage;
    private readonly IOnboardingStore _onboarding;
    private readonly DriverWallet _wallet;
    private readonly IRivalsService _rivals;
    private readonly GameStateSaver _saver;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    public TripBreakdowns(
        BreakdownStrategy breakdowns, Garage garage, IOnboardingStore onboarding,
        DriverWallet wallet, IRivalsService rivals, GameStateSaver saver,
        VoiceBeat beat, ILog log)
    {
        _breakdowns = breakdowns;
        _garage = garage;
        _onboarding = onboarding;
        _wallet = wallet;
        _rivals = rivals;
        _saver = saver;
        _beat = beat;
        _log = log;
    }

    /// <summary>
    /// One roll at a trip start: a stranded engine always fires the
    /// engine-failure variant, otherwise the roll runs against the wear-derived
    /// chance. Past onboarding and with a car only (Unity's
    /// <c>firstSceneComplete</c> + mechanic gates).
    /// </summary>
    public BreakdownOutcome? Roll(BreakdownKind kind)
    {
        // WHY the store read: the walk's progress is still the story
        // collaborator's state (S4 makes the story buildable in the root), but
        // the store is what the two agree on, so a walk that ends mid-session
        // unlocks jobs without rebuilding this instance.
        if (!_onboarding.Load().IsComplete || _garage.Current is not { } car)
        {
            return null;
        }

        double fee = _garage.MaintenanceRules.PickupFee;
        if (car.Engine.Broken)
        {
            return new BreakdownOutcome(kind, Fraction: 0.0, fee, EngineFailure: true);
        }

        double? fraction = _breakdowns.TryTrigger(car.Condition.BreakdownChance(car.Odometer));
        return fraction is { } hit
            ? new BreakdownOutcome(kind, hit, fee, EngineFailure: false)
            : null;
    }

    /// <summary>
    /// The mechanic tows the car in: the pickup fee is charged, the driver save
    /// banks it, and the returned page reports the interrupted trip. BACK
    /// belongs to the caller's own page flow, so it arrives as a command.
    /// </summary>
    public BreakdownScreenViewModel Interrupt(BreakdownOutcome breakdown, ICommand backCommand)
    {
        double fee = _wallet.ChargeUnavoidable(breakdown.PickupFee);
        _saver.SaveDriver(_rivals.LastResult);
        _log.LogWarning(
            $"[Breakdown] {breakdown.Kind}" +
            $"{(breakdown.EngineFailure ? " engine failure" : $" at {breakdown.Fraction:0.00} of the route")}," +
            $" tow fee {fee:0.00} CR (cash {_wallet.Cash:0.00})");
        return new BreakdownScreenViewModel(breakdown, _beat, backCommand);
    }
}
