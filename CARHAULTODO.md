# CARHAULTODO — car-hauling contracts (PORTPLAN item 20)

Status 2026-10-30: **the playable loop landed.** The control page's CAR HAULING
JOB tap now draws the offer (brand with driven km → class band → car → dealer →
customer door/name), ACCEPT pins the dealer on the companion, PICK UP arms the
delivery leg in game minutes, and DELIVER THE CAR settles the existing receipt.
The dealer's owner gains one friendship level per haul. The loyalty floor and
the one-haul-per-day cap stay behind `CarHaulPolicy` and are OFF in the build
today (`Development`), ready to switch to `Unity`.

Not yet: **persistence.** The active haul and the delivery date live in memory,
so a restart drops the job instead of reopening it (Unity reopens it). This is
the next slice; the save record is `CarHaulState`-shaped and intentionally does
not touch `DriverState` until the whole flow is stable.

Target: the full Unity lifecycle, timed in game minutes, with the *only*
remaining work after the freeroam map lands being the address/door provider
swap. Everything below is written to make that swap a one-file change.

Unity source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2\Assets\Scripts`.

---

## 1. Unity facts (read, not remembered)

### 1.1 Offer — `UI/JobSelectionScreen.cs:790-920`, `Utils/CarHaulOfferService.cs`

- The CAR HAUL button is always tappable; feasibility is checked on click and
  a hint flashes for 3 s (`FlashCarHaulHint`).
- Gates, in order (`TryBuildVehicleOffer`):
  1. `player.brandDistanceDriven != null` else "Drive one brand first."
  2. One haul per game day: fails while
     `lastCarHaulGameDay == clock.gameDay && lastCarHaulYear == year`
     (`today != 0` guard). Stamped only on delivery.
  3. Eligible brands = brands with `BrandLoyaltyPct > MinLoyaltyPct`
     (`0f` in the shipped source as a TESTING override; the release value in
     the comment is `100f`). Loyalty percent =
     `TaxiReceiptScreen.CalculateBrandLoyaltyBonus(km) * 100`.
  4. One eligible brand picked uniformly.
  5. Car class band from that brand's percent (`ClassRangeForPct`):
     <200% → 1-6, <300% → 1-8, <400% → 1-10, <500% → 1-12, <600% → 1-14,
     <700% → 1-16, <800% → 5-17, <900% → 6-21, else 7-21.
  6. Car: same brand, `year <= currentYear`, class inside the band; fallback 1
     = nearest class of the same brand (era-capped); fallback 2 = any eligible
     brand's in-band era-capped car; then fail "No era-eligible car…".
  7. Dealer: uniform over the player's POIs with `type == "cardealer"` and a
     non-empty `address.street`. Fail "No car dealers on the map yet."
- Customer address (`CarHaulPlacementService`): Overpass, *valuation's* radius
  and rules — towns around home within the valuation radius, home town
  excluded, one town uniform, named streets within 3000 m of the town, street
  center = average of its way centers, one street uniform. Failure = "Could not
  reach the map server — try again."
- Customer name (`ResolveCarHaulCustomerName`): name pool of the address's
  cultural zone, async with an 8 s cap then the sync pool; format
  `"{first} {surname}"`. **No resident POI is created** — the customer is
  forgotten when the haul ends.
- `CarHaulPreloadService` banks one customer address ahead of time (boot, first
  100% loyalty, every consumed preload, every game minute while empty). Pure
  latency hiding, not gameplay.
- Persist: `player.activeCarHaulJob` = `CarHaulJob` with
  `status = "offered"`, customer name/street/town/lat/lon, dealer
  key/name/street/house/town, `carId`, `carBrand`, `brandLoyaltyPct`. Save,
  close the screen, raise the map.
- Map task text (status `offered`, `MapScreen.cs:2843-2856`):
  `"{customerName} has bought a new car. Go pick it up at {dealerName} at
  {dealerStreet} {dealerHouseNumber}, {dealerTown}."`

