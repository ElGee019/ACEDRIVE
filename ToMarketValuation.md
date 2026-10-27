# ToMarketValuation — weekly market valuation (PORTPLAN item 49)

Port of LATESTDRIVEBS2's Monday market-valuation showcase: drive to the
week's venue, put one car forward, and spend the rest of the week being
compared against every other entrant. The Unity mechanic is copied; the
venue *selection* is the one part that cannot be implemented yet (Step 1 of
PORTPLAN is still open), so this slice draws a random house on the two
placeholder streets and marks the real discovery as TODO.

Confirmed decisions (2026-10-26):
- **Venue reach:** the Monday calendar row pins a random `Street 1`/`Street 2`
  house on the HUD; driving there and parking opens the valuation page
  automatically (Unity's arrival trigger). The real Overpass town/street
  discovery stays TODO.
- **Comparison scope:** full Unity parity now — entry, weekly list, drawn
  competitor, verdict + XP, live newcomer wins/losses, offline catch-up,
  Monday wipe and the house entry.
- **Entry window:** Unity's exact Monday 08:00–20:00 game-time gate; one
  entry per player per week; the list survives until the Monday 00:00 wipe.
- **No new polling in the 10 Hz loop.** The live comparison is a background
  watcher (REST has no `Listen`); the gameplay poll only drains its queue.

Unity source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2\Assets\Scripts`.

---

## 1. Unity source map

| File | What it owns |
|---|---|
| `Utils/ValuationService.cs` | Window, radius, venue discovery (Overpass towns → streets), address formatting |
| `UI/JobSelectionScreen.cs:368-450` | Monday button label (`MARKET VALUATION IN {TOWN}`), gate, discover-on-tap |
| `UI/JobSelectionCalendarView.cs:209/259` | The Monday row's window and short-address label |
| `UI/Handlers/ValuationHandler.cs` | Marker, DRIVE, arrival, consume venue, open showcase, offline-arrival ENTER |
| `UI/ValuationScreen.cs` | Car picker, score, Firebase push, competitor draw, verdict, XP-on-Continue |
| `API/ValuationEntriesService.cs` | Firestore collection, TTL, wipe, competitor/unseen draws, local house entry |
| `UI/ValuationPopScreen.cs` | Live listener, win/loss toast, XP grant, offline catch-up, aggregate popup |
| `Utils/ValuationScoreService.cs` | The score formula and the two XP divisors |
| `Utils/ValuationOfflineService.cs` | Wins-only catch-up math |
| `Managers/GameClockManager.cs:145/275-350` | Reset-overdue flag, 6 am preload, Monday 00:00 wipe |
| `Models/PlayerData.cs:317-344` | Persisted venue, counter, last day, driving flag, seen set, own score, week key |

---

## 2. Exact Unity mechanics (copy these)

### 2.1 Timing

- Venue is drawn from **Monday 06:00** (two hours before the button reveals
  it), retried at `:00`/`:30` while Monday is still inside 06:00–20:00.
- Entry window: **Monday 08:00–20:00** only (`IsValuationWindow`).
- One entry per player per day: the Monday gate also reads
  `lastValuationGameDay` (`JobSelectionCalendarView.IsCompletedToday`), so a
  completed showcase cannot be replayed that Monday.
- The weekly list is wiped exactly on the **Sunday→Monday midnight** crossing
  (`IsWipeDue` = Monday 00:00). A restart that missed the tick catches up via
  `IsResetOverdue(lastResetGameDay, gameDay, dayOfWeek)`.
- Each entry carries `expiresAt = enteredAt + 48 h`; Firestore TTL removes it
  server-side (deletion may lag ~24 h, accepted, no client filtering).
- The **house entry** `John and Jane Doe` (`house-entry`) is generated
  client-side, per device, per week, with a freshly drawn random car. It is
  never written to Firebase by Unity; it is the fallback opponent.

### 2.2 Venue selection (Unity, not portable yet)

Two Overpass rounds: towns within `15 km + 2 km × valuationEventsCompleted`
of home (home town excluded), then named streets within 3 km of the picked
town; the venue is the street centre. No houses or house numbers are used.
`Provider = OverpassService`, discover-and-forget, nothing cached or pooled.

**Port rule until the Eifel map lands:** draw a random house on the two
placeholder streets (`AddressMap.Default` / `Data/address_map.json`),
uniformly over every house on both streets. Mark the real discovery with a
`// TODO(Step 1)` at `ValuationRules.PickVenue`.

### 2.3 Trigger chain

1. Calendar Monday row (8–20) is the job-centre button. Unity's label is
   `MARKET VALUATION IN {TOWN}`; the overlay's resx label is `EventValuation`
   (`Market valuation`).
2. Tap = locate phase: inject a temporary POI/marker at the venue
   (`ValuationHandler.BeginDriveToValuation`). The overlay's equivalent is the
   companion HUD destination (the addresses page already pins destinations).
3. Tapping the marker arms **DRIVE**; DRIVE routes to the venue and sets
   `player.valuationDriving = true` so an app kill resumes the leg.
4. Arrival (`OnDestinationReached`):
   - consume the venue (`valuationStreet/Town/House/Lat/Lon = 0`), so the same
     address is never reused after the event;
   - `lastValuationGameDay = gameDay`, `lastValuationYear = currentYear`,
     `valuationEventsCompleted++`;
   - clear the marker/alarm, deselect the POI;
   - **open `ValuationScreen` immediately** (no ENTER).
   - **ACEDRIVE deviation (2026-10-27):** parking alone only raises the
     companion's ENTER button; the showcase opens when the player taps it (or
     says "enter"). The arrival line is deliberately empty: once the car stands
     on the venue's house, "park here to enter your vehicle" is dead copy. The
     venue stays pinned and `valuationDriving` stays true until that ENTER, so
     the button survives parking, driving off and a relaunch.
