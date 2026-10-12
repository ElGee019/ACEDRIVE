# CARSTATS_EVO v2 — Assetto Corsa EVO (build 0.9, roster verified from game files)

Roster source: `content.kspkg` file table dumped with Nenkai ACEvo.Package 1.1.0 → 69 cars.
`ks_` id = internal Kunos id. `*` after hp/kg = Kunos-verified from physics log (`POWER CLASS INDEX`, W includes driver/fluids).

Format follows mother game `CarClasses.md` + `CarStats.cs`:
`maxSpeed km/h | acceleration = sec to 60km/h (est = 0-100*0.6) | brakeDistance m (est) | trunkSpace L | passengerCapacity (driver excluded) | carClass (production table) | rarity=1 | hp | cc (0=EV) | gears | shiftTime s (est) | maxRpm (est) | maxFuel L (0=EV) | fuelConsumption L/100km (0=EV) | mass kg | price € today | year`. `odometerStartingValue=0, roadTested=0, designer=""` for all.

## What Kunos provides already vs what we add
- KUNOS_NATIVE: hp, cc, mass, gears, vmax, acceleration, tank, fuel burn, year/brand/model, Power Class Index. Provenance: `content\cars\<ks_id>\data\CarData.car` (protobuf) + physics log. Exact Kunos hp/kg captured for 14 cars below (`*`).
- WE_ADD: trunkSpace, passengerCapacity, carClass, rarity=1, price_today, brakeDistance, shiftTime/maxRpm, odometer=0, roadTested=0.
- EVs in build: Junior, A290 only (fuel 0).

