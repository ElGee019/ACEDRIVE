// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/16/20/43/47/48
// (Unity PlayerData.cash + PlayerData.xp accumulation on dropoff — the taxi
// and the delivery; the haul banks cash alone, see AddHaul; the login bonus
// lands here too, see AddBonus; the savings bank lives on the same
// PlayerData, see BankBalance/Withdraw/ApplyInterest) and item 06 (the
// driver-rating skills, see Rating).
namespace AceDrive.Core;

/// <summary>
/// The driver's persisted account summary: cash, XP, savings bank and the
/// three driver-rating skills. Every settled receipt lands here; the store
/// banks the snapshot. Cold paths only (dropoff, startup, the bank/wallet
/// screens), so rounding per move is free of the 10 Hz loop.
/// </summary>
public sealed class DriverWallet
{
    /// <summary>The CR a brand-new driver owns. It has to cover the overhead
    /// of the first jobs (fuel, food, a wash) before any fare is banked, so a
    /// fresh game never opens on an empty wallet. Only a first launch reads
    /// it: a resumed save always beats the seed, never re-applies it.</summary>
    public const double StartingCash = 100.0;

    // The driving record (PORTPLAN 44): the wallet owns it because the same
    // snapshot banks it, and only this class ever writes it.
    private readonly DrivingRecord _driving;

    public DriverWallet(DriverState? saved = null)
    {
        _driving = new DrivingRecord(
            saved?.JobDistanceKm ?? 0.0,
            saved?.AccidentFreeKm ?? 0.0,
            saved?.LongestAccidentFreeKm ?? 0.0,
            saved?.Accidents ?? 0);
        // A debt is a real state: an accident repair the player cannot refuse
        // can outrun the cash, and restarting must not forgive it. Only a
        // non-finite hand-edit falls back to the seed.
        Cash = saved is { Cash: var cash } && double.IsFinite(cash) ? cash : StartingCash;
        Xp = Math.Max(0.0, saved?.Xp ?? 0.0);
        // The 1000.0 record default IS Unity's seed (PlayerDefaults + the map
        // boot 0→1000 migration): a pre-bank save deserializes straight to the
        // saldo, so no version check is needed here. The clamp guards a
        // hand-edited negative only.
        BankBalance = Math.Max(0.0, saved?.Bank ?? BankRules.MinBankSaldo);
        HasEnteredBank = saved?.HasEnteredBank ?? false;
        PendingInsurance = Math.Max(0.0, saved?.PendingInsurance ?? 0.0);
        LastInsuranceChargeDate = saved?.LastInsuranceChargeDate;
        // The state's defaults are Unity's new-game profile; a save from
        // before the rating existed therefore reads 20/20/20 unchanged. The
        // clamp still catches a hand-edited zero (Unity's ClampStat).
        Rating = DriverRating.FromSaved(
            saved?.Cornering ?? DriverRating.NewGameOverall,
            saved?.Concentration ?? DriverRating.NewGameOverall,
            saved?.OffTheLine ?? DriverRating.NewGameOverall);
        // The ToDo subject's lifetime-earned reading (PORTPLAN 46, Unity
        // PlayerData.totalCREarned). Unlike Unity the port counts earnings
        // only, never bank withdrawals: the tip has to measure work, not
        // moving the same money around.
        CreditEarned = Math.Max(0.0, saved?.CreditEarned ?? 0.0);
        // The Gentlemen Drivers Club reputation (PORTPLAN 49, Unity
        // PlayerData.clubReputation): raw 0 for a fresh driver, exactly like
        // Unity — the Cento Miglia page applies the 1.0 display floor itself.
        ClubReputation = Math.Max(0.0, saved?.ClubReputation ?? 0.0);
        // The career's Star of the Show wins (Unity starOfTheShowWins): the race
        // prize scales on the new total, so the file must carry it.
        StarOfTheShowWins = Math.Max(0, saved?.StarOfTheShowWins ?? 0);
        // The glove compartment's capacity (PORTPLAN 61, Unity
        // PlayerData.gloveboxSlots). 0 means "never written": the Glovebox heals
        // it from the reward table's own starting capacity on its first read.
        GloveboxSlots = Math.Max(0, saved?.GloveboxSlots ?? 0);
    }

