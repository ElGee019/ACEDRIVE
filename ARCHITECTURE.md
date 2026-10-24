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

Extracted and pushed, each verified (build 0 warnings, 1659 tests, format clean):

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

`MainViewModel`: 2658 → 1151 lines. `Poll` is 46 code lines (160 with its
rationale): what is left is the idle-hide policy, the world ticks (city,
hunger, insurance), the wall-clock bank and the telemetry feed. The target
coordinator is roughly 500.

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
   │   TaxiJobFlow   DeliveryFlow     PoiWorld                 DayCycle
   │        │              │              │                         │
   │        └──────┬───────┴──────┬───────┘                         │
   │               │              │                                 │
   │          Navigation ─────────┼─────────────────────────────────┤
   │               │              │                                 │
   │        OnboardingFlow    VoiceRouter  ◄──── OverlayShell + brand/content
   │        (IStoryHost ← Navigation + saver)   (IVoiceHost ← shell + flows)
   │
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
| `TripBreakdowns` ✅ | — | `Roll(kind)`, `Interrupt(outcome, back)` | none: the flows put the returned page up |
| `DeliveryBoard` ✅ | the live catalogue (a holder, so a new shop replaces it) | `Catalog`, `Add(store)` | none |
| `PoiWorld` ✅ | the board growth (`Grow`, `AdoptDoors`, hub doors) | `Grow()`, `AdoptDoors()`, `EnsureHubDoor`, `StopDoor(leg)`, `PlaceHubDoor`, `Handlers(supplied)` | `IPoiWorldHost`: board refresh, bought-car page |
| `Navigation` ✅ | `_control`, `_menu`, `_calendar`, `_openDay`, `_roadTestDay` | `ShowScreen`, `ShowMenuPage`, `ShowControl`, `ShowDelivery`, `ShowHub`, `ShowDeliveryBoard`, `ControlPage`, `MenuPage`, `Tick`, `DisplayWeekChanged`, `CarBought`, `ControlCommand`, `MenuCommand`, `GarageCommand`, `TaxiShiftCommand` | `INavigationHost` (4 members) |
| `TelemetryReadouts` ✅ | the seven painted strings' caches (`_lastPacketId`, speed, gear, rpm/throttle/brake, the waiting key) | `Paint(sample, gameRunning)`, `PaintWaiting(gameRunning)`, `PaintJob(job, shift, now)` → `ReadoutPaint` | none: the coordinator's `Apply` copies the record |
| `DayCycle` ✅ | `_lastDay`, `_lastShiftSecs`, the display week | `Tick(now, jobInProgress, legActive)`, `DisplayWeek`, `EndShift(idle)` | `IDayCycleHost`: `DisplayWeekChanged`, `ReadoutsChanged`, `ShowShiftEnded`, `GoToControlPage` |
| `JobSettlement` ✅ | — | `SettleTaxiRide`, `SettleDeliveryRun` | `IJobSettlementHost` |
| `ShopInteriors` ✅ | — | `Bank`, `Dealer`, `CarWash`, `Mechanic`, `Insurance` | none |
| `FleetService` ✅, `GameStateSaver` ✅ | their own | — | none |
| `OnboardingFlow` (exists) | walk progress | — | `IStoryHost` ← `Navigation` + `GameStateSaver` |
| `VoiceRouter` (exists) | mute/beat state | — | `IVoiceHost` ← `OverlayShell` + content + watcher + `TaxiJobFlow.InProgress` |

## 4. What the coordinator keeps

After the split `MainViewModel` is a coordinator, not an owner:

- lifecycle: `Start`, `Poll`, `Dispose`, the timer;
- the 10 Hz loop: feed the meter, tick `TaxiJobFlow`, tick `DayCycle`, apply
  `ReadoutPaint` — 46 code lines, not 229;
- intent routing: `OnVoiceHeard`, `OnCarBrand`, the hotkey/toggle, POI `ENTER`;
- panel policy: the mid-job call gate (reads `TaxiJobFlow.InProgress`), which
  page a call opens, `Present`/`Hide`;
- session plumbing that is genuinely view-owned: settings store, save-data
  reset, app restart, HUD wiring, `SkipShutdownSave`;
- the bindable readout properties (the target of `ReadoutPaint`).

