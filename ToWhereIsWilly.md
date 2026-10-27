# ToWhereIsWilly — the weekly missing-person run (PORTPLAN items 27/45/52)

Port of LATESTDRIVEBS2's Wednesday "Where is Willy" quest: on Wednesday
08:00–20:00 game time the calendar row finds a missing elder hiding in one of
the town's coffee shops, pays a finder's fee for driving them home, moves
their friendship and leaves a Bring-Them-Home certificate in the car.

This dossier is the port plan. Its acceptance rule: **tapping the calendar
page's Wednesday row must start the same chain Unity's job-centre button
starts**, and every number, gate and phase below must match the Unity source,
with the adaptations listed in §7.

Confirmed decisions (2026-10-26):
- **Trigger:** the calendar's Wednesday row (already `ControlCalendar.Willy`,
  label `EventWilly`) is the job-centre button. Today the tap falls through
  `Navigation.OnDay` to the "waiting on the freeroam map" log; it must instead
  start the flow.
- **Gate:** Wednesday only, 08:00–20:00 game time, once per Wednesday
  (completion stamp), and only with the coffee career open — Unity's
  "UNLOCK COFFEE BEANS CAREER FIRST" state.
- **Exact mechanics:** the search (pick a coffee shop, wrong shop costs an
  attempt), the finder's fee formula, driving the elder home, the wrong-address
  penalty, GIVE UP SEARCH, the receipt page (paid on arrival, not on
  CONTINUE), the certificate and the friendship ±1.
- **Map stand-in:** the overlay has no marker map yet (PORTPLAN 59). The
  quest's "click a coffee shop" is a row on the quest page; the committed
  home address is pinned on the companion HUD. Data and visuals stay apart
  (generator → data → flow → page/HUD).
- **Persistence:** only the completion stamp is banked, exactly like Unity. A
  restart inside the quest loses the search — no receipt, no penalty, the row
  reopens. The paid/complete path is stamped and saved in the same call, so a
  kill can never pay twice.
- **No new polling.** Arrival rides the existing 10 Hz `Poll` through
  `ITelemetryHud.ParkedAtTarget`, like the market valuation's venue leg.

