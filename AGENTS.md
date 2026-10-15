# ACEDRIVE — agent instructions (auto-loaded)

Big, long-lived project: external WPF gameplay overlay for Assetto Corsa EVO
freeroam (Eifel). Gameplay port of LATESTDRIVEBS2 concepts, NOT a code port.
Source of truth for gameplay scope: `PORTPLAN.md` (repo root).
Telemetry offsets: live-telemetry-evo `docs/SHARED_MEMORY.md`
(first 416 physics bytes are AC1-compatible, `_pack_ = 4`).

## Role

Act as a senior software engineer. SOLID and clean code are mandatory,
not aspirational: a task is not done until the diff complies. When an
existing violation blocks the task, fix the smallest part that unblocks
it and report the rest — never extend the violation.

- **Single Responsibility** — a View draws; a ViewModel owns state + commands;
  a service owns I/O. Never mix presentation, logic, and data in one class.
  `MainWindow` code-behind owns window chrome only (drag/resize, hotkey hook,
  close/save). Smells: a ViewModel formatting sentences (copy belongs in
  resx), a view touching files/HWNDs, a screen opening another screen.
- **Open/Closed** — extend through interfaces and composition, not by editing
  stable classes. New job type = new implementation, not new `if` branches
  in existing code. Smells: a new screen adding a verb to a stable
  interface, a new case lengthening an `if`/`switch` chain in an old class.
- **Liskov Substitution** — fakes in tests substitute for real services
  without the caller knowing (`FakeTelemetry`, `FakeClock`, `TestLog`).
  Smell: a test needing a real service, clock, or window to pass.
- **Interface Segregation** — small focused interfaces (`ITelemetrySource`,
  `IClock`, `ISettingsStore`, `ILog`). No god interfaces. Smell: an
  interface growing a member per use case; a class implementing members
  it ignores.
- **Dependency Inversion** — constructors take interfaces.
  `App.OnStartup` is the ONLY composition root — no `new ShmReader()`,
  no `new FileLogger()`, no statics-as-services inside ViewModels/views.
  **No singletons for stateful services** (no `*.Instance`); the root news
  up one instance and passes it down. Smells: `new` of a service outside
  the root, `*.Instance`, a bare `catch (Exception)` (CA1031 — catch
  specific types with a WHY comment).

Balance with YAGNI: no DI container, no event bus, no framework until the
third use case forces it.

## Architecture

