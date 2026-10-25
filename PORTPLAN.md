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
- [x] 06 Driver-rating skill model — CONFIRMED (landed: the three skills persisted + shown on the wallet page; events wired — speed-challenge win +1 cornering, quiz correct +1 / wrong-or-timeout −1 off-the-line, filthy-car pickup refusal −1 concentration, streak day N grants N random +1s; insurance reads the live average; accident penalty + the Cento Miglia win await their ported source)
- [x] 07 ETA estimators (single + multi-leg) — CONFIRMED (rework: distance + ETA for Kunos map)
- [-] 08 Borrowed-car drive leg — DECLINED
- [-] 09 Background catch-up + offline arrival — DECLINED
- [-] 10 Engine audio tied to job state — DECLINED

## 2. Economy / Jobs
- [x] 11 Taxi job lifecycle (proposal→pickup→dropoff→receipt/tip) — CONFIRMED
- [x] 12 Taxi shifts with stats/standings — CONFIRMED
- [x] 13 Manual vs procedural/auto taxi jobs + radius — CONFIRMED
- [x] 14 Speed-challenge gamble — CONFIRMED
- [x] 15 Customer requests mini-games (quiz / WhatCar / DoubleNextTip) — CONFIRMED
- [x] 16 Delivery Model A fixed-fee — CONFIRMED
- [x] 17 Delivery Model B buy-resell + negotiation — CONFIRMED
- [x] 18 Wholesale hub ownership + credibility/CredP ramp — CONFIRMED
- [x] 19 Multi-stop delivery route + escrow cart / perishability — CONFIRMED
- [x] 20 Car-hauling contracts — CONFIRMED
- [x] 21 Care-package charity run — CONFIRMED
- [x] 22 Car-sharing passive income — CONFIRMED
- [x] 23 Employee-driven jobs — CONFIRMED

## 3. POI / Businesses
- [x] 24 Typed POI network (markers/handlers) — CONFIRMED
- [x] 25 Store inventory + citizen-demand depletion — CONFIRMED (city growth ported: 100 citizens per store, stock bands drive the tick, demand needs a hub, size saved on exit)
- [x] 26 Grocery shopping + hunger survival — CONFIRMED
- [x] 27 Story/quest POIs (Max/Justin/Grandma/Willy/photo-op) — CONFIRMED

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
- [-] 37 Fuel simulation (burn/prices/refuel) — DECLINED (use Kunos fuel sim)
- [ ] 38 Fuel-out stranding + roadside refuel — PENDING/CONDITIONAL (implement depending on Kunos)
- [x] 39 Mechanical breakdown session — CONFIRMED
- [-] 40 Accidents (damage roll / payout cut) — DECLINED (use ACE accident system)
- [x] 41 Police patrols + fines — CONFIRMED
- [x] 42 Protective consumables (Heat-Free / Fine Waived / Invincible / Half-Damage / DR Protection) — CONFIRMED

## 6. Reputation / Progression
- [x] 43 Dual-track XP/CredP + taxi/delivery levels + club rep — CONFIRMED
- [x] 44 Attentive-Driver score — CONFIRMED
- [x] 45 Friendship/relationship + street-knowledge XP — CONFIRMED
- [x] 46 Grandma ToDo tutorial quests (7 subjects) — CONFIRMED (landed: the 14-subject career ledger (Unity's 13 plus a gated follow-up, Open Delivery Career, revealed once the Max grocery tip completes) — Unity's four rotating slots, the six-job turn counter, the completion latch with TO JOBS, threshold curves and its own todos_state.json; two Unity subjects stay out by decision: ClubReputation waits on the Mille Miglia (49), VisitFuelStation on the declined fuel sim (37). The care-package grant and reward cards are the next slice)
- [x] 47 Daily streak + consecutive-day + starter bonus — CONFIRMED (starter bonus on the taxi receipt; delivery pending)
- [x] 48 Bank savings + interest — CONFIRMED
- [x] 49 Competitive events (Mille/Cento Miglia, Road-Test, Valuation, Star of Show) — CONFIRMED (road-test pick is sticky: one car until its test is driven, kept across overlay sessions; the Cento Miglia page landed — Sunday's calendar row opens the eight club tiers with their reputation gates and the pick's detail panel, from Data/cento_miglia.json; the race itself — route, NPC field, finish, Star of the Show receipt — and the club reputation it pays out are the next slice, so ENTER closes the page)

## 7. Time / World sim
- [x] 50 Game clock (16x) + calendar — CONFIRMED (start from Kunos clock + add calendar)
- [ ] 51 Persistent save + migration + glovebox/trunk — PENDING/CONDITIONAL (see what Kunos provides)
- [x] 52 Weekly/daily gates (club/pancakes/valuation/roster) — CONFIRMED

## 8. UI / Screens
- [x] 53 Screen system (registry/stack/HUD/dialogue) — CONFIRMED (rework for ACEVO Lua UI)
- [x] 54 Job-centre + calendar picker + community centre hiring — CONFIRMED

## 9. Map / Navigation
- [-] 55 Live map (tiles/cache/panning lock/tracker) — DECLINED (Kunos provides map)
- [-] 56 Real-world streets via Overpass — DECLINED
- [-] 57 Offline HOT packs — DECLINED
- [x] 58 Operating zone + discovery / expansion — CONFIRMED
- [x] 59 Marker layers + autocomplete search — CONFIRMED

## 10. Multiplayer / Social / Backend
- [x] 60 Online backend (Firestore/standings/rivals/boards/storage) — CONFIRMED (XP world standings mechanism landed: rank math, receipt rivals block, snapshot save and the `IRivalsService` seam run on the offline null-object; the live Firestore board arrives as a new implementation — no screen changes needed)

## 11. Other
- [x] 61 Reward catalog + glovebox cards + name pools — CONFIRMED
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
- 43 XP/CredP + levels + club rep
- 44 Attentive-Driver score
- 45 Friendship + street-knowledge XP
- 46 Grandma ToDo quests
- 47 Daily streak + bonuses
- 48 Bank savings + interest
- 49 Competitive events
- 50 Game clock — Kunos base + calendar add-on
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