Unity source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2\Assets\Scripts`.

---

## 1. Unity source map

| File | What it owns |
|---|---|
| `UI/JobSelectionScreen.cs:476-541` | Wednesday button wiring, gate, completed check, shop/elder draw, start |
| `UI/JobSelectionScreen.cs:543-580` | Coffee-career label gate ("UNLOCK COFFEE BEANS CAREER FIRST") |
| `UI/JobSelectionCalendarView.cs:102-117, 142-179, 268-274` | Wednesday row, completed row, 8–20 gate, `IsCompletedToday` |
| `UI/Handlers/WhereIsWillyHandler.cs` | The whole quest state machine: search, reward, leg 2, wrong address, friendship |
| `UI/MapScreen.cs:1254-1257, 7571-7577` | Start hook from `NavigationEvents` |
| `UI/MapScreen.cs:2637-2653, 2883-2887` | Leg-2 and search task lines; the search task text branch |
| `UI/MapScreen.cs:3373-3415` | Drive button during search / leg 2 / DROP OFF |
| `UI/MapScreen.cs:4335-4369` | Wrong-address CONTINUE, GIVE UP SEARCH, leg-2 DRIVE flag |
| `UI/MapScreen.cs:4570-4629` | DROP OFF → receipt, wrong-address dismissal |
| `UI/MapScreen.cs:6924-6930, 7026, 7712-7723, 7794-7797` | Street-marker protection, reset on breakdown, home-house click, HUD lock |
| `UI/WillyReceiptScreen.cs` | Receipt copy, payment, certificate, completion stamp |
| `UI/ScreenArgs.cs:168-185` | `WillyReceiptArgs` (the receipt's data contract) |
| `UI/NavigationEvents.cs:48-51, 169-170` | `StartWhereIsWillyRequested` |
| `Models/PlayerData.cs:275-276`, `Models/PlayerDefaults.cs:146-147` | `lastWillyGameDay`, `lastWillyYear` |
| `UI/Handlers/ShopArrivalHandler.cs:135-153` | The non-Willy store-arrival branch that must stay reachable |

---

## 2. Exact Unity mechanics (copy these)

### 2.1 Timing and gates

- Wednesday only (`dayCode = 4`, 1 = Sunday), **08:00 ≤ hour < 20:00**
  (`JobSelectionCalendarView.CreateRow`, `OnWhereIsWillyClicked`).
- Once per Wednesday: `IsCompletedToday(player, lastWillyGameDay,
  lastWillyYear)` — the saved game day **and** year must both match today.
  The row then reads `Where are Willy and Lily (Completed)`, grey italic, not
  clickable. Only the receipt writes the stamp, so an abandoned or restarted
  search leaves the row open.
- Coffee career gate: the job-centre button label is
  `UNLOCK COFFEE BEANS CAREER FIRST` when the player has neither a
  `coffee_shop` POI nor an owned `coffee_beans` wholesale hub
  (`HasCoffeeBeansCareer`). The row itself stays the event text; the gate is
  guidance, not a hard refusal (Unity still runs the click's own checks).
- Outside the window a click flashes
  `COME BACK ON WEDNESDAY BETWEEN 8am AND 8pm`; when completed,
  `SEE YOU NEXT WEDNESDAY` (3 s, then the label restores).
- While the quest is in flight (search or elder on board) Unity blocks the job
  centre, YOU and the employees button (`willyOwnsHud`), and the map's job
  centre click returns early. The player cannot start another job.

### 2.2 The draw (job-centre click)

1. Re-check Wednesday 08–20 and the completion stamp; flash and return
   otherwise.
2. Pick a random **coffee shop that has an address**; its `town` becomes the
   quest town. No coffee shop → silent return.
3. Build the excluded-owner set: the `ownerName` of **every non-resident POI**
   (shop owners can never be the missing elder).
4. Candidates: residents (`type == "resident"`) with an address **in that
   town**, an `ownerName`, and a name not in the excluded set. If none, retry
   with residents of any town. If still none → silent return.
5. Draw one candidate, hide the job centre, raise
   `StartWhereIsWilly(town, coffeeShop, missingPerson)` and show the map.

### 2.3 Quest state (`WhereIsWillyHandler`)

Fields: active, town, location (the hiding shop), found, attempts,
missingPersonPOI, the elder's home `AddressData` (house number + street), home
arrived, wrong address, leg-2 driving, reward, average car value, base reward,
coffee-shop count, shop multiplier, attempt multiplier, year, week.

### 2.4 Search phase (`active && !found`)

- Task text (`GetTaskText`):
  `Old {fullName} has gone missing! Can you find our lovely elder in one of the
  coffeeshops and bring {firstName} home?` then
  `Click a coffee shop in {town} to see whether {firstName} is there.`
- The drive button is Unity's map button, not a fixed HUD action:
  - a POI selected → `DRIVE`;
  - no POI selected and `attempts > 0` → `GIVE UP SEARCH`;
  - no POI selected and `attempts == 0` → no button at all;
  - route fetching → `Loading route`.
- Arrival at a **coffee shop**:
  - the hiding shop → `found = true`, `CalculateReward`, street-name markers
    on, button hidden, task
    `Well that's a relief. All well and good! Missing person found! Select
    {homeShortAddress} to drive {firstName} back home.`;
  - any other coffee shop → `attempts++`, task
    `They aren't here. Navigate to another coffee shop on the map, or give
    up.`, `GIVE UP SEARCH` offered;
  - not active (free play) → the generic store handler keeps the arrival, so
    the shop opens normally.
- Arrival at a **non-coffee-shop door** during the search falls through to the
  shop handlers: the place opens as usual, the quest does not react.
- Street-name markers are re-asserted whenever the map is shown while the
  home address is still to be found (`OnShow`), so a hide never sticks.

### 2.5 The finder's fee (`CalculateReward`, exact)

```
averageCarValue = mean price of every owned car whose catalogue price > 0
baseReward      = averageCarValue * 0.04
coffeeShopCount = coffee shops in the quest town
shopMult        = <=2 → 1.00 | <=5 → 1.25 | <=9 → 1.50 | <=14 → 1.75 | else 2.00
attemptMult     = min(coffeeShopCount, max(0.5, 3.0 - attempts))
reward          = roundToInt(baseReward * attemptMult * shopMult)
reward          = max(reward, 100)
```

`attempts` counts failed shop visits only; a first-try find uses 3.0 (capped by
the shop count), so a one-shop town always multiplies by 1.0. `year` and
`week` are the calendar's current ones (the certificate names them).

### 2.6 Leg 2 — elder on board (`found && !homeArrived && !wrongAddress`)

- Tapping the **elder's own house-number icon** selects their resident POI,
  highlights the house and exempts it from the marker-hide pass
  (`IsHomeAddress` compares street case-insensitively plus the number).
- `DRIVE` marks `leg2Driving`, clears the task text, and the map shows only
  `Driving to {street} {houseNumber}...` while the drive is live; the button
  stays hidden for the whole leg.
- Arrival at the **home address**: `NotifyHomeArrived` — leg-2 flag off,
  markers released, `homeArrived = true`, handler inactive, street markers
  off, **friendship +1**, button `DROP OFF`, task `Welcome home {firstName}!`.
- Arrival at **any other address** while the elder is on board:
  `NotifyWrongAddress` — `wrongAddress = true`, handler inactive, markers off,
  **friendship −1**, button `CONTINUE`, task
  `This is not {firstName}'s home address. {firstName} is still out there
  somewhere...`.
- `CONTINUE` on the wrong address resets the quest completely: no receipt, no
  completion stamp, no reward — the elder stays missing and the row can be
  started again.
- `GIVE UP SEARCH` (search phase, no POI selected, `attempts > 0`) resets the
  quest and leaves the task `Search abandoned. {firstName} is still out there
  somewhere...`. No penalty, no stamp; the next tap draws a fresh shop/elder.
- A breakdown/accident reset also calls `ResetState`, so the quest dies with
  the interrupted drive.

### 2.7 Receipt (`WillyReceiptScreen`)

DROP OFF hides the map and opens the receipt. **The receipt pays on show, not
on CONTINUE:**

- `cash += finderFee`, `totalCREarned += finderFee`;
- `Bring-Them-Home Certificate Week {week} {year}` is added to the glove
  compartment with **volume 0**;
- `lastWillyGameDay = clock.gameDay`, `lastWillyYear = year`, then
  `SaveManager.Save()`.

Copy (Unity's exact lines):

```
Returned: {person}
Found at: {coffeeShop}

