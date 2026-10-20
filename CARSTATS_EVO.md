# CARSTATS_EVO v2 — Assetto Corsa EVO (build 0.9, roster verified from game files)

Roster source: `content.kspkg` file table dumped with Nenkai ACEvo.Package 1.1.0 → 69 cars.
`ks_` id = internal Kunos id. `*` after hp/kg = Kunos-verified from physics log (`POWER CLASS INDEX`, W includes driver/fluids).

Format follows mother game `CarClasses.md` + `CarStats.cs`:
`maxSpeed km/h | acceleration = sec to 60km/h (est = 0-100*0.6) | brakeDistance m (est) | trunkSpace L | passengerCapacity (driver excluded) | carClass (production table) | rarity=1 | hp | cc (0=EV) | gears | shiftTime s (est) | maxRpm (est) | maxFuel L (0=EV) | fuelConsumption L/100km (0=EV) | mass kg | shop€ (game shop money exact; `~` = web-estimated, no game price — v5) | year`. `odometerStartingValue=0, roadTested=0, designer=""` for all.

## What Kunos provides already vs what we add
- KUNOS_NATIVE: hp, cc, mass, gears, vmax, acceleration, tank, fuel burn, year/brand/model, Power Class Index. Provenance: `content\cars\<ks_id>\data\CarData.car` (protobuf) + physics log. Exact Kunos hp/kg captured for 14 cars below (`*`).
- WE_ADD: trunkSpace, passengerCapacity, carClass, rarity=1, brakeDistance, shiftTime/maxRpm, odometer=0, roadTested=0. Since v5 EVERY car/variant has a price: bare numbers are the game's own shop money, exact from `system\prices.printableprices`; `~` numbers are we-added web market values for rows the game leaves unpriced (`no card`/`no entry` — not in any in-game table). The v2 real-world `price_today` guesses were placeholders and are dropped.
- EVs in build: Junior, A290 only (fuel 0).

## v6 — the tables are the shipped database (2026-10-18)
`Data/cars.json` now carries the whole row this document describes, one entry per
folder row plus one per non-base mechanical preset (101 rows): `id, preset?, year,
brand, model, passengers, class, price, trunkLitres, horsepower, engineCc, massKg,
topSpeedKmh, acceleration, gears, tankLitres, fuelPer100Km, brakeDistance`. It is
what the dealer, the garage and the receipt read (PORTPLAN items 28-30) — the markdown
tables below are the provenance record, not a runtime source. Rules applied by the
generator (`%LOCALAPPDATA%\Temp\opencode\kspkg-research\build_carsjson_v6.ps1`):
- Base (preset-less) rows take the **v2 physics values** and the v2 `shop€`, so the
  `*` values win over the card numbers exactly as noted below.
- Variant rows take their **v3 card row** (hp/gears/kg/vmax/tank), `cc` from the card's
  litres, `acceleration` = card 0-100 × 0.6 (the v2 column's unit, sec to 60 km/h),
  their per-preset price, and inherit `fuel`/`trunk`/`brake` from the folder row.
  A card with no spec block (i30 N Performance Limited) inherits the folder's specs.
- `year` is now the game's own `cars.table` model year (field 4/5 of the table dump:
  name + year per folder), not the v2 estimate — the "future full-DB work should adopt
  these" note below is done. 55 rows shifted by 1-9 years; nothing keys on the year
  (ACEDRIVE has no year system; the week is the only calendar axis).
- `cars.table` official **display names** (e.g. "Mazda MX-5 NA") are dumped and
  available; `model` still carries this repo's lower-case stem, so adopting the
  showroom labels stays an open labelling pass.
- Validation: `JsonCarStore` drops any row with a negative spec or price and says so;
  a row with `price <= 0` is legal data but "not for sale" (the dealer skips it).

