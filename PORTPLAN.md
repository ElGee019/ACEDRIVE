# ACEVO Free-Roam Gameplay App — PORTPLAN

Source: `C:\Users\gouwy\Documents\BS\BS\LATESTDRIVEBS2` (Unity driving game)
Target: Assetto Corsa EVO free-roam gameplay app (Lua / ACEVO mod)

## Step 1 — Map street-name / house-number grounding (do first, blocks jobs/POI/police)
Goal: decide how to get addressable locations on the Kunos 1:1 Eifel map.

1. Check if street names are included on the map:
   - Inspect free-roam release: map UI, nav/fast-travel data, SDK scene/DOT, photo-mode/GPS readout, game files for road spline names.
   - Yes → go to 2a. No → go to 2b.
2a. If YES (names present):
   - Link existing houses/buildings to house numbers alphabetically, no OSM needed.
   - Tasks: enumerate building objects per street/village in map order, assign A,B,C… per street side/order, door/gate anchor + walk-mode point, persistent `street|letter → xyz` table.
3. 2b. If NO (names missing):
   - Figure out how to assign real-world street names to the map (1:1 copy → OSM/Overpass overlay for names only).
   - Tasks: OSM Eifel extract, coordinate transform to game world, snap OSM ways to Kunos splines, bake `xyz → street` lookup. House numbers: same alphabetical per-street scheme as 2a, no OSM needed.
4. Done when: every job/POI/police spawn can resolve `address ↔ xyz` without OSRM/tiles. Unblocks: 11/13 taxi gen, 24 POI, 41 police, 58 zone/discovery, 59 markers, 07 ETA.

## Career Start — Screen 1 spec (onboarding, port of Register/Setup)
Fields in order:
1. Display name (text input, 2–16 chars, unique per save)
2. Country = fixed `Germany` (locked, no selector)
3. Town (dropdown, all towns from Step 1 table)
4. Street (dropdown, filtered to selected town only)
5. House number/letter (dropdown or stepper, alphabetical A,B,C… from Step 1; filtered to selected street)
6. Confirm button (disabled until 1+3+4+5 valid; on confirm → create PlayerData.home, seed zone/discovery, go to Screen 2)
Rules: changing Town resets Street+Housenumber; changing Street resets Housenumber; persist draft on back; validation messages inline.

## Career Start — full onboarding flow
1. Screen 1 above (name + home address) → create PlayerData.home, seed zone/discovery.
2. Favorite brand (single-select from 26 in-build brands: Abarth, Alfa Romeo, Alpine, Audi, BMW, Caterham, Chevrolet, Dallara, Datsun, Ferrari, Ford, Honda, Hyundai, KTM, Lamborghini, Lancia, Lotus, Maserati, Mazda, Mercedes, Mini, Peugeot, Porsche, Renault, Toyota, Volkswagen). Drives BrandLoyalty (35).
3. Grandma's car — the walk's closing page, opened after the birthday dialogue: one car, the 1971 Datsun 240Z (`ks_datsun_240z_fairlady`, free starter). The page shows the car's render with the instruction under it and one CONTINUE; confirming it sets the first OwnedCar + odometer 0 and drops the panel onto the mini window with the errand pinned.
4. Birthday dialogue (gift handover, tutorial tone, leads into Grandma ToDo (46)). Its last line sends the player out for maple syrup; page 3 follows and its CONTINUE closes the panel onto the HUD.
5. Max's grocery store job (first guided delivery: hub intro → Model A fixed-fee (16) → receipt). Unlocks free roam jobs board.

## Status legend
- [ ] PENDING — not yet asked / decided
- [x] CONFIRMED — port it
- [-] DECLINED — skip

## Working rules
- CACHE-FIRST (see AGENTS.md › Caching): every confirmed item below is built
  cache-first — cache at the owner, invalidate on write, no disk on the hot
  path, bounded, lazy. A ported feature that re-reads a file, re-parses a table
  or rebuilds a page it already built is not done. New caches get a row in
  ARCHITECTURE.md §7a.

