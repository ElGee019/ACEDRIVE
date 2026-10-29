# dsh-coord

Coordination for **multiple agent sessions working in one checkout at the same time**.

You start a fresh session whenever context fills up; several of them can be live in the
same folder. They cannot see each other, so one can `git add -A` over another's
half-finished work, or push a branch while a second session is rebasing it.

`dsh-coord` gives them a shared, file-based channel and a set of **leases** over the
operations that actually hurt when they race:

| Operation | Mechanism |
|---|---|
| `git push` | lease on `push:<remote>/<branch>`; the wrapper waits for it |
| `git commit` | lease on `commit`; serialized commits |
| long edit of a shared area | advisory `claim edit:<path>` so others see the overlap |
| awareness | `status` (who is live, what they hold) and `say` / `inbox` |

Everything lives in `<repo>/.dsh-coord/` and is git-ignored. No daemon, no network, no
dependencies — just Node and the filesystem, so it works across separate app windows
and separate processes.

## Quick start

```sh
# once per session, at the start of work
node tools/dsh-coord/coord.mjs init --as auth-fix --note "refactoring src/auth"

# before pushing (waits up to 5 min for another session to finish)
node tools/dsh-coord/coord.mjs push --as auth-fix --wait 300 -- origin feature/auth

# before committing
node tools/dsh-coord/coord.mjs commit --as auth-fix --wait 120 -m "auth: split token store"

# anytime
node tools/dsh-coord/coord.mjs status --as auth-fix
node tools/dsh-coord/coord.mjs say "rebasing main, hold pushes" --as auth-fix
node tools/dsh-coord/coord.mjs inbox --as auth-fix
```

`init` prints the session id it minted; reuse it as `--as` for the rest of the session
(or export `COORD_ID` once and drop the flag).

## What it looks like

```
time    2026-10-29T19:28:56Z
repo    /src/app  branch=feature/auth
you     auth-fix
others  2 active
  - tests-x   seen=1m  branch=feature/tests  "fixing flaky login test"
  - docs-y    seen=0m  branch=main           "updating README"
tree    3 changed files
claims
  - push:origin/main  by=docs-y  240s left  "git push"
recent
  19:27 docs-y note: rebasing main, hold pushes
  19:28 auth-fix claim: push:origin/feature/auth
```

## How enforcement works

Two layers, because they fail differently:

1. **Wrapper (primary).** `coord push` / `coord commit` take the lease, run git, and
   release it in a `finally`. This is the path that guarantees serialization, and it is
   the only one that works while commands run inside a restrictive agent sandbox.
2. **Git hooks (backstop).** `node install.mjs` writes `pre-push` and `pre-commit` hooks
   that refuse when another session holds the lease, so even a raw `git push` is gated.
   Optional: install only where hooks can execute.

Leases expire (10 min default, 5 min for push), so a crashed or killed session cannot
wedge the repository. `--ttl` and `--wait` are per-call.

## Design notes

- **Identity is explicit.** There is no reliable way to derive a stable per-session id
  from a shell, so `init` mints one and the model carries it. A harness plugin could set
  `COORD_ID` per session and remove that step.
- **No file locks.** Path claims are advisory and only warn; the hard guarantees are on
  commit and push. Use one git worktree per session if you want real isolation.
- **On demand only.** Nothing is injected into the system prompt, so sessions pay for
  coordination only when they call it, and prompt caches are untouched.
- **Bounded output.** `status` is ~15 lines; a full push/commit cycle costs on the order
  of a few hundred tokens.

## Limits

- Concurrent raw `git` invocations by sessions that ignore the wrapper are not prevented.
  The hooks catch them where hooks run; otherwise this is a convention with teeth only
  where you wire it in.
- `claim edit:<path>` does not block writes, it only surfaces overlap in `status`.
- Sessions that never heartbeat (`status`, `say`, `inbox`, `claim`, `push`, `commit`, or
  `heartbeat` all refresh it) disappear from `others` after 45 minutes.
- Under a sandbox that forbids pipe creation, git's shell-based transports and hooks can
  fail outright (`sh.exe: couldn't create signal pipe`). The lease logic still works;
  run pushes from outside the sandbox in that case.

## Files

| File | Purpose |
|---|---|
| `coord.mjs` | the CLI |
| `install.mjs` | installs/removes the git hooks |
| `agents-snippet.md` | rules to paste into the project's `AGENTS.md` |
| `test-sim.mjs` | two-session simulation, 19 checks |