FINDER'S FEE
Base Fee = {baseReward:F0}CR
Coffee shops in town: multiplier x{shopMult:F2}
Found on 1st try: multiplier x{attemptMult:F2}      (attempts == 0)
Found on attempt {attempts+1}: multiplier x{attemptMult:F2}

REWARD: {finderFee} CR
Certificate: Bring-Them-Home Certificate Week {week} {year}
Certificate added to glove compartment
Friendship level: {friendshipLevel}
```

Unity's old distance-payment block is gone, so the finder's fee is the whole
reward. CONTINUE closes the receipt and returns to the map; the quest is over
and the row reads `(Completed)`.

### 2.8 Friendship

- `AwardFriend` finds the resident POI by key, `friendshipLevel++`, refreshes
  the friendsbook tag.
- `PenalizeFriend` does `max(0, level - 1)` and refreshes the tag.
- The receipt prints the level **after** the award (award happens on arrival,
  before DROP OFF).

### 2.9 Persistence

Only `lastWillyGameDay` and `lastWillyYear` are saved. The handler is
in-memory only: quits, breakdowns and wrong addresses throw the search away
without a stamp, and the event can be started again the same Wednesday.

---

## 3. ACEDRIVE mapping

### 3.1 Core (pure, no WPF/IO/telemetry)

New files, one type per file:

- `WhereIsWillyRules` — the balance record:
  `FinderFeeRate`, `MinReward`, `AttemptBase`, `AttemptFloor`,
  `IReadOnlyList<WhereIsWillyShopBand> ShopBands`; `Default` fallback.
  Methods: `ShopMultiplier(int shopCount)`, `AttemptMultiplier(int shopCount,
  int attempts)`, `int Reward(double averageCarValue, int shopCount, int
  attempts)`, `double AverageCarValue(IEnumerable<CarSpec?>)`.
  `Reward` uses `Math.Round(..., MidpointRounding.ToEven)` and whole CR —
  Unity's `Mathf.RoundToInt` is `Math.Round`, not away-from-zero.
- `WhereIsWillyShopBand` — `int? MaxShops` (JSON `null` marks the open-ended
  last band) + `double Multiplier`.
- `WhereIsWillyReceipt` — the receipt's data: `PersonName`, `FirstName`,
  `ShopName`, `FinderFee`, `AverageCarValue`, `BaseReward`, `ShopCount`,
  `ShopMultiplier`, `Attempts`, `AttemptMultiplier`, `Week`, `Year`,
  `FriendshipLevel`.
- `WhereIsWillyReceiptBuilder` — one `Build(rules, person, shop, attempts,
  ownedCars, shopCount, week, year, friendshipLevel)` that applies the rules
  once, so the page and the tests read the same numbers (the
  `TaxiReceiptBuilder` shape).
- `WhereIsWillyLedger` — the persisted half:
  `string? LastCompletedDate` (`yyyy-MM-dd` game date, invariant),
  `CompletedOn(DateTime gameNow)`, `MarkCompleted(DateTime gameNow)`.
  WHY a date and not Unity's `gameDay + year` pair: one string carries both
  halves, survives year boundaries and hand-edited saves, and the gate is
  "same Wednesday", not a day counter.
- `WhereIsWillyCandidates` (pure selection helpers): `EligibleResidents(...)`
  (drop every name in the venue-owner set) and `Pick(...)` over a list with the
  caller's `nextInt` — seeded RNG only, never the shared global.
- `ResidentBook` gains a read-only `Residents` view (it currently only exposes
  `GetOrCreate`), so the candidate filter can see the table Unity's
  `player.pois` gave its quest. `GetOrCreate` already banks the file, so the
  empty-book fallback (A3) persists the drawn elder in the same call.
- The free-house draw for that fallback reuses `PoiGenerator.DrawHouse(street,
  excludedNumbers)` over the map's streets (every directory and resident
  address excluded), so the elder can never share a door with a known POI.

`DriverState` gains one optional field, so old saves load unchanged:
`string? LastWillyDate = null`. No wallet change: the fee lands through
`DriverWallet.AddBonus` (cash + `CreditEarned`, both rounded 2 dp), which is
exactly Unity's `cash += fee; totalCREarned += fee`.

`PoiTypes.Willy = "willy"` — the flow's temporary marker type (the quest's
home, wrong-address and search destinations). It must not reuse
`PoiTypes.Person`: that type is the friend pickup's story arrival.

`Trunk` needs keepsakes with **0 L** (Unity's certificate has volume 0; the
current `Add` rejects volume ≤ 0): add `AddKeepsake(string name, int quantity)`
and let `Trunk`'s loader accept a saved stack with `UnitVolume == 0.0` (drop
only negative/non-finite rows). No capacity change: a certificate never takes
trunk space, like Unity's glove compartment card.

### 3.2 Data (balance in `Data/`, no literals)

`Data/willy_rules.json`:

```json
{
  "FinderFeeRate": 0.04,
  "MinReward": 100,
  "AttemptBase": 3.0,
  "AttemptFloor": 0.5,
  "ShopBands": [
    { "MaxShops": 2, "Multiplier": 1.0 },
    { "MaxShops": 5, "Multiplier": 1.25 },
    { "MaxShops": 9, "Multiplier": 1.5 },
    { "MaxShops": 14, "Multiplier": 1.75 },
    { "MaxShops": null, "Multiplier": 2.0 }
  ]
}
```

`JsonWhereIsWillyRules` (Services) loads it next to the exe, validates
(finite positive rate, `MinReward ≥ 0`, `AttemptFloor > 0`, ascending bands,
one open-ended last band) and degrades to `WhereIsWillyRules.Default` with a
warning — the `JsonTaxiRulesStore` pattern.

### 3.3 Flow (`WhereIsWillyFlow`)

Owns every rule and transition, no UI copy, no `new` of services, no disk on
the poll. Constructor (interfaces/values only): `AddressMap`, `GameClock`,
`PoiDirectory`, `ResidentBook`, `Garage`, `IReadOnlyList<CarSpec> catalog`,
`WhereIsWillyRules`, `WhereIsWillyLedger`, `IFriendshipStore`, `Trunk`,
`DriverWallet`, `ITelemetryHud?`, `GameStateSaver`, `IRivalsService`,
`VoiceBeat`, `Func<int> displayWeek`, `Random`, `ILog`. Attach
`IWhereIsWillyHost` late, exactly like `ValuationFlow`.

Phases (session state, never persisted):

```
Idle → Searching → Found → AtHome → Idle (receipt) / stamped
                      ↘ WrongAddress → Idle
