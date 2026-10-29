## Concurrent sessions in this checkout

Several agent sessions may be working in this folder at the same time. They cannot see
each other's conversation, so coordinate through `tools/dsh-coord/coord.mjs`.

**At the start of work** (once per session), announce yourself and remember the id:

```sh
node tools/dsh-coord/coord.mjs init --as <short-session-name> --note "<what you are doing>"
```

Use that name as `--as` in every later coord call, or export `COORD_ID=<name>` once.

**Before any commit or push:**

```sh
node tools/dsh-coord/coord.mjs status --as <name>
```

- If `others` lists another active session, or `claims` shows a held lease, do **not**
  use raw `git commit` / `git push`. Use the serialized wrappers instead.
- Commit: `node tools/dsh-coord/coord.mjs commit --as <name> --wait 120 -m "..."`
- Push: `node tools/dsh-coord/coord.mjs push --as <name> --wait 300 -- origin <branch>`
  (omit `--wait` to fail fast instead of waiting)

**Before touching a large shared area** (optional, advisory):

```sh
node tools/dsh-coord/coord.mjs claim edit:src/auth --ttl 900 --reason "splitting token store" --as <name>
node tools/dsh-coord/coord.mjs release edit:src/auth --as <name>
```

**When starting something destructive** (rebase, reset, branch switch, mass rewrite),
announce it so the other sessions hold off:

```sh
node tools/dsh-coord/coord.mjs say "rebasing main onto origin/main, hold commits for 10m" --as <name>
```

**Check what others said** before beginning a turn and before finishing one:

```sh
node tools/dsh-coord/coord.mjs inbox --as <name>
```

Never run `git reset --hard`, `git checkout -B`, `git stash`, or `git clean` while
`status` shows another active session; those rewrite a working tree that both sessions
share. If you need isolation, ask the user to give this session its own worktree
(`.agents/worktrees/<name>`) before starting.
