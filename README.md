# AceDrive

External gameplay overlay for Assetto Corsa EVO freeroam (Eifel). EVO exposes
no in-game UI/scripting API (SDK is cars-only), so gameplay lives in a
transparent always-on-top WPF window driven by EVO shared memory
(`Local\acevo_pmf_*`). Reads only — coexists with PitVox and SimHub.

Gameplay scope: [`PORTPLAN.md`](PORTPLAN.md). Car data: [`CARSTATS_EVO.md`](CARSTATS_EVO.md).
Agent working rules: [`AGENTS.md`](AGENTS.md).

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.x)
- Windows 10/11, EVO in **borderless** (exclusive fullscreen hides overlays)

## Build / test / run

```powershell
dotnet build ACEDRIVE.slnx   # zero warnings (enforced)
dotnet test ACEDRIVE.slnx    # 17 tests
dotnet run --project src/AceDrive.Overlay
```

Tester build (self-contained single exe, no runtime install needed):

```powershell
dotnet publish src/AceDrive.Overlay -p:PublishProfile=win-x64
# → src/AceDrive.Overlay/bin/Release/net10.0-windows/win-x64/publish/
```

The output is one compressed ~85 MB exe plus a loose `Data\` folder (content
tables, car thumbnails) and `Assets\Fonts\*-OFL.txt`. Send the folder (or a zip
of it), never the exe alone: the overlay reads `Data\` beside itself. Publish
copies the Vosk model from `%APPDATA%\AceDrive\vosk-model` when present
(~68 MB, voice).

The Firebase config is **opt-in**: a publish ships no key unless you name one,
so a demo build cannot carry the production credential by accident. Without a
config the overlay is fully playable and the online boards use their offline
fallbacks (last saved rank, rarity 1, name reservation skipped) — see
[`FIREBASE.md`](FIREBASE.md).

```powershell
# demo build for a friend: no key in the zip, and its own fresh profile
dotnet publish src/AceDrive.Overlay -p:PublishProfile=win-x64 -p:DemoBuild=true

# online build: point it at a throwaway project, never the live one
dotnet publish src/AceDrive.Overlay -p:PublishProfile=win-x64 `
  -p:FirebaseConfigPath="$env:APPDATA\AceDrive\firebase.test.json"
```

`DemoBuild` compiles the demo to keep its saves and logs in
`%APPDATA%\AceDrive-Demo` instead of `%APPDATA%\AceDrive`, so the first run
always starts at onboarding — even on a machine that already plays AceDrive
(there is no pre-existing profile to include or overwrite).

The model is never committed; override its path with `-p:VoskModelDir`. The
published exe is signed after the single-file bundle, because the Build-time
signature cannot survive bundling. The development certificate is trusted only
on the machine that made it, so another PC still shows SmartScreen's "Unknown
publisher" — trust everywhere needs a publicly trusted OV/EV certificate (a
PFX in `ACEDRIVE_SIGN_PFX`) or Azure Trusted Signing. Until then the tester
unblocks the zip, extracts it and chooses More info → Run anyway.

`dotnet format ACEDRIVE.slnx --verify-no-changes` must pass before
committing; CI enforces it. Fix findings by hand — never run unattended
fixer passes (one once removed a needed using via an IDE0005 false positive).

## Using the test shell over EVO

1. Start EVO, go on track. Start the overlay.
2. It opens **Locked**: clicks pass through to the game.
3. `Ctrl+Shift+C` unlocks: drag the header to move, corner grip to resize,
   buttons clickable. Same hotkey locks it again to drive.
4. `New job` → `Pickup` → drive: distance and fare are calculated from the
   lap (where you started → pickup → drop-off), not from the driven path.
   `Dropoff` shows receipt + tip and accumulates the shift total.
5. Bounds persist to `%AppData%\AceDrive\window.json`,
   logs to `%AppData%\AceDrive\logs\`.

## Structure

```
src/AceDrive.Core/      pure logic (jobs, fares, clock/log abstractions)
src/AceDrive.Overlay/   WPF MVVM: Services/ ViewModels/ Localization/
                        Resources/ (Strings.*.resx) Data/ (fares.json)
tests/                  xUnit, fakes for time/telemetry/log/store
```

`Data/*.json` is edited in `src/AceDrive.Overlay/Data/` only. The `Data\` folder
beside a built or published exe is a copy refreshed from that source (and the
package ships its own), so a line changed there is a build artifact: it never
reaches a release, and a clean build overwrites it.

## Troubleshooting

- `waiting for EVO…` with EVO running: shared memory appears once a session
  loads, not in menus. If it never appears after an EVO update, the SHM
  layout likely changed — offsets are isolated in `ShmReader.cs`, logs show
  attach/loss transitions.
- Overlay invisible: use borderless, not exclusive fullscreen. VR headsets
  need a separate OpenVR path (not built yet).