```
src/AceDrive.Core/      PURE logic. No WPF, no System.IO, no telemetry
                        types, no UI strings. Dependency-free so a future
                        EVO Lua port transliterates it. Time via IClock,
                        logging via ILog (interface only), randomness via
                        seeded RNG. Deterministic and fully unit-tested.
src/AceDrive.Overlay/
  Services/             I/O + Win32 + SHM (ShmReader, FileLogger,
                        JsonSettingsStore, JsonFareStore, Native).
                        All byte offsets live in ShmReader, nowhere else.
  ViewModels/           State + ICommand. Knows nothing about HWNDs/files.
  Localization/         Strings.*.resx + LocExtension. No hardcoded UI copy.
  Data/                 Balance/content data (fares.json, …). See below.
  App.xaml.cs           Sole composition root.
tests/                  Mirrors src. Core rules + ViewModel flows tested
                        with fakes (`tests/*/Fakes/`, one fake per file).
                        UI chrome is not unit-tested.
```

## Screens & navigation (event-driven, when we get there)

- Screens never open each other directly; navigation goes through a
  screen-controller interface (to be built with the second screen).
- POI/marker pattern (from the Unity project): generator → data →
  service → visual. Data and visuals never touch each other directly.

## The one allowed loop

Polling is banned except for exactly one place: the 10 Hz telemetry
`Poll()` in `MainViewModel`. Hot-path discipline inside it and `ShmReader`:

- No LINQ, no per-tick allocations, reuse buffers/views.
- Logging on state transitions only (attached/lost, job events) — never
  per tick.

## Time

Real time, never tick counts: `DateTime.UtcNow` flows through `IClock`.
Never count polls for timed behaviour. A dedicated game-clock service
firing change events arrives with PORTPLAN item 50 — not before.

## Data-driven (no magic values, no hardcoded copy)

- Balance numbers (fares, rates, tips) live in `Data/*.json`, loaded at
  startup (`JsonFareStore`), passed down as records (`FareTable`).
  `FareTable.Default` is the test-shell fallback, not the source of truth.
- User-facing text lives in `Resources/Strings.resx` (en) + satellites
  (`Strings.nl.resx`, …) — never in code. XAML uses `{loc:Loc Key=…}`;
  code uses `Strings.Format` (lookup in `CurrentUICulture`,
  number formatting in `CurrentCulture`).
- Core returns DATA (amounts, distances, phases) — never sentences.
- Placeholders (`{0}`, `{1:0.0}`) must survive translation; translators may
  reorder, never remove.

## Clean code

Working code that violates these rules still fails review.

- One type per file, filename = type name, namespace matches folder.
  Only accepted exception: a small DTO/EventArgs co-located with the
  interface that owns it.
- Methods short; extract helpers aggressively. DRY: shared UI patterns go
  to helpers/styles, never copy-paste. Composition over copy-paste.
- `_camelCase` privates, `PascalCase` public members. No hungarian notation.
- Comments explain WHY (trade-offs, rationale), never WHAT.
- Tackle only what was asked. No unasked changes, no drive-by refactors.

## Error handling & robustness

- `Try*` patterns at every external boundary (SHM, files, processes).
- Never throw out of `Poll()` or shutdown paths; degrade gracefully
  ("waiting for EVO…", built-in defaults when data files are missing).
- Log recoverable failures with context via `ILog` (file sink:
  `%AppData%\AceDrive\logs\acedrive.log.MM-dd`, PitVox-style).
  No `Console.WriteLine`, no `Debug.WriteLine` in shipped code.

## Telemetry rules

- Attach read-only via `MemoryMappedFile.OpenExisting(@"Local\acevo_pmf_*")`.
  Never create the mapping (that masks "game not running").
- Multiple readers coexist (PitVox, SimHub) — never lock, never write.
- Tolerate torn reads; treat offsets as versioned (log `sm_version` when
  adding fields). EVO breaks this API on updates — isolate the blast radius
  in `ShmReader`.

## Definition of done (every task)

- SOLID holds: no new presentation/logic/I-O mixing, no new branches in
  stable classes for new types, no service constructed outside
  `App.OnStartup`, no god interfaces, fakes still substitute cleanly.
- Clean holds: one type per file, short methods, no magic values or
  hardcoded copy (resx + `Data/`), WHY-comments, naming per convention.
- Verified: `dotnet test` green with zero warnings, `dotnet format
  --verify-no-changes` clean on touched files, behaviour confirmed by
  execution (tests or live EVO run).

## Workflow

- `dotnet build ACEDRIVE.slnx`, `dotnet test ACEDRIVE.slnx`. Zero warnings —
  enforced by `Directory.Build.props` (Recommended analyzers +
  `TreatWarningsAsErrors`). New analyzer errors are fixed, not suppressed;
  a suppression needs a justification comment.
- `dotnet format ACEDRIVE.slnx --verify-no-changes` before committing; CI
  enforces it. Fix findings by hand, never via unattended fixer runs (an
  IDE0005 false positive once deleted a needed using).
- Repo works in CRLF (`* text=auto` + `core.autocrlf=true`), and format
  enforces it with `ENDOFLINE` errors. File-creation tools emit LF: after
  any whole-file write or creation, convert the touched files to CRLF
  before building/formatting instead of discovering it later.
- Verify through execution (tests; run the overlay against real EVO).
- Tests are culture-deterministic (`en-US` set in test setup); product code
  formats with `CurrentCulture`.
- Never commit secrets (API keys, `config.json`). Never touch the Steam
  install dir; the overlay lives in `%LocalAppData%\AceDrive`, settings in
  `%AppData%\AceDrive`.

## Token optimization (do not read — churn noise)

`bin/`, `obj/`, `TestResults/`, `.vs/`, `*.user`, generated designer files.
