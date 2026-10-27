# Operating Zone + POI network — TOZONES port plan

Status: implemented — P1–P7 landed 2026-10-27, each phase green on its own.
§1 is the pre-implementation snapshot that motivated the plan and is kept as the
rationale record; §10's limitations still hold. Scope: PORTPLAN items 24 (typed
POI network), 58 (operating zone + discovery/expansion) and the screens both
need.
Source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2` (Unity), referenced per
method below. Everything here is a gameplay port, not a code port
(AGENTS.md › Role).

Owner files touched by this plan live in `src/AceDrive.Core` and
`src/AceDrive.Overlay`; the work is sliced so each phase builds and tests green
on its own.

---

## 1. Where the feature stands today

| Piece | ACEDRIVE today | Unity source |
|---|---|---|
| Zone counters + threshold curve | `Core/OperatingZone` (2, 3, then 4–6 jobs) | `MapScreen.GetNextExpansionThreshold` |
| Zone growth at a settle | `ViewModels/ZoneGrowth.Expand()` — onboarding gate, interim ids `s1`, `s2`, km push | `MapScreen.ExpandOperatingZone` + `FinishExpandCoroutine` |
| Expansion screen | **missing** | `UI/ZoneExpansionScreen.cs` |
| Zone world standings | `Services/FirestoreZoneStandings` exists but is constructed nowhere; `Core/ZoneStandings` + `ZoneStandingsResult` exist | `API/ZoneStandingsService` |
| POI spawn curves | `Core/PoiSpawner` + `PoiSpawnRule` + `Data/pois.json` (Unity's numbers) | `CarWashGenerator`, `BankGenerator`, `InsuranceBrokerGenerator`, `CarDealerGenerator`, `CarMechanicGenerator`, `StorePlacementHelper` |
| POI naming | `Core/ProceduralNameGenerator` + `NamePattern` (`{0}'s Bank`), id `type_n` | `NamePoolManager.GenerateProceduralName`, owner `First Last` |
| Map / streets | `Data/address_map.json`: Street 1, Street 2 only; zone km seeded from the map | `StreetPool` + Overpass (DECLINED) |
| POI interiors | Bank, Dealer, CarWash, Mechanic, Insurance (`ViewModels/ShopInteriors`), Grocery/Coffee store page (`ViewModels/StorePoiHandler`) | `BankScript`, `CarDealerScreen`, `CarWashScreen`, `CarMechanicScreen`, `InsuranceScreen`, `MaxsScreen` |
| POI placement at expansion | `PoiWorld.Grow()` runs on every settle/boot/board open; draws from the whole map | `FinishExpandCoroutine` steps 1–7 (first POI on the new street, rest on existing streets) |
| Zone readout on the HUD | none (no map HUD) | `MapScreenHud.UpdateOperatingZoneDisplay` |

### What is actually wrong

1. **The zone cannot grow past Street 2.** `ZoneGrowth` mints ids `sN` that no
   street on the map carries, so nothing ties the zone to real addresses and
   `AddressBook.ZoneStreets` silently caps at the map's two streets.
2. **No expansion screen.** The reachable signal of growth is one log line.
3. **POIs are not tied to the expansion or to the discovered streets.**
   `PoiSpawner` draws from every street on the map, so a POI can sit on a
   street the zone has not reached (already true for Street 2, worse with a
   fictional pool), and the settle places doors without ever telling the
   player which ones opened.
4. **The standings seam is dead code.** `FirestoreZoneStandings` cannot be
   consumed by a screen: no interface, no offline null-object, no wiring.
5. **No zone state on screen.** Unity answers "OPERATING ZONE: n streets" and
   "Expansion: j" at a glance; the port answers nothing.

---

## 2. Decisions taken (Q&A, 2026-10-27)

