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

`MainViewModel`: 2658 → 1596 lines. Over 1,000 of the remaining lines are
domain work that belongs to someone else; the target coordinator is roughly
500.

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
| `PoiWorld` ✅ | the board growth (`Grow`, `AdoptDoors`, hub doors) | `Grow()`, `AdoptDoors()`, `EnsureHubDoor`, `StopDoor(leg)`, `PlaceHubDoor`, `Handlers(supplied)` | `IPoiWorldHost`: board refresh, calendar week, bought-car page |
| `Navigation` | `_control`, `_menu`, `_calendar`, `_openDayAction`, `_roadTestDay` | `Control()`, `Menu()`, `Calendar()`, `Home()`, `Wallet()`, `Garage()`, `Addresses()`, `Trunk()`, `Settings()`, `Delivery()`, `Hub()` | `OverlayShell` (page swap) + `GameStateSaver` |
| `TelemetryReadouts` | the seven painted strings + their caches | `Paint(sample)`, `PaintWaiting()`, `RefreshJob(job, shift)` | `IReadoutSink` (or returns a record) |
| `DayCycle` | `_displayWeek`, `_lastDay`, `_lastShiftSecs` | `Tick(today)`, `DisplayWeek`, `EndShift(idle)` | `IDayCycleHost`: pages' `DisplayWeek`, shift-ended page, `SaveClock` |
| `JobSettlement` ✅ | — | `SettleTaxiRide`, `SettleDeliveryRun` | `IJobSettlementHost` |
| `ShopInteriors` ✅ | — | `Bank`, `Dealer`, `CarWash`, `Mechanic`, `Insurance` | none |
| `FleetService` ✅, `GameStateSaver` ✅ | their own | — | none |
| `OnboardingFlow` (exists) | walk progress | — | `IStoryHost` ← `Navigation` + `GameStateSaver` |
| `VoiceRouter` (exists) | mute/beat state | — | `IVoiceHost` ← `OverlayShell` + content + watcher + `TaxiJobFlow.InProgress` |

## 4. What the coordinator keeps

After the split `MainViewModel` is a coordinator, not an owner:

- lifecycle: `Start`, `Poll`, `Dispose`, the timer;
- the 10 Hz loop: feed `TripMeter`, tick `TaxiJobFlow`, tick `DayCycle`, paint
  readouts — the loop body becomes ~60 lines, not 229;
- intent routing: `OnVoiceHeard`, `OnCarBrand`, the hotkey/toggle, POI `ENTER`;
- panel policy: the mid-job call gate (reads `TaxiJobFlow.InProgress`), which
  page a call opens, `Present`/`Hide`;
- session plumbing that is genuinely view-owned: settings store, save-data
  reset, app restart, HUD wiring, `SkipShutdownSave`;