## In-build cars (69)
| ks_id | yr | hp | cc | kg | vmax | 0-60 | gears | tank | trunk | pax | class | shop€ | fuel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ks_abarth_695_biposto | 2014 | 190 | 1368 | 997 | 230 | 3.5 | 5 | 35 | 185 | 1 | 15 | 40000 | 8 | 38 |
| ks_alfa_romeo_75_turbo_evo | 1987 | 156* | 1762 | 1253* | 220 | 4.3 | 5 | 49 | 380 | 4 | 16 | 60000 | 10 | 45 |
| ks_alfa_romeo_giulia_gta_sprint | 1965 | 158* | 1570 | 848* | 200 | 4.0 | 5 | 46 | 250 | 3 | 16 | 220000 | 9 | 50 |
| ks_alfa_romeo_giulia_gtam | 2021 | 540 | 2891 | 1580 | 300 | 2.2 | 8 | 58 | 480 | 1 | 16 | 175000 | 12 | 34 |
| ks_alfa_romeo_junior | 2024 | 280 | 0 | 1635 | 200 | 3.5 | 1 | 0 | 400 | 4 | 6 | 47500 | 0 | 36 |
| ks_alpine_a110_s | 2019 | 300 | 1798 | 1114 | 260 | 2.5 | 7 | 45 | 196 | 1 | 13 | 70000 | 8 | 34 |
| ks_alpine_a290_b | 2024 | 220 | 0 | 1479 | 170 | 3.8 | 1 | 0 | 326 | 2 | 6 | 189000 | 0 | 36 |
| ks_audi_rs_3_sportback | 2021 | 400 | 2480 | 1565 | 290 | 2.3 | 7 | 55 | 282 | 4 | 6 | 62000 | 10 | 34 |
| ks_audi_rs_6_avant | 2019 | 600 | 3996 | 2075 | 305 | 2.2 | 8 | 73 | 565 | 4 | 6 | 125000 | 13 | 35 |
| ks_audi_sport_quattro | 1983 | 306 | 2133 | 1290 | 248 | 2.9 | 5 | 90 | 300 | 3 | 17 | ~550000 | 13 | 48 |
| ks_audi_r8_v10_performance | 2019 | 620 | 5204 | 1595 | 331 | 1.9 | 7 | 73 | 112 | 1 | 12 | ~150000 | 14 | 33 |
| ks_audi_r8_lms_gt4_evo | 2021 | 495 | 5204 | 1350 | 280 | 2.0 | 6 | 110 | 0 | 0 | 17 | ~198000 | 25 | 30 |
| ks_audi_r8_lms_gt2 | 2021 | 640 | 5204 | 1350 | 310 | 1.8 | 6 | 110 | 0 | 0 | 17 | ~338000 | 26 | 30 |
| ks_audi_r8_lms_gt3_evo_2 | 2022 | 585 | 5204 | 1225 | 300 | 1.8 | 6 | 110 | 0 | 0 | 17 | ~429000 | 28 | 30 |
| ks_bmw_m2_coupe | 2023 | 460 | 2993 | 1725 | 285 | 2.5 | 8 | 52 | 390 | 3 | 6 | ~65000 | 11 | 34 |
| ks_bmw_m2_cs_racing | 2023 | 450 | 2993 | 1500 | 270 | 2.4 | 7 | 80 | 0 | 0 | 17 | 95000 | 22 | 30 |
| ks_bmw_m3_e30_evo_iii | 1990 | 233* | 2467 | 1375* | 248 | 3.7 | 5 | 55 | 425 | 3 | 16 | 200000 | 11 | 44 |
| ks_bmw_m3_e46_csl | 2003 | 360 | 3246 | 1385 | 250 | 2.9 | 6 | 63 | 410 | 3 | 15 | 140000 | 12 | 36 |
| ks_bmw_m4_csl | 2022 | 550 | 2993 | 1625 | 307 | 2.2 | 8 | 59 | 440 | 1 | 15 | 165200 | 11 | 33 |
| ks_bmw_m4_gt3 | 2023 | 590 | 2993 | 1300 | 290 | 2.0 | 6 | 110 | 0 | 0 | 17 | 625000 | 28 | 30 |
| ks_bmw_m8_competition | 2019 | 625 | 4395 | 1885 | 305 | 1.9 | 8 | 68 | 420 | 3 | 6 | 165000 | 13 | 34 |
| ks_caterham_academy | 2024 | 125 | 1596 | 550 | 180 | 3.5 | 5 | 36 | 0 | 1 | 17 | 49500 | 7 | 42 |
| ks_caterham_485_csr | 2014 | 193* | 1999 | 695* | 225 | 2.4 | 5 | 36 | 130 | 1 | 14 | ~70000 | 9 | 40 |
| ks_chevrolet_camaro_zl1 | 2017 | 650 | 6162 | 1772 | 319 | 2.1 | 10 | 72 | 257 | 3 | 12 | 82400 | 15 | 34 |
| ks_dallara_stradale_coupe | 2017 | 400 | 2261 | 855 | 280 | 2.0 | 6 | 60 | 0 | 1 | 16 | 256000 | 9 | 32 |
| ks_dallara_exp | 2021 | 500 | 2300 | 890 | 290 | 1.9 | 6 | 50 | 0 | 1 | 18 | ~340000 | 20 | 30 |
| ks_datsun_240z_fairlady | 1970 | 198* | 2393 | 1235* | 201 | 4.8 | 5 | 60 | 200 | 1 | 10 | ~48000 | 10 | 48 |
| ks_ferrari_288_gto | 1984 | 400 | 2855 | 1160 | 304 | 2.9 | 5 | 110 | 0 | 1 | 17 | ~6000000 | 15 | 44 |
| ks_ferrari_296_gtb | 2021 | 819 | 2992 | 1470 | 330 | 1.7 | 8 | 65 | 198 | 1 | 14 | 275000 | 12 | 32 |
| ks_ferrari_296_gt3 | 2023 | 600 | 2992 | 1280 | 300 | 1.8 | 6 | 110 | 0 | 0 | 17 | ~650000 | 28 | 30 |
| ks_ferrari_488_challenge_evo | 2020 | 670 | 3902 | 1340 | 330 | 1.8 | 7 | 126 | 0 | 0 | 16 | 390000 | 28 | 30 |
| ks_ferrari_f40_lm | 1989 | 720 | 2936 | 1050 | 367 | 1.9 | 5 | 120 | 0 | 1 | 19 | 2800000 | 20 | 40 |
| ks_ferrari_daytona_sp3 | 2022 | 829 | 6496 | 1485 | 340 | 1.7 | 7 | 86 | 0 | 1 | 16 | 1500000 | 18 | 32 |
| ks_ferrari_f2004 | 2004 | 835 | 2997 | 605 | 350 | 1.6 | 7 | 120 | 0 | 0 | 19 | 3000000 | 35 | 28 |
| ks_ferrari_sf_25 | 2025 | 1000 | 1600 | 798 | 350 | 1.5 | 8 | 110 | 0 | 0 | 20 | 5000000 | 35 | 28 |
| ks_ford_escort_rs_cosworth | 1992 | 204* | 1993 | 1373* | 232 | 3.4 | 5 | 65 | 380 | 4 | 14 | 60000 | 11 | 44 |
| ks_ford_mustang_gt3 | 2024 | 550 | 5400 | 1289 | 280 | 1.9 | 6 | 110 | 0 | 0 | 18 | ~550000 | 28 | 30 |
| ks_honda_nsx_r | 1992 | 270 | 2977 | 1230 | 270 | 3.0 | 5 | 70 | 150 | 1 | 17 | 110000 | 11 | 42 |
| ks_honda_s2000_ap1 | 1999 | 240 | 1997 | 1274 | 240 | 3.7 | 6 | 50 | 140 | 1 | 11 | 26000 | 10 | 38 |
| ks_hyundai_i30_n_hb | 2020 | 272* | 1998 | 1513* | 250 | 3.5 | 6 | 50 | 395 | 4 | 6 | 40000 | 9 | 34 |
| ks_ktm_x_bow_gt2 | 2020 | 600 | 2480 | 1048 | 300 | 2.0 | 6 | 120 | 0 | 0 | 18 | ~294000 | 25 | 30 |
| ks_ktm_x_bow_gt4 | 2021 | 360 | 1984 | 1050 | 260 | 2.2 | 6 | 100 | 0 | 0 | 17 | ~174000 | 22 | 30 |
| ks_lamborghini_countach | 1985 | 353* | 5167 | 1588* | 295 | 2.9 | 5 | 120 | 0 | 1 | 16 | ~550000 | 18 | 46 |
| ks_lamborghini_huracan_sto | 2021 | 631 | 5204 | 1339 | 310 | 1.8 | 7 | 80 | 100 | 1 | 14 | 310000 | 15 | 32 |
| ks_lamborghini_huracan_st_evo2 | 2021 | 611 | 5204 | 1270 | 300 | 1.8 | 6 | 100 | 0 | 0 | 16 | 270000 | 26 | 30 |
| ks_lancia_delta_hf_integrale_evo_ii | 1993 | 198* | 1995 | 1396* | 220 | 3.4 | 5 | 57 | 250 | 4 | 15 | 150000 | 11 | 44 |
| ks_lotus_emira | 2022 | 400 | 3456 | 1486 | 290 | 2.6 | 6 | 60 | 208 | 1 | 6 | 82000 | 11 | 34 |
| ks_lotus_exige_v6_cup | 2014 | 350 | 3456 | 1110 | 274 | 2.4 | 6 | 40 | 30 | 1 | 17 | 77000 | 11 | 33 |
| ks_maserati_mc20_gt2 | 2023 | 640 | 2992 | 1250 | 330 | 1.7 | 6 | 100 | 0 | 0 | 18 | 385000 | 26 | 30 |
| ks_mazda_mx5_na | 1990 | 139* | 1598 | 1098* | 185 | 5.3 | 5 | 48 | 150 | 1 | 9 | 12000 | 8 | 44 |
| ks_mazda_mx5_nd_cup | 2016 | 181 | 1998 | 1060 | 210 | 3.6 | 6 | 45 | 130 | 0 | 17 | 51000 | 9 | 36 |
| ks_mazda_rx7_fd | 2002 | 280 | 1308 | 1340 | 250 | 3.2 | 5 | 76 | 160 | 1 | 15 | ~45000 | 12 | 38 |
| ks_mercedes_190e_evo_ii | 1990 | 232 | 2463 | 1340 | 250 | 4.3 | 5 | 70 | 400 | 3 | 16 | 299000 | 11 | 44 |
| ks_mercedes_amg_gt2 | 2023 | 707 | 3982 | 1400 | 320 | 1.8 | 6 | 120 | 0 | 0 | 17 | 590000 | 26 | 30 |
| ks_mini_jcs_1990 | 1990 | 78~ | 1275 | 700~ | 150 | 7.0 | 4 | 34 | 150 | 3 | 13 | 18000 | 7 | 46 |
| ks_peugeot_205_t16 | 1984 | 218* | 1775 | 1248* | 209 | 3.6 | 5 | 60 | 215 | 1 | 17 | 250000 | 11 | 44 |
| ks_porsche_718_cayman_gt4_cs_mr | 2019 | 425 | 3800 | 1320 | 290 | 2.2 | 6 | 100 | 0 | 0 | 16 | 196000 | 24 | 30 |
| ks_porsche_718_cayman_gt4_rs | 2022 | 493 | 3996 | 1415 | 315 | 2.0 | 7 | 64 | 260 | 1 | 14 | 150000 | 12 | 32 |
| ks_porsche_991ii_gt2_rs_clubsport_evo | 2023 | 690 | 3800 | 1400 | 330 | 1.7 | 7 | 115 | 0 | 0 | 17 | ~405000 | 28 | 30 |
| ks_porsche_992_gt3_cup | 2021 | 503 | 3996 | 1260 | 278 | 1.8 | 6 | 110 | 0 | 0 | 14 | 230000 | 26 | 30 |
| ks_porsche_992_gt3_r_rennsport | 2023 | 611 | 4194 | 1240 | 300 | 1.8 | 6 | 117 | 0 | 0 | 18 | ~951000 | 28 | 30 |
| ks_porsche_992_gt3_rs | 2022 | 518 | 3996 | 1450 | 296 | 1.9 | 7 | 64 | 137 | 1 | 14 | ~230000 | 13 | 32 |
| ks_porsche_964_turbo_3_6 | 1993 | 355 | 3600 | 1470 | 280 | 2.9 | 5 | 77 | 100 | 3 | 15 | 200000 | 12 | 40 |
| ks_porsche_935 | 2019 | 690 | 3800 | 1380 | 330 | 1.7 | 7 | 115 | 0 | 0 | 18 | ~702000 | 28 | 30 |
| ks_renault_5_gt_turbo | 1985 | 109* | 1397 | 933* | 201 | 4.7 | 5 | 43 | 245 | 3 | 9 | 21000 | 8 | 44 |
| ks_toyota_ae86_sprinter_trueno | 1983 | 248* | 1587 | 1018* | 195 | 5.3 | 5 | 50 | 290 | 3 | 9 | ~40000 | 9 | 44 |
| ks_toyota_gr86 | 2021 | 216* | 2387 | 1382* | 226 | 3.8 | 6 | 50 | 178 | 3 | 10 | 35000 | 9 | 35 |
| ks_toyota_supra_mkiv | 1993 | 330 | 2997 | 1565 | 257 | 3.2 | 6 | 70 | 284 | 3 | 12 | 83000 | 12 | 38 |
| ks_volkswagen_golf_gti_mk1 | 1976 | 110 | 1588 | 810 | 182 | 5.5 | 5 | 45 | 345 | 3 | 9 | ~27000 | 8 | 46 |
| ks_volkswagen_golf_gti_mk8 | 2020 | 296 | 1984 | 1464 | 250 | 3.4 | 7 | 50 | 374 | 4 | 6 | 45000 | 8 | 34 |
| ks_volkswagen_golf_r_mk8 | 2020 | 315 | 1984 | 1520 | 270 | 2.8 | 7 | 55 | 340 | 4 | 6 | ~49000 | 9 | 34 |