## 1. Core driving
- [-] 01 FixedUpdate physics loop — DECLINED (ACEVO has own driving model)
- [-] 02 OSRM route fetch + interpolation / odometer — DECLINED (use Kunos free-roam map)
- [-] 03 Bend/turn + destination braking — DECLINED
- [-] 04 Street-resolved driving — DECLINED
- [-] 05 Speed-limit policy per street — DECLINED
- [x] 06 Driver-rating skill model — CONFIRMED (landed: the three skills persisted + shown on the wallet page; events wired — speed-challenge win +1 cornering, quiz correct +1 / wrong-or-timeout −1 off-the-line, filthy-car pickup refusal −1 concentration, streak day N grants N random +1s; insurance reads the live average; the accident penalty landed with the reward-card slice — every crash drops all three skills by Unity's banded amount, DR Protection skips it; the Cento Miglia win still awaits its ported source)
- [x] 07 ETA estimators (single + multi-leg) — CONFIRMED (rework: distance + ETA for Kunos map)
- [-] 08 Borrowed-car drive leg — DECLINED
- [-] 09 Background catch-up + offline arrival — DECLINED
- [-] 10 Engine audio tied to job state — DECLINED

## 2. Economy / Jobs
- [x] 11 Taxi job lifecycle (proposal→pickup→dropoff→receipt/tip) — CONFIRMED (the job's distance and fare are calculated, never metered: the drive from the car's position at ACCEPT to the pickup plus the pickup→drop-off leg on the closed lap, so a detour or a shortcut cannot move the fare)
- [x] 12 Taxi shifts with stats/standings — CONFIRMED
- [x] 13 Manual vs procedural/auto taxi jobs + radius — CONFIRMED
- [x] 14 Speed-challenge gamble — CONFIRMED
- [x] 15 Customer requests mini-games (quiz / DoubleNextTip) — CONFIRMED (WhatCar dropped: it needs a passing-car image for the roster and the overlay cannot build one, so the tier-3 slot is quiz-only)
- [x] 16 Delivery Model A fixed-fee — CONFIRMED
- [x] 17 Delivery Model B buy-resell + negotiation — CONFIRMED (negotiation is out of scope: delivery is a whole-store full restock — one row tap orders every missing unit the wallet and the trunk's free litres cover, and on DELIVER the run charges the goods at the shelf price — so there is no per-item order screen and no haggling; `DeliveryJobScreenViewModel` exposes one `ToggleOrderCommand` per store, never a unit stepper or a price offer)
- [x] 18 Wholesale hub ownership + credibility/CredP ramp — CONFIRMED
- [x] 19 Multi-stop delivery route + escrow cart / perishability — CONFIRMED (the cart stages one order per store, DELIVER charges the wholesale sum and holds it as the escrow the settle releases — a breakdown or crash refunds it only through the Delivery Insurance Shield — and the run drives every hub pickup, then every store in cart order, settling once at the last drop-off; the charge is the planned route, priced at DELIVER from the doors the companion will pin (hub first, then the stores) and measured on the closed lap, never the odometer, so a detour, a shortcut or a longer way round the lap cannot move the fare; perishability is out of scope by that same decision: the run's risk is the route it commits to at DELIVER, not a clock to beat, and the game clock's accelerated minutes (item 50) would make any shelf life a guess until the freeroam map lands)
- [x] 20 Car-hauling contracts — CONFIRMED
- [x] 21 Care-package charity run — CONFIRMED
- [x] 22 Car-sharing passive income — CONFIRMED
- [x] 23 Employee-driven jobs — CONFIRMED

## 3. POI / Businesses
- [x] 24 Typed POI network (markers/handlers) — CONFIRMED
- [x] 25 Store inventory + citizen-demand depletion — CONFIRMED (city growth ported: 100 citizens per store, stock bands drive the tick, demand needs a hub, size saved on exit)
- [x] 26 Grocery shopping + hunger survival — CONFIRMED (the meter fills one catalogue hour at a time, banks itself on every crossing, and the trunk page's EAT takes the eaten row's hunger off it; the job entries now refuse in Unity's block band (hunger >= cap - 1) instead of only logging, and the TOO HUNGRY page names the trunk and the shops so the refused tap still answers — stores and the Friday row stay reachable, so a broke driver is never locked out. Grandma's pancakes are real trunk food: the intro dialogue packs the first stack, Friday's weekly visit packs another, and the appetite's one-shot event fires the hour the reading lands exactly on the threshold while the trunk carries that stack — the companion announces it)
- [x] 27 Story/quest POIs (Max/Justin/Grandma/Willy/photo-op) — CONFIRMED (Willy landed: the Wednesday missing-person run, ToWhereIsWilly.md — the calendar row draws a hiding coffee shop and a resident elder, wrong shops cost an attempt, the finder's fee is priced from the garage's average value, parking at the elder's door awards friendship, and DROP OFF pays, stamps the Wednesday and leaves the Bring-Them-Home certificate in the trunk; the map stand-in is the quest page plus the companion's pinned door, and only the completion stamp is banked. The guided tour's doors join one street neighbourhood: Max's grocery opens beside the home street, Justin and Annabella share a street connecting home or Max's, and the community centre plus the door-only La Strada take further connecting rows)

## 4. Vehicles / Ownership / Maintenance
- [x] 28 Car database with real specs (class 1-21) — CONFIRMED (use Kunos specs + add missing fields like trunk/fuel use) → see CARSTATS_EVO.md v2 (69 cars verified from content.kspkg, 14 with Kunos-exact hp/kg)
- [x] 29 Dealer buy flow + procedural stock — CONFIRMED
- [x] 30 Multi-car garage — CONFIRMED
- [x] 31 Per-part wear + repair pricing — CONFIRMED
- [x] 32 Engine health (weekly/annual) — CONFIRMED
- [x] 33 Dirt/filth + car wash — CONFIRMED
- [x] 34 Insurance per car + broker + cargo cover — CONFIRMED
- [x] 35 Brand systems (loyalty/nationality/era/rarity) — CONFIRMED
- [-] 36 Car horns + display names — DECLINED

## 5. Fuel / Breakdown / Police
- [x] 37 Fuel prices + refuel receipt — CONFIRMED (the burn stays Kunos' — item 38 below; the overlay reads EVO's own fuel level from the documented graphics field (<c>fuel_liter_current_quantity</c>; the AC1 physics slot is a deprecated mirror) and prices the fill it detects: litres × the market's CR/L, charged in full so a short wallet goes into debt like Unity's <c>cash -= cost</c> (the fuel is already in the tank), a Free Tank Refill card makes it free, and the receipt prints the litres, the pump price, the paid amount and the station attendant's familiarity +1. The price is Unity's: 1.65 start, one uniform ±0.15 drift per game day at the midnight roll, clamped to 1.40–2.20, persisted in <c>fuel.json</c> so a restart continues the curve. The fill detector settles a run of rising samples after 1.5 s of stillness and re-primes on a reload/new track/car change. The top-ups EVO makes on load, restart and at the pit box are never billed: the graphics pit-box/pit-lane flags re-prime the detector, and a fill only counts after the car has driven <c>MinDriveBeforeFillMetres</c> away from the spawn (the temporary freeroam map can spawn a fresh tank). The introduction drives stay free. Data: <c>Data/fuel_rules.json</c>)
- [ ] 38 Fuel-out stranding + roadside refuel — PENDING/CONDITIONAL (implement depending on Kunos)
- [x] 39 Mechanical breakdown session — CONFIRMED
- [-] 40 Accidents (damage roll / payout cut) — DECLINED as a random roll (ACE owns collisions); the ACE collision damage itself now prices a body repair: a hit banks a twentieth of impact speed ÷ car top speed × car value (a top-speed impact costs 5% of the car, twenty unrepaired ones total it), banked per car and covered 90% by insurance. A crash during a taxi ride or a delivery run ends that job on the spot (no fare, no receipt) and costs the customer one friendship; the companion then offers REPAIR THE CAR, which takes the bill, banks the insured share for the broker and returns the main page
- [x] 41 Police patrols + fines — CONFIRMED
- [x] 42 Protective consumables (Heat-Free / Fine Waived / Invincible / Half-Damage / DR Protection) — CONFIRMED (landed: every stored card now carries a data-authored `Effect` in `reward_cards.json`, and the glovebox consumes one card on the event Unity's per-effect helper names — Next Fine Waived waives the fine and keeps the tip, Double Next Tip doubles the settled tip, Delivery Profit Boost scales the goods payout, Free Car Repair covers a body repair, Half Damage halves the bill before insurance, Accident Loyalty Shield keeps a customer's/hub owner's friendship, Delivery Insurance Shield refunds the invested goods on a crash or breakdown, Journalist Test Retake re-runs the finished road-test leg, Heat-Free Rides arms no patrols and Invincible Rides suppresses the crash roll on their five rides, DR Protection keeps the banded accident rating penalty off, and the claim-time slot/cash cards behave as before; effects are communicated where they happen — the FINE WAIVED toast, the receipt's waive/boost/tip rows, the crash prompt's card notes, the mechanic's free-repair line and the retake button — plus the trunk's card count and each counter card's live charge count. Two cards stay carried but inert until their source systems land: Free Tank Refill (fuel sim declined, 37) and Worker Bonus (now spent by an employee delivery, 23))

## 6. Reputation / Progression
- [x] 44 Attentive-Driver score — CONFIRMED (the design doc's score was never built in Unity, so there is no behaviour to transliterate: the port turns it into an event-driven driving record. A finished taxi, delivery or car-haul job compares the driven car's odometer at the job's start against the settle, banks that one delta (no per-tick tracker), and every 5 clean kilometres pays one random skill point (`DrivingRecord.AwardKilometres`). A billed crash folds the run into the longest reading, resets the live run and adds the crash; the wallet page shows the longest run without an accident and the attentive-driver percentage — career km per crash, capped at 100 %, and 100 % while no crash has been billed. The whole record rides `driver.json`, so a restart neither re-pays nor loses a point. Freight risk still moves through the delivery crash rule (40), not the doc's damage roll)
- [x] 45 Friendship/relationship + street-knowledge XP — CONFIRMED
- [x] 46 Grandma ToDo tutorial quests — CONFIRMED (landed: the 16-subject ledger (all 15 of Unity's plus the gated Open Delivery Career follow-up, revealed once the Max grocery tip completes) — Unity's four rotating slots, the six-job turn counter, the completion latch with TO JOBS, threshold curves and its own todos_state.json; ClubReputation landed once the Mille Miglia's reputation existed (49): a doubling gate curve (1, 2, 4, 8, …) read off the wallet and queued after the delivery tips, because reputation only moves at a settled race and a tip nobody can complete in the first career week would take a slot for nothing; VisitFuelStation landed with no door to open: EVO's pump is a forecourt, not a marker the network can place, so the first settled fill is the visit — the fuel sim's money side (37) stamps the one-time flag and the tip retires like the other visits. The SetDestinationsManual tip counts every settled job as a reached destination — the port has no GPS, so every drive is a drive without GPS and Unity's manual-pin count (a map action) has no source here; revisit once navigation lands. The care-package grant now rides every completion into the bag run — kits land at a town store, the companion walks the pickup legs, the community-centre drop-off opens the delivered page with the receipt and the run's two reward cards (item 61))
- [x] 47 Daily streak + consecutive-day + starter bonus — CONFIRMED (starter bonus on the taxi receipt; delivery pending)
- [x] 48 Bank savings + interest — CONFIRMED
- [x] 49 Competitive events (Mille/Cento Miglia, Road-Test, Valuation, Star of Show) — CONFIRMED (road-test pick is sticky: one car until its test is driven, kept across overlay sessions; the road-test drive leg landed — the Tuesday row opens the week's briefing first (the pick, La Strada's address, both legs and the photo, START DRIVE / BACK), its START pins La Strada and hides the panel, the companion guides the drive, parking raises ENTER, and the test centre page names the week's car, the driver rating and Unity's 0-60 button; the 0-60 leg runs on the companion: Unity's 3-2-1 counter and its random 0.3-3 s reaction hold, then the 3-decimal stopwatch ticks while the player accelerates in EVO, and the crossing is interpolated between two 10 Hz samples (a rolling start above the target banks nothing until a real crossing); at 60 km/h the same page reopens on Unity's finish sentence and the car's per-car board (top 10, own row highlighted, own row repeated under the separator when it fell outside the slice), pushed best-guarded to the acevo_roadTestStandings doc and read back behind the local row, with the all-cars record book under it: every tested car's best, one row per car, the car under test highlighted, read off the same collection and merged with the run just driven so an improvement shows before the push lands; a miles player reads the same 60 km/h target converted (0-37 mph); the leg only runs in the week's pick: the announced car must match that row (compared by ks id + mechanical preset, an unannounced car counting as no mismatch like the HUD's wrong-car warning), a mismatch at the door names the car to come back in and hides the 0-60, and a car swapped mid-run voids the run instead of banking another car's time on the pick's row; the pick is only banked as tested once the whole test is driven: the 100-0 leg waits for 100 km/h and integrates the braking distance to the halt (same local row, push and board read, in metres), and once both legs ran the photo mission puts the panel down — the page's photo brief asks for any spot on the map, the companion carries the ask with its PHOTOSHOOT button, and a shot without a resolvable address keeps the mission up with a notice. The shot settles the receipt the way Unity's RoadTestReceiptScreen does: people reached from the friends (5+) and acquaintances (2-4) over the town's citizens, variety as the lap distance from the last shoot over half the lap (the widest span a closed lap allows; the first shoot is full variety), the natural car-journalist reputation (one point per tested car, capped at 100) scaled by the neutral test result and √variety, the magazine price, the commission over the 400 CR forfeit and the earnings — each with the previous issue's reading beside it (the crowd line instead states how many of the town's citizens bought La Strada) and the personal bests below; the page is titled LA STRADA ROAD TEST RECEIPT and its CONTINUE hands back the menu page rather than the jobs page. It is banked once into the wallet and roadtest.json (with the last shoot and the bests, so the next issue has its baseline), and the pick rotates there, Unity's MarkCurrentAsTested, which is the only thing that moves it; the Cento Miglia page landed — Sunday's calendar row opens the eight club tiers with their reputation gates and the pick's detail panel, from Data/cento_miglia.json; ENTER EVENT builds the event the way Unity's screen does — the tier issues a registration number (its digit count grows with the club), the page shows Unity's four-second setup countdown, then the route: the furthest two doors the operating zone's streets hold, planned on the placeholder map and shown on the page; the built event drives the race port — the companion guides the player to the start door, arrival opens Unity's registration check (the number is picked on a 0-9 keypad: picked digits print teal and repeat on the SELECTED line, each key also answers its spoken word, and the second wrong number closes the entry), CONTINUE starts the race with the finish held in the companion's task text and the live club reputation on its own line beneath it; the counter grows one Unity tick per 0.01 km driven, so a parked car earns nothing, and parking on the finish door settles the receipt: the congratulation naming the event week, the placement purse (ladder × tier × reputation) from Data/cento_race.json, the reputation the race grew, the driver-rating concentration award, the Star of the Show logistic and its generated award/certificate (stored in the trunk until the glove compartment lands), banked to driver.json, and its CONTINUE hands the panel back to the menu (a race is not a job, so it never lands on the jobs list); the Cento field landed — three club rivals from Data/cento_race.json who never drive, the settle shuffling the player and the three into a seeded random finishing order, so the drawn place drives the purse ladder, the concentration award and the Star of the Show placement multiplier; the receipt prints the final standings (place, name, car, DR, the player's row highlighted), and assertivity is ported too — a declined passive/speed-challenge request adds 0.1, each whole point adds 1/1,000,000 to the race reputation's per-10 m growth factor (Unity's truncating int cast), and the event page and receipt print the stat; the weekly market valuation landed — Monday's calendar row is the job-centre button: from 06:00 the flow draws a venue on the placeholder streets (uniform over every house on Street 1/Street 2; the real Overpass discovery stays TODO on Step 1) and pins it on the companion, the 8am-8pm tap sends the player off, parking opens the showcase, and ENTER pushes the Unity score (class + top speed/2 + horsepower/10 — the RPM column has no EVO source — + price/1000 + the car's settled taxi/delivery jobs + engine condition) to the weekly Firestore list and draws one rival, else the local John and Jane Doe house entry; every later entrant compares live (win = gap/500, loss = a flat 0.1 consolation, one 4 s notice at a time, XP granted when shown) through a 30 s background re-read rather than a listener, a restart inside the week catches up wins only in one aggregate notice, and the Monday 00:00 reset deletes the player's own doc (never a rival's) and clears the venue, the entry and the settled set; the entry itself lives two real-world days (the away-earning cap) — past that window the flow settles only the newcomers who arrived inside it, deletes the own doc and forgets the entry, so an idle driver stops earning even when the game week has not turned; Star of the Show stays on the Cento receipt, and the weekly one-entry gate remains the next slice)