- the bindable readout properties (delegating to `TelemetryReadouts`).

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
  └ DeliveryBoard (the live catalogue)
  └ TripBreakdowns (breakdowns, garage, onboarding progress, wallet, saver)
      └ JobSettlement (state owners + board + meter; the host supplies the saves)
      └ ShopInteriors (saver)
          └ TaxiJobFlow (meter, saver, proposals, requests, police, wallet, breakdowns, …)
          └ DeliveryFlow (board, career, city, meter, breakdowns)
          └ PoiWorld (growth, directory, career, shops, board)
              └ Navigation (flows' commands, shop pages, saver)
                  └ OnboardingFlow (Navigation, saver)
                  └ VoiceRouter (shell, brand, content, TaxiJobFlow)
                      └ MainViewModel (all of the above)
```

`PoiArrivalRouter` is built in the root right after `PoiWorld`, from
`PoiWorld.Handlers(...)`; the store handler still comes in from the root.

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

**S4 — `Navigation`** (~260 lines: control/menu/calendar/home/wallet/garage/
addresses/trunk/settings/delivery/hub + the bought-car choice). Takes the
flows' `ICommand`s; owns the page caches.
*Unblocks:* `IStoryHost.ControlScreen`/`MenuScreen`, so `OnboardingFlow` is
constructible in the root. After S4 the coordinator constructs nothing.

**S5 — `TelemetryReadouts` + `DayCycle`** (~270 lines). The readouts become a
formatter the coordinator applies; the day flip, week display and shift timeout
leave `Poll`. `Poll` ends near 60 lines.
*Unblocks:* nothing construction-wise; this is the SRP win on the hot path and
what makes `Poll` reviewable.

**S6 — `App.OnStartup` rewrite.** Build the DAG above, delete the 224-line
constructor's parameter list, collapse the test builders to named collaborators.

### Host implementations after S4

Neither host interface needs the coordinator once the pieces above exist:

- `IStoryHost` — `Navigation` supplies `ControlScreen`/`MenuScreen`/`ShowScreen`/
  `Present`/`Hide`/`ShowMenu`, `OverlayShell` supplies `CurrentScreen`/`IsShown`,
  and `GameStateSaver` supplies `SaveDriver`.
- `IVoiceHost` — `OverlayShell` supplies `IsShown`/`CurrentScreen`/`Show`/
  `RecordActivity` once the call gate reads `TaxiJobFlow.InProgress`,
  `IContentSource` supplies `Content`, `ICarBrandSource` supplies `CarBrand`.

If assembling those reads into a host object is not worth a file, `VoiceRouter`
can take the four dependencies directly and `IVoiceHost` is deleted; the
interface exists only to keep the router off the coordinator's surface. Decide
in S1, when the gate moves.

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

## 9. Open decisions

1. **`RollTripStart` / `ShowBreakdown`** — settled in S2: `TripBreakdowns`
   (roll, tow fee, page) is taken by both flows; neither host carries them.
2. **`OnCarHaul`** (PORTPLAN 20) is a third job type in disguise: settle-only
   today. Keep it in `TaxiJobFlow` for S1 and split it when it grows legs, or
   make it its own flow now.
3. **Readouts**: `IReadoutSink` with property setters (binds directly) or a
   returned `ReadoutPaint` record (purer, coordinator applies). Prefer the
   record; revisit if binding notifications get awkward.
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

State: S1 pushed as `4d4c0e2`; S2 pushed; S3 is green locally.
`MainViewModel` 1596 lines (was 1742), −146 lines: the POI growth, shop
adoption, hub doors and handler assembly left, along with `_poiGrowth` and
`_jobs`. 1678 tests green (+8: `PoiWorldTests`).

Next: **S4 — `Navigation`** (~260 lines: control/menu/calendar/home/wallet/
garage/addresses/trunk/settings/delivery/hub + the bought-car choice). Takes
the flows' `ICommand`s; owns the page caches.
*Unblocks:* `IStoryHost.ControlScreen`/`MenuScreen`, so `OnboardingFlow` is
constructible in the root. After S4 the coordinator constructs nothing.

Working agreements earned so far:

- The coordinator implements the host; a flow attaches with `Attach(this)` in
  the coordinator's constructor — the one late edge, closed before any command
  can fire. `PoiWorld` joins that list: its arrival delegates only read the
  host when a door is entered.
- A collaborator that only *produces* a page takes no host at all:
  `TripBreakdowns.Interrupt` charges the tow and returns the page, and the
  caller's own host puts it up with its own BACK command.
- Collaborators take the saver, watcher and rivals directly; the host only
  reports (page, saves, readouts). A stop's door is asked as
  `bool PinStop(leg)` — reporting plus a degrade signal, never a host getter.
- A value that a collaborator needs now but another collaborator will own later
  crosses the host explicitly and moves with its new owner (`DisplayWeek`:
  coordinator → `DayCycle` in S5).
- Tests: one instance per service graph; builders hoist their locals.
- Never round-trip UTF-8 through `Get-Content`/`Set-Content` (PowerShell 5.1
  decodes as ANSI and re-encodes, silently mangling `—` and `€`); use
  `[IO.File]::ReadAllText/WriteAllText`.
