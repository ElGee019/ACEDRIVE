# GARAGETODO — vehicle track (PORTPLAN items 28/29/30, then 31/32/33/34/39)

Working order: **garage + dealer first, then the condition cluster.** The
acquisition slice turns the placeholder settles into a real maintenance loop:
money buys cars, the cars have odometers, and the condition slice prices what
driving does to them.

Confirmed decisions (2026-10-18):
- **No year system in ACEDRIVE, ever.** The week is the only pacing axis, so
  Unity's "brand new this year vs older" dealer band and the year half of its
  draw seed are dropped. `DealerRules.SeedFor(week) = week * 131`.
- **Dealer stock = one independent week-seeded draw** (Unity's partial
  Fisher-Yates over the whole roster, owned rows excluded before picking,
  ordinal sort, ≤14 shown, nothing persisted). A skipped car can reappear.
- **`Data/cars.json` carries the full v2/v3 spec row** for all 101 rows, with
  the game's own `cars.table` model years adopted (still nothing keys on a
  year). Provenance + generator: `CARSTATS_EVO.md` v6 note.
- **Odometer comes from the ACEVO telemetry** (`speed × dt` integrated in the
  existing 10 Hz `Poll`), not from placeholder km per settled leg. The 8.2 km
  placeholder stays for the receipt's distance arithmetic only.
- **`RoadTestCar` renamed to `CarSpec`**: the record is the car database now,
  not a road-test row.
- Garage page and dealer floor are **temporary control-page entries**
  (GARAGE, CAR DEALER + voice keywords) until the marker network (items 24/59)
  gives them a place in the world — the BANK button precedent.
- Deferred, with the reason: no selling/trade-in/depreciation (do not exist in
  the mother game), dealer familiarity bump (needs the POI owner — the bank's
  decision), garage job actions (car sharing/employee/zone expansion = items
  22/23/58), cargo cover (item 61 reward catalog), the concentration half of
  the dirty-taxi refusal (item 06).

Unity source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2\Assets\Scripts`.

---

## Batch 1 — car database + dealer buy flow + multi-car garage (28/29/30) — DONE

Unity facts (`Models/CarStats.cs`, `Models/PlayerData.cs` OwnedCar,
`Utils/CarDealerSelectionService.cs`, `UI/CarDealerScreen.cs`,
`UI/BoughtFirstCar.cs`, `UI/GarageScreen.cs`, `Utils/CarDealerGenerator.cs`):

- Stock: one draw per (year, week), `MaxShownCars 14` (≤8 of the current year +
  older), partial Fisher-Yates, pooled ids sorted ordinal, owned excluded
  *before* picking (a run-down roster shows fewer cars, never a refill).
- Purchase gate: `cash >= price` and not already owned. No slot cap, no class
  lock, no insurance requirement, no trade-in.
- New car defaults: `odometer 0`, `fuel = tank × 0.5`, `hasInsurance false`,
  `breakdownChance 0`, maintenance parts initialized at the current odometer.
- After buying: `BoughtFirstCar` = DRIVE NEW / KEEP CURRENT, with a summary
  block only when the garage now holds two cars. The current car
  (`PlayerData.grandmaCar`) is what the game spawns and what every per-car
  system reads.
- Garage: unbounded `ownedCars`, sorted by id for display, `CURRENT CAR` row
  non-interactable, value shown as `price × (1 − engineBrokenPercent)`.
- Dealer POI: first at `jobsCompleted >= 10`, then one per 37 discovered
  streets. The garage is a screen off the You page, not a POI.

Port to ACEDRIVE:

- [x] `CarSpec` (was `RoadTestCar`) carries the full spec row: id, preset, year,
      brand, model, passengers, class, price, trunkLitres, horsepower, engineCc,
      massKg, topSpeedKmh, acceleration, gears, tankLitres, fuelPer100Km,
      brakeDistance. `Key` = `id` or `id#preset`.
- [x] `Data/cars.json`: 101 rows regenerated from the CARSTATS v2/v3 tables +
      `prices_all_presets.csv` + `cars_dump.txt` (official years). Shipped-data
      test pins every row's price/specs.
- [x] `JsonCarStore` validates the spec columns (negative value = unusable row,
      dropped with a warning; `price <= 0` = legal but not for sale).
- [x] `DealerRules` (Core): `MaxShownCars 14`, `WeekSeedStride 131`,
      `SeedFor(week)`, and the parked POI gate (`FirstDealerJobs 10`,
      `DealerIntervalStreets 37`, `DealersForStreets`).
