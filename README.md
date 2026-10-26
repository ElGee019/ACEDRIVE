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
also copies two machine-local pieces when they exist, so a fresh PC gets voice
and the online layer without setup:

- `%APPDATA%\AceDrive\vosk-model` → `Data\vosk-model` (~68 MB, voice)
- `%APPDATA%\AceDrive\firebase.json` → `Data\firebase.json` (online layer; the
  shipped fallback `FirebaseOptions` reads when no per-user config exists)

Neither is ever committed; override the source paths with `-p:VoskModelDir` /
`-p:FirebaseConfigPath`. The published exe is signed after the single-file
bundle, because the Build-time signature cannot survive bundling. The
development certificate is trusted only on the machine that made it, so
another PC still shows SmartScreen's "Unknown publisher" — trust everywhere
needs a publicly trusted OV/EV certificate (a PFX in `ACEDRIVE_SIGN_PFX`) or
Azure Trusted Signing. Until then the tester unblocks the zip, extracts it and
chooses More info → Run anyway.

`dotnet format ACEDRIVE.slnx --verify-no-changes` must pass before
committing; CI enforces it. Fix findings by hand — never run unattended
fixer passes (one once removed a needed using via an IDE0005 false positive).

## Using the test shell over EVO

1. Start EVO, go on track. Start the overlay.
2. It opens **Locked**: clicks pass through to the game.
3. `Ctrl+Shift+C` unlocks: drag the header to move, corner grip to resize,
   buttons clickable. Same hotkey locks it again to drive.
4. `New job` → `Pickup` → drive: the meter runs off your real speed.
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

## Troubleshooting

- `waiting for EVO…` with EVO running: shared memory appears once a session
  loads, not in menus. If it never appears after an EVO update, the SHM
  layout likely changed — offsets are isolated in `ShmReader.cs`, logs show
  attach/loss transitions.
- Overlay invisible: use borderless, not exclusive fullscreen. VR headsets
  need a separate OpenVR path (not built yet).
