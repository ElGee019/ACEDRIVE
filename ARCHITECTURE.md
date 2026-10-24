# Overlay architecture — the session split

How the overlay's session layer is meant to be shaped, and the order to get
there. `AGENTS.md` states the rules; this file states the target those rules
point at, so each slice can be judged against something instead of against
"smaller than before".

Scope: `src/AceDrive.Overlay/ViewModels` and the composition root
(`App.OnStartup`). `AceDrive.Core` is untouched by this plan.

## 1. The problem, in one paragraph

`MainViewModel` is not a shell with collaborators: it is the application. It
owns the job state, the delivery run, the POI world, the page cache, the day
flip, the shift timeout and the readouts, and it builds the three collaborators
that need those (`OnboardingFlow`, `VoiceRouter`, `PoiArrivalRouter`) from
`this`. That is why the composition root is stuck: every collaborator that
needs policy needs the coordinator, and the coordinator needs the collaborator.

The fix is not to move more methods out. It is to give each domain its own
collaborator, with its own state and its own commands, and let the coordinator
route intent into them. Only then can `App.OnStartup` build everything.

## 2. Where the split stands

Extracted, each verified (build 0 warnings, 1723 tests, format clean); S1–S4 are
pushed, S5–S10 are green locally:

| Slice | Result |
|---|---|
| `798b00e` | One JSON boundary: `JsonFile`, `JsonTable`, `DataPaths`; 34 stores migrated, −680 lines |
| `2edc6df` | `FleetService` reads the watcher and content, not the shell; `IFleetHost` deleted |
| `bbbad66` | `GameStateSaver` + `FleetService` built in `App.OnStartup` |
| `e332839` | Receipts → `JobSettlement` + `IJobSettlementHost` (12 members), −284 lines |
| `231be1f` | Panel/page/activity → `OverlayShell` |
| `c5bd948` | Five shop interiors → `ShopInteriors`, −83 lines |
| `4d4c0e2` | Taxi job → `TaxiJobFlow` + `ITaxiJobHost`, −397 lines and six constructor parameters |
| S2 | Delivery run → `DeliveryFlow` + `IDeliveryHost`; `TripBreakdowns` and the `DeliveryBoard` holder, −145 lines |
| S3 | POI growth/doors/handlers → `PoiWorld` + `IPoiWorldHost`, −146 lines |
| S4 | Pages → `Navigation` + `INavigationHost`; the control/menu/calendar caches and the bought-car choice, −291 lines |
| S5 | Readouts → `TelemetryReadouts` + `ReadoutPaint`; day flip, week and shift timeout → `DayCycle` + `IDayCycleHost`; the week's mirror in `Navigation` and the host getter in `PoiWorld` go with it, −155 lines |
| S6 | The composition root builds the whole DAG: `PanelPresenter` + `StoryHost`, `OnboardingFlow` and `VoiceRouter` (which loses `IVoiceHost`) are root-built; `MainViewModel` takes the finished graph, −184 lines |
| S7 | The game-clock world → `WorldTicks` (town drift, appetite, nightly premium); the hunger-block latch leaves the coordinator, −52 lines and four constructor parameters |
| S8 | The boot catch-up → `DriverCatchUp` (grandma's-car seed, login streak recount + grant); `DailyStreak` and the streak's seeded `Random` leave the coordinator, −31 lines |
| S9 | The settle-hook zone expansion → `ZoneGrowth` (onboarding gate, interim street id, log line, world push); `OperatingZone` and `IPlayerTelemetry` leave the coordinator, which keeps the one call, −27 lines and two constructor parameters |
| S10 | The car haul → `CarHaulFlow` (the tap's settle, the receipt it builds and its calendar checkpoint); the coordinator loses the job, its placeholder constants and the car pool, −58 lines and one constructor parameter |

`MainViewModel`: 2658 → 799 lines. `Poll` is 40 code lines (150 with its
rationale): what is left is the idle-hide policy, the wall-clock bank and the
telemetry feed — the world rules answer one question per tick. The coordinator
now takes 32 named parameters (29 required, 3 optional), all of them finished
services: the root does every `new`. What still keeps the list long is not the
ticks but the one leftover §11 names (the onboarding forwarding), not a
constructor rewrite.

## 3. Target shape

```
                         App.OnStartup  (sole composition root)
                                   │ builds, in this order
   ┌───────────────┬───────────────┼────────────────┬───────────────┐
   │               │               │                │               │
 stores        FleetService    GameStateSaver    TripMeter      OverlayShell
   │               │               │                │               │
   │           JobSettlement ──────┘                │               │
   │           ShopInteriors                        │               │
   │               │                                │               │
   │        ┌──────┴───────┬──────────────┬─────────┘               │
   │        │              │              │                         │
   │   TaxiJobFlow  DeliveryFlow   PoiWorld    DayCycle   WorldTicks│
   │   DriverCatchUp (boot only: starter car, login bonus)          │
   │   ZoneGrowth (settle hook: next street, world push)            │
   │   CarHaulFlow (haul tap: settle, receipt, calendar)            │
   │        │              │              │                         │
   │        └──────┬───────┴──────┬───────┘                         │
   │               │              │                                 │
   │          Navigation ─────────┼─────────────────────────────────┤
   │               │              │                                 │
   │          PanelPresenter ─────┤  (call / reveal / hide policy)  │
   │               │              │                                 │
   │          StoryHost ──► OnboardingFlow     VoiceRouter         │
   │                                                 │              │
   └────────────► MainViewModel  (lifecycle, poll, intent routing, readouts)
```

Dependencies point one way: a collaborator takes services and a *reporting*
host; it never takes another collaborator as its host, and it never queries the
coordinator. Pages take commands from the domain that owns them.

### Collaborators

| Collaborator | Owns (state) | Public surface | Reporting host |
|---|---|---|---|
| `TaxiJobFlow` ✅ | the job, `_dirtyRefusedJob`, `_pendingRequest`, `_requestDueAt`, `_requestScreen`, `_proposal`, `_acceptJobAction`, `_speedChallengeArmed`, `_trackLengthMeters` | `OpenProposal`, `Tick(now)`, `AdvanceLeg(sample, dt)`, `Job`, `InProgress`, `LegActive`, `PickupCommand`, `DropoffCommand`, `SetTrackLength` | `ITaxiJobHost` (8 members) |
| `DeliveryFlow` ✅ | the run, `_storeLegStarted`, `_boardPage` | `OpenBoard`, `RefreshBoard`, `Board`, `Start(run)`, `Advance(sample, dt)`, `Deliver`, `Stop`, `InProgress` | `IDeliveryHost` (8 members): page, panel, HUD pin, hub page, settle |
| `CarHaulFlow` ✅ | — (settle-only today: the legs arrive with the driving slice) | `Settle(back)` → the receipt page; `_wallet`, `_saver`, `_brands`, the pool and the calendar as dependencies | none: the caller puts the returned receipt up with its own BACK command |
| `TripBreakdowns` ✅ | — | `Roll(kind)`, `Interrupt(outcome, back)` | none: the flows put the returned page up |
| `DeliveryBoard` ✅ | the live catalogue (a holder, so a new shop replaces it) | `Catalog`, `Add(store)` | none |
| `PoiWorld` ✅ | the board growth (`Grow`, `AdoptDoors`, hub doors) | `Grow()`, `AdoptDoors()`, `EnsureHubDoor`, `StopDoor(leg)`, `PlaceHubDoor`, `Handlers(supplied)` | `IPoiWorldHost`: board refresh, bought-car page |
| `Navigation` ✅ | `_control`, `_menu`, `_calendar`, `_openDay`, `_roadTestDay` | `ShowScreen`, `ShowMenuPage`, `ShowControl`, `ShowDelivery`, `ShowHub`, `ShowDeliveryBoard`, `ControlPage`, `MenuPage`, `Tick`, `DisplayWeekChanged`, `CarBought`, `ControlCommand`, `MenuCommand`, `GarageCommand`, `TaxiShiftCommand` | `INavigationHost` (3 members) |
| `TelemetryReadouts` ✅ | the seven painted strings' caches (`_lastPacketId`, speed, gear, rpm/throttle/brake, the waiting key) | `Paint(sample, gameRunning)`, `PaintWaiting(gameRunning)`, `PaintJob(job, shift, now)` → `ReadoutPaint` | none: the coordinator's `Apply` copies the record |
| `DayCycle` ✅ | `_lastDay`, `_lastShiftSecs`, the display week | `Tick(now, jobInProgress, legActive)`, `DisplayWeek`, `EndShift(idle)` | `IDayCycleHost`: `DisplayWeekChanged`, `ReadoutsChanged`, `ShowShiftEnded`, `GoToControlPage` |
| `WorldTicks` ✅ | the hunger-block latch | `Tick(onboardingDone)` → whether the town moved | none: the movement returns to the coordinator, which owns the cached board page |
| `DriverCatchUp` ✅ | none: the streak counter and the garage are the services' state; it runs once and exits | `Run(displayWeek)` | none: it banks through `GameStateSaver` and never reaches back into the panel |
| `ZoneGrowth` ✅ | none: the zone's counters are the services' state | `Expand()` | none: the settle hook calls it, then `PoiWorld.Grow` |
| `JobSettlement` ✅ | — | `SettleTaxiRide`, `SettleDeliveryRun` | `IJobSettlementHost` |
| `ShopInteriors` ✅ | — | `Bank`, `Dealer`, `CarWash`, `Mechanic`, `Insurance` | none |
| `FleetService` ✅, `GameStateSaver` ✅ | their own | — | none |
| `PanelPresenter` ✅ | — (`_introShown` became `OverlayShell.HasBeenShown`) | `Show`, `Hide`, `Present`, `PresentJob`, `ShowMenu`, `RecordActivity`, `IsShown`, `CurrentScreen` | none: it *is* the reporting host for the panel |
| `StoryHost` ✅ | — | the `IStoryHost` members | `IStoryHost` ← `Navigation` + `PanelPresenter` + `GameStateSaver` |
| `OnboardingFlow` ✅ | walk progress, profile, errand POI | pages + `Done`/`Intro*` | `IStoryHost` |
| `VoiceRouter` ✅ | mute state | `Handle`, `ResetMute` | none: `PanelPresenter` + `ICarBrandSource` + `IContentSource` (the `IVoiceHost` interface is deleted) |

## 4. What the coordinator keeps

After the split `MainViewModel` is a coordinator, not an owner:

- lifecycle: `Start`, `Poll`, `Dispose`, the timer;
- the 10 Hz loop: feed the meter, tick `TaxiJobFlow`, tick `DayCycle`, tick
  `WorldTicks`, apply `ReadoutPaint` — 40 code lines, not 229;
- intent routing: `OnVoiceHeard`, the hotkey/toggle, POI `ENTER`;
- the panel policy's *call sites*: the navigator's page reports route into
  `PanelPresenter`, which owns the mid-job call gate, which page a call opens
  and `Present`/`Hide` (S6 moved the policy off the coordinator so the story
  flow and the voice router could be root-built);
- session plumbing that is genuinely view-owned: settings store, save-data
  reset, app restart, HUD wiring, `SkipShutdownSave`;
- the bindable readout properties (the target of `ReadoutPaint`).

Constructor: 32 parameters (29 required, 3 optional), all of them finished
services — the root does every `new`. No `MainViewModelDependencies` god record:
each collaborator takes its own dependencies, which is what makes it testable.
The game-clock world left in S7, the boot catch-up in S8, the zone expansion in
S9 and the haul in S10, so the list now grows only with the one leftover §11
names: a follow-up slice, not a constructor rewrite.

## 5. Construction order in `App.OnStartup`

The DAG, top to bottom; each row may only use rows above it.

```
stores + saved state
  └ FleetService (watcher, content)      TaxiDriveMeter (taxiRules)
  └ GameStateSaver                        OverlayShell
  └ DeliveryBoard (the live catalogue)    TelemetryReadouts (no dependencies)
  └ TripBreakdowns (breakdowns, garage, onboarding progress, wallet, saver)
      └ JobSettlement (state owners + board + meter; the host supplies the saves)
      └ ShopInteriors (saver)
      └ DayCycle (game clock, shift, records, shift board, telemetry, saver)
      └ WorldTicks (game clock, city, hunger, fleet, board, career, saver, rivals)
      └ DriverCatchUp (onboarding store, garage, streak, wallet, rng, saver, rivals)
      └ ZoneGrowth (zone, map, onboarding store, profile store, online, log)
      └ TaxiJobFlow (meter, saver, proposals, requests, police, wallet, breakdowns, …)
      └ DeliveryFlow (board, career, city, meter, breakdowns)
      └ CarHaulFlow (watcher, pool, wallet, DayCycle, saver; the legs come later)
      └ PoiWorld (growth, directory, career, shops, board, DayCycle's week)
          └ Navigation (flows' pages, page data, saver, DayCycle's week)
              └ PanelPresenter (shell, pages, the two flows' InProgress)
                  └ StoryHost (panel, pages, saver, standings)
                      └ OnboardingFlow (story host, pages, brand, week)
                      └ VoiceRouter (panel, brand source, content, HUD)
                          └ MainViewModel (all of the above)
```

`PoiArrivalRouter` is built in the root right after `PoiWorld`, from
`PoiWorld.Handlers(...)`; the store handler still comes in from the root.
`Navigation` reports back to `MainViewModel` through `INavigationHost`, the
same late `Attach` the four flows use, and reads the display week off
`DayCycle` instead of keeping a mirror of it. `CarHaulFlow` takes no host at
all: the control page's tap calls `Settle`, and the navigator puts the
returned receipt up with its own `ControlCommand`. The story flow and the voice
router are root-built too: they take `PanelPresenter`/`StoryHost` instead of
the coordinator, so nothing but `MainViewModel`'s own late `Attach`s remains
coordinator-assembled.

## 6. Extraction sequence

Each slice is one commit, green at the end. Do not start the next until the
previous is pushed.

**S1 — `TaxiJobFlow`** ✅ (`4d4c0e2`, ~430 lines: open/accept, pickup, request,
quiz, speed challenge, police, dropoff, abandon, guidance).
Owns the job state and the proposal page. `Poll` calls `TaxiJobFlow.Tick` and
hands it the paid leg.
*Unblocked:* `IVoiceHost.Show`'s gate now reads `jobFlow.InProgress`, the control
page's `TaxiShiftCommand` no longer needs the coordinator's job, and six
constructor parameters left with the flow.
*Deviation:* no `TripMeter` wrapper — it would have had no behaviour of its own,
so both flows take `TaxiDriveMeter` directly.

**S2 — `DeliveryFlow`** ✅ (~150 lines incl. `SourceOwners`). Owns the run, the
meter edge and the board page; `Start` rolls the breakdown, `Stop` drops the run
so the settle can bank it.
*Unblocked:* `JobSettlement.SettleDeliveryRun` has one caller, the shared meter
has exactly two consumers, and `ITaxiJobHost` lost the two breakdown members it
was only forwarding.
*Deviations:* decision 1 settled as the small `TripBreakdowns` collaborator
(roll, tow fee, page) that both flows take, so neither host knows about the
breakdown. `DeliveryBoard` (rule 1's holder) arrived here rather than in S3: the
board page, the run planner and the settle all read it, and `JobSettlement` now
takes the holder instead of a boot-time snapshot. The flow's `Guidance` moved to
the host as `PinStop(leg)` returning false: the stop's door (catalogue row or hub
POI) is still the shell's map knowledge until `PoiWorld` owns it in S3.

**S3 — `PoiWorld`** ✅ (this commit, ~225 lines: `Grow`, `AdoptDoors`,
hub doors, `StopDoor`, `Handlers`). Takes the `DeliveryBoard` holder S2
introduced and owns its growth.
*Unblocked:* `PoiArrivalRouter` is built in the root now (`PoiWorld.Handlers`
returns the full list), and `MainViewModel` lost `GrowPois`,
`AdoptShopDoors`, `BuildPoiHandlers`, the hub-door trio and their two fields.
*Deviations:* `IPoiWorldHost` also carries `DisplayWeek` and `CarBought`.
The dealer handler needs the calendar week and the bought-car choice page;
both stay coordinator state until `DayCycle` (S5) and `Navigation` (S4) own
them, so they cross as reports instead of the world reaching for the shell.
The world takes `IOnboardingStore` for its introduction gate, the same
`Load().IsComplete` contract `TripBreakdowns` already uses.

**S4 — `Navigation`** ✅ (~465 lines: control/menu/calendar/home/wallet/
garage/addresses/trunk/delivery/hub + the bought-car choice). Takes the flows
(their pages are theirs), the session's data and the saver; owns the page
caches and the one page-swap seam.
*Unblocked:* `IStoryHost.ControlScreen`/`MenuScreen` now answer from the
navigator, so the settings page, the menu and the control page are one graph.
`Poll` lost the five page-maintenance lines and the week-flip push, and the
constructor lost `TodoTable`, `StoreCatalog`, `RoadTestLedger` and seven
store/router parameters it never read.
*Deviations:*
- The settings page stays with the coordinator (`INavigationHost.ShowSettings`).
  It is the story flow's (it owns the preferences writer) and RESET GAME is
  session plumbing (§4), so moving it into the navigator would close a cycle
  Navigation ↔ `OnboardingFlow` ↔ `IStoryHost`. The menu's SETTINGS tile is the
  one report that is a page.
- The haul settle and a shift that timed out before the tap are reports too
  (`SettleHaul`, `ShiftTimedOut`): the first is a job settle, the second is
  `EndShift` (S5's `DayCycle`).
- `OpenTaxiShift` moved in, because the control page owns the tap: it starts
  the shift, banks it and opens the flow's proposal page, and the timeout edge
  is reported instead of closed.
- The display week is a mirror, not a delegate: the coordinator still owns the
  number (the day flip rolls it) and reports it through `DisplayWeekChanged`,
  which also repaints the cached header and strip. It moves with its owner in
  S5.
- The coordinator still constructs `OnboardingFlow` and `VoiceRouter`: making
  them root-built needs the S6 rewrite, so "the coordinator constructs
  nothing" lands there, not here.

**S5 — `TelemetryReadouts` + `DayCycle`** ✅ (~390 new lines: the formatter and
its paint record, the calendar and its host; −155 lines of coordinator). The
readouts became a formatter the coordinator applies; the day flip, the week and
the shift timeout left `Poll`, which is 46 code lines now.
*Unblocks:* the week has one owner — `Navigation`'s mirror and
`IPoiWorldHost.DisplayWeek` both died, so `PoiWorld` and `Navigation` read
`DayCycle.DisplayWeek` instead of asking the coordinator. `IPoiWorldHost` is
down to the two reports that are genuinely the shell's (board refresh, bought-car
page); the second cannot move to the navigator without closing a cycle
(`Navigation` → `PoiWorld` → `Navigation`).
*Deviations:*
- `Poll` ends at 46 code lines, not 60: the remaining body is the idle-hide
  policy (§4), the city/hunger/insurance world ticks and the wall-clock bank,
  none of which belong to the calendar or to a readout.
- The readouts are a returned `ReadoutPaint` record, per §9 decision 3; the
  coordinator's `Apply` copies it onto the bindable properties. `PaintJob` is
  static because nothing in it is memoised — the caches only exist for the
  10 Hz path.
- `DayCycle.Tick(now, jobInProgress, legActive)` takes the two job reads as
  values instead of taking the flows: it stays a leaf, and a live leg keeps the
  countdown quiet exactly as `Poll` used to.
- `DayCycle` reads the confirmed name off `IProfileStore` for the shift push
  (the story flow's live profile is still coordinator-built until S6), so the
  push keeps naming the driver without a host getter.
- `GoToControlPage` joined `IDayCycleHost`: the shift-ended page is the
  cycle's, so it builds it and the host only reveals it — the same split
  `IJobSettlementHost.ShowReceipt` already uses.

**S6 — `App.OnStartup` rewrite.** ✅ (−184 lines in the coordinator): the root
now builds the whole DAG, including `PanelPresenter`, `StoryHost`,
`OnboardingFlow` and `VoiceRouter`; `MainViewModel` takes the finished graph and
news up nothing. The three test builders collapsed into `TestSession`.
*Unblocked:* the coordinator constructs nothing (the two host reads are
root-assembled), and the constructor lost the story's 20-odd parameters
(`IOnboardingStore`, `IContentSource`, profiles, name registry, `PoiGenerator`,
directory + store, `PoiArrivalRouter`, `Trunk`, preference store + preferences,
culture, `FareTable`, `InsuranceRules`, `IFriendshipStore`, delivery fares,
`TaxiDriveMeter`, `ShopInteriors`).
*Deviations:*
- A `PanelPresenter` owns the call gate, the first-show rule and
  `Present`/`Hide`; the story flow and the voice router both need them before
  the coordinator exists. The latch that used to be the coordinator's
  `_introShown` moved to `OverlayShell.HasBeenShown` (it is a fact about the
  panel, not a policy).
- `IVoiceHost` is deleted: `VoiceRouter` takes `PanelPresenter`,
  `ICarBrandSource`, `IContentSource`, the HUD, the clock and the log directly,
  which is the option §6 left open.
- The brand moved into `ICarBrandSource` (`Brand`; the watcher already owned the
  identity). The coordinator now only logs the transition instead of mirroring
  the field, so the UI thread reads the source's volatile brand.
- The panel stays a coordinator *call site* only: `Show`/`Hide`/
  `RecordActivity`/`IsShown`/`CurrentScreen` forward to the presenter; the idle
  countdown still lives in `Poll` and reads `OverlayShell.LastActivity`.
- 39 constructor parameters, not 15-20: the world ticks (§4) kept their own
  state at the time. S7 and S8 answered that; the zone expansion is the one
  remainder.
- `RoadTestDayOfWeek` was dead on the coordinator; `Brands` only fed the
  deleted host, and `CarBrand`/`CarIdentity` were mirrors of the watcher, which
  the flow, the voice router and the haul now read directly.

**S7 — `WorldTicks`.** ✅ (−52 lines in the coordinator, five injected fields
and four net constructor parameters): the town's drift, the appetite and the
nightly
premium now share one collaborator that reads the game clock. `Poll` asks one
question — did the town move? — and repaints the cached board page only when it
did.
*Unblocked:* the game moment is read in exactly one place for the world rules
(`GameClock` still feeds the calendar and the fleet), and `CityEconomy`,
`Hunger`, `DeliveryBoard` and `DeliveryCareer` left the coordinator's surface.
The hunger-block latch, the last non-readonly piece of world state, left with
them.
*Deviations:*
- The introduction gate arrives as a value (`Tick(bool onboardingDone)`) instead
  of the store: `IOnboardingStore.Load()` is a file read, and the 10 Hz poll
  must never touch the disk. The coordinator's live `Story.Done` is the value —
  the same shape `DayCycle.Tick` already uses for the two job reads.
- The town's movement returns from `Tick` instead of going through a host: the
  coordinator is the only object that knows the cached board page, and a `bool`
  beats a one-method host that nothing else implements.
- The premium banks through `GameStateSaver` + `IRivalsService` directly (the
  services the settles already take), so the save report stays off the host.
- The boot catch-up (streak grant, grandma's-car seed) and the zone expansion
  did **not** move: they are session-start and progress work, not ticks.
  Grouping them here would have given one collaborator three triggers; S8 took
  the catch-up and the zone expansion is the slice after it.

**S8 — `DriverCatchUp`.** ✅ (−31 lines in the coordinator; two constructor
parameters net −1): the boot block that seeded grandma's car and recounted plus
granted the login streak is one collaborator with one `Run(week)` entry point.
It takes `IOnboardingStore`, `Garage`, `DailyStreak`, `DriverWallet`, the
seeded `Random`, `GameStateSaver`, `IRivalsService` and the log.
*Unblocked:* `DailyStreak` and the streak's `Random` (`_streak`, `_rng`) left
the coordinator's surface, and the streak's `SaveDriver` and log line went with
them. Nothing else on the coordinator reads the streak, so no host was needed.
*Deviations:*
- `Run(int displayWeek)` takes the week as a value instead of taking
  `DayCycle`: the seeded car's purchase stamp is the only calendar fact the
  catch-up needs, and a leaf that takes a value adds no edge to the graph. The
  call site reads `_day.DisplayWeek` where the old block read it.
- The `IOnboardingStore` read happens inside the collaborator instead of the
  gate arriving as a value: this runs once from the coordinator's constructor
  and never from the poll, so "no disk in the 10 Hz loop" is not in play — the
  same contract `PoiWorld` and `TripBreakdowns` already use.
- `IRivalsService` joined the constructor (it was not in the slice's sketch):
  the grant's `SaveDriver` carries the standings snapshot, exactly like
  `WorldTicks`' premium charge. The save itself stays inside `GameStateSaver`.
- The recount stays ungated while the grant is gated: Unity refreshes the
  counter on every launch and only refuses the payout before
  `firstSceneComplete`, and `StreakGrant_BeforeOnboarding_HoldsBack` asserts
  exactly that split.

**S9 — `ZoneGrowth`.** ✅ (−27 lines in the coordinator, two net constructor
parameters): the zone expansion the settle hook used to inline is one
collaborator with one `Expand()` entry point. It takes `OperatingZone`, the
`AddressMap`, `IOnboardingStore`, `IProfileStore`, `IPlayerTelemetry` and the
log.
*Unblocked:* `OperatingZone` and `IPlayerTelemetry` left the coordinator's
surface, so the only thing it still knows about the zone is that a settle grows
it. The introduction gate, the interim street id, the log line and the world
push went with the collaborator.
*Deviations:*
- The gate reads `IOnboardingStore` instead of the coordinator's live
  `OnboardingDone`: this runs from the settle, never from the poll, so it is the
  same cold-path contract `PoiWorld.Grow` (called one line later) and
  `TripBreakdowns` already use.
- The pushed name comes off `IProfileStore` like `DayCycle`'s shift push, not
  the story flow's in-memory profile: the opening screen banks the profile
  before any job can settle, so the two answers agree and the collaborator stays
  a leaf.
- No host at all: `OnProgressBanked` calls `Expand()` and the next line is
  `PoiWorld.Grow()`. The zone's counters and km are banked by the settle's own
  driver save, so the collaborator has nothing to report.

**S10 — `CarHaulFlow`.** ✅ (−58 lines in the coordinator, one net constructor
parameter): the haul the control page's tap settled inline is its own job
collaborator, the third job type. It takes `ICarBrandSource`, the car pool,
`DriverWallet`, `DayCycle` (the week its checkpoint banks), `GameStateSaver`,
`VoiceBeat` and the log.
*Unblocked:* the last job type left the coordinator's control-page path, so the
coordinator now knows nothing about a haul at all: `INavigationHost` lost
`SettleHaul` (down to 3 members), the placeholder constants left with the job,
and the car pool left the constructor. `Navigation` consumed one report fewer
and one page more.
*Deviations:*
- `Settle(backCommand)` returns the receipt, the shape `TripBreakdowns.Interrupt`
  already uses: the caller's own host puts it up with its own BACK command, so
  the flow takes no host, no late `Attach` and no `Navigation` handle. The
  control page's tap stays a page-open command (like `ShowDelivery`), not a
  `CarHaulFlow` command: nothing else ever settles a haul.
- The calendar arrives as the `DayCycle` instance instead of a `Func<int>`: the
  flow is built after the calendar and only reads `DisplayWeek`, the same way
  `Navigation` stamps its pages.
- The rating is still the wallet's persisted average and the 30-minute
  placeholder leg is still the pre-driving-slice stand-in; both moved with the
  job, and the legs arrive here when the driving slice feeds them.

### Host implementations after S10

The reads now live with their owners:

- `IStoryHost` — `StoryHost` composes `Navigation` (pages), `PanelPresenter`
  (reveal/hide), `GameStateSaver` + `IRivalsService` (the shared driver save).
- `IVoiceHost` — deleted; `VoiceRouter` takes its six collaborators directly
  (panel, brand source, content, HUD, clock, log).
- `INavigationHost` — three members: settings page, readout refresh, timed-out
  shift into `DayCycle.EndShift`. The haul left it in S10 (the flow's page).
- `IDayCycleHost` — unchanged; its four members are the coordinator's panel
  call sites and page routing.
- `WorldTicks` takes no host at all: its one report is the `bool` the tick
  returns.
- `DriverCatchUp` takes no host either: it runs once from the constructor,
  banks through `GameStateSaver` and has nothing to show.
- `ZoneGrowth` takes no host either: the settle hook calls it and the zone's
  counters are banked by the saver at that same settle.
- `CarHaulFlow` takes no host either: `Settle` returns its receipt and the
  navigator shows it with its own BACK command.

## 7. Rules for every slice

1. **State has one owner.** The collaborator that owns a field is the only one
   that writes it. `_deliveryCatalog` is the known offender: it is reassigned
   in `AdoptShopDoors`, so it becomes a holder (`DeliveryBoard`) before S3.
2. **Commands belong to the domain.** `RelayCommand`s for a job are created by
   the flow that performs them; `Navigation` consumes them. A page must never
   be built from callbacks into the coordinator.
3. **Host interfaces carry reporting only** — show a page, save, refresh a
   readout. If a collaborator needs a value, it takes the value or owns it; a
   host member that answers a question about the coordinator's state is a
   design error.
4. **No collaborator takes another collaborator as its host.** Dependencies
   point at services and at a host the coordinator (or `App`) implements.
5. **A new job type is a new collaborator**, not a branch in `Poll` or in the
   entry path (open/closed).
6. **`AceDrive.Core` stays pure**; no collaborator here may add WPF, IO or
   telemetry offsets.

## 8. Tests

- Fakes live in `tests/AceDrive.Core.Tests/Fakes`, one per file.
- One instance per service graph: a collaborator must be built from the *same*
  store fakes the assertions read (this is why the builders hoist their locals).
- Each new collaborator gets a fake host and its own tests; the flows'
  money-adjacent paths (settle, ratings, shift records) keep their existing
  flow tests untouched as the regression net.
- `JobSettlement` is now testable without a session: the two direct tests ("an
  unowned car pays but banks no progress", "the run sums per-store CredP")
  landed with S2.
- `DayCycle` and `TelemetryReadouts` landed with their own suites in S5: the
  week roll, the countdown guard and the timeout page through
  `FakeDayCycleHost`; the paint record through direct calls (there is nothing
  to fake, and the assertions are the returned strings).
- S6 collapsed the three duplicated graphs into `TestSession` (see
  `TestSessionOptions`): every suite builds the *same* session the root builds,
  with named collaborators and fakes, so a constructor change fans out once.
  `FakeCarBrandSource` owns the primed brand the watcher would publish, and the
  builder primes it (`Start` does that in production) so voice tests can speak
  the wake word without a lifecycle call.
- `WorldTicks` landed with its own suite in S7: the town anchor and the
  30-minute boundary through the returned flag, the appetite's introduction
  gate and its one-shot block warning, and the premium's once-per-game-date
  charge with the driver save it runs. The quiet charge hour is the test that
  proves the poll writes nothing per tick.
- `DriverCatchUp` landed with its own suite in S8: the seed's two gates (empty
  garage, finished introduction), the resumed garage it leaves alone, the
  streak's ungated recount against its gated grant, day 3's 4 CR plus its three
  skill points in one driver save, and the quiet launch that writes nothing.
  The coordinator-level `StreakGrant_*` and `Boot_SeedsGrandmasCar*` tests stay
  as the regression net for the call site.
- `ZoneGrowth` landed with its own suite in S9: the first and the second
  expansion through the street list, the introduction gate and the below-curve
  case through the absent push, the nameless push before the opening screen is
  answered, and the log line. The coordinator-level
  `ZoneJobCount_GrowsAtTheSettles_AndExpandsOnTheCurve` test stays as the
  regression net for the call site.
- `CarHaulFlow` landed with its own suite in S10: the driven car's band against
  the placeholder one, the wallet's cash-only bank, the calendar checkpoint's
  week and the caller's own CONTINUE. The navigator-level
  `HaulDoor_SettlesTheFlowAndItsReceipt_WithoutTheCoordinator` and the
  coordinator-level `CarHaulTap_*` / `HaulSettle_NeverCountsTheZone` tests stay
  as the regression net for the tap.

## 9. Open decisions

1. **`RollTripStart` / `ShowBreakdown`** — settled in S2: `TripBreakdowns`
   (roll, tow fee, page) is taken by both flows; neither host carries them.
2. **`OnCarHaul`** (PORTPLAN 20) — settled in S10: its own `CarHaulFlow`, a
   leaf that returns its receipt. The driving slice grows the legs there.
3. **Readouts** — settled in S5: `TelemetryReadouts` returns a `ReadoutPaint`
   record and the coordinator's `Apply` copies it onto the bindable properties.
   The caches stayed in the formatter, so the poll formats only changed values
   and no property is set twice.
4. **Page lifetime**: control/menu/calendar are cached, interiors are fresh per
   visit. `Navigation` should keep that split explicit rather than accidental.
5. **Does `Poll` stay in the coordinator?** Yes for now. A `SessionLoop` is
   only worth it if a second host (tests, a headless run) needs the tick.

## 10. Definition of done (per slice)

- `dotnet build ACEDRIVE.slnx` — 0 warnings.
- `dotnet test ACEDRIVE.slnx` — all green, count not reduced.
- `dotnet format ACEDRIVE.slnx --verify-no-changes` — clean.
- CRLF on every touched file.
- The coordinator's line count and field list both shrink; if a slice only moves
  code without shrinking either, it is not a slice — stop and reconsider.
- The commit message names what unblocked, not only what moved.

## 11. Resume here

State: S1 pushed as `4d4c0e2`; S2, S3 and S4 pushed; S5–S10 are green locally.
`MainViewModel` 799 lines (was 857 after S9), −58 lines: the car haul (the tap's
settle, its receipt, its calendar checkpoint and the car pool behind it) left
for `CarHaulFlow`, and `INavigationHost` lost its haul report. The injected
field list is 32, the constructor 32 parameters (29 required, 3 optional). 1723
tests green (+5: four `CarHaulFlowTests` and the navigator's haul tap).

Next: the one candidate the slices left behind:

- `MainViewModel`'s onboarding forwarding (`ChooseBrand`/`ChooseCar`/
  `ShowDialogue`/`FinishDialogue` + the three flags) exists for the tests now
  that the view binds only `CurrentScreen`; it can move behind `_story`. The
  view's two remaining reads (`App`'s tour gate, the window's placement gate)
  take the story from the root, which builds it.

Working agreements earned so far:

- The coordinator implements the host; a flow attaches with `Attach(this)` in
  the coordinator's constructor — the one late edge, closed before any command
  can fire. `PoiWorld` joins that list: its arrival delegates only read the
  host when a door is entered. `Navigation` joins it too, for the three entries
  that are not pages. `DayCycle` joins it for the shift-end page it builds.
  The story flow and the voice router are the exception: their host reads are
  root-built objects (`StoryHost`, `PanelPresenter`), so they take no late
  edge at all.
- A collaborator that only *produces* a page takes no host at all:
  `TripBreakdowns.Interrupt` charges the tow and returns the page, and the
  caller's own host puts it up with its own BACK command. The opposite case
  (`DayCycle`'s shift-ended page) takes one report for its CONTINUE instead, so
  the page stays the cycle's and the panel policy stays the presenter's.
  `CarHaulFlow.Settle(back)` is the same shape: the receipt it just priced goes
  back to the navigator, which shows it with its own control command.
- Collaborators take the saver, watcher and rivals directly; the host only
  reports (page, saves, readouts). A stop's door is asked as
  `bool PinStop(leg)` — reporting plus a degrade signal, never a host getter.
- Pages are built by one collaborator and cached there; the coordinator keeps
  only the panel *call sites* (`PanelPresenter` owns `Present`/`Hide`, the
  mid-job call gate and which page a call opens) and routes its own page swaps
  through `Navigation.ShowScreen`, the one beat-cancelling seam.
- A page whose domain belongs elsewhere is a report on the host, not a builder
  in the navigator: the settings page (story flow + RESET GAME) and a shift that
  already timed out (`DayCycle.EndShift`). A page a collaborator already returns
  (the haul receipt) is not even that: the navigator shows it.
- A value that a collaborator needs now but another collaborator will own later
  crosses the host explicitly and moves with its new owner. `DisplayWeek` made
  that trip: coordinator → `DayCycle` in S5, with the navigator's mirror and
  the world's host getter both deleted rather than kept in sync.
- A per-tick gate that lives behind a store is a value, not a read: the world
  ticks take `onboardingDone` because `IOnboardingStore.Load()` is a file read
  and the 10 Hz poll must never touch the disk. The same rule keeps
  `DayCycle.Tick`'s two job reads as parameters. Where the poll cannot reach
  it, the read is fine: `DriverCatchUp` loads the onboarding file once from the
  coordinator's constructor, so its two gates (finished introduction, picked
  car) arrive as one read instead of two more constructor parameters.
- A `bool` is a host when it is the whole report: `WorldTicks.Tick` answers
  "did the town move?" and the coordinator repaints its own cached page. No
  interface for one transition that only one caller consumes.
- A settle-hook collaborator needs no host either: `ZoneGrowth.Expand()` is
  called by `OnProgressBanked`, the state it changed is banked by the settle's
  driver save on the next line, and `PoiWorld.Grow()` follows. The coordinator
  keeps the one call and nothing else about the zone.
- Tests: one instance per service graph; builders hoist their locals. A
  collaborator test wires the *same* store fakes its assertions read, and a
  culture-sensitive one pins `en-US` in its constructor (the suite shares one
  process, so a neighbour's language otherwise leaks into `Strings.Get`).
- Never round-trip UTF-8 through `Get-Content`/`Set-Content` (PowerShell 5.1
  decodes as ANSI and re-encodes, silently mangling `—` and `€`); use
  `[IO.File]::ReadAllText/WriteAllText`.
