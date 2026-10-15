# DEPENDENCIES — what influences what in LATESTDRIVEBS2 gameplay

Notation: `A -> B` means **A influences B** (B's numbers, availability or gates are decided by A).
Scope: gameplay systems only, as they exist in `Assets/Scripts` (code survey, not design docs).
Class names per system: appendix at the end.

---

## 1. The big picture

```
CONFIG (ScriptableObjects) -----------------------------------> everything (balance numbers)
SAVE / PlayerData <------------------------------------------> everything (reads + writes)
GAME CLOCK ---> Calendar ---> all weekly/daily gated content
MAP DATA (streets/houses/POIs) ---> Driving, all jobs, POI placement, names
DRIVING ---> fuel, wear, accidents, fines, job accounting, race/road-test physics
JOBS (taxi/delivery/hauling/care) ---> money, XP, dirt, friendships, todos, zone growth
ZONE GROWTH ---> more streets/houses/shops ---> better jobs   (loop back)
ONLINE (Firebase) ---> decoration only: standings, rivals, rarity, sprites
```

Rule that holds in the shipped code: influence flows downward only — data systems never
depend on gameplay, and screens never influence state (they display it).

---

## 2. The five hubs (what almost everything hangs on)

| Hub | Directly influences |
|---|---|
| **Save / PlayerData** | Every system: it is the only state. No money, jobs, cars or progress without it. |
| **Game Clock** | Hunger, fuel price (daily), insurance premiums (01:00), car-share bookings (00:15) + arrival alarms, employee trip arrivals, store stock depletion, engine weekly/annual checks, race route preload (Sun 04:00), valuation venue preload (Mon 06:00), week rollover, daily login streak. |
| **Calendar (week/year)** | Dealer stock, road-test car of the week, employee roster, race week, and the weekday gates (Sun race, Mon show, Tue road test, Wed Willy, Fri Grandma). |
| **Map data (StreetPool / Overpass / houses)** | Which streets exist, house pairs for taxi, shop POIs for delivery, police patrol coverage, every POI placement rule, name-pool region match, zone expansion candidates. |
| **Driving** | Fuel burn, odometer, part wear, accident rolls, speed-fine checks, job distance/fare, ETA + job alarms, race NPC progress, road-test and car-haul borrowed legs. |

---

## 3. Influence chains

### Time & calendar
- Clock -> hunger (+1/hour; pancake pauses it)
- Clock -> fuel price (daily random walk, clamped 1.4–2.2) -> refuel cost -> job profit
- Clock -> insurance premium bill -> cash -> willingness to stay insured
- Clock -> store stock depletion (90-min ticks) -> see Town economy below
- Clock -> car-share nightly booking; employee arrivals; offline-job alarms
- Clock -> Calendar -> weekly content refresh (dealer stock, road-test car, roster, race)
- Calendar -> every weekday-gated story event: if the day is wrong, the event is simply absent

### Cars
- Current car choice (Garage) -> top speed, tank size, trunk volume, brake distance
  -> which jobs are possible (trunk caps delivery load; tank caps distance)
- Driving km -> part wear (8 parts) -> breakdown chance -> tow fee + failed job + lost cargo
- Driving km -> engine health decay -> annual inspection -> engine can strand -> repair costs
- Driving km + car class -> valuation score -> Monday show result
- Accident roll (busy days, >=13 streets) -> repair cost (fraction of car value) -> insurance payout
- Dealer stock (Calendar week seed + car database + money) -> garage fleet -> everything above
- Job count -> car wash dirt accumulates -> dirty car -> taxi refusals + customer grumbles -> income
- Money -> wash, refuel, repairs, insurance, new cars -> keeps the above cycles running

### Jobs & money
- Taxi: map houses -> job pair; name jar -> passenger; hunger gate -> can start at all;
  fuel/cleanliness -> can finish; police streets -> fine vs tip; distance x class x rarity -> fare;
  tips -> friendship; fare -> money, XP, shift stats, bank interest, todo counters, zone growth.
- Delivery: owned hub (CredP) -> wholesale cart (escrow money); trunk volume -> cart size;
  shop POI + stock state -> destination + sell price; delivered -> shop stock +20, profit,
  CredP ramp -> bigger hub tiers; also dirt, XP, employee-mirror deliveries.
- Car hauling: brand loyalty 100% -> offer; dealer -> pickup leg; borrowed car -> drive leg;
  game minutes driven -> pay.
- Care packages: todo counters (from ALL jobs + money + XP + zone + cars) -> bag spawns in a
  shop -> delivery run -> pick-1-of-2 reward card.
- Reward cards -> cancel slices of other systems: Free Tank (fuel), Fine Waived (police),
  Half-Damage/Invincible (accidents), Heat-Free (breakdowns), Worker Bonus (employees).
- Bank: money parked -> interest credited per finished job -> receipts; daily streak -> bonus.
- XP & levels: jobs grant XP -> pay multipliers + job-type unlocks;
  driver rating -> insurance discount; street knowledge -> job quality; friendship -> tips.

### Town economy (the cross-system one)
- First hub owned -> unlocks store stock depletion (before it, stock is static)
- Deliveries + employee runs -> shop stock up -> citizens grow -> car-share demand +
  road-test audience size + shopping availability -> money + XP -> back to hubs.

### Zone growth
- Every few finished jobs -> one new street discovered (StreetPool + Overpass) ->
  POI respawn pass (new carwash/stores/fuel/dealer/bank/mechanic per placement interval) ->
  more houses/shops -> more and better jobs -> more job completions -> growth again.
- Real-world add-street: GPS -> employee drives there -> street joins the pool -> same chain.

### Weekly stories
- All five depend on: Calendar gate + Clock + Driving + a system to score:
  race (car class, borrowed physics, rival NPCs), show (engine health, price, rivals),
  road test (garage, store citizens as audience), Willy (shop list, friendship),
  Grandma (houses, dialogue, pancake -> hunger pause).
- Onboarding influences every other system once: starter car, first-hire -> taxi license,
  intro flags -> when hunger/clock/jobs switch on at all.

### Online (Firebase)
- Influences: standings names on receipts, rival lists, valuation entries, car rarity
  (global km -> rarity tier -> fare multiplier), car sprites, name reservation.
- Is influenced by: km, XP, zone, shift results (fire-and-forget pushes).
- **Never gates gameplay** — offline play is identical; only decoration is missing.

---

## 4. Loops (circular influence)

1. **Growth loop:** jobs -> new streets -> more shops/houses -> better jobs -> jobs.
2. **Town loop:** hub -> deliveries -> stock up -> citizens up -> car-share/road-test demand -> money -> higher hub tier.
3. **Wear spiral:** driving -> km -> wear -> breakdown -> failed job + fees -> less money -> deferred service -> more wear risk.
4. **Quest loop:** every system's counters -> todos -> care packages -> reward cards -> mute parts of those same systems.
5. **Hunger gate:** clock -> hunger -> job center locked -> buy/eat food (or Grandma pancakes) -> unlocked.

---

## 5. Sources and sinks

Pure sources (influence, depend on nothing gameplay-side):
- ScriptableObject configs (GameTimeConfig, StoreEconomyConfig, PoliceConfig, Car/Item databases)
- Map data pipeline (Overpass/HOT packs/OSRM) — external ground truth
- Game clock (real time in, game time out)

Pure sinks (influenced only, influence nothing):
- Screens/HUD and receipts — display state, never decide it
- Job alarm notifications, audio (SFX only)
- All Firestore writes and leaderboard reads (the one exception: rarity feeding fare math)

---

## 6. What stops if you remove one

| Remove | Stops |
|---|---|
| Save/PlayerData | Everything; no state exists |
| Game clock | Hunger, fuel price, premiums, car-share, employee trips, stock depletion, weekly preloads |
| Calendar | All weekly content refreshes and weekday events |
| Map data | Streets/houses/shops: no job can generate or finish |
| Driving | Every job, fuel, wear, accidents, fines, race, road test |
| Stores | Groceries, restock targets, citizen counts -> car-share and audience collapse |
| Job-completion accounting | XP, money, todos, zone growth, receipts, streaks |
| Firebase | Nothing gameplay-side |

---

## Appendix — system -> main classes

| System | Classes |
|---|---|
| Save | `Models/PlayerData.cs`, `Managers/SaveManager.cs`, `Composition/GameInitializer.cs` |
| Clock | `Managers/GameClockManager.cs`, `ScriptableObjects/GameTimeConfigSO.cs` |
| Calendar | `Managers/CalendarManager.cs`, `UI/YearTransitionScreen.cs` |
| Map data | `Map/StreetPool.cs`, `API/OverpassService.cs`, `Map/MapDataPackService.cs`, `Map/HouseNumberGenerator.cs`, `Map/Services/MapMarkerServices.cs` |
| Driving | `Managers/DrivingManager.cs`, `Driving/Services/*` (FuelSimulator, StandardBreakdownStrategy, RouteServiceWrapper…) |
| Taxi | `Utils/TaxiJobGenerator.cs`, `UI/Handlers/TaxiJobLifecycleHandler.cs`, `UI/TaxiReceiptScreen.cs`, `Utils/TaxiShiftService.cs` |
| Delivery/hubs | `UI/Handlers/DeliveryJobHandler.cs`, `Utils/HubCareerCatalog.cs`, `Utils/DeliveryCartAccounting.cs`, `UI/DeliveryHubScreen.cs` |
| Stores | `Managers/StoreManager.cs`, `Models/ItemDatabase.cs`, `Utils/StorePlacementHelper.cs` |
| Cars | `Utils/CarMaintenanceService.cs`, `Utils/EngineHealthService.cs`, `Utils/AccidentService.cs`, `Utils/CarWashService.cs`, `UI/CarDealerScreen.cs`, `UI/GarageScreen.cs`, `Utils/InsurancePricing.cs` |
| Money | `UI/BankScript.cs`, `Utils/BankInterestHelper.cs`, `Utils/DailyStreakService.cs`, `Utils/CarShareService.cs`, `Utils/EmployeeService.cs` |
| Progression | `Utils/XpEarningHelper.cs`, `Utils/StreetKnowledgeHelper.cs`, `Utils/FriendshipHelper.cs`, `Utils/DriverRatingService.cs`, `Todo/TodoManager.cs`, `Utils/CarePackageService.cs`, `Utils/RewardCatalog.cs` |
| Zone | `UI/MapScreen.cs` (expansion flow), `Utils/ZoneExpansionService.cs`, `Map/LocationService.cs` |
| Weekly events | `Managers/MilleMigliaManager.cs`, `Utils/ValuationService.cs`, `UI/RoadTestScreen.cs`, `UI/Handlers/WhereIsWillyHandler.cs`, `UI/Grandma01DialogueScreen.cs` |
| Car hauling | `Utils/CarHaulOfferService.cs`, `UI/Handlers/CarHaulDeliveryHandler.cs`, `UI/CarHaulingScreen.cs` |
| Onboarding | `UI/ScreenStart.cs`, `UI/SetupScreen.cs`, `UI/FavBrandScreen.cs`, `UI/GrandmaCarScreen.cs`, `UI/TutScreen.cs` |
| Offline jobs | `Managers/JobAlarmService.cs`, `Utils/DriveEtaEstimator.cs`, `Utils/MultiLegEtaEstimator.cs` |
| Online | `API/FirestoreService.cs`, `API/RivalsService.cs`, `API/*StandingsService.cs`, `API/ValuationEntriesService.cs` |
| Screens | `UI/ScreenManager.cs`, `UI/ScreenRegistry.cs`, `UI/IScreen.cs`, `UI/NavigationEvents.cs` |