## In-build cars (69)
| ks_id | yr | hp | cc | kg | vmax | 0-60 | gears | tank | trunk | pax | class | price€ | fuel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ks_abarth_695_biposto | 2014 | 190 | 1368 | 997 | 230 | 3.5 | 5 | 35 | 185 | 1 | 15 | 35000 | 8 | 38 |
| ks_alfa_romeo_75_turbo_evo | 1987 | 156* | 1762 | 1253* | 220 | 4.3 | 5 | 49 | 380 | 4 | 16 | 50000 | 10 | 45 |
| ks_alfa_romeo_giulia_gta_sprint | 1965 | 158* | 1570 | 848* | 200 | 4.0 | 5 | 46 | 250 | 3 | 16 | 250000 | 9 | 50 |
| ks_alfa_romeo_giulia_gtam | 2021 | 540 | 2891 | 1580 | 300 | 2.2 | 8 | 58 | 480 | 1 | 16 | 181500 | 12 | 34 |
| ks_alfa_romeo_junior | 2024 | 280 | 0 | 1635 | 200 | 3.5 | 1 | 0 | 400 | 4 | 6 | 48500 | 0 | 36 |
| ks_alpine_a110_s | 2019 | 300 | 1798 | 1114 | 260 | 2.5 | 7 | 45 | 196 | 1 | 13 | 75000 | 8 | 34 |
| ks_alpine_a290_b | 2024 | 220 | 0 | 1479 | 170 | 3.8 | 1 | 0 | 326 | 2 | 6 | 41900 | 0 | 36 |
| ks_audi_rs_3_sportback | 2021 | 400 | 2480 | 1565 | 290 | 2.3 | 7 | 55 | 282 | 4 | 6 | 68000 | 10 | 34 |
| ks_audi_rs_6_avant | 2019 | 600 | 3996 | 2075 | 305 | 2.2 | 8 | 73 | 565 | 4 | 6 | 128000 | 13 | 35 |
| ks_audi_sport_quattro | 1983 | 306 | 2133 | 1290 | 248 | 2.9 | 5 | 90 | 300 | 3 | 17 | 500000 | 13 | 48 |
| ks_audi_r8_v10_performance | 2019 | 620 | 5204 | 1595 | 331 | 1.9 | 7 | 73 | 112 | 1 | 12 | 250000 | 14 | 33 |
| ks_audi_r8_lms_gt4_evo | 2021 | 495 | 5204 | 1350 | 280 | 2.0 | 6 | 110 | 0 | 0 | 17 | 250000 | 25 | 30 |
| ks_audi_r8_lms_gt2 | 2021 | 640 | 5204 | 1350 | 310 | 1.8 | 6 | 110 | 0 | 0 | 17 | 300000 | 26 | 30 |
| ks_audi_r8_lms_gt3_evo_2 | 2022 | 585 | 5204 | 1225 | 300 | 1.8 | 6 | 110 | 0 | 0 | 17 | 400000 | 28 | 30 |
| ks_bmw_m2_coupe | 2023 | 460 | 2993 | 1725 | 285 | 2.5 | 8 | 52 | 390 | 3 | 6 | 80000 | 11 | 34 |
| ks_bmw_m2_cs_racing | 2023 | 450 | 2993 | 1500 | 270 | 2.4 | 7 | 80 | 0 | 0 | 17 | 180000 | 22 | 30 |
| ks_bmw_m3_e30_evo_iii | 1990 | 233* | 2467 | 1375* | 248 | 3.7 | 5 | 55 | 425 | 3 | 16 | 200000 | 11 | 44 |
| ks_bmw_m3_e46_csl | 2003 | 360 | 3246 | 1385 | 250 | 2.9 | 6 | 63 | 410 | 3 | 15 | 120000 | 12 | 36 |
| ks_bmw_m4_csl | 2022 | 550 | 2993 | 1625 | 307 | 2.2 | 8 | 59 | 440 | 1 | 15 | 165000 | 11 | 33 |
| ks_bmw_m4_gt3 | 2023 | 590 | 2993 | 1300 | 290 | 2.0 | 6 | 110 | 0 | 0 | 17 | 450000 | 28 | 30 |
| ks_bmw_m8_competition | 2019 | 625 | 4395 | 1885 | 305 | 1.9 | 8 | 68 | 420 | 3 | 6 | 180000 | 13 | 34 |
| ks_caterham_academy | 2024 | 125 | 1596 | 550 | 180 | 3.5 | 5 | 36 | 0 | 1 | 17 | 52000 | 7 | 42 |
| ks_caterham_485_csr | 2014 | 193* | 1999 | 695* | 225 | 2.4 | 5 | 36 | 130 | 1 | 14 | 80000 | 9 | 40 |
| ks_chevrolet_camaro_zl1 | 2017 | 650 | 6162 | 1772 | 319 | 2.1 | 10 | 72 | 257 | 3 | 12 | 90000 | 15 | 34 |
| ks_dallara_stradale_coupe | 2017 | 400 | 2261 | 855 | 280 | 2.0 | 6 | 60 | 0 | 1 | 16 | 230000 | 9 | 32 |
| ks_dallara_exp | 2021 | 500 | 2300 | 890 | 290 | 1.9 | 6 | 50 | 0 | 1 | 18 | 340000 | 20 | 30 |
| ks_datsun_240z_fairlady | 1970 | 198* | 2393 | 1235* | 201 | 4.8 | 5 | 60 | 200 | 1 | 10 | 35000 | 10 | 48 |
| ks_ferrari_288_gto | 1984 | 400 | 2855 | 1160 | 304 | 2.9 | 5 | 110 | 0 | 1 | 17 | 3500000 | 15 | 44 |
| ks_ferrari_296_gtb | 2021 | 819 | 2992 | 1470 | 330 | 1.7 | 8 | 65 | 198 | 1 | 14 | 270000 | 12 | 32 |
| ks_ferrari_296_gt3 | 2023 | 600 | 2992 | 1280 | 300 | 1.8 | 6 | 110 | 0 | 0 | 17 | 500000 | 28 | 30 |
| ks_ferrari_488_challenge_evo | 2020 | 670 | 3902 | 1340 | 330 | 1.8 | 7 | 126 | 0 | 0 | 16 | 330000 | 28 | 30 |
| ks_ferrari_f40_lm | 1989 | 720 | 2936 | 1050 | 367 | 1.9 | 5 | 120 | 0 | 1 | 19 | 5000000 | 20 | 40 |
| ks_ferrari_daytona_sp3 | 2022 | 829 | 6496 | 1485 | 340 | 1.7 | 7 | 86 | 0 | 1 | 16 | 2000000 | 18 | 32 |
| ks_ferrari_f2004 | 2004 | 835 | 2997 | 605 | 350 | 1.6 | 7 | 120 | 0 | 0 | 19 | 3000000 | 35 | 28 |
| ks_ferrari_sf_25 | 2025 | 1000 | 1600 | 798 | 350 | 1.5 | 8 | 110 | 0 | 0 | 20 | 5000000 | 35 | 28 |
| ks_ford_escort_rs_cosworth | 1992 | 204* | 1993 | 1373* | 232 | 3.4 | 5 | 65 | 380 | 4 | 14 | 60000 | 11 | 44 |
| ks_ford_mustang_gt3 | 2024 | 550 | 5400 | 1289 | 280 | 1.9 | 6 | 110 | 0 | 0 | 18 | 450000 | 28 | 30 |
| ks_honda_nsx_r | 1992 | 270 | 2977 | 1230 | 270 | 3.0 | 5 | 70 | 150 | 1 | 17 | 150000 | 11 | 42 |
| ks_honda_s2000_ap1 | 1999 | 240 | 1997 | 1274 | 240 | 3.7 | 6 | 50 | 140 | 1 | 11 | 20000 | 10 | 38 |
| ks_hyundai_i30_n_hb | 2020 | 272* | 1998 | 1513* | 250 | 3.5 | 6 | 50 | 395 | 4 | 6 | 38000 | 9 | 34 |
| ks_ktm_x_bow_gt2 | 2020 | 600 | 2480 | 1048 | 300 | 2.0 | 6 | 120 | 0 | 0 | 18 | 300000 | 25 | 30 |
| ks_ktm_x_bow_gt4 | 2021 | 360 | 1984 | 1050 | 260 | 2.2 | 6 | 100 | 0 | 0 | 17 | 200000 | 22 | 30 |
| ks_lamborghini_countach | 1985 | 353* | 5167 | 1588* | 295 | 2.9 | 5 | 120 | 0 | 1 | 16 | 500000 | 18 | 46 |
| ks_lamborghini_huracan_sto | 2021 | 631 | 5204 | 1339 | 310 | 1.8 | 7 | 80 | 100 | 1 | 14 | 300000 | 15 | 32 |
| ks_lamborghini_huracan_st_evo2 | 2021 | 611 | 5204 | 1270 | 300 | 1.8 | 6 | 100 | 0 | 0 | 16 | 250000 | 26 | 30 |
| ks_lancia_delta_hf_integrale_evo_ii | 1993 | 198* | 1995 | 1396* | 220 | 3.4 | 5 | 57 | 250 | 4 | 15 | 100000 | 11 | 44 |
| ks_lotus_emira | 2022 | 400 | 3456 | 1486 | 290 | 2.6 | 6 | 60 | 208 | 1 | 6 | 95000 | 11 | 34 |
| ks_lotus_exige_v6_cup | 2014 | 350 | 3456 | 1110 | 274 | 2.4 | 6 | 40 | 30 | 1 | 17 | 100000 | 11 | 33 |
| ks_maserati_mc20_gt2 | 2023 | 640 | 2992 | 1250 | 330 | 1.7 | 6 | 100 | 0 | 0 | 18 | 450000 | 26 | 30 |
| ks_mazda_mx5_na | 1990 | 139* | 1598 | 1098* | 185 | 5.3 | 5 | 48 | 150 | 1 | 9 | 12000 | 8 | 44 |
| ks_mazda_mx5_nd_cup | 2016 | 181 | 1998 | 1060 | 210 | 3.6 | 6 | 45 | 130 | 0 | 17 | 95000 | 9 | 36 |
| ks_mazda_rx7_fd | 2002 | 280 | 1308 | 1340 | 250 | 3.2 | 5 | 76 | 160 | 1 | 15 | 70000 | 12 | 38 |
| ks_mercedes_190e_evo_ii | 1990 | 232 | 2463 | 1340 | 250 | 4.3 | 5 | 70 | 400 | 3 | 16 | 250000 | 11 | 44 |
| ks_mercedes_amg_gt2 | 2023 | 707 | 3982 | 1400 | 320 | 1.8 | 6 | 120 | 0 | 0 | 17 | 400000 | 26 | 30 |
| ks_mini_jcs_1990 | 1990 | 78~ | 1275 | 700~ | 150 | 7.0 | 4 | 34 | 150 | 3 | 13 | 15000 | 7 | 46 |
| ks_peugeot_205_t16 | 1984 | 218* | 1775 | 1248* | 209 | 3.6 | 5 | 60 | 215 | 1 | 17 | 300000 | 11 | 44 |
| ks_porsche_718_cayman_gt4_cs_mr | 2019 | 425 | 3800 | 1320 | 290 | 2.2 | 6 | 100 | 0 | 0 | 16 | 160000 | 24 | 30 |
| ks_porsche_718_cayman_gt4_rs | 2022 | 493 | 3996 | 1415 | 315 | 2.0 | 7 | 64 | 260 | 1 | 14 | 170000 | 12 | 32 |
| ks_porsche_991ii_gt2_rs_clubsport_evo | 2023 | 690 | 3800 | 1400 | 330 | 1.7 | 7 | 115 | 0 | 0 | 17 | 550000 | 28 | 30 |
| ks_porsche_992_gt3_cup | 2021 | 503 | 3996 | 1260 | 278 | 1.8 | 6 | 110 | 0 | 0 | 14 | 250000 | 26 | 30 |
| ks_porsche_992_gt3_r_rennsport | 2023 | 611 | 4194 | 1240 | 300 | 1.8 | 6 | 117 | 0 | 0 | 18 | 1000000 | 28 | 30 |
| ks_porsche_992_gt3_rs | 2022 | 518 | 3996 | 1450 | 296 | 1.9 | 7 | 64 | 137 | 1 | 14 | 240000 | 13 | 32 |
| ks_porsche_964_turbo_3_6 | 1993 | 355 | 3600 | 1470 | 280 | 2.9 | 5 | 77 | 100 | 3 | 15 | 200000 | 12 | 40 |
| ks_porsche_935 | 2019 | 690 | 3800 | 1380 | 330 | 1.7 | 7 | 115 | 0 | 0 | 18 | 1450000 | 28 | 30 |
| ks_renault_5_gt_turbo | 1985 | 109* | 1397 | 933* | 201 | 4.7 | 5 | 43 | 245 | 3 | 9 | 20000 | 8 | 44 |
| ks_toyota_ae86_sprinter_trueno | 1983 | 248* | 1587 | 1018* | 195 | 5.3 | 5 | 50 | 290 | 3 | 9 | 30000 | 9 | 44 |
| ks_toyota_gr86 | 2021 | 216* | 2387 | 1382* | 226 | 3.8 | 6 | 50 | 178 | 3 | 10 | 35000 | 9 | 35 |
| ks_toyota_supra_mkiv | 1993 | 330 | 2997 | 1565 | 257 | 3.2 | 6 | 70 | 284 | 3 | 12 | 90000 | 12 | 38 |
| ks_volkswagen_golf_gti_mk1 | 1976 | 110 | 1588 | 810 | 182 | 5.5 | 5 | 45 | 345 | 3 | 9 | 25000 | 8 | 46 |
| ks_volkswagen_golf_gti_mk8 | 2020 | 296 | 1984 | 1464 | 250 | 3.4 | 7 | 50 | 374 | 4 | 6 | 55000 | 8 | 34 |
| ks_volkswagen_golf_r_mk8 | 2020 | 315 | 1984 | 1520 | 270 | 2.8 | 7 | 55 | 340 | 4 | 6 | 60000 | 9 | 34 |

## NOT in 0.9 build (web-spec, kept for later updates/mods)
No ks_id in kspkg: Ferrari Purosangue, Ford Fiesta ST, Hyundai i20N/i30N-sedan/Ioniq5N/ElantraTCR/N Vision, Lamborghini Temerario, Lancia Delta Evo3 proto, Mercedes GT63/A45/G63, Mini JCW F66 (modern — build has 1990 classic instead), Morgan Super3, Nissan R34, Porsche 718 GT4 base road (build has CS MR race + GT4 RS), McMurtry Speirling, McLaren 765LT.
UNCONFIRMED (not in kspkg, needs in-game proof): Mazda MX-5 ND RS 2.0 (184hp) — tell me the exact showroom name if you see it and I will add it as verified.

Notes:
- shiftTime/maxRpm defaults per gearbox (see v1). Fill per-car in Lua import.
- carClass from CarClasses.md on model production; mass ongoing = 6.
- trunk 0 / pax 0 = no taxi/delivery jobs for that car.
- Kunos tunes differ from brochure (e.g. AE86 248hp, Datsun 198hp, MX-5 NA 139hp) — gameplay uses Kunos `*` values where present.
- TO_VERIFY (`~`): Mini 1990, race tank/trunk/fuel/brake/class, all prices.