- [x] `DealerStock.Build(pool, ownedKeys, week)` (Core, pure): the week's floor.
- [x] `OwnedCar` (Core): key, purchaseWeek, odometer, insurance flag;
      `Drive(km)` rejects negative/NaN distance.
- [x] `Garage` (Core): no slot cap, ordinal display order, current car,
      `Owns`/`Find`/`SpecOf`, `SetCurrent` (false for an unknown key), snapshot
      and a resume that drops broken rows.
- [x] `GarageState`/`OwnedCarState` + `IGarageStore` + `JsonGarageStore`
      (`%AppData%\AceDrive\garage.json`), never throws, unusable rows drop with
      a warning, a current key no car backs clears.
- [x] `DriverWallet.TrySpend(amount)`: the buy gate, false and no move when the
      cash is short (reused later by the mechanic/wash/insurance).
- [x] Onboarding: grandma's car pick creates the first `OwnedCar` and makes it
      current; a save with a finished onboarding and an empty garage seeds the
      car it picked (every pre-garage save).
- [x] Screens: `GarageScreenViewModel`/`View` (rows with specs + value, CURRENT
      marker, tap to switch, BACK), `DealerScreenViewModel`/`View` (week floor,
      picked car's specs, live saldo, BUY IT / CAN'T AFFORD, but never a spend
      without the cash), `BoughtFirstCarScreenViewModel`/`View` (DRIVE IT /
      KEEP CURRENT, summary on the second car).