| Decision | Choice |
|---|---|
| POI screens in scope | **Only the families the zone spawns** (car wash, bank, insurance, dealer, mechanic, grocery, coffee). The five business interiors + the store page already exist; this slice verifies and wires them for freshly placed doors. No generic "place" page, no fuel-station/La Strada page (PORTPLAN 37 declined). |
| POI timing + naming | **Unity's curves and the name pool stay untouched** (car wash at 6 streets, dealer at 10 jobs/37 streets, mechanic at 36 jobs, insurance 13, bank 15, stores per owned hub). Demo invention is limited to the **street identities** (Street 3, 4, …). |
| Fictional street geometry | **Label streets onto the existing 1,929 m lap.** Street 3+ reuse the two half-lap spans; house numbers, drive-to guidance and distances resolve. Known limitation: a fictional street's doors coincide in space with Street 1/2's doors (§10). |
| Zone standings | **Wire the existing Firestore board** behind a new `IZoneStandings` seam with an offline null-object, and show rank + delta on the expansion page. |

---

## 3. Target behaviour

1. A settled taxi ride or delivery (never a car haul) bumps the zone counter.
   Reaching the curve — 2 jobs, then 3, then 4–6 — registers the **next street
   on the map**, home street first, then map order: Street 2, Street 3,
   Street 4, … Each expansion adds that street's own length to the zone km.
2. Ports in the same growth then open any businesses whose street/job
   threshold is now met. The **first new door of the expansion goes on the new
   street**; further doors spread over the zone's existing streets (Unity's
   `ResolvePlacementTarget` rule). Every door is on a street the zone already
   covers, never on an undiscovered one.
3. The receipt's CONTINUE opens the **OPERATING ZONE EXPANDED** page first:
   new street name, street count, previous / new area, jobs to the next
   expansion, the businesses that just opened (type label + name), the world
   standings block, CONTINUE and FIND STREET. CONTINUE then lands on the page
   the receipt would have opened (control page or delivery board).
4. FIND STREET opens the addresses page — the port's map stand-in — with its
   BACK returning to the same continuation, so the button keeps Unity's
   contract without a map.
5. The addresses page carries the zone summary (streets, km, jobs to the next
   expansion, street names), which is the stand-in for Unity's map HUD line.
6. Every zone-spawned POI still opens its existing interior from its freshly
   placed door, with the procedural owner name from the name pool.

---

## 4. Design

### 4.1 Street model (data)

`Data/address_map.json` grows the fictional pool:

```json
{ "id": "street-3", "name": "Street 3", "fromFraction": 0.0, "toFraction": 0.5, "fictional": true }
```

* Street 1 (`0.0–0.5`) and Street 2 (`0.5–1.0`) stay exactly as they are.
* Street 3 … Street 10 alternate the two half-lap spans, 33 house numbers each
  (30 m spacing on the 1,929 m lap). Eight fictional streets give nine
  expansions from a fresh career — enough for the whole POI ladder (dealer at
  37 streets is out of reach, but the mechanic is job-gated at 36 and stays
  reachable).
* `Core/AddressStreet` gains `bool Fictional = false` (last optional
  parameter, so every existing construction keeps compiling). Why the flag:
  it keeps `AddressBook.MapLengthKm` meaning *the placeholder area's base
  length* (1.929 km, the boot seed for the zone and the starter bonus), and it
  labels the rows a real map will replace later. Without it the boot zone
  length would jump to 9.645 km and quietly nerf the starter bonus. The street
  pool itself is fixed content: nothing fetches or appends streets at runtime.
* `Core/AddressMap.Default` **stays at two streets** (it is the shipped
  fallback and the deterministic test fixture; `AddressPlanTests` pins it).
  A fallback build without `address_map.json` still expands through Street 2
  and then reports the pool as exhausted — degraded content, never a crash.

### 4.2 The operating zone owns the addressable area (Core)

`OperatingZone` currently knows counters and km only. It becomes the one owner
of "which streets are discovered":

```csharp
public OperatingZone(DriverState? saved, int seed, AddressMap map,
                     string? homeStreetId = null, double initialKm = 0.0)

public AddressMap ZoneMap { get; }          // cached, rebuilt on TryExpand
public AddressStreet? NextStreet { get; }   // null once the pool is exhausted
```