## NOT in 0.9 build (web-spec, kept for later updates/mods)
No ks_id in kspkg: Ferrari Purosangue, Ford Fiesta ST, Hyundai i20N/i30N-sedan/Ioniq5N/ElantraTCR/N Vision, Lamborghini Temerario, Lancia Delta Evo3 proto, Mercedes GT63/A45/G63, Mini JCW F66 (modern — build has 1990 classic instead), Morgan Super3, Nissan R34, Porsche 718 GT4 base road (build has CS MR race + GT4 RS), McMurtry Speirling, McLaren 765LT.
UNCONFIRMED (not in kspkg, needs in-game proof): Mazda MX-5 ND RS 2.0 (184hp) — tell me the exact showroom name if you see it and I will add it as verified.

Notes:
- shiftTime/maxRpm defaults per gearbox (see v1). Fill per-car in Lua import.
- carClass from CarClasses.md on model production; mass ongoing = 6.
- trunk 0 / pax 0 = no taxi/delivery jobs for that car.
- Kunos tunes differ from brochure (e.g. AE86 248hp, Datsun 198hp, MX-5 NA 139hp) — gameplay uses Kunos `*` values where present.
- TO_VERIFY (`~`): Mini 1990, race tank/trunk/fuel/brake/class, and every `~price` row (web market value — the game has no shop money for it). Bare numbers are exact game shop money — see shop€ note below.
- shop€ = exact in-game shop money for bare numbers, base card price = mech_1 × visual_1 (`system\prices.printableprices`, 2026-10-15 dump; replaced the v2 real-world guesses). Build 0.9: 48/71 cars shop-priced, 14 `no card` (0xFFFFFFFF — starter/reward/license cars, license column in the v3 table), 9 `no entry` (absent from the price table). Skins shift the price within a car (GTAm 175000→178000, A110 →78000, RS6 →138000, S2000 →34000, Daytona 1.5M→3M); the table always shows the cheapest visual_1 card price. Per-preset map for all 101: `prices_all_presets.csv` in the research dir; non-base presets in the v3 table below. v5 (2026-10-15): the rule is every variant carries a price, so all 23 `no card`/`no entry` cars now show a `~` web market value (EUR, Oct-2026 research; current market value for road/classic cars, ex-VAT customer-racing order price for race cars, rounded to 1k). The in-game counts (48/71 shop-priced) are unchanged — only `~` cells are we-added.