- [x] Wiring: GARAGE + CAR DEALER control-page entries with voice keywords,
      resx en+nl, DataTemplates, `Garage`/`JsonGarageStore` built in
      `App.OnStartup` only, every mutation banked by the host (`SaveGarage`),
      also at window close (the odometer's last bank).
- [x] Tests: DealerStock, Garage, DriverWallet.TrySpend, JsonGarageStore,
      GarageScreen, DealerScreen, BoughtFirstCarScreen, GarageFlow (boot
      seeding, choose-car, garage switch, buy → drive new / keep current, no
      cash → no purchase), shipped-data guard. 782 green, format clean.

## Batch 2 — condition cluster (31/32/33/34/39) — DONE

Unity facts already collected (`Utils/CarMaintenanceService.cs`,
`EngineHealthService.cs`, `CarWashService.cs`, `InsurancePricing.cs`,
`Driving/Services/StandardBreakdownStrategy.cs`, `UI/CarMechanicScreen.cs`,
`UI/RepairScreen.cs`, `UI/InsuranceScreen.cs`, `Managers/GameClockManager.cs`,
`Utils/BreakdownFlow.cs`):

Per-part wear (odometer-driven only — no hard-driving model):
`oil_change 1200 km, wipers 1000, air_filter 1500, brake_pads 2700, tires 2500,
brake_fluid 3000, transmission_fluid 3000, car_battery 3600`;
`partFraction = clamp01((odo − lastService) / interval)`;
`avgWear = Σ partFraction / 8`; `breakdownChance = clamp01(0.10 × avgWear²)`.
Parts are replaced at a fixed price × class multiplier
(`≥16 → 1.0, ≥11 → 0.8, ≥7 → 0.6, else 0.4`):
`oil 250, wipers 90, air 110, pads 350, tires 900, fluid 180, trans 350, battery 400`.

Engine: `broken% = clamp01(t²)`, `t = (odo − engineBaseOdometer) / redKm` with
redKm 250k/300k/350k/400k/450k/500k by cc band; weekly check every 7 game days,
annual every 364-371; failed annual roll = `engineBroken`; repair =
`value × 0.10 × classMult` (odometer kept), new engine = `value × 0.30`
(odometer 0 + every part baseline 0).

Dirt: an int 0-25 per car, `+1` per completed taxi/delivery leg (never time/km),
flat **CR50** wash clears it, filth 25 refuses a taxi pickup (−1 friendship,
−1 concentration — the concentration half is parked for item 06).

Insurance: per car, `max(1, price × 0.0005 × 1.15 × (1 − driverRating/100))`
charged nightly at 01:00 game time for every insured car (needs a saved
`LastInsuranceDay` stamp); a repair pays out `× 0.9` into an escrow the player
collects. Cargo cover waits for the reward catalog (61).

Breakdown: one roll at trip start (`rand < chance`), then a fixed fraction
0.10-0.90 of the route; pickup fee **CR100**; consequences per job kind (taxi job
cut, delivery cargo stripped + shield refund, freerun nothing); engine-failure
variant at the mechanic. Screen: `CarMechanicScreen` (parts with efficiency %,
replace, engine repair/new).

Port to ACEDRIVE:

- [x] `MaintenanceRules`/`InsuranceRules` (Core records + `Data/maintenance_rules.json`
      and `Data/insurance_rules.json`, `HungerRules` precedent) holding the
      internals/prices/multipliers/engine bands/breakdown envelope. `MaintenanceRules.Default`
      is Unity's shipped table; the JSON is the source of truth.
- [x] `CarCondition` (Core): the per-part deck on `OwnedCar`, `PartFraction`,
      `AverageWear`, `BreakdownChance`, `ServicePart`, `ResetAll`, snapshot/resume
      with EnsureInitialized backfill; `EngineHealth` (Core): the quadratic broken
      curve, cc redKm bands, the DateTime schedule (weekly 7 / annual 364-371,
      seeded RNG), `RunAnnual`/`Repair`/`ReplaceEngine`.
- [x] Odometer feed: `Poll` integrates telemetry speed × dt into the current car
      (the fare meter's dt guard + a 500 km/h clamp), runs the engine inspections
      while driving past onboarding, and banks garage.json on a 60 s throttle
      beside the clock save.
- [x] `CarFilth` (Core) + the `+1` hooks at the two settle sites + the wash
      screen; the taxi refusal sits on ACCEPT until the driving slice gives
      the pickup its own moment (concentration −1 and the wash owner's
      friendship bump stay parked).
- [x] `InsuranceRules` + `InsurancePricing` (Core) + the per-car flag + the
      nightly charge (once per game date in the 01:00 hour, `ChargeUnavoidable`
      so a premium is a commitment and the ledger never goes negative) + the
      broker page and the escrow collect (the 0.9× payout is wired to the
      mechanic repair — DIVERGENCE: Unity fed it from the accident RepairScreen,
      which item 40 declines; the ACE accident signal could feed it later).
- [x] `BreakdownStrategy`/`BreakdownKind`/`BreakdownOutcome` (Core) + the roll at
      the trip start (taxi ACCEPT, delivery DELIVER — the placeholder's trip
      starts, since there is no driving distance yet) + the per-kind consequences
      (taxi job cut + customer −1; delivery cargo lost + producing-hub owner −1;
      freeroam nothing) + the CR100 tow fee + `BreakdownScreenViewModel`/`View`.
      A stranded engine (`engineBroken`) always fires the engine-failure variant.
- [x] `MaintenanceRules`/`InsuranceRules` + `JsonMaintenanceRules`/`JsonInsuranceRules`
      loaders (never throw, fall back to Default with a warning) + the shipped-data
      guards.
- [x] Temp control-page entries for mechanic / car wash / insurance broker
      (MECHANIC + INSURANCE buttons, voice keywords), moving behind the marker
      network (items 24/59); the mechanic/broker owner familiarity bumps stay
      parked with the wash's (no POI owner).
- [x] Tests: Unity-value parity for every formula (parts/prices/curve/redKm/
      insurance/tow fee), schedule/due gates, the annual roll stranding, the
      nightly charge once per game date, the odometer feed + throttle, the
      trip-start roll and its per-kind consequences, the screens. 962 green,
      format clean.
- Divergence notes: the engine schedule keys on `GameClock.GameNow` (a real
  DateTime) instead of Unity's int day/minute counters; the tow fee is charged
  at the outcome (no map to teleport the car to a mechanic); the breakdown
  roll sits at the placeholder trip starts until the driving slice moves it to
  the real pickup/drop-off.
- **Temporary testing hook (remove when the driving slice lands):** a settled
  taxi/delivery leg banks its placeholder km into the current car's odometer
  (`MainViewModel.BankJobDistance`), so the mechanic can be exercised without
  driving the route. Live telemetry already banks the odometer per tick, so
  once the real pickup/drop-off feeds distances this would double-count.

## Definition of done (per batch)

- `dotnet build ACEDRIVE.slnx` + `dotnet test ACEDRIVE.slnx` — green, zero
  warnings (TreatWarningsAsErrors).
- `dotnet format ACEDRIVE.slnx --verify-no-changes` clean on touched files;
  created files converted to CRLF before building.
- SOLID/clean rules hold: one type per file, Core stays pure (no WPF/IO),
  all balance numbers in `Data/*.json` or rules constants, all copy in resx
  (en + nl), services constructed only in `App.OnStartup`.