```

`AtHome` and `WrongAddress` exist so the arrival check can never run twice on
the same parked car: the 10 Hz poll keeps seeing `ParkedAtTarget` until the
player drives off, and a re-awarded friendship (or fee) must be impossible.

- `Begin()` — the calendar tap:
  - refusals (log, return): not Wednesday 08–20; `_ledger.CompletedOn(game
    date)`; no coffee shop at all.
  - already running (`_phase` Searching/Found) → reopen the quest page instead
    of drawing a second search (Unity's blocked job centre).
  - `Draw()`: hiding shop = random coffee shop; town = `_map.City`; candidates
    = resident book minus every directory `OwnerName`; if empty, draw a free
    house (not on any known POI/resident address) and `ResidentBook
    .GetOrCreate` it (adaptation A3), then pick one.
  - `_phase = Searching`, present the quest page, pin nothing yet.
- `ChooseCoffeeShop(PointOfInterest shop)` — the page row's tap:
  pins the shop on the HUD (`HudDestinationCopy("HudWillySearchDrive")`), hides
  the panel. Unity's select + DRIVE in one tap.
- `Advance(TelemetrySample? sample)` — the one poll hook, after
  `hud.Update`:
  - `Searching && ParkedAtTarget && CurrentDestination is a coffee shop`:
    the hiding shop → `Find()` (reward computed now; the pinned shop is
    cleared, so a stray ENTER cannot open the shop, and the street markers have
    no port equivalent — the home row carries the address) → `_phase = Found`
    → present the found page; any other shop → `attempts++`, clear the pinned
    shop, present the page with the "They aren't here" line and GIVE UP.
  - `Found && ParkedAtTarget && CurrentDestination is not null`: compare its
    address with the elder's home; equal → `HomeArrived()` (friendship +1,
    `_phase = AtHome`, re-pin the home marker with the DROP OFF action); else
    → `WrongAddress()` (friendship −1, `_phase = WrongAddress`, re-pin a marker
    at that door with the CONTINUE action).
  - `AtHome` and `WrongAddress` resolve their last step through the router
    handler (ENTER), never on the poll, so the fee is paid once and the
    penalty applies once.
- `ChooseHome()` — the found page's DRIVE HOME row: pins the elder's home
  marker (`HudDestinationCopy("HudWillyHomeDrive", "HudWillyHomeArrived",
  "HudWillyDropOff")`), hides the panel.
- `Enter()` — the flow's `IPoiArrivalHandler` for `PoiTypes.Willy`:
  - home marker → pay, certificate, stamp, save, clear the destination, build
    the receipt page and return it (the router presents it);
  - wrong-address marker → reset, clear the destination, hide the panel,
    return null (the player is back on the HUD);
  - anything else → null.
- `Abandon()` — GIVE UP SEARCH (only offered when `attempts > 0`): reset, HUD
  notice with Unity's "Search abandoned…" sentence, hide the panel.
- `Continue()` — the receipt's CONTINUE: clear everything, `ShowControl()`.
- `Back()` — the quest page's BACK: hide the panel, keep the quest alive (the
  calendar row reopens it). Unity has no such verb because its map is always
  on screen; this is the port's one page affordance.

`IWhereIsWillyHost` (small, one job per member):
`HidePanel()`, `PresentQuest(object page)`, `ShowControl()`.
`MainViewModel` implements it with `PanelPresenter.Hide`,
`PanelPresenter.PresentJob` and `Navigation.ShowControl`.

`WhereIsWillyPoiHandler` (Views/VM layer, one type per file) adapts the router
to the flow: `Owns` = `PoiTypes.Willy`; `Open` = `flow.Enter()`. Registered in
`PoiWorld.Handlers(...)` beside the shop handler, first in the list.

### 3.4 Pages

**`WhereIsWillyScreenViewModel`** (+ `WhereIsWillyScreenView.xaml`), the
overlay's stand-in for Unity's map during the quest. Its `Phase` (Searching /
Found) decides the layout; BACK is the same page affordance in both:

- *Searching*: title `WHERE IS WILLY`, Unity's intro task text, the town's
  coffee shops as rows (name + `AddressFormatter`), the "They aren't here"
  line after a failed visit, `GIVE UP SEARCH` only when `attempts > 0`, BACK.
  A row taps → `ChooseCoffeeShop`; the page hides.
- *Found*: Unity's found sentence naming the short home address, one
  `DRIVE {firstName} HOME` row (the elder's door), BACK. A row taps →
  `ChooseHome`; the page hides.

Voice: every row answers to its label, GIVE UP and BACK answer theirs, all
through `IVoiceScreen` + `VoiceBeat` (the addresses-page shape).

**`WhereIsWillyReceiptScreenViewModel`** (+ view), Unity's receipt lines and
CONTINUE, `IKeepPanelOpen` + `IVoiceScreen` (the Cento receipt shape). It only
formats `WhereIsWillyReceipt`; the flow has already paid.

Both views join `MainWindow.xaml` as `DataTemplate`s; code-behind stays empty.

### 3.5 Calendar row states

`CalendarRow` gains `bool Completed` and `bool Locked` (default false). The
calendar page takes `WhereIsWillyLedger` and `Func<bool> hasCoffeeCareer`, and
for the Willy day:

| State | Label | Tap |
|---|---|---|
| open | `EventWilly` | dispatch + teal flash |
| completed today | `EventWillyCompleted` (`Where are Willy and Lily (Completed)` equivalent: `Where is Willy (Completed)`), grey italic | 3 s warning `ControlGateWillyDone` (`SEE YOU NEXT WEDNESDAY`) |
| coffee career closed | `EventWillyLocked` (`UNLOCK COFFEE BEANS CAREER FIRST`), grey | 3 s warning `ControlGateWillyLocked` |
| outside Wed 08–20 | normal | existing `ControlGateWarning` |

The page's existing 3 s warning slot carries both new warnings, so no new
timer. The view adds two `DataTrigger`s for the grey/italic state.

### 3.6 Wiring

| Point | Change |
|---|---|
| `Navigation` | ctor takes `WhereIsWillyFlow`; `_willyDay` read off `ControlCalendar.Week`; `OnDay(day == _willyDay)` → `_willy.Begin()` (a running quest reopens its page); the *other* event days plus `OpenTaxiShift` and `ShowDelivery` guard on `_willy.InFlight` (Unity's blocked job centre) |
| `MainViewModel` | ctor takes the flow; `Attach(this)`; `Poll` calls `_willy.Advance(s)` right after `_valuation.Advance(s)`; implements `IWhereIsWillyHost` |
| `CalendarScreenViewModel` | ctor takes the ledger + the career predicate; builds Completed/Locked labels and the two warnings |
| `PoiWorld` | the flow's handler joins `Handlers(...)` (registered first) |
| `GameStateSaver` | ctor takes `WhereIsWillyLedger`; `SaveDriver` writes `LastWillyDate` |
| `App.OnStartup` | load the rules; build the ledger from `savedDriver`; build the flow after `dayCycle`/`poiDirectory`/`residents`; pass ledger to the saver/nav/calendar, flow to nav/VM |
| `TestSession` | rules, ledger, flow, handler; expose flow + ledger in `TestSessionResult` |
| `Data/` + `JsonWhereIsWillyRules` | the new balance file and its loader |
| `ARCHITECTURE.md` | §7a: no new cache — say so; the flow's phase/draw/attempts are session state, not a cache |
| `PORTPLAN.md` | item 27 note: Willy landed; item 52 note: the Wednesday gate is wired |
| `HudVoiceKeywords` | `HudWillyDropOff` → the `DropOff` words, `HudWillyContinue` → the `Enter` words |
| `FIREBASE.md` | no change (local quest) |

### 3.7 resx (en + nl, no sentences in code)

```
EventWillyCompleted          Where is Willy (Completed)
EventWillyLocked             UNLOCK COFFEE BEANS CAREER FIRST
ControlGateWillyDone         SEE YOU NEXT WEDNESDAY
ControlGateWillyLocked       UNLOCK COFFEE BEANS CAREER FIRST