Target constructor: ~15–20 parameters, all collaborators plus the view-owned
bits (`ITelemetrySource`, `IClock`, `ILog`, `ITelemetryHud`, `ISettingsStore`,
`ISaveDataResetter`, `IAppRestarter`, `IContentSource`, `AddressMap`,
`IVoiceListener`). No `MainViewModelDependencies` god record: each collaborator
takes its own dependencies, which is what makes it testable.

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
          └ TaxiJobFlow (meter, saver, proposals, requests, police, wallet, breakdowns, …)
          └ DeliveryFlow (board, career, city, meter, breakdowns)
          └ PoiWorld (growth, directory, career, shops, board, DayCycle's week)
              └ Navigation (flows' pages, page data, saver, DayCycle's week)
                  └ OnboardingFlow (Navigation, saver)
                  └ VoiceRouter (shell, brand, content, TaxiJobFlow)
                      └ MainViewModel (all of the above)
```

`PoiArrivalRouter` is built in the root right after `PoiWorld`, from
`PoiWorld.Handlers(...)`; the store handler still comes in from the root.
`Navigation` reports back to `MainViewModel` through `INavigationHost`, the
same late `Attach` the four flows use, and reads the display week off
`DayCycle` instead of keeping a mirror of it.

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

**S6 — `App.OnStartup` rewrite.** Build the DAG above, delete the 195-line
constructor's parameter list, collapse the test builders to named collaborators.

### Host implementations after S5

The navigator and the calendar exist, so the reads are close to place:

- `IStoryHost` — `Navigation` supplies `ControlScreen`/`MenuScreen`/
  `ShowScreen`/`ShowMenu`, `OverlayShell` supplies `CurrentScreen`/`IsShown`/
  `Present`/`Hide`, and `GameStateSaver` supplies `SaveDriver`. Until the S6
  rewrite the coordinator still assembles them.
- `IStoryHost`'s remaining gap is `Present`/`Hide`: they carry the reveal
  policy (`_introShown`, the log lines), so they stay with the coordinator —
  the same panel policy §4 keeps there.
- `IVoiceHost` — `OverlayShell` supplies `IsShown`/`CurrentScreen`/`Show`/
  `RecordActivity` once the call gate reads `TaxiJobFlow.InProgress`,
  `IContentSource` supplies `Content`, `ICarBrandSource` supplies `CarBrand`.
- `INavigationHost` — the navigator's own reports stay (the settings page, the
  haul settle, the readout refresh); a shift that already timed out is a
  one-liner into `DayCycle.EndShift`, which builds its own page.
- `IDayCycleHost` — nothing to assemble in S6: its four members are the
  coordinator's own panel policy and page routing.

If assembling those reads into a host object is not worth a file, `VoiceRouter`
can take the four dependencies directly and `IVoiceHost` is deleted; the
interface exists only to keep the router off the coordinator's surface. Decide
in S6, when the root assembles it.

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

## 9. Open decisions

1. **`RollTripStart` / `ShowBreakdown`** — settled in S2: `TripBreakdowns`
   (roll, tow fee, page) is taken by both flows; neither host carries them.
2. **`OnCarHaul`** (PORTPLAN 20) is a third job type in disguise: settle-only
   today. Keep it in `TaxiJobFlow` for S1 and split it when it grows legs, or
   make it its own flow now.
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

State: S1 pushed as `4d4c0e2`; S2, S3 and S4 pushed; S5 is green locally.
`MainViewModel` 1151 lines (was 1306), −155 lines: the readout formatting and
its seven caches, the day flip, the display week, the shift countdown and the
shift-end path left, along with `_lastDay`, `_displayWeek`, `_lastShiftSecs`,
`_shifts`, `_shiftBoard` and the readout sentinels. 1701 tests green (+14:
`DayCycleTests`, `TelemetryReadoutsTests`).

Next: **S6 — the `App.OnStartup` rewrite.** Build the DAG above (it is all
root-built except `OnboardingFlow`/`VoiceRouter`), delete the 195-line
constructor's parameter list, and collapse the test builders to named
collaborators. `MainViewModel` should end near 500 lines; what is left is
lifecycle, `Poll`'s world ticks, intent routing and panel policy.
*Unblocks:* the coordinator constructs nothing (the two host reads `IStoryHost`
and `IVoiceHost` get assembled in the root), and the tests build a session from
the same named collaborators instead of a 50-argument call.

Working agreements earned so far:

- The coordinator implements the host; a flow attaches with `Attach(this)` in
  the coordinator's constructor — the one late edge, closed before any command
  can fire. `PoiWorld` joins that list: its arrival delegates only read the
  host when a door is entered. `Navigation` joins it too, for the four entries
  that are not pages. `DayCycle` joins it for the shift-end page it builds.
- A collaborator that only *produces* a page takes no host at all:
  `TripBreakdowns.Interrupt` charges the tow and returns the page, and the
  caller's own host puts it up with its own BACK command. The opposite case
  (`DayCycle`'s shift-ended page) takes one report for its CONTINUE instead, so
  the page stays the cycle's and the panel policy stays the coordinator's.
- Collaborators take the saver, watcher and rivals directly; the host only
  reports (page, saves, readouts). A stop's door is asked as
  `bool PinStop(leg)` — reporting plus a degrade signal, never a host getter.
- Pages are built by one collaborator and cached there; the coordinator keeps
  only the panel policy (`Present`/`Hide`, the mid-job call gate, which page a
  call opens) and routes its own page swaps through `Navigation.ShowScreen`,
  the one beat-cancelling seam.
- A page whose domain belongs elsewhere is a report on the host, not a builder
  in the navigator: the settings page (story flow + RESET GAME), the haul
  settle (a job) and a shift that already timed out (`DayCycle.EndShift`).
- A value that a collaborator needs now but another collaborator will own later
  crosses the host explicitly and moves with its new owner. `DisplayWeek` made
  that trip: coordinator → `DayCycle` in S5, with the navigator's mirror and
  the world's host getter both deleted rather than kept in sync.
- Tests: one instance per service graph; builders hoist their locals. A
  collaborator test wires the *same* store fakes its assertions read, and a
  culture-sensitive one pins `en-US` in its constructor (the suite shares one
  process, so a neighbour's language otherwise leaks into `Strings.Get`).
- Never round-trip UTF-8 through `Get-Content`/`Set-Content` (PowerShell 5.1
  decodes as ANSI and re-encodes, silently mangling `—` and `€`); use
  `[IO.File]::ReadAllText/WriteAllText`.