## Variants (mechanical presets) — v3 dump 2026-10-15 (read-only kspkg run)

Showroom "variants" are per-car `content\cars\<ks_id>\presets\*.mechanicalcarpreset`
files inside the SAME ks_ folder (PORTPLAN 62), not extra folders — that is why the
69-car file-table roster showed one row per car. Dumped with Nenkai
ACEvo.Package.CLI 1.1.0 (`list-files` + `unpack-file`), runbook + task spec in
VARIANT_RESEARCH.md.

Sources: all 101 `*.mechanicalcarpreset` files (71 folders);
`system\prices.printableprices` `mechanical_visuals` entries (price per (mech,visual)
pair); `system\cars.table` (official card names + years); preset naming scheme is
`preset_*_mech_N` (mechanical = variant axis we model) / `preset_*_visual_N`
(paint/skins — ignored). Spec-block ids (preset field 19) decoded cross-car:
1=hp, 2=Nm, 4=litres, 7=cylinders, 8=0-100 km/h s, 9=vmax km/h, 10=gears, 11=kg,
18=rev-limiter rpm (race cars only), 19=tank L. Field 7 = shop card/tier code (not
unique per preset, shown as `f7` for cross-reference). These are CARD spec numbers —
where they differ from the physics `*` values in the v2 table above, the `*` values
win for gameplay (existing v2 note: AE86 card 130 vs physics 248, Datsun 153 vs 198,
MX-5 NA 115 vs 139).