    public double Cash { get; private set; }
    public double Xp { get; private set; }

    /// <summary>
    /// Unity's direct <c>player.xp +=</c> for a bonus the receipt system does
    /// not own (the market valuation verdict and its live/offline results).
    /// Non-positive and non-finite amounts are ignored, so a malformed draw
    /// can never move the ledger.
    /// </summary>
    public void AddXp(double xp)
    {
        if (xp > 0.0 && double.IsFinite(xp))
        {
            Xp += xp;
        }
    }

    /// <summary>Every CR this driver ever earned from a job, a bonus or an
    /// insurance payout (Unity totalCREarned).</summary>
    public double CreditEarned { get; private set; }

    /// <summary>The Gentlemen Drivers Club reputation the Cento Miglia page
    /// gates its tiers on (Unity PlayerData.clubReputation). A finished race
    /// writes the reading its ticker reached (Unity's
    /// StopClubReputationTicker), so the next visit gates on the new level.</summary>
    public double ClubReputation { get; private set; }

    /// <summary>Star of the Show wins so far (Unity starOfTheShowWins): each
    /// new win pays more than the last, so the total is career state.</summary>
    public int StarOfTheShowWins { get; private set; }

    /// <summary>The persisted driver-rating skills (Unity PlayerData's three
    /// driver* fields). Skill events move them through the Add* helpers below;
    /// the wallet screen shows them and the haul/insurance math reads the
    /// overall.</summary>
    public DriverRating Rating { get; private set; }

    /// <summary>Unity DriverRatingService.ApplyCorneringDelta: a skill event
    /// moves one stat (clamped) and the next SaveDriver banks it.</summary>
    public void AddCornering(double delta) => Rating = Rating.AddCornering(delta);

    /// <summary>Unity DriverRatingService.ApplyConcentrationDelta.</summary>
    public void AddConcentration(double delta) => Rating = Rating.AddConcentration(delta);

    /// <summary>Unity DriverRatingService.ApplyOffTheLineDelta.</summary>
    public void AddOffTheLine(double delta) => Rating = Rating.AddOffTheLine(delta);

    /// <summary>
    /// Unity DriverRatingService.ApplyAccidentPenalty: one crash drops all
    /// three skills by the band of the current overall rating. The DR
    /// Protection card is consumed by the caller before this runs.
    /// </summary>
    public void ApplyAccidentPenalty() => Rating = Rating.ApplyAccidentPenalty();

    /// <summary>Unity ApplyRandomSkillDelta: the streak tail's one random
    /// skill point, rolled on the caller's seeded RNG.</summary>
    public void AddRandomSkill(Random rng, double delta) =>
        Rating = Rating.AddRandomSkill(rng, delta);

    /// <summary>The kilometres every finished job has banked (PORTPLAN 44).</summary>
    public double JobDistanceKm => _driving.JobDistanceKm;

    /// <summary>The clean run since the last billed crash.</summary>
    public double AccidentFreeKm => _driving.AccidentFreeKm;

    /// <summary>The longest clean run ever banked.</summary>
    public double LongestAccidentFreeKm => _driving.LongestAccidentFreeKm;

    /// <summary>Billed crashes over the career.</summary>
    public int Accidents => _driving.Accidents;

    /// <summary>The attentive-driver reading (PORTPLAN 44): career km per crash,
    /// capped at 100, and 100 while no crash has been billed.</summary>
    public double AttentiveRating => _driving.AttentiveRating;