WillyTitle                   WHERE IS WILLY
WillyIntro                   Old {0} has gone missing! Can you find our lovely elder in one of the coffeeshops and bring {1} home? Click a coffee shop in {2} to see whether {1} is there.
WillyNotHere                 They aren't here. Navigate to another coffee shop on the map, or give up.
WillyGiveUp                  GIVE UP SEARCH
WillyFound                   Well that's a relief. All well and good! Missing person found! Select {0} to drive {1} back home.
WillyDriveHome               DRIVE {0} HOME

HudWillySearchDrive          Drive to {0}, {1} No. {2} in {3} and look for the missing elder.
HudWillyHomeDrive            Drive {0} home to {1} No. {2} in {3}
HudWillyHomeArrived          Welcome home {0}!
HudWillyDropOff              DROP OFF
HudWillyWrongAddress         This is not {0}'s home address. {0} is still out there somewhere...
HudWillyContinue             CONTINUE
HudWillyAbandoned            Search abandoned. {0} is still out there somewhere...

WillyReceiptTitle            MISSING PERSON RECEIPT
WillyReceiptReturned         Returned: {0}
WillyReceiptFoundAt          Found at: {0}
WillyReceiptFeeTitle         FINDER'S FEE
WillyReceiptBaseFee          Base Fee = {0}CR
WillyReceiptShopMult         Coffee shops in town: multiplier x{0:0.00}
WillyReceiptFirstTry         Found on 1st try: multiplier x{0:0.00}
WillyReceiptAttempt          Found on attempt {0}: multiplier x{1:0.00}
WillyReceiptTotal            REWARD: {0} CR
WillyReceiptCertificate      Certificate: Bring-Them-Home Certificate Week {0} {1}
WillyReceiptCertificateStored  Certificate added to the trunk of your car.
WillyReceiptFriendship       Friendship level: {0}
```

HUD copies keep the generic placeholders (`{0}` name, `{1}` street, `{2}`
number, `{3}` city) so translators can reorder them; the page copies take the
values the flow already resolved.

---

## 4. Acceptance: the calendar tap, step by step

1. Game time is Wednesday between 08:00 and 20:00, the coffee career is open,
   and this Wednesday has no stamp. The row reads `Where is Willy`.
2. Tap → teal flash, panel swaps to `WHERE IS WILLY` with the Unity intro
   sentence and the town's coffee-shop rows; the panel hides only when a row
   is tapped.
3. Tap a shop → the HUD pins it (`Drive to … and look for the missing elder`),
   the panel closes, the distance line counts down.
4. Park at the wrong shop → `attempts = 1`, the panel reopens with
   `They aren't here…` and `GIVE UP SEARCH`.