* `ZoneMap` = `AddressBook.ZoneStreets(map, StreetIds.Count, homeStreetId)`
  re-wrapped as an `AddressMap` (same scalars, filtered streets). Cached once
  per growth step, rebuilt inside `TryExpand` (single writer, invalidate on
  write — AGENTS.md › Caching; a new row lands in ARCHITECTURE.md §7a).
* `NextStreet` = the first street of the home-first order
  (`AddressBook.ZoneStreets(map, int.MaxValue, homeStreetId)`) at index
  `StreetIds.Count`, or null when the count already covers the map. Legacy
  saves (`home`, `s1`, …) need no migration: only the **count** is read, and
  the order is stable, so a two-street legacy save continues at Street 3.
* The root passes the profile's home street (`profileStore.Load()?.StreetId`)
  so a player whose home is Street 2 gets Street 1 as its first expansion;
  a null id degrades to plain map order.
* `TryExpand(streetId, lengthKm)` keeps its contract, but receives a real map
  street id from now on.
* The seeded threshold curve is unchanged (Unity parity, including the
  per-launch re-draw).

No new `AddressBook` lookup is needed (`ZoneStreets`, `MapLengthKm`,
`AverageStreetLengthKm` already exist); `MapLengthKm` and
`AverageStreetLengthKm` both count the non-fictional rows only, so the
placeholder's base length stays 1.929 km.

### 4.3 Growth returns what happened (Core record)

```csharp
public sealed record ZoneExpansion(
    string StreetName,
    int StreetCount,
    double PreviousKm,
    double NewKm,
    int JobsToNext);
```

`ZoneGrowth.Expand()` returns `ZoneExpansion?` instead of `void` (null when
the introduction is unfinished, the curve is not reached, or the street pool is
exhausted). Same gates as today: `IOnboardingStore.Load().IsComplete` and
`_zone.ExpansionDue`; the km push stays the same. The pool-exhausted case logs
and returns null — Unity's "Pool is empty" path, except the port does **not**
consume the counter for a street it cannot name (it stays due until a street
exists; the data file is the fix).

### 4.4 POIs are placed by the expansion (Core + world)

`PoiSpawner` learns two things:

```csharp
public IReadOnlyList<PointOfInterest> Spawn(
    int discoveredStreets, int settledJobs, PoiDirectory known,
    AddressMap addressable, string? openedStreet = null,
    Predicate<string>? ownsCategory = null)
```

* `addressable` is `zone.ZoneMap`: the random walk only picks streets the
  player has discovered, so no generated business ever appears off-zone.