    /// <summary>
    /// Banks one finished job's odometer delta and pays one random skill point
    /// per five clean kilometres (PORTPLAN 44), rolled on the caller's seeded
    /// RNG. Answers how many points were paid, so the caller can tell a silent
    /// bank from one that moved the rating.
    /// </summary>
    public int BankJobDistance(double km, Random rng)
    {
        int awards = _driving.Bank(km);
        for (int i = 0; i < awards; i++)
        {
            Rating = Rating.AddRandomSkill(rng, 1.0);
        }

        return awards;
    }

    /// <summary>A billed crash ends the clean run but keeps the record.</summary>
    public void RegisterAccident() => _driving.RegisterAccident();

    /// <summary>The savings-bank balance (Unity PlayerData.bankAmount).
    /// Interest accrues here and compounds; job payouts land in Cash.</summary>
    public double BankBalance { get; private set; }

    /// <summary>Insurance cash an insured repair paid out, waiting to be
    /// collected at the broker (Unity PlayerData.pendingInsuranceCashIn). The
    /// 90% of the repair cost lands here; the broker moves it to Cash.</summary>
    public double PendingInsurance { get; private set; }

    /// <summary>The last game date the nightly premium was charged
    /// (yyyy-MM-dd), so the charge fires once per date (Unity
    /// lastInsurancePremiumDay). Null until the first charge.</summary>
    public string? LastInsuranceChargeDate { get; private set; }

    /// <summary>Unity PlayerData.hasEnteredBank, stamped by the screen's
    /// Show; completes the VisitBank todo once progress persistence (item
    /// 51) lands.</summary>
    public bool HasEnteredBank { get; private set; }

    /// <summary>
    /// How many reward cards the glove compartment may hold (PORTPLAN 61,
    /// Unity PlayerData.gloveboxSlots). It lives on the driver's readings, so
    /// the one save that banks the trunk banks the capacity that measures it;
    /// the Glovebox owns what the number means.
    /// </summary>
    public int GloveboxSlots { get; private set; }

    /// <summary>
    /// Heals a save written before the glove compartment existed: 0 slots is
    /// never a legal capacity, so the first owner to read it seeds the
    /// table's default (Unity's GetCapacity does the same at its read site).
    /// A positive count is never touched, so a slot reward always survives.
    /// </summary>
    public void EnsureGloveboxSlots(int fallback)
    {
        if (GloveboxSlots <= 0)
        {
            GloveboxSlots = Math.Max(0, fallback);
        }
    }

    /// <summary>The "+1 Glovebox Slot" reward card (PORTPLAN 61, Unity
    /// TodoRewardsScreenController's slot branch).</summary>
    public void AddGloveboxSlot() => GloveboxSlots++;

    /// <summary>Unity BankScript.Show: the visit stamps once and rides the
    /// next settle save out (Unity saves with the withdraw, not on open).</summary>
    public void MarkBankEntered() => HasEnteredBank = true;

    /// <summary>
    /// Unity BankScript.OnWithdrawClicked: money above the 1000 saldo floor
    /// moves bank → cash. The no-op answers 0 both on a floored account (the
    /// button click legally does nothing there) and on a non-positive
    /// amount; an oversized amount clamps instead of failing. Returns the
    /// CR that left the bank — 0 means nothing moved.
    /// </summary>
    public double Withdraw(double amount)
    {
        double max = BankRules.MaxWithdraw(BankBalance);
        if (max <= 0.0 || amount <= 0.0) return 0.0;
        double moved = Math.Min(max, amount);
        BankBalance = Math.Round(BankBalance - moved, 2, MidpointRounding.AwayFromZero);
        Cash = Math.Round(Cash + moved, 2, MidpointRounding.AwayFromZero);
        return moved;
    }