5. Tap the hiding shop, drive, park → `found`, reward computed, panel reopens
   with `Well that's a relief…` and `DRIVE {first} HOME`.
6. Tap that row → the elder's home is pinned (`Drive {first} home to …`).
7. Park at the home → friendship +1, `Welcome home {first}!`, button
   `DROP OFF`.
8. `DROP OFF` → paid, certificate in the trunk, stamp saved, receipt opens.
9. `CONTINUE` → panel back on the control page; the HUD is clear.
10. Reopen the calendar: the row reads `Where is Willy (Completed)`, grey; a
    tap answers `SEE YOU NEXT WEDNESDAY` and does nothing else.

Alternative branches that must also work:

- Wrong address: during leg 2 pin another door from the addresses page, park →
  friendship −1, `This is not {first}'s home address…`, `CONTINUE` clears the
  quest without paying or stamping.
- GIVE UP SEARCH after a failed visit → the quest clears, the HUD says
  `Search abandoned. {first} is still out there somewhere…`, no stamp, and the
  row can start a fresh draw.
- Outside Wednesday 08–20, or completed, or with no coffee shop: the row's
  warning appears and no quest starts.
- Restart mid-quest: nothing resumes, the row is open (Unity parity).

---

## 5. Tests

Core:
- `WhereIsWillyRulesTests`: shop multiplier at every band edge (2/3, 5/6,
  9/10, 14/15), attempt multiplier at 0/1/2/3/10 capped by the shop count, the
  100 CR floor, banker's rounding on a .5 case, average car value skipping
  priceless cars, `Default` values, `JsonWhereIsWillyRules` validation and
  fallback.