### 1.2 Pickup — `MapScreen.cs:4421-4445`, `Handlers/ShopArrivalHandler.cs:100-125`

- Dealer arrival is a normal `ArrivalType.CarDealer`; the haul claims it only
  when `status == "offered"` and the arrived dealer key equals `dealerKey`.
- Then: button `PICK UP`, task `Pickup the {car model} and bring it to
  {street, town}.`, `status = "picked_up"`, `ArmBorrowedCar(carId)`,
  `BeginDelivery()` (inject temp `"carhaul"` POI named `"{Street}, {Town}"` at
  the customer lat/lon), save.
- A non-haul dealer arrival opens the dealer shop as before.

### 1.3 Delivery — `Handlers/CarHaulDeliveryHandler.cs`

- Locate phase like the valuation venue: the temp customer marker must be
  tapped once (`OnCustomerMarkerClicked`) before DRIVE appears.
- DRIVE: stamps `deliveryStartGameMinute = clock.CurrentGameMinute()`, saves,
  `StartRouteToWithBorrowedCar(tempPoi, carId)` — the player drives the *new*
  car; own car, fuel and odometer stay untouched; swap-back is automatic when
  the leg ends. HUD odometer on this leg is a live trip counter from 0
  (`MapScreen.cs:62`).
- Customer arrival (destination name equals `CustomerAddressDisplay`): stamps
  `deliveryEndGameMinute`, clears `CarHaulArrivedAtCustomer`, removes the temp
  marker, task `You've arrived at {address}.`, button `DELIVER THE CAR`, save.
- Reboot heal (`MapScreen.cs:826-850`): a persisted `picked_up` job re-arms the
  borrowed car and re-injects the marker if missing.

### 1.4 Settlement — `MapScreen.cs:4447-4520`

- Minutes only when `deliveryEnd > deliveryStart && deliveryStart > 0`:
  `minutes = end - start`; `driveHours = minutes / 60`, `driveMinutes = % 60`.
- `rating = DriverRatingService.Overall(player)` (0-100 float);
  `carClass = CarData.Get(carId).carClass` (1 when unknown).
- `CarHaulingScreen.CalculateEarnings(rating, class, minutes)`:
  `tier = TierForCarClass(class)`; `rate = 0.83 * (rating * tier / 100)`;
  `total = round2(rate * minutes)`. Tier bands 1-6→1, 7-8→2, 9-10→3, 11-12→4,
  13-14→5, 15-16→6, 17+→7.
- Dealer friendship: `dealerPoi.friendshipLevel++` (only when the dealer POI
  still exists), `tierUp = FriendshipHelper.TierIncreased(prev, new)`,
  owner name from the POI. Nothing else moves.
- `cash += total`; stamp `lastCarHaulGameDay` / `lastCarHaulYear`; save; open
  the receipt.
- Receipt (`CarHaulingScreen`): car model (underscores → spaces), friendship
  line green on tier-up (hidden when no owner), `The drive took you {h} hour(s)
  and {m} minute(s)`, `You earned: {total:F2}CR`, `Your Driver Rating: {r:F1}`,
  `The current class is Tier {tier}`, `Resulting in a rate of {rate:F2}/min`,
  CONTINUE.
- CONTINUE (`ContinueAfterCarHaulReceipt`): clear `activeCarHaulJob`, park the
  player's own car at the dealer's lat/lon when known, save, close.
- No cancel, no timeout, no abandon, no penalty anywhere: a haul stays
  `offered`/`picked_up` until delivered, across restarts (the marker and the
  borrowed car are re-armed on boot).
- One small extra: the delivery leg's odometer is a trip counter; the player's
  own odometer must not move (`DrivingManager` skips brand/car distance on a
  borrowed leg unless it is a haul, which records nothing either).

---

## 2. Port mapping (decisions to confirm)