* `openedStreet` is the expansion's new street: the **first** missing door of
  the expansion is forced there when that street still has a free house; the
  rest draw over the whole addressable map (Unity's rule). A full street falls
  back to the random walk instead of skipping the spawn.
* The curve comparison, the collision-free address draw, the id scheme
  (`type_n`) and the `{0}` name pattern stay untouched.
* `Place(...)` (owned hub doors) also takes the addressable map.

`PoiWorld.Grow()` becomes `Grow(ZoneExpansion? expansion = null)` and returns
the doors it banked:

```csharp
public IReadOnlyList<PointOfInterest> Grow(ZoneExpansion? expansion = null)
```

* Called by the coordinator at the settle (with the expansion), at boot and on
  the delivery board/hub close (without one) — the job-gated families
  (dealer/mechanic) keep Unity's `MapScreen.Show` catch-up behaviour.
* When the expansion changed the addressable map, the world refreshes the
  board/city adoption first (`AdoptDoors`) so a new store is on the delivery
  board before the expansion page lists it.

### 4.5 The expansion page (Overlay)

New `ViewModels/ZoneExpansionScreenViewModel` + `Views/ZoneExpansionScreenView.xaml`
+ a `DataTemplate` in `MainWindow.xaml`. Copy comes from resx, never code.

Layout (Unity parity, top to bottom):

| Row | Copy key | Value |
|---|---|---|
| Title | `ZoneExpansionTitle` | OPERATING ZONE EXPANDED |
| Label | `ZoneExpansionNewStreet` | New street discovered |
| Street | — | `streetName.ToUpper()` |
| Zone | `ZoneExpansionZoneLine` | Operating Zone: {0} streets |
| Previous area | `ZoneExpansionPreviousArea` | Previous Area: {0:0.0} km |
| New area | `ZoneExpansionNewArea` | New Total Area: {0:0.0} km |
| Next | `ZoneExpansionJobsToNext` | Jobs to next expansion: {0} |
| Businesses label | `ZoneExpansionNewBusinesses` | New businesses available |
| Business row | `ZoneExpansionPoiLine` | {0}: {1} — label from `PoiTypeLabels`, name as spawned |
| Standings | `ZoneStandingsLabel` / `ZoneStandingsRank` / `StandingsCopy` delta | rank + delta, hidden when the backend is offline |
| Buttons | `ZoneExpansionContinue`, `ZoneExpansionFindStreet` | CONTINUE, FIND STREET |

* `ViewModels/PoiTypeLabels` maps a POI type tag to a resx label (car wash,
  bank, insurance, dealer, mechanic, grocery, coffee); an unknown tag degrades
  to Unity's title-cased fallback (`GetFriendlyTypeLabel`). No type string is
  printed raw.
* `ViewModels/ZoneStandingsBlock` mirrors the existing `StandingsBlock`
  against `IZoneStandings` (reads `LastResult` on construct, follows
  `ResultUpdated`, `Unhook()` on leave, reuses `StandingsCopy.Delta`).
* `IVoiceScreen`: CONTINUE and FIND STREET speak their button labels through
  `VoiceActions`, same as the receipt pages.

Routing (no screen opens another screen — AGENTS.md):

* `Navigation.ShowZoneExpansion(ZoneExpansion zone, IReadOnlyList<PointOfInterest> added, Action next)`
  builds the page, calls `_zoneStandings.Refresh(zone.NewKm)` first, and wires
  CONTINUE to `next`.
* FIND STREET → `Navigation.AddressesPage(new RelayCommand(next))` (the
  existing addresses page constructor, extracted into one private builder so
  `OpenAddresses` and this route share it).
* `Navigation` gains `IZoneStandings` (constructor) and the `VoiceBeat` it
  already has.

Pending hand-off (the coordinator, one field):

* `MainViewModel` implements `IJobSettlementHost.GoToControlPage` /
  `GoToDeliveryBoard` as `ContinueAfterSettle(target)`:
  `if (_pendingExpansion is { } report) { _pendingExpansion = null; _nav.ShowZoneExpansion(report.Zone, report.Added, target); } else target();`
* `OnProgressBanked` composes the report: `var zone = _zoneGrowth.Expand();
  var added = _poiWorld.Grow(zone); _pendingExpansion = zone is null ? null :
  new ZoneExpansionReport(zone, added);`
* `ZoneExpansionReport` (ViewModels record) = the Core facts + the doors.
  Report only exists when a street actually joined; store-only or
  counter-only changes never open the page (Unity's `zoneExpanded || poiAdded`
  guard, minus the "businesses only" title the port has no use for yet).

### 4.6 Zone standings seam (Overlay + backend)

* New `Services/IZoneStandings`: `LastResult`, `ResultUpdated`, `IsAvailable`,
  `Start()`, `Refresh(double zoneKm)`.
* `FirestoreZoneStandings` implements it (`IsAvailable => true`); its unused
  `Push` is deleted so the km push keeps one writer,
  `IPlayerTelemetry.PushZoneKm` (already called by `ZoneGrowth`).
* New `Services/OfflineZoneStandings`: null result, no event,
  `IsAvailable => false` — the page hides the block (Unity compiles it out
  when rivals are disabled).
* `App.OnStartup`: construct it beside the shift standings (live inside the
  Firebase branch, offline otherwise), `Start()` at boot, pass it to
  `Navigation` and `TestSession`.

### 4.7 Zone-scoped addressing (required by the fictional pool)

The moment the map carries ten streets, every map-wide draw can send the
player to an undiscovered street. Unity never does that (draws come from the
discovered pool), so these three draws move to `zone.ZoneMap`:

* `Core/TaxiJobProposer` takes `OperatingZone` instead of `AddressMap` and
  draws on `zone.ZoneMap`. A one-street zone would fail today's
  "two different streets" draw, so `TaxiAddressPlan.TryDraw` relaxes to
  **a different house when a second street exists, the same street when it
  does not** (the proposal copy already handles a same-street second
  residence: `ProposalDoorResidence`).
* `ViewModels/ValuationFlow` takes `OperatingZone` and draws the venue on
  `zone.ZoneMap` (Unity draws a venue inside the zone).
* `ViewModels/WhereIsWillyFlow` takes `OperatingZone` and draws the elder's
  home and the shop filter on `zone.ZoneMap`.

Deliberately left map-wide, with the reason written next to the call:

* `PoiGenerator` — the introduction's errand and the friends' doors are
  scripted onboarding content, not zone draws (and their tests pin seeds).
* `PolicePatrolPlanner` — patrols are road events around the player, not
  destinations.

### 4.8 POI screens (verification, not new pages)

The seven families the zone spawns already own interiors through
`PoiArrivalRouter`: bank, dealer, car wash, mechanic, insurance
(`PoiWorld.Handlers`) and grocery/coffee (`StorePoiHandler`). This slice adds
no new interior and instead locks the wiring down:

| Family | Type tag | Handler | Page |
|---|---|---|---|
| Car wash | `carwash` | `PoiWorld.Handlers` | `CarWashScreenViewModel` |
| Bank | `bank` | `PoiWorld.Handlers` | `BankScreenViewModel` |
| Insurance | `insurance` | `PoiWorld.Handlers` | `InsuranceScreenViewModel` |
| Dealer | `dealer` | `PoiWorld.Handlers` | `DealerScreenViewModel` |
| Mechanic | `mechanic` | `PoiWorld.Handlers` | `MechanicScreenViewModel` |
| Grocery store | `grocery_store` | `StorePoiHandler` | `StoreScreenViewModel` |
| Coffee shop | `coffee_shop` | `StorePoiHandler` | `StoreScreenViewModel` |

One router test per family (a freshly placed door opens the expected page
type) is the deliverable; if a family turns out unclaimed, the fix is one
handler registration, never a branch in the entry path.

---

## 5. Data and content

* `Data/address_map.json`: add Street 3 … Street 10 (fictional), as above.
* `Data/pois.json`: unchanged (Unity's curves, name patterns, categories).
* `Resources/Strings.resx` + `Strings.nl.resx` (placeholders must survive
  translation, AGENTS.md › Data-driven):
  `ZoneExpansionTitle`, `ZoneExpansionNewStreet`, `ZoneExpansionZoneLine`,
  `ZoneExpansionPreviousArea`, `ZoneExpansionNewArea`,
  `ZoneExpansionJobsToNext`, `ZoneExpansionNewBusinesses`,
  `ZoneExpansionPoiLine`, `ZoneExpansionContinue`, `ZoneExpansionFindStreet`,
  `PoiTypeCarWash`, `PoiTypeBank`, `PoiTypeInsurance`, `PoiTypeDealer`,
  `PoiTypeMechanic`, `PoiTypeGrocery`, `PoiTypeCoffee`,
  `ZoneStandingsLabel`, `ZoneStandingsRank`, `ZoneStandingsUnknown`,
  `AddressesZoneLine`, `AddressesZoneNext`, `AddressesStreetsLabel`.
* No magic numbers: street spans/spacing live in the data file, the expansion
  curve in `OperatingZone`, POI pacing in `pois.json`.

---

## 6. Persistence and migration

* `driver.json` already stores `ExpansionJobs`, `ExpansionCount`,
  `StreetIds`, `ZoneKm` (`GameStateSaver.SaveDriver`). Nothing new is stored:
  the fictional street identity is derived from the map + the count, so a
  save with `["home"]` reopens on Street 1 and its next expansion claims
  Street 2; `["home","s1"]` claims Street 3.
* `ZoneKm` keeps growing by each street's own length; old zones keep their
  saved km (a saved length always wins, as today).
* `addresses.json` keeps the spawned doors; a door banked on Street 7 keeps
  working after a restart because `ZoneMap` is only used for **new** draws.
* No save-version bump: every new field is derived.

---

## 7. Tests

Core (`tests/AceDrive.Core.Tests`):

* `OperatingZoneTests` — `NextStreet` walks the home-first order; legacy ids
  keep counting; pool exhaustion answers null; `ZoneMap` is rebuilt on
  expansion (invalidate on write) and capped by the street count.
* `ZoneGrowthTests` — first/second expansion now register `street-2` /
  `street-3`; the returned `ZoneExpansion` carries street name, street count,
  previous/new km and jobs-to-next; the introduction gate and below-curve
  cases return null; the km push is unchanged; the pool-exhausted case
  returns null and consumes nothing.
* `PoiSpawnerTests` — a draw with `addressable` never leaves the discovered
  streets; the first door of an expansion sits on `openedStreet`; a full
  opened street falls back; ids stay unique; naming still `{0}` + name pool.
* `TaxiAddressPlanTests` — one-street map draws a same-street second address;
  two streets keep the different-street rule.
* `PoiWorldTests` — `Grow(expansion)` returns the banked doors, adopts stores
  before returning, and still places nothing before the introduction.
* `ZoneStandings` already covered; add a page-level test for `IsAvailable`
  false → block hidden.
* `JsonAddressMapStore`-style content test: street ids/names unique, spans
  inside `0..1`, at least one fictional row (guards a hand-edited data file).

ViewModels:

* `ZoneExpansionScreenViewModelTests` — title/street/zone/area/jobs copy from
  resx, the business rows in spawn order, FIND STREET dispatches the
  continuation, CONTINUE clears nothing but navigates, voice actions exist.
* `MainViewModelTests` — a settle that reaches the curve shows the expansion
  page on the receipt CONTINUE, and its CONTINUE lands on the control page /
  delivery board; a settle below the curve goes straight through.
* `NavigationTests` — FIND STREET opens the addresses page whose BACK runs
  the same continuation.
* `TestSession` gains `ZoneStandings` (fake) and builds `OperatingZone` with
  the map + profile street id, mirroring the root.

Every new collaborator gets a fake (`FakeZoneStandings`), one file each.

---

## 8. Verification

* `dotnet build ACEDRIVE.slnx`, `dotnet test ACEDRIVE.slnx`,
  `dotnet format ACEDRIVE.slnx --verify-no-changes` — zero warnings, all
  green, CRLF on every touched file.
* Live check: drive one taxi shift; the career's second settled job reaches the
  first expansion → the receipt CONTINUE must show the page with **Street 2**,
  the new area (+1.0 km as printed) and the next threshold. Continue claiming
  jobs until Street 6; the car wash must open **on Street 6** and be listed on
  the page, and its door must open the car-wash page.
* Faster check without grinding: stop the overlay, edit
  `%AppData%\AceDrive\driver.json` (`expansionJobs`, `expansionCount`,
  `streetIds`) to sit one job below a threshold, restart, settle one job.
  This is a test procedure, not a shipped cheat.
* Offline: without `config.json` the standings block stays hidden and the page
  is otherwise identical; with the Firebase config it must show a rank within
  a few seconds of opening (the board is loaded in the background).

---

## 9. Phases (each one green on its own)

| Phase | Content | Files (new → touched) |
|---|---|---|
| **P1 — addressable zone** | `Fictional` street flag, Street 3…10 data, `OperatingZone(map, homeStreetId)`, `ZoneMap`, `NextStreet`, `MapLengthKm` over base streets, `ZoneExpansion` record, `ZoneGrowth.Expand()` returning it | `Core/ZoneExpansion.cs`; `Core/AddressStreet.cs`, `Core/AddressBook.cs`, `Core/OperatingZone.cs`, `Overlay/ViewModels/ZoneGrowth.cs`, `App.xaml.cs`, `TestSession.cs`, `tests/*` |
| **P2 — POIs at the threshold** | `PoiSpawner` addressable + opened street, `PoiWorld.Grow(expansion)` returning doors, adoption order | `Core/PoiSpawner.cs`, `Overlay/ViewModels/PoiWorld.cs`, `App.xaml.cs`, tests |
| **P3 — expansion page** | VM + view + DataTemplate, `PoiTypeLabels`, resx en/nl, `Navigation.ShowZoneExpansion` + addresses builder, pending hand-off in `MainViewModel` | `Overlay/ViewModels/ZoneExpansionScreenViewModel.cs`, `Overlay/Views/ZoneExpansionScreenView.xaml(.cs)`, `Overlay/ViewModels/PoiTypeLabels.cs`; `MainWindow.xaml`, `Navigation.cs`, `MainViewModel.cs`, resx, tests |
| **P4 — zone standings** | `IZoneStandings`, offline null-object, Firestore implementation, `ZoneStandingsBlock`, wiring + fake | `Services/IZoneStandings.cs`, `Services/OfflineZoneStandings.cs`; `Services/FirestoreZoneStandings.cs`, `App.xaml.cs`, `Navigation.cs`, `tests/Fakes/FakeZoneStandings.cs`, tests |
| **P5 — zone-scoped addressing** | taxi proposer/plan, valuation venue, Willy draws on `zone.ZoneMap` | `Core/TaxiJobProposer.cs`, `Core/TaxiAddressPlan.cs`, `Overlay/ViewModels/ValuationFlow.cs`, `Overlay/ViewModels/WhereIsWillyFlow.cs`, `App.xaml.cs`, tests |
| **P6 — zone state on the stand-in** | addresses page header: streets count, km, jobs to next, street names | `Overlay/ViewModels/AddressesScreenViewModel.cs`, `Views/AddressesScreenView.xaml`, resx, tests |
| **P7 — docs** | PORTPLAN 58 note, ARCHITECTURE §7a row (`OperatingZone.ZoneMap`), collaborator table, this file's status line | `PORTPLAN.md`, `ARCHITECTURE.md`, `TOZONES.md` |

P1–P3 are the user-visible feature; P4 is the standings decision; P5 is the
correctness tail the fictional pool forces; P6 is the zone readout; P7 closes
the docs.

---

## 10. Risks and limitations

* **Coincident geometry.** A fictional street reuses a half-lap span, so two
  doors on different streets can sit at the same lap offset. Guidance, radius
  and race maths stay valid, but the town is not spatially consistent until
  the real map lands. This is the accepted cost of the "label onto the lap"
  decision (§2).
* **The street pool is fixed and ends at Street 10.** After that the zone is
  complete: the growth logs the completion once and the counter stays due, but
  no further expansion page appears. Changing the set is a content decision,
  not a runtime discovery feature — nothing is fetched or appended online.
* **One-street taxi zone.** Until the first expansion the zone covers one
  street; P5's relaxation keeps proposals working there. The copy already
  reads naturally ("the {surname} residence" on the same street).
* **Expansion page is receipt-bound.** Like Unity, the page only appears on a
  paid job's receipt CONTINUE. A player who closes the overlay before tapping
  CONTINUE loses the page (the street/POI growth is already banked); this is
  Unity parity, not a new hole. If we ever want it robust, the pending report
  would have to persist — out of scope.
* **POI curves mean the first business is late.** Unity's numbers put the car
  wash at 6 streets (~20 settled jobs). The page will list a new street only
  for the first several expansions; that is fidelity, not a bug.

---

## 11. Out of scope / follow-ups

* Fuel station, La Strada and any generic "place" page (declined / not asked).
* Real Overpass streets, house-number grounding and the freeroam map
  (PORTPLAN Step 1).
* HUD-banner notification on expansion (Unity's map HUD has no port target
  yet; the addresses page header is the interim readout).
* Restoring an expansion page after a panel close (§10).
* Scoping police patrols to the zone.