- `WhereIsWillyReceiptBuilderTests`: the exact fee/base/multipliers/attempt
  line data for a first-try find, a 3-attempt find, a one-shop town, an empty
  garage and a premium garage.
- `WhereIsWillyLedgerTests`: round-trip through `DriverState`, `CompletedOn`
  same date / next date / null, `MarkCompleted`.
- `WhereIsWillyCandidatesTests`: venue owners excluded (shop and hub names),
  empty candidate list, deterministic pick with a pinned seed.
- `TrunkTests`: `AddKeepsake` adds with 0 L, never consumes capacity, survives
  the snapshot/load round-trip.

Flow (`TestSession`, the `ValuationFlowTests` shape — Wednesday pinned via
`GameStart`, seeded rng, `FakeTelemetryHud.AtDestination`):
- Wednesday tap opens `WhereIsWillyScreenViewModel`, draws one shop + one
  elder, pins nothing; the shop list is the town's coffee shops.
- Row tap pins `PoiTypes.CoffeeShop` on the HUD and hides the panel.
- Park at the hiding shop → found page; park at another shop → attempts,
  not-here line, GIVE UP visible only from then on.
- Found page → home row pins `PoiTypes.Willy`; park at home → DROP OFF action
  on the HUD; `hud.RaiseEnter()` → receipt page; the fee, the certificate and
  the stamp land in one banked call; friendship +1.
- Wrong address: found, pin a non-home door, park → friendship −1, CONTINUE
  action; `RaiseEnter()` clears the quest, no cash, no stamp.
- GIVE UP after one attempt → HUD notice, no cash/stamp, `InFlight` false.
- Completed Wednesday → the calendar row is grey/completed, the tap warns and
  `Navigation.OnDay` never calls `Begin` again; next Wednesday reopens.
- No coffee shop → locked row, warning, no draw.
- Restart mid-quest (new session, same driver state) → no quest resumes, the
  row is open, nothing was paid.
- A stamped Wednesday pays nothing on a second entry attempt.
- Quest in flight → the other event rows, the taxi shift and the delivery
  career refuse; the Willy row reopens the quest page (Unity's blocked job
  centre).

---

## 6. Implementation checklist

Landed 2026-10-26. Two small deltas from §3.1/§3.7, recorded here so the spec
and the code stay one story: the flow takes the `PoiGenerator` the free-house
fallback (A3) needs, and §3.7 grew `WillyBack` (the quest page's one page
affordance) and `WillyCertificateName` (the item name the trunk banks and the
receipt line reuses, so the page and the certificate can never drift).