| Unity | ACEDRIVE |
|---|---|
| CR money | EUR (`ReceiptText`/wallet convention) |
| Year cap (`year <= currentYear`) | dropped — ACEDRIVE has no year system (GARAGETODO decision) |
| Overpass town/street placement | draw from `OperatingZone.ZoneMap` (`TaxiAddressPlan` pattern); no network |
| lat/lon doors | `AddressLocation` (street + house number + side) on the lap |
| dealer = POI type `cardealer` | `PointOfInterest` with `Type == PoiTypes.Dealer` from `PoiDirectory` |
| dealer owner + friendship level on the POI | owner name already rides `PointOfInterest.OwnerName`; level needs a small store keyed by POI id (`ResidentBook` pattern) |
| game day + year stamp | game date (`GameClock.GameNow.Date`) in `DriverState` |
| game-minute stamps | `GameClock` minute count (add `GameMinute`), never wall clock |
| borrowed car swap | **not possible from the overlay** — see §3.3 |
| temp customer POI | `IPoiArrivalHandler` owning a temporary marker (Valuation/WhereIsWilly pattern) |
| receipt page | exists (`CarHaulReceiptScreenViewModel` + view) |

### 2.1 Existing ACEDRIVE pieces

- `CarHaulReceiptBuilder` / `CarHaulReceipt`: tier table + rate + total +
  dealer friendship fields — already Unity-exact.
- `CarHaulReceiptScreenViewModel` / `CarHaulReceiptScreenView`: the receipt,
  with the friendship block already hidden when empty.
- `CarHaulFlow.Settle`: placeholder, to be replaced by the real flow.
- Control page `HaulCommand` + voice keyword ("car hauling"), `Navigation`
  owning the receipt page.
- `DriverRating`, `DriverWallet`, `DayCycle`, `GameStateSaver`, `ILog`.

---

## 3. What is blocked on the real map — and what is not

### 3.1 Blocked (genuinely needs the freeroam map)

1. Real street names / house numbers / towns for the offer text and the door.
2. Dealer and customer *buildings*: the real door, its coordinates and the
   arrival radius. Today the dealer is a generated door on the lap; the
   customer door does not exist in the world at all.
3. Distance/ETA for the offer and the HUD route: Unity never shows one (pay is
   time-based); the port can show the calculated leg distance only once a real
   route/geometry exists.
4. Map marker placement / map panning (PORTPLAN 55 declined — Kunos map).
5. Physical arrival detection calibration (lap offset vs free-roam metres).

### 3.2 Not blocked — buildable now

- Offer data model, eligibility (loyalty gate, class bands, one-per-day),
  car/dealer draw and the class-band table.
- Customer address draw on the stand-in lap (one house on a discovered street)
  and the customer name draw.
- Run state machine (`offered → picked_up → delivered`), game-minute leg
  stamps, minutes rule, wrong-dealer rejection, idempotent settlement.
- Persistence of the active run + last-haul date (resume after restart,
  including re-arming the delivery marker).
- Dealer friendship store (owner name already exists on the door).
- Offer page, HUD task/guidance lines, PICK UP / DELIVER buttons, receipt
  settlement and CONTINUE clearing the job.
