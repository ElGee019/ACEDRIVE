using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The session-start catch-up for the driver and the garage (Unity's
/// MapScreen.Show boot block, PORTPLAN items 30/47): a save whose introduction
/// already finished but whose garage is still empty gets grandma's car seeded,
/// and the login streak is recounted with today's bonus granted once.
///
/// It runs exactly once, from the coordinator's constructor, so reading
/// <see cref="IOnboardingStore"/> here is not a poll-time disk read: the same
/// contract <c>PoiWorld</c> and <c>TripBreakdowns</c> already use. The display
/// week arrives as a value (the starter's owned-car stamp) instead of a
/// <c>DayCycle</c> reference, so this class stays a leaf with no host and no
/// cycle back into the session.
/// </summary>
public sealed class DriverCatchUp
{
    private readonly IOnboardingStore _onboarding;
    private readonly Garage _garage;
    private readonly DailyStreak _streak;
    private readonly DriverWallet _wallet;
    // The streak's random skill gains (PORTPLAN 06/47, Unity
    // ApplyRandomSkillDelta): the root owns the seed, so a test can pin the
    // skills a resumed streak lands on.
    private readonly Random _rng;
    // The shared state bank: a seeded car and a granted bonus are both progress
    // a kill would replay, so each banks through the one writer.
    private readonly GameStateSaver _saver;
    // The standings snapshot that rides the driver save (null keeps the last
    // known board).
    private readonly IRivalsService _rivals;
    private readonly ILog _log;

    public DriverCatchUp(
        IOnboardingStore onboarding, Garage garage, DailyStreak streak,
        DriverWallet wallet, Random rng, GameStateSaver saver,
        IRivalsService rivals, ILog log)
    {
        _onboarding = onboarding;
        _garage = garage;
        _streak = streak;
        _wallet = wallet;
        _rng = rng;
        _saver = saver;
        _rivals = rivals;
        _log = log;
    }

    /// <summary>
    /// Seeds the picked starter car and pays the login bonus. Runs once, before
    /// the first readout is painted; <paramref name="displayWeek"/> is the week
    /// the seeded car is stamped with. Never throws (the stores swallow IO).
    /// </summary>
    public void Run(int displayWeek)
    {
        // One read answers both gates: the introduction's completion and the
        // car it picked. A fresh career is still in onboarding, so the count is
        // false and the garage stays empty until the pick itself creates the car.
        OnboardingState progress = _onboarding.Load();
        bool onboardingDone = progress.IsComplete;
        if (onboardingDone)
        {
            SeedStarterCar(progress.GrandmaCarId, displayWeek);
        }

        // The counter is refreshed on every launch, gate or not (Unity recounts
        // from the saved dates before anything reads the driver). Only the grant
        // waits for the introduction, Unity's firstSceneComplete gate.
        _streak.Update();
        if (onboardingDone && _streak.BonusDue)
        {
            GrantLoginBonus();
        }
    }

    // Grandma's car is the first OwnedCar (PORTPLAN Career Start item 3 + item
    // 30): a save whose onboarding already ran but whose garage is still empty —
    // every save written before the garage existed — seeds the car it picked
    // instead of opening on an empty garage. Unity creates the starter OwnedCar
    // at the pick itself, so this is the same state one session later.
    private void SeedStarterCar(string? starter, int displayWeek)
    {
        if (_garage.Cars.Count != 0 || starter is not { Length: > 0 })
        {
            return;
        }

        _garage.Add(starter, displayWeek);
        _saver.SaveGarage();
        _log.LogInfo($"garage seeded with grandma's car: {starter}");
    }

    // Unity's GrantDailyBonusIfDue: the curve pays CR for the streak day, then
    // day N hands N random skill points out, silently, before the save.
    private void GrantLoginBonus()
    {
        int days = _streak.DaysInARow;
        int bonus = ConsecutiveDayBonus.ComputeBonus(days);
        _streak.StampBonus();
        _wallet.AddBonus(bonus);
        for (int i = 0; i < days; i++)
        {
            _wallet.AddRandomSkill(_rng, 1.0);
        }

        _saver.SaveDriver(_rivals.LastResult);
        _log.LogInfo($"[Streak] consecutive-day bonus granted: {bonus} CR (day {days}, " +
            $"{days} random skill point(s))");
    }
}
