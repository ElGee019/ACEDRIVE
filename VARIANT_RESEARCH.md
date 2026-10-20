# VARIANT_RESEARCH — AC EVO car variants (mechanical presets)

Status: **DONE 2026-10-15** (build 0.9, Nenkai ACEvo.Package.CLI 1.1.0, read-only).
Answers + full per-variant spec/price table landed in `CARSTATS_EVO.md`
("Variants (mechanical presets) — v3") and the 30 variant rows in
`Data/cars.json`. The raw CSVs sit in `%LOCALAPPDATA%\Temp\opencode\kspkg-research\`
(`content_files.txt`, `preset_specs.csv`, `prices_mech.csv`, `prices_all_presets.csv`, `variant_table.md`) —
temp, so re-run this runbook after big game updates to refresh.

## Findings (answers to the questions below)

1. **Completeness** — 101 `*.mechanicalcarpreset` files across 71 car folders;
   24 folders carry 2+ mechanical presets (the full variant roster is the v3 table
   in `CARSTATS_EVO.md`: M2, M2 CS Racing, Caterham 485 CSR, Camaro ZL1, Dallara
   Stradale, 240Z, 296 GTB, Escort RS, i30 N, Huracan STO, Delta Evo 2, Emira,
   Exige V6 Cup, MX-5 NA, MX-5 ND Cup, Mini JCS, 718 GT4 RS, 964 Turbo, 992 GT3
   Cup, 992 GT3 R Rennsport, 992 GT3 RS, AE86, GR86, Supra MKIV).
2. **M2 proof** — CONFIRMED from the game's own files: `preset_m2_mech_1`
   (internal name "M2 Coupe Preset Base", suffix "Standard") and `preset_m2_mech_2`
   ("M2 Coupe Performance", suffix "Performance"). Same engine spec block; the
   Performance card differs by mandatory `m2_perf_*` parts + `steptronic`
   tuning gearbox, exactly what the log's preset path announces.
3. **Stats source** — each `*.mechanicalcarpreset` carries a spec block (proto
   field 19, entries `{id, unit, f32}`): 1=hp, 2=Nm, 4=litres, 7=cylinders,
   8=0-100 s, 9=vmax km/h, 10=gears, 11=kg, 18=rev-limiter (race cars), 19=tank L.
   These are showroom/CARD numbers; where they differ from the physics-verified
   `*` values (AE86 130 vs 248, 240Z 153 vs 198, MX-5 NA 115 vs 139) the
   existing v2 note stands: gameplay uses the `*` values.
4. **Names** — card brand/name/suffix live directly in the mech preset (proto
   fields 1/2/3). `uiresources\localization\cars\<ks_id>\` holds `<preset-stem>.en.loc`
   overrides (long description prose) for zl1/296gtb/mx5na/mx5nd/jcs/gt3rs/
   supramkiv/ae86-mech4 + totem; `system\cars.table` is the canonical per-folder
   display name + model year (differs from v2 year estimates on several cars).
5. **Preset scheme** — `preset_*_mech_N.mechanicalcarpreset` (mechanical variant,
   the axis we model) + `preset_*_visual_N.visualcarpreset` (skins — ignored).
   Other preset-dir files (`carpart`/`compatiblepart`/`compatibletyres`/`carkit`)
   are parts/tires/kits, not variant identities.
6. **Telemetry cross-check** — the preset path appears ONLY in the
   `onSetPlayerCurrentCarCommand` log line (SHM's `car_model` is the folder id).
   `CarLogWatcher` + `CarBrand.FindCurrentIdentity`/`RefinedBy` (preset-less
   "Loaded car" echo keeps the spawn line's preset) is sufficient.

Prices: `system\prices.printableprices`, category `mechanical_visuals`, one entry
per (mech, visual) pair with field 3 = in-game shop price. `0xFFFFFFFF` = "no
card" (starter/reward/license-gated, incl. both M2 variants); a few variant
presets have NO price row ("no entry" — e.g. M2 CS Racing 450, 240Z Tuned, Escort
"Motorsport" Spec, GT3 R Unrestricted). V4 probe (2026-10-15): the "sold as
carkit/parts upgrade instead" theory is dead — `parts`/`rims`/`carkits` carry zero
real prices (all 0xFFFFFFFF) and no rows for the 13 no-entry presets; base card
price = mech × visual_1, skins shift it (GTAm +3k, Daytona up to 2×). Exact shop
prices for all 101 presets: `prices_all_presets.csv`; the single-variant cars'
prices are now in the `shop€` column of `CARSTATS_EVO.md`'s v2 table.

## Open / in-game verification

- Exact showroom labels per preset (the loc overrides are prose; our cars.json
  model strings use base stem + game card suffix, close enough — spot-check the
  showroom cards when convenient).
- Whether "no card" cars display a shop price sourced elsewhere. — RESOLVED v4
  (2026-10-15): no. All four `printableprices` categories probed (parts/rims/carkits
  rows are 0xFFFFFFFF or absent) and `cars.table`/`showroom_*` files carry no
  price fields. Not-shop-priced presets have no money price anywhere in build 0.9.
- Huracan STO entry name was empty in the cars.table parse.

## Runbook (to refresh after a game update)

1. Fetch `ACEvo.Package.CLI-<ver>-win-x64.zip` from
   https://github.com/Nenkai/ACEvo.Package/releases , verify the published SHA256,
   extract to `%LOCALAPPDATA%\Temp\opencode\kspkg-research\cli\`.
2. `ACEvo.Package.CLI.exe list-files -i "<steam>\Assetto Corsa EVO\content.kspkg"`.
   NOTE: the CLI writes `content_files.txt` NEXT TO the kspkg (game dir!) — move it
   to the research dir immediately; the game dir must stay clean.
3. Parse `content\cars\<id>\presets\` rows for `*.mechanicalcarpreset` paths
   (rebuild from capture groups, do not slice the trailing " - ").
4. `unpack-file -f <path> -o <research>\files\` per mech preset (writes the full
   relative tree), plus `system\prices.printableprices`, `system\cars.table`,
   `system\mechanical_preset_ids.table`.
5. Decode with a wire-format dumper (see `proto-dump.ps1`/`research.ps1` in the
   research dir) and regenerate the v3 table + cars.json rows.
6. Rebuild `Data/cars.json` with `build_carsjson_v6.ps1` (same dir): it reads the
   v2/v3 tables of `CARSTATS_EVO.md`, the `prices_all_presets.csv` game dump and
   `cars_dump.txt` (cars.table names + model years), cross-checks every price and
   writes the 101 spec rows. `JsonCarStoreTests.ShippedData_CarriesTheWholeRosterWithItsSpecRow`
   pins the result, so a drifted file fails the build instead of the game.
7. Everything READ-ONLY against the Steam dir; only ever move away the file the
   CLI itself dropped there.

## Historical task spec (original questions, kept for provenance)

Context at the time of writing: `cars.json` was one row per `ks_` folder and the
identity plumbing carried no preset; the hypothesis was that a showroom "variant"
= a distinct `.mechanicalcarpreset` inside one car folder, keyed in the localized
name path `coui://uiresources/localization/cars/<ks_id>/<preset-stem>.en.loc`.
Ground-truth log lines that motivated it:

```
[gameplay] ACEVO-2629 onSetPlayerCurrentCarCommand: Set new car 4c729a71-d828-0c01-… content\cars\ks_abarth_695_biposto\presets\preset_695b_mech_1.mechanicalcarpreset
[ui] Selected BMW M2 Coupe preset_m2_mech_1 preset_m2_visual_1
[gameface] Failed loading resource: coui://uiresources/localization/cars/ks_bmw_m2_coupe/preset_m2_mech_1.en.loc
[gameface] Failed loading resource: coui://uiresources/localization/cars/ks_bmw_m2_coupe/en.loc
```