- A `IBorrowedCarHost` seam: null-object now ("drive your own car; the leg is
  timed; swap is the Lua layer's job"), real swap later. Nothing else changes
  when the Lua host arrives.

### 3.3 The borrowed-car constraint

Unity switches the player into the customer's new car for the delivery leg.
The overlay is read-only telemetry: it cannot spawn, swap or lock a car. So the
delivery leg must be driven in whatever car the player is in. The port keeps
the *leg semantics* (start/end stamps, distance, odometer untouched, receipt)
behind an interface; the overlay implementation is a null-object, the future
Lua/mod implementation performs the real swap. This is a deliberate,
documented deviation — not a missing feature.

---

## 4. Slices (map-free first)

- **S1 — Core contract — DONE.** `CarHaulClassBand`, `CarHaulPolicy` (the two
  switchable gates), `CarHaulOffer`, `CarHaulOfferService` (brand → car →
  dealer → customer door/name draw), `CarHaulAddressPlan` (dealer and home
  street kept out), `CarHaulRun` (phases, stamps, minutes, wrong-dealer guard).
- **S2 — Dealer friendship — DONE.** The dealer's existing `PointOfInterest`
  owner rides into `IFriendshipStore.RegisterRide`, so a haul raises the same
  familiarity the shops use; no owner (or no dealer) hides the receipt box and
  awards nothing. No new store and no POI schema change.
- **S3 — Offer UI — DONE.** `CarHaulScreenViewModel`/`CarHaulScreenView` show
  the offer (car, dealer, customer, tier) with ACCEPT, then the active leg's
  task line; the control page's CAR HAULING entry opens it. Copy in resx (en +
  nl); BACK reuses `DeliveryBack`.
- **S4 — Pickup — DONE.** The companion pins the dealer door; ENTER at it
  calls `PickUp` and stamps the delivery leg. There is no dealer interior to
  open, so the haul never competes with the dealer shop page.
- **S5 — Delivery — DONE.** The customer leg pins the customer address through
  the same `HudJobGuidance` the taxi/delivery legs use (no temp POI needed);
  ENTER settles into the existing receipt, and CONTINUE returns to the control
  page. No borrow host: see §3.3.
- **S6 — Persistence.** `CarHaulState` on `DriverState` (phase, car key,
  dealer id/owner, customer door/name, brand pct, leg stamp, last delivery
  date), reloaded at boot; reboot re-pins the leg. Needed before the map move
  so the map swap is not mixed with a save migration.
- **S7 — Map move.** New address provider behind `AddressMap`/`AddressBook`;
  recalibrate arrival; delete the stand-in draw. No flow changes.

---

## 5. Last-move checklist (S6)

- [ ] `Data/address_map.json` replaced by the real Eifel table (streets, house
      numbers, coordinates) or a provider that answers the same records.
- [ ] Dealer doors exist in `PoiDirectory` with real addresses and arrive by
      proximity, not lap offset.
- [ ] Customer door resolution: address → world position, arrival radius.
- [ ] `AddressBook` / `LapGeometry` distance replaced by map distance for the
      HUD line (the receipt stays time-based).
- [ ] Re-run the S1 tests against the real map data (same contracts).
- [ ] Live drive: offer → dealer → pickup → customer → receipt, plus a
      restart between pickup and delivery.

## 6. Decisions (2026-10-30)

1. **Home street excluded too.** `CarHaulAddressPlan` keeps the customer away
   from the dealer's street and the player's home street while a third street
   exists; on a two-street map it prefers another door on the dealer's block
   over the home street, and only a single-street map ever falls back to home.
2. **Dealer owners exist already** (the spawner's `NamePattern` gives every
   generated dealer an owner); a haul raises that owner's familiarity through
   the shared `IFriendshipStore`. No dealer on the map, or no owner, means no
   friendship increase and the receipt hides the line.
3. **The control-page CAR HAULING button stays the entry.** It draws the offer
   when none is running and reopens the active leg otherwise.
4. **The loyalty gate ships on.** `CarHaulPolicy.LoyaltyGate` is the build
   (Unity's 100% floor, about 250 km driven in the brand); a refused tap
   names it on the CAR HAULING button for three seconds.
   `CarHaulPolicy.Development` keeps the test shell's "any driven brand"
   reading, and `CarHaulPolicy.Unity` restores the full release rules.
5. **No daily cap yet.** `CarHaulPolicy.LoyaltyGate` allows another haul the
   same game day; `CarHaulPolicy.Unity` restores the stamp (it needs the
   delivery date on the driver save first — the flow keeps it in memory today).
   The in-flight guard stays on in every reading (Unity would overwrite the
   active job — not ported).