- [x] Core: `WhereIsWillyRules`, `WhereIsWillyShopBand`,
      `WhereIsWillyReceipt`, `WhereIsWillyReceiptBuilder`,
      `WhereIsWillyLedger`, `WhereIsWillyCandidates`, `PoiTypes.Willy`,
      `Trunk.AddKeepsake` + 0 L load, `DriverState.LastWillyDate`.
- [x] Data: `Data/willy_rules.json` + `JsonWhereIsWillyRules`.
- [x] Flow: `WhereIsWillyFlow`, `IWhereIsWillyHost`,
      `WhereIsWillyPoiHandler`, payment/certificate/stamp in one banked call.
- [x] UI: `WhereIsWillyScreenViewModel` + view, receipt VM + view,
      `MainWindow.xaml` templates, `HudVoiceKeywords` keys.
- [x] Calendar: `CalendarRow.Completed/Locked`, page labels + warnings, view
      triggers.
- [x] Wiring: `Navigation` (+`_willyDay` + job gates), `MainViewModel`,
      `GameStateSaver`, `PoiWorld` handler list, `App.OnStartup`,
      `TestSession`/`TestSessionResult`.
- [x] resx en + nl per §3.7.
- [x] Docs: this dossier's checklist, `ARCHITECTURE.md` §7a note,
      `PORTPLAN.md` item 27/52 note.
- [x] Tests per §5, then `dotnet build ACEDRIVE.slnx`,
      `dotnet test ACEDRIVE.slnx`,
      `dotnet format ACEDRIVE.slnx --verify-no-changes`, CRLF on touched files.

---

## 7. Adaptations and open items

1. **A1 — the map is a page.** Unity's coffee-shop markers and house-number
   icons have no port equivalent until PORTPLAN 59 lands. The quest page lists
   the town's coffee shops and the elder's home; the HUD carries the pinned
   door. Data still flows separately (directory → flow → page/HUD).
2. **A2 — no street markers.** The found sentence names the home address in
   the port's one shape (`HudAddress` → `Street 1 · No. 12`, city appended via
   `HudAddressWithCity`) and the home row pins the exact door. Unity's
   street-name reveal is a map affordance, not a rule.
3. **A3 — elder candidates.** Unity draws from its resident POIs (the taxi
   resident service fills them). The port's `ResidentBook` is that same table;
   when it is empty the flow draws one free house and banks a resident, so the
   event never dead-ends. Venue owners stay excluded, exactly as in Unity.
4. **A4 — glove compartment.** Not ported (PORTPLAN 51/61): the certificate is
   a 0 L trunk keepsake and the receipt says "trunk of your car", the same
   substitution the Cento receipt already made.
5. **A5 — copy and the lock.** The row keeps the port's `Where is Willy` label
   (and its "where is" voice alias, tuned against the logged mishearings)
   instead of Unity's "Where are Willy and Lily"; the completed variant follows
   the same wording. Unity's unlock text lives on the separate job-centre
   button, which does nothing when clicked without a coffee shop; the port's
   merged row shows that text as the row label and answers the tap with the
   same warning. Flag if you want Unity's exact plural instead.
6. **A6 — no resume.** Unity's quest is in-memory only; the port mirrors that
   and banks only the completion stamp (see §2.9). Do not add a mid-quest
   ledger without a parity decision: resuming would let a player dodge the
   attempt multiplier by restarting.
7. **A7 — the job-centre lock.** Unity disables the whole job centre, YOU and
   employees. The port blocks the only conflicting actions (another event via
   the calendar, the taxi shift, the delivery career) and keeps the panel
   callable, because the panel is also the quest's page host.
8. **A8 — wrong address reachability.** The rule and the penalty are ported in
   full; the branch is reachable by pinning any non-home door from the
   addresses page while the elder is on board. Once the marker network lands
   it becomes reachable the Unity way (tap any house).
9. **Open — audio.** Unity plays the money sound on the receipt. The overlay
   has no audio seam; the receipt page is the equivalent.
10. **Open — year.** The port does not persist a game year (PORTPLAN 51), so
    the certificate prints `CalendarStamp.Default.Year` (1924), the same fixed
    year every other page stamps.

---

## 8. Verification (after the build)

```powershell
dotnet build ACEDRIVE.slnx
dotnet test ACEDRIVE.slnx
dotnet format ACEDRIVE.slnx --verify-no-changes
```

Then a live EVO run: Wednesday 08:00, open the calendar, tap WHERE IS WILLY,
pick a coffee shop, park, confirm the wrong-shop line and GIVE UP, find the
hiding shop, drive the elder home, DROP OFF, read the receipt, CONTINUE, and
confirm the row reads completed and the trunk holds the certificate.