## 7. Time / World sim
- [x] 50 Game clock (~27×) + calendar — CONFIRMED (an independent clock, not seeded from Kunos/EVO: 2.2 real seconds per game minute, so a game day lasts ~53 real minutes; it advances from its own anchor — the saved game moment, or a fresh Saturday 10 Oct 2026 08:00 — through IClock, never by reading or syncing EVO's clock; the calendar is drawn on top)
- [ ] 51 Persistent save + migration + glovebox/trunk — PENDING/CONDITIONAL (see what Kunos provides)
- [x] 52 Weekly/daily gates (club/pancakes/valuation/roster) — CONFIRMED (the Wednesday gate is wired: the missing-person row keeps its standard label in every state, answers a stamped tap with COME BACK NEXT WEEK and a closed coffee-beans career with the unlock hint, and a run in flight blocks the other event rows, the taxi shift and the delivery career; the playtest bypass is gone, so every row answers only on its own weekday inside the 8am-8pm window, and every event is once a week: the valuation and the missing-person run keep their own stamps, while the road test, the Cento Miglia and the Touristenfahrten run share `WeeklyEventLedger`'s persisted game-date stamps; the Saturday row paints no button in the first display week, so the career's first Saturday is behind the player before the run is offered. The Friday row has its page and leg now: its briefing names grandma's house and the reading it will move, START pins the door the profile owns, the other rows refuse while that leg is live, and ENTER at her door serves the plate and packs a fresh pancake stack — the week's stamp is written on the visit, not on the page, so a drive abandoned on the way leaves the row playable)

## 8. UI / Screens
- [x] 53 Screen system (registry/stack/HUD/dialogue) — CONFIRMED (rework for ACEVO Lua UI)
- [x] 54 Job-centre + calendar picker + community centre hiring — CONFIRMED

## 9. Map / Navigation
- [-] 55 Live map (tiles/cache/panning lock/tracker) — DECLINED (Kunos provides map)
- [-] 56 Real-world streets via Overpass — DECLINED
- [-] 57 Offline HOT packs — DECLINED
- [x] 58 Operating zone + discovery / expansion — CONFIRMED (the zone owns the addressable area: `Data/address_map.json` splits the 1,929 m lap into thirty-two contiguous 3-house streets (60.28 m each, 30 m spacing) so every door has its own physical spot, and `OperatingZone.ZoneMap`/`NextStreet` name the discovered prefix home street first; the base length still sums to 1.929 km, so `AddressMap.Default` remains the two-street fallback and test fixture; `ZoneGrowth.Expand()` reports a `ZoneExpansion` and `PoiWorld.Grow(expansion)` banks a door only on a discovered street, with the first new door of the growth on the new street; a settled job that reaches the curve opens the OPERATING ZONE EXPANDED page on the receipt's CONTINUE — new street, street count, previous/new area, jobs to the next expansion, the businesses that just opened and the longest-operating-zone world standings behind `IZoneStandings` with an offline null-object — whose FIND STREET opens the addresses page as the map stand-in and whose CONTINUE lands on the page the receipt would have shown; the addresses page carries the zone readout the map HUD would (streets, km, jobs to next, street names); taxi proposals, the valuation venue and the Willy draws read `zone.ZoneMap`, so no draw sends the player to an undiscovered street; the thirty-two short streets are a temporary labelling of the Brands Hatch lap until the real freeroam map lands)
- [x] 59 Marker layers + autocomplete search — CONFIRMED

## 10. Multiplayer / Social / Backend
- [x] 60 Online backend (Firestore/standings/rivals/boards/storage) — CONFIRMED (XP world standings mechanism landed: rank math, receipt rivals block, snapshot save and the `IRivalsService` seam run on the offline null-object; the live Firestore board arrives as a new implementation — no screen changes needed)

## 11. Other
- [x] 61 Reward catalog + glovebox cards + name pools — CONFIRMED (landed: `Data/reward_cards.json` carries Unity's fifteen cards, the two glovebox slots a driver starts with and the 500 CR bonus; the delivered care-package page carries the reward pair — two distinct cards drawn from the catalogue, one claim — and the claim settles by kind: a card lands in the glove compartment as a 0 L keepsake beside the race awards and certificates, `+1 GLOVEBOX SLOT` raises the capacity from the driver file (Unity's PlayerData.gloveboxSlots) and `+500CR BONUS` credits the bank saldo as a gift rather than job earnings (Unity's `bankAmount += 500`; the player draws it out at the bank above the 1000 saldo floor). A full glovebox refuses a card and keeps both rewards up so the other pick can be taken, exactly like Unity's full-text branch; the page prints the claim's outcome and the capacity reading, and voices each reward by its own title. Every stored card's consume-on-use effect now lands with the protective-consumables slice (item 42): the file authors each card's `Effect`/`Amount`, one card is spent by the event that owns it, the counter pair prints its remaining rides, and the trunk page prints the card count (`TrunkGloveCount`). Two effects stay carried but inert until their sources land: Free Tank Refill and Worker Bonus)
- [x] 62 Mechanical-variant car identity — CONFIRMED + DATA LANDED (EVO showroom variants are per-car `*.mechanicalcarpreset` files, not extra ks_ folders: identity = ks id + mech preset parsed from the same `onSetPlayerCurrentCarCommand` log line the watcher already reads; kspkg dump done 2026-10-15 — 101 presets / 24 multi-variant cars with game card specs + shop prices in `CARSTATS_EVO.md` v3, 30 variant rows in cars.json keyed `id#preset`; refresh runbook in VARIANT_RESEARCH.md)

---
Confirmed list will grow below as you approve.
## CONFIRMED TO PORT
- 06 Driver-rating skill model (cornering/concentration/off-line 11-100)
- 07 ETA estimators — reworked for distance + ETA on Kunos map
- 11 Taxi job lifecycle
- 12 Taxi shifts with stats/standings
- 13 Manual vs procedural/auto taxi jobs + radius
- 14 Speed-challenge gamble
- 15 Customer requests mini-games
- 16 Delivery Model A fixed-fee
- 17 Delivery Model B buy-resell + negotiation
- 18 Wholesale hub ownership + credibility/CredP
- 19 Multi-stop delivery route + escrow/perishability
- 20 Car-hauling contracts
- 21 Care-package charity run
- 22 Car-sharing passive income
- 23 Employee-driven jobs
- 24 Typed POI network
- 25 Store inventory + citizen-demand
- 26 Grocery shopping + hunger
- 27 Story/quest POIs
- 28 Car database — Kunos specs + missing fields
- 29 Dealer buy flow + procedural stock
- 30 Multi-car garage
- 31 Per-part wear + repair pricing
- 32 Engine health
- 33 Dirt/filth + car wash
- 34 Insurance + broker + cargo cover
- 35 Brand systems
- 39 Mechanical breakdown session
- 41 Police patrols + fines
- 42 Protective consumables
- 44 Attentive-Driver score
- 45 Friendship + street-knowledge XP
- 46 Grandma ToDo quests
- 47 Daily streak + bonuses
- 48 Bank savings + interest
- 49 Competitive events
- 50 Game clock — independent ~27× clock + calendar (not seeded from Kunos)
- 52 Weekly/daily gates
- 53 Screen system — rework for ACEVO Lua
- 54 Job-centre + calendar + hiring
- 58 Operating zone + discovery
- 59 Marker layers + autocomplete
- 60 Online backend
- 61 Reward catalog + glovebox cards + name pools

## CONDITIONAL / TBD (depends on Kunos free-roam)
- 38 Fuel-out stranding + roadside refuel
- 51 Persistent save + migration + glovebox/trunk

## DECLINED (use ACEVO/Kunos native)
- 01 Physics loop, 02 OSRM routing, 03 Bend braking, 04 Street-resolved, 05 Speed limits
- 08 Borrowed-car, 09 Catch-up, 10 Engine audio
- 36 Horns/names, 37 Fuel sim (Kunos), 40 Accidents (ACE)
- 55 Live map tiles, 56 Overpass, 57 HOT packs