    /// <summary>Unity ApplyForCompletion's tail: interest compounds here,
    /// before the settle's save. Rounded at this boundary like every other
    /// money move — Unity keeps a float ledger, the CR ledger here is
    /// 2-decimal, so a real balance always resumes the same.</summary>
    public void ApplyInterest(double interest) =>
        BankBalance = Math.Round(BankBalance + interest, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// An unavoidable charge the player cannot refuse and Unity floors (the
    /// nightly insurance premium, the breakdown tow fee): it takes what the cash
    /// can cover and no more. Returns the CR actually charged; the caller logs a
    /// short wallet. Rounded like every other money move.
    /// </summary>
    public double ChargeUnavoidable(double amount)
    {
        if (amount <= 0.0)
        {
            return 0.0;
        }
        // WHY the outer floor: a balance already in debt covers nothing, so the
        // charge must not "pay itself" out of the negative and erase the debt.
        double moved = Math.Round(Math.Max(0.0, Math.Min(amount, Cash)), 2, MidpointRounding.AwayFromZero);
        ApplyCash(-moved);
        return moved;
    }

    /// <summary>
    /// A charge the player cannot refuse even when the cash is short, the way
    /// Unity's RepairScreen settles an accident: <c>cash -= cost</c> with no
    /// floor, so the balance may go into debt. The repair is the only caller —
    /// a voluntary purchase still goes through <see cref="TrySpend"/>.
    /// </summary>
    public void ChargeInFull(double amount)
    {
        if (amount > 0.0 && double.IsFinite(amount))
        {
            ApplyCash(-amount);
        }
    }

    /// <summary>Unity RepairScreen's insurance payout: 90% of the repair cost
    /// lands in the escrow for the broker to collect (never straight to cash).</summary>
    public void AddInsurancePayout(double amount)
    {
        if (amount > 0.0)
        {
            PendingInsurance = Math.Round(PendingInsurance + amount, 2, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Unity InsuranceScreen.CollectPayout: the escrow moves to cash
    /// and clears. Returns the CR moved — 0 means the escrow was empty.</summary>
    public double CollectInsurancePayout()
    {
        double moved = PendingInsurance;
        if (moved <= 0.0)
        {
            return 0.0;
        }
        PendingInsurance = 0.0;
        Cash = Math.Round(Cash + moved, 2, MidpointRounding.AwayFromZero);
        Earn(moved);
        return moved;
    }

    /// <summary>Stamps the game date the premium was charged for, so the
    /// nightly charge fires once per date (Unity lastInsurancePremiumDay).</summary>
    public void MarkInsuranceCharged(string date) => LastInsuranceChargeDate = date;

    /// <summary>
    /// The buy gate (Unity CarDealerScreen.OnBuyClicked's
    /// <c>cash &lt; cost</c> return): a purchase, a repair, a wash and a
    /// premium all move money out of cash the same way. False and no move when
    /// the cash is short — the caller then keeps the action a no-op instead of
    /// going negative; an oversized amount never overshoots (the balance is
    /// the cap).
    /// </summary>
    public bool TrySpend(double amount)
    {
        if (amount <= 0.0 || Cash < amount)
        {
            return false;
        }
        Cash = Math.Round(Cash - amount, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>The consecutive-days login bonus (PORTPLAN 47, Unity
    /// GrantDailyBonusIfDue): cash only, never XP — Unity matches. It feeds
    /// the lifetime-earned reading the ToDo subject shows.</summary>
    public void AddBonus(double bonus)
    {
        ApplyCash(bonus);
        Earn(bonus);
    }

    /// <summary>
    /// A cash gift banked for the player (PORTPLAN 61, Unity's "+500CR Bonus"
    /// reward card: <c>bankAmount += 500</c> — the savings saldo, not the cash
    /// on hand, so the bonus can only reach the player through a withdrawal
    /// above the saldo floor). It deliberately does not touch the
    /// lifetime-earned reading, so a gift never credits the "earn CR" ToDo
    /// subject the way a settled job does.
    /// </summary>
    public void AddBankGift(double amount)
    {
        if (amount > 0.0 && double.IsFinite(amount))
        {
            BankBalance = Math.Round(BankBalance + amount, 2, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// A dealer trade-in: the money returns capital already spent on the car, so
    /// it moves cash but deliberately not the lifetime-earned reading (a resale
    /// is not job income; the "earn CR" ToDo must not be farmed by flipping
    /// cars). Non-positive and non-finite amounts are ignored.
    /// </summary>
    public void AddSale(double amount)
    {
        if (amount > 0.0 && double.IsFinite(amount))
        {
            ApplyCash(amount);
        }
    }

    public void AddReceipt(TaxiReceipt receipt)
    {
        ApplyCash(receipt.Total);
        Xp += receipt.XpEarned;
        Earn(receipt.Total);
    }

    /// <summary>One settled delivery run: the wholesale cost left with the
    /// goods and the retail payout plus the fare came back — the receipt's
    /// TotalEarned IS the wallet delta, so no separate lines to keep apart.</summary>
    public void AddDelivery(DeliveryReceipt receipt)
    {
        ApplyCash(receipt.TotalEarned);
        Xp += receipt.XpEarned;
        Earn(receipt.TotalEarned);
    }

    /// <summary>One settled car-haul: the car reached the customer and the
    /// cash came back. Unity's haul settle banks cash and nothing else (the
    /// driver rating scales the pay in; no XP for handing over a car), so
    /// the XP total stays untouched here too.</summary>
    public void AddHaul(CarHaulReceipt receipt)
    {
        ApplyCash(receipt.TotalEarned);
        Earn(receipt.TotalEarned);
    }

    /// <summary>
    /// One settled road test (PORTPLAN item 49, Unity
    /// RoadTestReceiptScreen's <c>player.cash += totalEarnings</c>): the issue's
    /// earnings land in cash and in the lifetime-earned reading. Unity pays no
    /// XP for a road test, so the XP total stays untouched here too.
    /// </summary>
    public void AddRoadTest(RoadTestReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ApplyCash(receipt.Payment);
        Earn(receipt.Payment);
    }

    /// <summary>
    /// One finished Cento Miglia (PORTPLAN 49, Unity CentoMigliaScreen's
    /// ProcessReceiptRewards): the purse — placement earnings plus any Star of
    /// the Show prize — lands in cash, the reputation the race's ticker reached
    /// replaces the club level (Unity writes tempClubReputation back on
    /// arrival), and the win total the prize scales on persists with it.
    /// </summary>
    public void AddCentoRace(CentoReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ApplyCash(receipt.TotalEarned);
        Earn(receipt.TotalEarned);
        if (double.IsFinite(receipt.EndReputation) && receipt.EndReputation > 0.0)
        {
            ClubReputation = receipt.EndReputation;
        }
        StarOfTheShowWins = Math.Max(StarOfTheShowWins, receipt.StarWins);
    }

    public DriverState Snapshot() => new(Cash, Xp)
    {
        Bank = BankBalance,
        HasEnteredBank = HasEnteredBank,
        PendingInsurance = PendingInsurance,
        LastInsuranceChargeDate = LastInsuranceChargeDate,
        Cornering = Rating.Cornering,
        Concentration = Rating.Concentration,
        OffTheLine = Rating.OffTheLine,
        JobDistanceKm = JobDistanceKm,
        AccidentFreeKm = AccidentFreeKm,
        LongestAccidentFreeKm = LongestAccidentFreeKm,
        Accidents = Accidents,
        CreditEarned = CreditEarned,
        ClubReputation = ClubReputation,
        StarOfTheShowWins = StarOfTheShowWins,
        GloveboxSlots = GloveboxSlots,
    };

    // Every cash delta lands here: rounded to the cent, sign preserved. A debt
    // is a real state (Unity's cash can sit below zero after an accident
    // repair); only a voluntary purchase is gated, by TrySpend.
    private void ApplyCash(double delta)
    {
        if (!double.IsFinite(delta))
        {
            return;
        }
        Cash = Math.Round(Cash + delta, 2, MidpointRounding.AwayFromZero);
    }

    private void Earn(double amount)
    {
        if (amount > 0.0)
        {
            CreditEarned = Math.Round(CreditEarned + amount, 2, MidpointRounding.AwayFromZero);
        }
    }
}