Prices = in-game shop money (€) for rows with a price entry; shown price = base card (mech × visual_1 — skins shift it, see the shop€ note under the v2 table). Game semantics of the dropped markers: `no card` = 0xFFFFFFFF entry (not purchasable in the shop: starter/reward/license cars), `no entry` = no (mech,visual) price row — per v5 both now render as `~` web market estimates in this table (full per-preset game dump still in `prices_all_presets.csv`). Uplifts between sibling `~` presets (Track/Final Edition, Weissach, tuned/spec rows) are estimated spreads around the one researched figure per car, following the game's own ~5–50% variant spacing. The earlier theory "sold as `carkit`/`parts` upgrade instead" is DEAD: v4 probe 2026-10-15 shows `parts`/`rims`/`carkits` carry zero real prices (every row 0xFFFFFFFF) and no rows at all for the 13 no-entry presets — they are simply not sold in-game.
Blank spec row (i30n mech_2): the file carries NO spec block — the Limited inherits its base.

24 multi-variant cars, every preset row (base included):

| ks_id | preset | f7 | in-game name | hp | Nm | L | cyl | g | kg | red | 0-100 | vmax | tank | price | license |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| bmw_m2_coupe | preset_m2_mech_1 | 302 | M2 Coupe Preset Base | 460 | 550 | 3.0 | 6 | 6 | 1775 | | 3.8 | 302 | 52 | ~65000 | Supercar |
| bmw_m2_coupe | preset_m2_mech_2 | 71 | M2 Coupe Performance | 460 | 550 | 3.0 | 6 | 6 | 1775 | | 3.8 | 302 | 52 | ~70000 | Supercar |
| bmw_m2_cs_racing | preset_m2csr_mech_1 | 10 | M2 CS Racing 350 | 365 | 550 | 3.0 | 6 | 7 | 1610 | | 4.2 | 270 | 120 | 95000 | Racing2 |
| bmw_m2_cs_racing | preset_m2csr_mech_2 | 10 | M2 CS Racing 450 | 450 | 550 | 3.0 | 6 | 7 | 1610 | | 3.8 | 285 | 120 | ~100000 | |
| caterham_485_csr | ks_caterham_485_csr_mech_1 | 305 | 485 CSR Road | 237 | 206 | 2.0 | 4 | 5 | 630 | | 3.9 | 225 | 36 | ~70000 | Entry |
| caterham_485_csr | ks_caterham_485_csr_mech_2 | 101 | 485 CSR Track | 237 | 206 | 2.0 | 4 | 5 | 630 | | 3.9 | 225 | 36 | ~75000 | Entry |
| caterham_485_csr | ks_caterham_485_csr_mech_3 | 101 | 485 CSR Final Edition | 237 | 206 | 2.0 | 4 | 5 | 630 | | 3.9 | 225 | 36 | ~82000 | Entry |
| chevrolet_camaro_zl1 | preset_zl1_mech_1 | 302 | ZL1 Standard | 659 | 881 | 6.2 | 8 | 6 | 1753 | | 3.5 | 318 | 72 | 82400 | Supercar |
| chevrolet_camaro_zl1 | preset_zl1_mech_2 | 90 | ZL1 1LE | 659 | 881 | 6.2 | 8 | 6 | 1753 | | 3.5 | 318 | 72 | 92500 | Hypercar |
| dallara_stradale_coupe | preset_dalsc_mech_1 | 61 | Stradale Coupe Preset | 400 | 500 | 2.3 | 4 | 6 | 855 | | 3.25 | 280 | 55 | 256000 | Hypercar |
| dallara_stradale_coupe | preset_dalsc_mech_2 | 60 | Stradale Spider Preset | 400 | 500 | 2.3 | 4 | 6 | 855 | | 3.25 | 280 | 55 | 236000 | Hypercar |
| dallara_stradale_coupe | preset_dalsc_mech_3 | 20 | Track - Stradale Coupe Preset | 400 | 500 | 2.3 | 4 | 6 | 855 | | 3.25 | 280 | 55 | 276000 | Hypercar |
| datsun_240z_fairlady | preset_240z_mech_1 | 305 | 240Z Preset Base | 153 | 198 | 2.4 | 6 | 4 | 1115 | | 8.0 | 201 | 60 | ~48000 | Entry |
| datsun_240z_fairlady | preset_240z_mech_2 | 101 | 240Z Preset Tuned | 197 | 240 | 2.8 | 6 | 5 | 1140 | | 7.0 | 220 | 60 | ~52000 | |
| ferrari_296_gtb | preset_296gtb_mech_1 | 302 | 296 GTB Standard Preset | 830 | 740 | 3.0 | 6 | 8 | 1675 | | 2.9 | 330 | 65 | 275000 | Hypercar |
| ferrari_296_gtb | preset_296gtb_mech_2 | 70 | 296 GTB Assetto Fiorano | 830 | 740 | 3.0 | 6 | 8 | 1660 | | 2.9 | 330 | 65 | 278000 | Hypercar |
| ford_escort_rs_cosworth | preset_escortrs_mech_1 | 305 | Escort RS Cosworth Preset Base | 227 | 294 | 2.0 | 4 | 5 | 1275 | | 6.1 | 230 | 65 | 60000 | Entry |
| ford_escort_rs_cosworth | preset_escortrs_mech_2 | 305 | Escort RS Cosworth "Motorsport" Spec | 227 | 294 | 2.0 | 4 | 5 | 1275 | | 6.1 | 230 | 65 | ~95000 | |
| hyundai_i30_n_hb | preset_i30n_hb_mech_1 | 301 | i30 N Performance Preset | 280 | 392 | 2.0 | 4 | 6 | 1490 | | 5.5 | 250 | 50 | 40000 | Entry |
| hyundai_i30_n_hb | preset_i30n_hb_mech_2 | 301 | i30 N Performance Limited | inherits base | | | | | | | | | | 42000 | Entry |
| lamborghini_huracan_sto | preset_sto_mech_1 | 303 | STO Preset Base | 640 | 565 | 5.2 | 10 | 7 | 1519 | | 3.0 | 310 | 80 | 310000 | Hypercar |
| lamborghini_huracan_sto | preset_sto_mech_2 | 80 | STO Preset Track | 640 | 565 | 5.2 | 10 | 7 | 1519 | | 3.0 | 310 | 80 | 310000 | Hypercar |
| lancia_delta_hf_integrale_evo_ii | preset_hfevoii_mech_1 | 301 | HF Integrale Evo 2 Preset Base | 215 | 314 | 2.0 | 4 | 5 | 1357 | | 7.1 | 220 | 57 | 150000 | Sportscar |
| lancia_delta_hf_integrale_evo_ii | preset_hfevoii_mech_2 | 301 | HF Integrale Evo 2 Evo 3 Prototype | 237 | 320 | 2.0 | 4 | 5 | 1357 | | 7.1 | 220 | 57 | 150000 | Sportscar |
| lotus_emira | preset_emira_mech_1 | 304 | Touring | 406 | 430 | 3.5 | 6 | 6 | 1405 | | 4.3 | 290 | 53 | 82000 | Supercar |
| lotus_emira | preset_emira_mech_2 | 70 | Sports | 406 | 430 | 3.5 | 6 | 6 | 1405 | | 4.3 | 290 | 53 | 82000 | Supercar |
| lotus_exige_v6_cup | preset_exigev6_mech_1 | 60 | Exige V6 Cup Base Preset | 350 | 400 | 3.5 | 6 | 6 | 1110 | | 3.8 | 275 | 40 | 77000 | Supercar |
| lotus_exige_v6_cup | preset_exigev6_mech_2 | 60 | Exige V6 Cup Motorsport Preset | 350 | 400 | 3.5 | 6 | 6 | 1110 | | 3.8 | 275 | 40 | 88000 | Supercar |
| mazda_mx5_na | preset_mx5na_mech_1 | 405 | MX5 NA Preset Base | 115 | 135 | 1.6 | 4 | 5 | 950 | | 8.8 | 195 | 45 | 12000 | Sportscar |
| mazda_mx5_na | preset_mx5na_mech_2 | 305 | MX5 NA Eunos RS Limited | 131 | 152 | 1.6 | 4 | 5 | 950 | | 8.8 | 195 | 45 | 18000 | Sportscar |
| mazda_mx5_nd_cup | preset_mx5ndcup_mech_1 | 25 | MX-5 ND Cup Preset Base (Global Cup ND1) | 155 | 200 | 2.0 | 4 | 6 | 1020 | 7000 | 6.5 | 225 | 45 | 51000 | Racing1 |
| mazda_mx5_nd_cup | preset_mx5ndcup_mech_2 | 25 | MX-5 ND Cup Sequential (ND2) | 181 | 200 | 2.0 | 4 | 6 | 1020 | 7500 | 6.0 | 240 | 45 | 53000 | Racing1 |
| mini_jcs_1990 | preset_jcs_mech_1 | 305 | John Cooper S | 83 | 114 | 1.3 | 4 | 4 | 710 | | 9.8 | 163 | 34 | 18000 | Entry |
| mini_jcs_1990 | preset_jcs_mech_2 | 101 | John Cooper S Tune | 160 | 150 | 1.6 | 4 | 5 | 710 | | 6.0 | 200 | 34 | 22000 | Entry |
| porsche_718_cayman_gt4_rs | preset_718gt4rs_mech_1 | 302 | 718 GT4 RS Preset | 500 | 450 | 4.0 | 6 | 7 | 1490 | 9000 | 3.4 | 315 | 64 | 150000 | Supercar |
| porsche_718_cayman_gt4_rs | preset_718gt4rs_mech_2 | 71 | 718 GT4 RS Weissach Package | 500 | 450 | 4.0 | 6 | 7 | 1490 | 9000 | 3.4 | 315 | 64 | 210000 | Supercar |
| porsche_964_turbo_3_6 | preset_964_mech_1 | 302 | 964 Turbo Preset Base | 360 | 520 | 3.6 | 6 | 5 | 1460 | | 4.8 | 280 | 92 | 200000 | Supercar |
| porsche_964_turbo_3_6 | preset_964_mech_2 | 302 | 964 Turbo Preset X88 | 385 | 520 | 3.6 | 6 | 5 | 1460 | | 4.8 | 280 | 92 | 200000 | Supercar |
| porsche_992_gt3_cup | preset_992c_mech_1 | — | Carrera Cup ABS TC Variant | 510 | 474 | 4.0 | 6 | 6 | 1260 | 8750 | 2.9 | 293 | 112 | 230000 | Racing3 |
| porsche_992_gt3_cup | preset_992c_mech_2 | — | Carrera Cup ABS Variant | 510 | 474 | 4.0 | 6 | 6 | 1260 | 8750 | 2.9 | 293 | 112 | 230000 | Racing3 |
| porsche_992_gt3_cup | preset_992c_mech_3 | — | Carrera Cup Base Variant (no ABS/TC) | 510 | 474 | 4.0 | 6 | 6 | 1260 | 8750 | 2.9 | 293 | 112 | 230000 | Racing3 |
| porsche_992_gt3_r_rennsport | preset_992ren_mech_1 | 5 | GT3 R Rennsport Base | 540 | 510 | 4.2 | 6 | 6 | 1250 | 9400 | 2.9 | 290 | 117 | ~951000 | Entry |
| porsche_992_gt3_r_rennsport | preset_992ren_mech_2 | 5 | GT3 R Rennsport Unrestricted | 620 | 530 | 4.2 | 6 | 6 | 1240 | 9400 | 2.9 | 310 | 117 | ~1000000 | |
| porsche_992_gt3_rs | preset_gt3rs_mech_1 | 302 | 992 GT3 RS Preset Clubsport | 525 | 465 | 4.0 | 6 | 7 | 1450 | 8500 | 3.2 | 296 | 90 | ~230000 | Entry |
| porsche_992_gt3_rs | preset_gt3rs_mech_2 | 70 | 992 GT3 RS Preset Weissach Package | 525 | 465 | 4.0 | 6 | 7 | 1430 | 8500 | 3.2 | 296 | 90 | ~260000 | Entry |
| toyota_ae86_sprinter_trueno | preset_ae86_mech_1 | 305 | GT-Apex Zenki Preset Base | 130 | 149 | 1.6 | 4 | 5 | 920 | | 8.5 | 180 | 50 | ~40000 | Entry |
| toyota_ae86_sprinter_trueno | preset_ae86_mech_2 | 305 | GT-Apex Kouki Preset Base | 130 | 149 | 1.6 | 4 | 5 | 920 | | 8.5 | 180 | 50 | ~40000 | Entry |
| toyota_ae86_sprinter_trueno | preset_ae86_mech_3 | 305 | GT-Apex Kouki Black Limited | 130 | 149 | 1.6 | 4 | 5 | 920 | | 8.5 | 180 | 50 | ~45000 | Entry |
| toyota_ae86_sprinter_trueno | preset_ae86_mech_4 | 101 | GT-Apex Zenki Anime Tribute | 250 | 182 | 1.6 | 4 | 6 | 920 | | 6.0 | 240 | 50 | ~50000 | Entry |
| toyota_gr86 | preset_gr86_mech_1 | 302 | GR86 Preset Base | 235 | 250 | 2.4 | 4 | 6 | 1270 | 7400 | 6.3 | 225 | 50 | 35000 | Sportscar |
| toyota_gr86 | preset_gr86_mech_2 | 302 | GR86 Trueno Edition | 235 | 250 | 2.4 | 4 | 6 | 1270 | 7400 | 6.3 | 225 | 50 | 40000 | Sportscar |
| toyota_gr86 | preset_gr86_mech_3 | 302 | GR86 Hakone Edition | 235 | 250 | 2.4 | 4 | 6 | 1270 | 7400 | 6.3 | 225 | 50 | 42000 | Sportscar |
| toyota_supra_mkiv | preset_supramkiv_mech_1 | 303 | Supra MKIV Preset Base | 280 | 451 | 3.0 | 6 | 6 | 1510 | | 5.1 | 254 | 70 | 83000 | Sportscar |
| toyota_supra_mkiv | preset_supramkiv_mech_2 | 101 | Supra MKIV Drift | 600 | 700 | 3.0 | 6 | 6 | 1510 | 8000 | 4.0 | 280 | 70 | 125000 | Sportscar |

