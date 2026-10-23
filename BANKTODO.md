# BANKTODO — daily streak + operating zone + bank (items 47 / 58 / 48)

Working order: **streak first, then zone, then bank.** The bank's interest
rate is `discoveredStreetCount × daysPlayedInARow` (Unity
`BankInterestHelper`), so 47 and 58 must feed it before 48 lands.

Confirmed decisions (2026-10-17):
- Bank screen entry = temporary BANK button on the main control page
  (answers to voice "bank"); relocated when markers (59) land.
- Bank-owner friendship bump on first withdrawal = DEFERRED until the
  POI network (24) gives the bank a real owner.
- Job payouts stay in `cash`; interest accrues only on the bank balance.

Unity source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2\Assets\Scripts`.

---

## Batch 1 — Daily streak (PORTPLAN item 47)

Unity facts (`Utils/DailyStreakService.cs`, `Utils/ConsecutiveDayBonus.cs`,
`UI/MapScreen.cs:1802 GrantDailyBonusIfDue, 3464`):

- Streak = **real-world consecutive LOGIN days**, not game days.
  `daysPlayedInARow`, `lastLoginDate`, `lastDailyBonusDate` on PlayerData.
- Counter is always recomputed from dates against today's date string
  (`yyyy-MM-dd`), never trusted from storage; idempotent per day:
  - no lastLoginDate → 1
  - lastLogin == today → keep stored (min 1)
  - lastLogin == yesterday → stored + 1
  - else → 1
- Bonus curve: day 1 = 1 CR; each later day multiplies the running total
  by `2.00 − 0.05·(n−1)`, floored at 1.0 (plateaus from day 20). Integer,
  away-from-zero.
- Grant: once per real day (after onboarding complete — Unity's
  `firstSceneComplete`), on screen open: `cash += bonus`,
  `totalCREarned += bonus`, stamp `lastDailyBonusDate`, text =
  "You have played {n} consecutive days and earned a bonus of {b} CR.
  Tomorrow's bonus will be {t} CR."

Port to ACEDRIVE:

- [x] `DriverState` gains `DaysInARow` (int), `LastLoginDate` +
      `LastDailyBonusDate` (string?, ISO date) — nullable-safe defaults so
      old driver.json files load unchanged.
- [x] `ConsecutiveDayBonus.cs` (Core, pure) — exact curve port, tested
      against Unity values incl. the day-20 plateau.
- [x] `DailyStreak.cs` (Core) — owns the streak state + `IClock`; date
      strings from `clock.UtcNow` (AGENTS.md time rule; Unity uses local
      `DateTime.Now` — divergence documented in the WHY-comment). Methods:
      `Update()` (idempotent recount), `BonusDue` check, snapshot into the
      new DriverState fields.
- [x] `DriverWallet.AddBonus(double)` — cash only (no XP; Unity matches).
- [x] Grant site: once per session/day after onboarding is complete —
      MainViewModel ctor grants through the shared `SaveDriver` helper;
      stat log line only, resx string still due when the bonus becomes
      visible UI (no popup screen ported yet).
- [x] Driver-rating tail (day N grants N random skill +1s) — landed with
      item 06: the boot grant rolls N random +1s on the seeded RNG before
      the same save. Tests: grant-once sum + the saved skills.
- [x] Tests: recount idempotency, broken streak, bonus curve, grant-once,
      wallet cash delta, grant gate (pre-onboarding), settle-save carries
      the streak slice (660 tests green, format clean).

## Batch 2 — Operating zone skeleton (PORTPLAN item 58, rework)

Unity facts (`UI/MapScreen.cs:2196/2213/2221 ExpandOperatingZone`,
`Handlers/*.cs:1093/1492`, `Models/StreetZone`):

- Every settled **taxi or delivery** job: `expansionJobsCompleted++`
  (car-haul does NOT count).
- After onboarding, when the counter reaches the needed jobs the zone
  expands: threshold curve = first expansion **2** jobs, second **3**,
  then **random 4–6** per expansion (Unity `UnityEngine.Random`; port uses
  the Core seeded RNG). Expand: add one street, reset the job counter,
  `expansionCount++`, next threshold recomputed.
- Zone radius (km) sums discovered street lengths; the bank interest rate
  uses the discovered **count**, not km.
- Unity's discovery pipeline (OSM fetch/houses/markers) is DECLINED —
  candidate streets for the port come from the Step-1 Eifel table.

Port to ACEDRIVE:

- [x] `OperatingZone.cs` (Core) — job counter, expansionCount, discovered
      street-id list, zone km; `RegisterJob()`, threshold curve (seeded RNG
      injected), `Expand(streetId, lengthKm)` — TryExpand guards due +
      duplicates, Unity GetNextExpansionThreshold curve exact.
- [x] `DriverState` gains the zone fields (ids + counters + km) with
      versioned defaults so old saves load unchanged; an old save with no
      street list re-seeds the home street (Unity: registered at onboarding).
- [x] Settle hooks in `MainViewModel` at exactly the two Unity sites
      (taxi + delivery), never in the haul settle; expansion gated on
      onboarding-complete (Unity firstSceneComplete).
- [x] INTERIM (until Step 1 street table lands): expansions register
      abstract street ids (`s1`, `s2`, …); zone km stays 0.0 and no POIs
      spawn — documented divergence. The COUNT the bank interest sees is
      Unity-exact from day one: `OperatingZone.HomeStreetId` seeds the
      fresh driver, Unity-exact (home street registered at onboarding).
      Divergence note: Unity expands at receipt-CONTINUE with preloaded
      real streets; the interim registers the placeholder at the settle.
- [x] Tests: thresholds (2/3/4–6 curve), counter reset, count+1 per
      expand, no-count for haul, resume/roundtrip, VM settle flow (672
      green, format clean).

## Batch 3 — Bank (PORTPLAN item 48)

Unity facts (`BankScript.cs`, `BankInterestHelper.cs`, `BankGenerator.cs`,
`MapScreen.cs:3470`, `PlayerDefaults:197`):

- Balance on the player, seeded **1000 CR**; existing saves with 0 → 1000.
- Interest **into the bank**, credited at taxi+delivery settle only
  (after payday, before todo sync): `rate = streetCount × daysInARow`,
  `interest = bank × rate / 10000`. Compounds; car-haul never accrues.
- Bank generator: first bank at 15 streets, then one per 51 more —
  wired when POIs (24) land; the gate constants move into Core rules now.
- Screen: withdraw-only; min saldo **1000 CR** (amount clamped to
  `bank − 1000`, withdraw blocked at ≤ saldo); live "balance after
  withdrawal" preview; withdrawing moves bank→cash; opening the screen
  sets `hasEnteredBank` (VisitBank todo); +1 friendship once per visit
  (DEFERRED — see decisions).

Port to ACEDRIVE:

- [x] `DriverState` gains `Bank` (double, **default 1000** — the record
      default IS the Unity seed, so a pre-bank save migrates implicitly;
      a legal balance can never reach 0) + `HasEnteredBank` (bool);
      `JsonDriverStore` validates Bank ≥ 0.
- [x] `DriverWallet` gains `BankBalance`, `Withdraw(amount)` (min-saldo
      clamp, oversized clamps, floored account no-ops — Unity
      OnWithdrawClicked exact), `ApplyInterest(amount)`,
      `MarkBankEntered()`; the snapshot carries the bank slice.
- [x] `BankRules.cs` (Core) — `Rate(streets, daysInARow)`,
      `ComputeInterest(bank, …) = bank × rate / 10000`,
      `MaxWithdraw(bank) = bank − 1000`, `MinBankSaldo = 1000`. The 15/51
      placement gate moved to `Data/pois.json` and `PoiSpawner` (item 24).
- [x] Interest applied at the two settle points with the REAL factors:
      `zone.StreetCount × streak.DaysInARow` — no placeholders left. It
      lands before the expansion street joins (Unity orders it the same),
      after the payday so it compounds, and the haul settle calls nothing.
- [x] `BankScreenViewModel` + `BankScreenView` (saldo, input with live
      clamped "after withdrawal" preview, withdraw, back) + the entry as
      `MainViewModel.BankCommand` handed into the control page — planned
      as an `IScreenController.OpenBank()` verb, but the page's existing
      command-passing pattern (taxi/delivery/haul) fits without growing
      the controller interface. Temp BANK button + voice "bank" (resx,
      en + nl).
- [x] A withdraw persists through the shared `SaveDriver` (Unity
      SaveManager.Save parity); the screen never touches files itself.
- [x] `HasEnteredBank` set on screen open, saved with the next settle;
      completes the VisitBank todo once progress persistence (51) lands.
- [x] No deposit flow (Unity parity), no HUD saldo, no tutorial popup, no
      bank-owner friendship, no `totalCREarned` counter — out of scope
      today; the POI spawn wiring waits for items 24/59.
- [x] Tests: seed/migration, interest math + no accrual on haul, withdraw
      clamps + no-ops, preview clamp, withdraw persist, screen open flag,
      the 15/51 gate.

---

## Definition of done (per batch)

- `dotnet build ACEDRIVE.slnx` + `dotnet test ACEDRIVE.slnx` — green, zero
  warnings (TreatWarningsAsErrors).
- `dotnet format ACEDRIVE.slnx --verify-no-changes` clean on touched files;
  created files converted to CRLF before building.
- Behaviour verified by tests (fakes, deterministic clock); no live-EVO
  requirement until the wiring run.
- SOLID/clean rules hold: one type per file, Core stays pure, all balance
  numbers in `Data/*.json` or rules constants, all copy in resx, state
  persisted only through `IDriverStore`.