5. Offline arrival (app killed after DRIVE): on next boot the marker is back
   and the button reads **ENTER**, which opens the showcase without re-driving;
   the same ENTER runs the consume/bookkeeping if the live arrival never ran.

### 2.4 Showcase screen

Entry phase:
- Owned cars listed (Unity clones the garage's car row); current car opens
  selected; six dealer-style preview values (price, engine, power, topspeed,
  0-60, RPM).
- Button reads `ENTER WITH {car}`; disabled with no car.
- ENTER records one doc per player (`{playerName, carId, jobsWithCar,
  engineBrokenPercent, score, enteredAt, expiresAt}`), keyed by anonymous uid
  (`SetAsync MergeAll`, so a second entry overwrites the same doc), and seeds
  the seen-uid set from the current list so pre-existing entries are never
  paid.

Action phase:
- Own side: player name, car image/name, `YOUR SCORE: {F2}`.
- Draw a random entry that is **not** the player (`SelectCompetitor`), else
  the local house entry.
- Verdict: ties favour the player. `won = own >= opponent`; gap =
  `abs(own - opponent)`; XP = `gap / 500` when won, a flat `0.1` consolation
  when lost — and never less than the consolation either way, so no verdict
  ever pays zero.
- Text: `YOU WON` / `YOU LOST`, the International Automotive Jury sentence,
  `You receive {xp:F2} XP`.
- XP is **not** paid until CONTINUE. A double click cannot pay twice.

Score (`ValuationScoreService.ComputeScore`):

```
carClass + maxSpeed / 2 + maxRpm / 250 + price / 1000
+ jobsWithCar + (1 - clamp(engineBrokenPercent, 0, 1)) * 100
```

Displayed with two decimals. `jobsWithCar` = completed taxi + delivery jobs
with that car (`CarJobsService.GetJobs`); `engineBrokenPercent` = the car's
engine state at entry time.

**ACEDRIVE addition — stretch leaderboard term (2026-10-27):** the port adds
`pow(leaderboardRanking, 2) * 150`, where `leaderboardRanking` is a per-owned-car
value in 0..1 (best percentile of its model's stretch boards). It starts on the
top percentile (1 = the full 150 points) because the stretch leaderboards do not
exist yet; the future board source writes the computed value through
`OwnedCar.SetLeaderboardRanking`, and `ValuationRules.LeaderboardRanking` already
implements the calculation (each stretch's `(field - rank) / field` percentile,
best three averaged). The house entry carries no ranking (0 points).

### 2.5 Live comparison (ValuationPopScreen)

- Firestore snapshot listener on the weekly list. First snapshot only seeds
  the seen set. A missing own entry re-seeds silently (covers the Monday
  wipe): winning or losing is impossible without an own entry.
- For every newcomer uid not seen and not own:
  - lower score → win, XP `max((own - newcomer) / 500, 0.1)`, popup
    `You won a Valuation event against {name} and received {xp:F3}XP`;
  - higher score → loss, the flat `0.1` consolation (never a share of the
    gap), popup `You just lost a Valuation Event against {name} (+{xp:F2} XP).`;
  - tie → ignored.
- Each popup is 4 s, plays the Completion sound, and grants XP when shown.
- Seen uid set + own score + week key are mirrored into the save.
- Offline catch-up on restart: one one-shot load diffs the list against the
  persisted seen set; **wins only** (losses while away pay nothing), each win
  at the live rate, one aggregate popup `While you were away you won N
  Valuation events and gained X XP`. The seen set is unioned, never overwritten.

---

## 3. ACEDRIVE mapping

### 3.1 Core (pure, no WPF/IO/telemetry)

- `ValuationRules` extensions:
  - `WinXpDivisor = 500`, `ConsolationXp = 0.1`, `MondayDayOfWeek = 2`.
  - `IsWindow(dayOfWeek, hour)` → Monday 8–20.
  - `IsPreloadWindow(dayOfWeek, hour)` → Monday 6–20.
  - `WeekKey(DateTime)` → whole weeks since Monday 2024-01-01; the weekly
    reset/entry gate.
  - `PickVenue(AddressMap, Func<int,int>)` → random house, TODO for Step 1.
  - `Score(CarSpec?, jobsWithCar, engineBrokenPercent, leaderboardRanking)`.
  - `LeaderboardRanking(ranks)` → the 0..1 ranking from `StretchRank` rows
    (`(field - rank) / field` per stretch, best three averaged, 0 with none).
  - `LeaderboardPoints(ranking)` → `ranking² × LeaderboardMaxPoints` (150).
  - `Verdict(ownScore, competitorScore)` → `ValuationVerdict`.
  - `SelectCompetitor(entries, ownUid, nextInt)` (skip self + `house-entry`).
  - `FindUnseen(seenUids, entries, ownUid)`.
  - `ComputeOfflineGains(ownScore, seen, entries, ownUid)`.
  - Keep the existing `IsWipeDue`, `IsResetOverdue`, `PickRandomCarId`,
    `ComputeExpiryUtc` (the last one is used by the Firestore service).
- New types, one per file:
  - `ValuationEntry` (uid, playerName, carKey, jobsWithCar,
    engineBrokenPercent, score, enteredAt).
  - `ValuationVenue` (street id/name, house number, `AddressLocation`).
  - `ValuationVerdict` (won, gap, xp).
  - `ValuationOfflineGains` (totalXp, winCount, newSeenUids).
  - `ValuationLedger` (persisted half): venue, driving flag, events
    completed, entry week/car/score, seen uids, last reset week.
  - `StretchRank` (sameCarRank, totalSameCarPlayers): one stretch's standing
    for a car model, the input the leaderboard term is computed from.
- `DriverState` gains nine optional fields (old saves load unchanged):
  `ValuationVenueStreetId`, `ValuationVenueHouseNumber`, `ValuationDriving`,
  `ValuationEventsCompleted`, `ValuationEntryWeek` (`-1`), `ValuationEntryCarKey`,
  `ValuationOwnScore`, `ValuationSeenUids`, `LastValuationResetWeek` (`-1`).
- `DriverWallet.AddXp(double)` for the verdict/result grants (cash stays put).
- `OwnedCar.JobsCompleted` + `RegisterJob()`; `OwnedCarState` gains
  `JobsCompleted = 0`. `JobSettlement` increments the current car's counter
  in the `earnsProgress` block at both settles (taxi and delivery).
- `OwnedCar.LeaderboardRanking` + `SetLeaderboardRanking(double)`;
  `OwnedCarState` gains `LeaderboardRanking = 1.0` (the top-percentile
  placeholder until the stretch boards land). It rides the garage save per
  owned car and feeds the score's leaderboard term at ENTER.
- `PoiTypes.Valuation = "valuation"` for the temporary venue marker.

**Score deviation to approve:** EVO's `cars.json` has no `maxRpm` column.
The port keeps every other term exactly and substitutes `horsepower / 10`
for `maxRpm / 250` (same point scale, ~20–65 vs ~24–36). This is flagged in
the code comment; if RPM data arrives, swap the term and the scores change
retroactively for new weeks only (old entries keep their stored score).

### 3.2 Services

- `IValuationBoard` (Services): `IsOnline`, `OwnUid`, `ListAsync`,
  `PushEntryAsync`, `WipeOwnAsync`, `StartWatching`/`StopWatching`,
  `event Changed`.
- `FirestoreValuationBoard`: wraps the existing `FirestoreValuationEntries`,
  maps docs through `ValuationEntryMapper`, filters the legacy cloud
  `house-entry` doc, and runs a **30 s background watcher** while an entry is
  live. WHY: Firestore REST has no `Listen`; this task is off the 10 Hz loop
  and only raises `Changed`.
- `ValuationEntryMapper`: doc → `ValuationEntry` (`uid`, `playerName`,
  `carId`, `jobsWithCar`, `engineBrokenPercent`, `score`, `enteredAt`).
- `OfflineValuationBoard`: empty list, push returns true, watcher no-op.
- `FirestoreValuationEntries` correction: add `WipeOwnAsync(uid)` and use it
  for the weekly reset. Unity wipes **only the player's own doc**; the
  existing `ResetAsync` deletes the whole collection and must not be the
  weekly path. Keep `ResetAsync` only for the backend-parity test/legacy.
  The 48 h `expiresAt` already handles stale rival docs.

### 3.3 Flow (`ValuationFlow`)

Owns all timing/state transitions; no UI copy, no direct `new` of services.

- `Attach(IValuationHost)` — re-pins a saved in-flight drive (offline arrival).
- `BeginDrive()` — gate on Monday 8–20, draw/pin venue, set
  `valuationDriving`, save, hide panel, log.
- `Tick(now)` — Monday reset, 6 am venue draw, offline catch-up, drain board
  snapshots, drain draw/catch-up tasks, show queued 4 s notices.
- `EnterVenue(poi)` — the venue's POI handler (`ValuationPoiHandler`): when
  driving, consume the venue, `valuationEventsCompleted++`, clear the HUD
  destination, build and return the showcase page. Parking itself does nothing;
  the HUD's ENTER (voice "enter") is the only way in, so the arrival copy is
  never a dead sentence. Driven by the HUD's `EnterRequested` through
  `OnboardingFlow.EnterPoi` → `PoiArrivalRouter`, the same path a shop door
  takes.
- `Enter(carKey)` — score from garage + per-car jobs + engine state; persist
  week entry; push async; start watcher; start competitor list task.
- `Continue(xp)` — add XP, save, back to control page.
- `Cancel()` — entry phase BACK; no entry recorded.
- Live snapshot processing: own entry missing → reseed silently + stop;
  otherwise queue win/loss results, union seen, save.
- Offline catch-up: one list read when `ValuationEntryWeek == currentWeek`
  and `OwnUid` is available; wins-only aggregate result; retry after 60 s on
  a failed read.
- Result queue: grant XP, `hud.ShowNotice(..., 4 s)`, one notice at a time
  (Unity's queued toasts).

### 3.4 Screen

- `ValuationScreenViewModel` (entry + comparison phases):
  - picker of owned cars (`GarageRow`), stats (`GarageStatRow`), current car
    selected, `ENTER WITH {car}`.
  - comparison: own name/car/score, opponent name/car/score, verdict,
    jury sentence, XP line, CONTINUE. BACK only before ENTER.
  - `IKeepPanelOpen`, `IVoiceScreen` (car titles, ENTER, BACK, CONTINUE).
  - It never computes scores or talks to the board/wallet; the flow does.
- `ValuationScreenView.xaml` + empty code-behind, same page skeleton as
  Garage/Cento; add `<DataTemplate DataType="{x:Type vm:ValuationScreenViewModel}">`
  in `MainWindow.xaml`.
- `IValuationHost`: `HidePanel()`, `ShowControl()`. The showcase page reaches
  the panel through the venue's POI handler, so the host has no page verb.
  `MainViewModel` implements it with `PanelPresenter.Hide`,
  `Navigation.ShowControl`.

### 3.5 Wiring

| Point | Change |
|---|---|
| `Navigation` | ctor takes `ValuationFlow`; `_valuationDay` read from `ControlCalendar.Week`; `OnDay(valuationDay)` → `_valuation.BeginDrive()` (calendar gate already checks day + 8–20) |
| `MainViewModel` | ctor takes `ValuationFlow`; `Attach(this)`; `Poll` calls `_valuation.Tick(now)` after `_day.Tick`; implements `IValuationHost` |
| `GameStateSaver` | ctor takes `ValuationLedger`; `SaveDriver` writes the nine valuation fields with `with { … }` |
| `App.OnStartup` | build `ValuationLedger`; choose `FirestoreValuationBoard` (online branch, after `_firebase`) or `OfflineValuationBoard`; build `ValuationFlow` before `Navigation`; register `ValuationPoiHandler` on the arrival router; pass ledger to saver and flow to nav/VM |
| `TestSession` | `FakeValuationBoard` option, ledger, flow, nav/VM/saver params; expose the flow for integration tests |
| `firestore.rules` | already allows `acevo_enteredForValuation`; no change |
| `FIREBASE.md` | move weekly valuation entries from “backend-only” to wired; document the live watcher and own-entry wipe |
| `PORTPLAN.md` | item 49 note: valuation slice landed / remaining Star-of-Show, NPC field |

### 3.6 resx (en + nl)

Add to `Strings.resx` and `Strings.nl.resx`; no sentences in code.

```
ValuationTitle             MARKET VALUATION
ValuationVenueLine         This week's venue: {0}
ValuationIntro             Choose the car you want the jury to value this week.
ValuationSelect            Select car
ValuationNoCars            There is no car in the garage to enter.
ValuationEnter             ENTER WITH {0}
ValuationEnterEmpty        ENTER
ValuationBack              BACK
ValuationContinue          CONTINUE
ValuationOwnScore          YOUR SCORE: {0}
ValuationOpponentScore     OPPONENT'S SCORE: {0}
ValuationScore             {0:0.00}
ValuationWaiting           Drawing an opponent…
ValuationWon               YOU WON
ValuationLost              YOU LOST
ValuationJuryWon           The International Automotive Jury values your car higher than your opponent's!
ValuationJuryLost          The International Automotive Jury values your car lower than your opponent's!
ValuationXp                You receive {0:0.000} XP
ValuationYou               You
ValuationTopSpeedLabel     Top speed
ValuationAccelerationLabel 0-60
ValuationClassLabel        Class
ValuationTopSpeed          {0} km/h
ValuationAcceleration      {0:0.#}s
ValuationClass             {0}
HudValuationDrive          Drive to {0}: {1} {2}, {3} to enter your vehicle into the weekly tier-ranking showcase.
ValuationNoticeWin         You won a Valuation event against {0} and received {1:0.000}XP
ValuationNoticeLoss        You just lost a Valuation Event against {0}.
ValuationOfflineSummary    While you were away you won {0} Valuation events and gained {1:0.000} XP
```

(HUD copies keep the generic placeholders `{0}` name, `{1}` street,
`{2}` number, `{3}` city so a translator can reorder them.)

The venue's action button reuses the generic `HudEnter` copy ("ENTER" /
"BINNENGAAN") and therefore its voice keyword "enter"; its copy carries an
empty `ArrivedKey`, which tells the HUD to leave the arrival sentence down
(`HudDestinationCopy`). The old `HudValuationArrived` key is gone.

---

## 4. Tests

Core:
- `ValuationRulesTests` additions: window/preload edges, week key boundary
  (Sunday 23:59 → Monday 00:00), venue draw spreads over both streets and
  respects house counts, score with/without a car and broken clamp, verdict
  win/loss/tie and XP divisors, competitor skips self + house, unseen draw,
  offline wins-only/tie ignored/seen union.
- `ValuationLedgerTests`: round-trip all fields, reset clears entry/venue/seen
  and stamps the week, `HasEntry` only for the stored week, stale seen dedupe.
- `OwnedCar` job counter round-trip.

Services:
- `ValuationEntryMapperTests` / `FirestoreValuationBoardTests`: field mapping,
  legacy house doc filtered, push path calls the underlying service, own
  wipe path uses the uid, offline board never reports rivals.
- `FirestoreValuationEntriesTests`: add `WipeOwnAsync` deletes only the
  caller's path (keep the existing full-reset test for `ResetAsync`).

Flow/integration (`TestSession`):
- Monday 08:00 start → `vm.Poll()` draws a venue; `BeginDrive` pins the HUD
  and hides the panel; the pin carries the `HudEnter` action and no arrival
  copy. Fake HUD parked → the button is up and the showcase is **not** open;
  the fake HUD's `RaiseEnter` (the same event the button and the mic raise)
  opens `ValuationScreenViewModel` and consumes the venue.
- ENTER with an owned car pushes one entry, computes the Unity score
  (including per-car jobs + engine), draws the fake board's rival, paints
  win/loss and pays XP only on CONTINUE.
- Empty board → house entry fallback.
- `FakeValuationBoard.RaiseChanged` with a lower/higher newcomer → HUD notice,
  XP once, seen persisted; a second snapshot does not pay again.
- Restart inside the week with persisted seen + score → catch-up pays wins
  only and shows the aggregate notice; missing own doc reseeds.
- Monday 00:00 tick → own entry gone, venue cleared, wipe called, fresh week.

---

## 5. Implementation checklist

- [x] Core: `ValuationRules` extensions + five new types + `ValuationLedger`.
- [x] Persist: `DriverState` fields, `DriverWallet.AddXp`, `OwnedCar.JobsCompleted`,
      `OwnedCarState`, `JobSettlement` increments, `GameStateSaver`.
- [x] Services: `IValuationBoard`, `FirestoreValuationBoard`,
      `OfflineValuationBoard`, `ValuationEntryMapper`,
      `FirestoreValuationEntries.WipeOwnAsync`.
- [x] Flow: `ValuationFlow` + `IValuationHost`.
- [x] UI: `ValuationScreenViewModel`, `ValuationScreenView.xaml(.cs)`,
      `MainWindow.xaml` template, resx en/nl.
- [x] Wiring: `Navigation`, `MainViewModel`, `App.OnStartup`, `TestSession`.
- [x] Docs: `FIREBASE.md`, `PORTPLAN.md` item 49 note, `ARCHITECTURE.md`
      cache/anomaly rows if the watcher stays.
- [x] Tests per §4, then `dotnet build ACEDRIVE.slnx`,
      `dotnet test ACEDRIVE.slnx`,
      `dotnet format ACEDRIVE.slnx --verify-no-changes`, CRLF on touched files.

---

## 6. Open items / risks

1. **Real venue selection** — blocked on PORTPLAN Step 1 (Eifel map + street
   names). The placeholder draw is explicitly marked TODO. The Unity radius
   (15 km + 2 km per completed event) is not used until then.
2. **maxRpm** — no column in `cars.json`; horsepower/10 substitution needs
   sign-off (or an RPM data pass before launch).
3. **REST live comparison** — Unity's listener is immediate; the port's
   background watcher re-reads every 30 s. If Firestore adds a realtime REST
   path later, replace `FirestoreValuationBoard.WatchAsync`.
4. **House entry** — Unity's is local-only. The legacy `ResetAsync` writes a
   shared `house-entry` doc; the new board filters it. Decide whether to keep
   writing it for older clients or drop it in the next backend cleanup.
5. **One entry per Monday** — the venue is consumed on arrival, so the same
   day cannot be replayed. If a player re-enters via a stale screen, the
   `MarkEntry` overwrite matches Unity's `MergeAll`.
6. **Audio** — Unity plays the Completion sound on each toast; the overlay has
   no audio seam for it. The HUD notice is the port's equivalent.
7. **Voice** — the screen's spoken labels are wired through `IVoiceScreen`;
   the "market valuation" calendar keyword already exists.
8. **Stretch leaderboards** — not built yet. Every owned car carries
   `LeaderboardRanking = 1` (full 150 points) until a board source reports
   per-stretch, same-model ranks; the seam is
   `OwnedCar.SetLeaderboardRanking(ValuationRules.LeaderboardRanking(rows))`
   at ENTER (`ValuationFlow.Enter`'s TODO). Until then the player's entry
   out-scores the house fallback by the 150-point placeholder.

---

## 7. Working tree at the time this dossier was written

The slice is complete and wired; the note below records where it started.

Original state of the first slice (before this pass): untracked and **not yet
wired** into `App`, `Navigation`, `MainViewModel`, `GameStateSaver` or
`TestSession`, so the feature did not run.

Files that carried the slice into the tree:
`src/AceDrive.Core/ValuationEntry.cs`, `ValuationLedger.cs`,
`ValuationOfflineGains.cs`, `ValuationVenue.cs`, `ValuationVerdict.cs`;
`src/AceDrive.Overlay/Services/IValuationBoard.cs`,
`FirestoreValuationBoard.cs`, `OfflineValuationBoard.cs`,
`ValuationEntryMapper.cs`;
`src/AceDrive.Overlay/ViewModels/IValuationHost.cs`,
`ValuationFlow.cs`, `ValuationScreenViewModel.cs`;
`src/AceDrive.Core/DriverState.cs`, `DriverWallet.cs`, `GarageState.cs`,
`OwnedCar.cs`, `PoiTypes.cs`, `ValuationRules.cs`;
`src/AceDrive.Overlay/Services/FirestoreValuationEntries.cs`,
`GameStateSaver.cs`;
`src/AceDrive.Overlay/ViewModels/JobSettlement.cs`, `Navigation.cs`,
`MainViewModel.cs`; `src/AceDrive.Overlay/App.xaml.cs`,
`MainWindow.xaml`, `Views/ValuationScreenView.xaml(.cs)`,
`Resources/Strings.resx`, `Strings.nl.resx`;
`tests/AceDrive.Core.Tests/` (`ValuationRulesTests`, `ValuationLedgerTests`,
`ValuationEntryMapperTests`, `FirestoreValuationBoardTests`,
`FirestoreValuationEntriesTests`, `ValuationFlowTests`, `GarageTests`, and the
`TestSession`/`TestSessionOptions`/`TestSessionResult` graph).

**Build note:** builds and test runs need full access in this session: the
workspace tree carries the `Everyone:(DENY)(DC)` grant on `obj`, so a sandboxed
`dotnet build` fails at restore with no diagnostic. `dotnet test` also reports
"Zero tests ran" on this machine even with full access (the xUnit v3/MTP host
never reaches the run); `dotnet run --project tests/AceDrive.Core.Tests` and
the built `AceDrive.Core.Tests.exe` run all 2014 tests in about a second, so
that is the verification path used here. Nothing in the test project config was
changed.

---

## 8. Verification (after wiring)

```powershell
dotnet build ACEDRIVE.slnx
dotnet run --project tests/AceDrive.Core.Tests        # `dotnet test` is broken here, see §7
dotnet format ACEDRIVE.slnx --verify-no-changes
```

Verified in this pass: the solution builds with zero warnings, the full suite
passes (2014 tests, 0 failed), and the new valuation suite covers the schedule,
the ledger round-trip, the mapper/board plumbing, the Monday drive → arrival →
entry → verdict path, the live newcomer notices, the wins-only catch-up and the
Monday wipe.

Then a live EVO run: Monday 08:00 game time, tap the calendar's MARKET
VALUATION row, drive to the pinned house, park → showcase opens; enter a car;
restart inside the week and confirm the catch-up notice; let Monday 00:00
pass and confirm the entry/venue clear.