The M2 case (PORTPLAN 62 motivation): `preset_m2_mech_1` = "M2 Coupe Preset Base"
(the showroom's M2 G87 Coupe) and `preset_m2_mech_2` = "M2 Coupe Performance"
(the M Performance card) — same engine spec block; the Performance card differs by
mandatory `m2_perf_*` parts (body/bumpers/suspension + `steptronic` tuning gearbox),
which is what the log announces via the preset path.

cars.json variant rows (30 new): base rows stay PRESET-LESS (folder row = base card,
so saved road-test keys keep resolving); every non-base preset got a row with
`"preset": <mech file stem>`, year/brand/passengers from the base row and model =
base stem + game variant suffix. `RoadTestCar.Key` = `id#preset` keeps road tests,
banked ids and taxi capacity per variant.

`cars.table` official card names + model years for these folders — **adopted into
`Data/cars.json` in v6** (years are the shipped values now):
BMW M2 Coupe 2021; BMW M2 CS Racing 2020; Caterham 485 CSR 2023; Chevrolet Camaro
ZL1 2023; Dallara Stradale 2017; Datsun 240Z (S30) 1971; Ferrari 296 GTB 2022; Ford
Escort RS Cosworth 1994; Hyundai i30 N Hatchback 2021; Lancia Delta HF integrale
Evoluzione II 1993; Lotus Emira 2022; Lotus Exige V6 Cup 2014; Mazda MX-5 NA 1990;
Mazda MX-5 ND Cup 2016; Mini John Cooper S 1990; Porsche 718 Cayman GT4 RS 2022;
Porsche 911 Turbo 3.6 (964) 1993; Porsche 911 GT3 Cup (992) 2021; Porsche 911 GT3 R
Rennsport (992) 2022; Porsche 911 GT3 RS (992) 2021; Toyota Sprinter Trueno
1600GT-Apex (AE86) 1984; Toyota GR86 2021; Toyota Supra MKIV 1998.
(Huracan STO name row was empty in the parse — verify in-game.)

Still IN-GAME: exact showroom labels per preset (per-preset `.en.loc` overrides exist
for zl1/296gtb/mx5na/mx5nd/jcs/gt3rs/supramkiv/ae86-mech4 + totem; the rest fall back
to the car `en.loc`/`cars.table` name). The "does `no card` price itself from another
table?" question is CLOSED (v4 probe: no — v5 shows `~` web estimates for those rows
instead). `parts`/`rims`/`carkits` price rows also live in
`prices.printableprices` — relevant when the dealer/upgrade flows (PORTPLAN 28/29)
need per-part money (dealer prices should then follow this table's rule: bare =
game money, `~` = web estimate).
