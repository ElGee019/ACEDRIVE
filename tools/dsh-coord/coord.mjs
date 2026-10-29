#!/usr/bin/env node
// dsh-coord — cross-session coordination for agents sharing one project checkout.
// Dependency-free. State lives in <root>/.dsh-coord and is shared by every
// session whose cwd is inside that checkout (git worktrees get their own root).
//
// Design:
//   sessions/<id>.json   self-written heartbeat; one writer per file
//   claims/<slug>.json   exclusive-create (O_EXCL) leases; the only mutual exclusion
//   log.jsonl            append-only broadcast timeline
//   cursor/<id>          per-session read position into log.jsonl
//
// Identity: --as <id>, or COORD_ID, or a per-invocation anonymous id.

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';

const TTL_SESSION_MS = Number(process.env.COORD_SESSION_TTL_MS || 45 * 60_000);
const TTL_DEFAULT_MS = Number(process.env.COORD_CLAIM_TTL_MS || 10 * 60_000);

// ---------- plumbing ----------

function die(msg, code = 1) {
  process.stderr.write(msg.endsWith('\n') ? msg : msg + '\n');
  process.exit(code);
}

function now() {
  return Date.now();
}

function iso(t) {
  return new Date(t).toISOString();
}

function sleepSync(ms) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

// Git output is captured through a file descriptor, never a pipe: sandboxed
// agent shells forbid opening pipes for child processes (EPERM), while a file
// handle works everywhere.
function git(args, cwd) {
  const dir = process.env.COORD_TMP || os.tmpdir();
  const file = path.join(dir, `dsh-coord-${process.pid}-${crypto.randomBytes(4).toString('hex')}.out`);
  let fd = null;
  try {
    fd = fs.openSync(file, 'w');
    const r = spawnSync('git', args, { cwd, stdio: ['ignore', fd, fd] });
    fs.closeSync(fd);
    fd = null;
    if (r.error || r.status !== 0) return '';
    return fs.readFileSync(file, 'utf8').trim();
  } catch {
    return '';
  } finally {
    if (fd !== null) {
      try {
        fs.closeSync(fd);
      } catch {
        /* ignore */
      }
    }
    try {
      fs.unlinkSync(file);
    } catch {
      /* ignore */
    }
  }
}

function parseArgs(argv) {
  const positional = [];
  const flags = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--') {
      positional.push(...argv.slice(i + 1));
      break;
    }
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const next = argv[i + 1];
      if (next !== undefined && !next.startsWith('--')) {
        flags[key] = next;
        i++;
      } else {
        flags[key] = true;
      }
    } else {
      positional.push(a);
    }
  }
  return { positional, flags };
}

function repoRoot(explicit) {
  if (explicit) return path.resolve(explicit);
  if (process.env.COORD_ROOT) return path.resolve(process.env.COORD_ROOT);
  const top = git(['rev-parse', '--show-toplevel'], process.cwd());
  return top ? path.resolve(top) : process.cwd();
}

function store(root) {
  const dir = process.env.COORD_DIR
    ? path.resolve(process.env.COORD_DIR)
    : path.join(root, '.dsh-coord');
  for (const sub of ['sessions', 'claims', 'cursor']) {
    fs.mkdirSync(path.join(dir, sub), { recursive: true });
  }
  const ignore = path.join(dir, '.gitignore');
  if (!fs.existsSync(ignore)) fs.writeFileSync(ignore, '*\n!.gitignore\n');
  return dir;
}

function readJson(file, fallback = null) {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch {
    return fallback;
  }
}

function writeJson(file, value) {
  const tmp = `${file}.${process.pid}.${crypto.randomBytes(4).toString('hex')}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(value, null, 2));
  fs.renameSync(tmp, file);
}

function slugFor(resource) {
  const safe = resource.toLowerCase().replace(/[^a-z0-9._-]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 60);
  const hash = crypto.createHash('sha1').update(resource).digest('hex').slice(0, 8);
  return `${safe || 'res'}_${hash}`;
}

function currentBranch(root) {
  return git(['rev-parse', '--abbrev-ref', 'HEAD'], root) || '(none)';
}

function shortId(id) {
  return id.length > 12 ? `${id.slice(0, 6)}..${id.slice(-4)}` : id;
}

// ---------- identity ----------

function resolveId(flags) {
  return String(flags.as || process.env.COORD_ID || '').trim() || null;
}

function requireId(flags, verb) {
  const id = resolveId(flags);
  if (!id) die(`coord ${verb} needs an identity: pass --as <id> or set COORD_ID.\nRun "coord init" once per session to get one.`);
  return id;
}

// ---------- sessions ----------

function touchSession(dir, id, root, patch = {}) {
  const file = path.join(dir, 'sessions', `${encodeURIComponent(id)}.json`);
  const prev = readJson(file, {});
  writeJson(file, {
    id,
    pid: process.pid,
    cwd: patch.cwd || prev.cwd || root,
    branch: patch.branch || prev.branch || currentBranch(root),
    startedAt: prev.startedAt || iso(now()),
    heartbeatAt: iso(now()),
    note: patch.note !== undefined ? patch.note : prev.note || '',
  });
}

function activeSessions(dir, { includeSelf = null } = {}) {
  const cutoff = now() - TTL_SESSION_MS;
  const out = [];
  for (const name of fs.readdirSync(path.join(dir, 'sessions'))) {
    if (!name.endsWith('.json')) continue;
    const s = readJson(path.join(dir, 'sessions', name));
    if (!s || !s.id) continue;
    if (includeSelf && s.id === includeSelf) continue;
    if (Date.parse(s.heartbeatAt) < cutoff) continue;
    out.push(s);
  }
  return out.sort((a, b) => a.id.localeCompare(b.id));
}

// ---------- claims (the only hard guarantee) ----------

function claimFile(dir, resource) {
  return path.join(dir, 'claims', `${slugFor(resource)}.json`);
}

function readClaim(dir, resource) {
  return readJson(claimFile(dir, resource));
}

function claimIsStale(claim) {
  return !claim || Date.parse(claim.expiresAt) <= now();
}

function acquire(dir, resource, { owner, ttlMs = TTL_DEFAULT_MS, reason = '', waitMs = 0 }) {
  const file = claimFile(dir, resource);
  const deadline = now() + waitMs;
  for (;;) {
    const claim = {
      resource,
      owner,
      reason,
      createdAt: iso(now()),
      expiresAt: iso(now() + ttlMs),
    };
    try {
      fs.writeFileSync(file, JSON.stringify(claim, null, 2), { flag: 'wx' });
      return claim;
    } catch (err) {
      if (err.code !== 'EEXIST') throw err;
    }
    const held = readClaim(dir, resource);
    if (held && held.owner === owner) {
      // renewal: replace our own lease
      writeJson(file, { ...held, reason: reason || held.reason, expiresAt: iso(now() + ttlMs) });
      return readClaim(dir, resource);
    }
    if (claimIsStale(held)) {
      try {
        fs.unlinkSync(file);
      } catch {
        /* someone else reaped it; retry */
      }
      continue;
    }
    if (now() >= deadline) {
      const left = Math.max(0, Math.ceil((Date.parse(held.expiresAt) - now()) / 1000));
      die(
        `resource "${resource}" is held by ${held.owner}${held.reason ? ` (${held.reason})` : ''} for another ${left}s.`,
      );
    }
    sleepSync(500);
  }
}

function release(dir, resource, owner) {
  const file = claimFile(dir, resource);
  const held = readJson(file);
  if (!held) return false;
  if (held.owner !== owner) die(`resource "${resource}" is held by ${held.owner}, not ${owner}.`);
  fs.unlinkSync(file);
  return true;
}

// ---------- log ----------

function logPath(dir) {
  return path.join(dir, 'log.jsonl');
}

function appendLog(dir, entry) {
  fs.appendFileSync(logPath(dir), JSON.stringify(entry) + '\n');
}

function readLog(dir, sinceTs = 0) {
  let text = '';
  try {
    text = fs.readFileSync(logPath(dir), 'utf8');
  } catch {
    return [];
  }
  const out = [];
  for (const line of text.split('\n')) {
    if (!line.trim()) continue;
    try {
      const entry = JSON.parse(line);
      if (entry.ts > sinceTs) out.push(entry);
    } catch {
      /* ignore torn line */
    }
  }
  return out;
}

function cursorFile(dir, id) {
  return path.join(dir, 'cursor', `${encodeURIComponent(id)}.json`);
}

// ---------- commands ----------

function cmdInit({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = resolveId(flags) || `${path.basename(root).slice(0, 12)}-${crypto.randomBytes(3).toString('hex')}`;
  touchSession(dir, id, root, { note: flags.note ? String(flags.note) : '' });
  appendLog(dir, { ts: now(), from: id, kind: 'join', text: flags.note ? String(flags.note) : 'joined' });
  process.stdout.write(JSON.stringify({ id, store: dir, branch: currentBranch(root) }, null, 2) + '\n');
}

function cmdHeartbeat({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'heartbeat');
  touchSession(dir, id, root, { note: flags.note !== undefined ? String(flags.note) : undefined });
  process.stdout.write('ok\n');
}

function cmdStatus({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const me = resolveId(flags);
  if (me) touchSession(dir, me, root);

  const others = activeSessions(dir, { includeSelf: me });
  const claims = fs
    .readdirSync(path.join(dir, 'claims'))
    .map((n) => readJson(path.join(dir, 'claims', n)))
    .filter((c) => c && !claimIsStale(c));

  const lines = [];
  lines.push(`time    ${iso(now())}`);
  lines.push(`repo    ${root}  branch=${currentBranch(root)}`);
  if (me) lines.push(`you     ${shortId(me)}`);

  if (others.length === 0) {
    lines.push('others  none active');
  } else {
    lines.push(`others  ${others.length} active`);
    for (const s of others) {
      const ageMin = Math.round((now() - Date.parse(s.heartbeatAt)) / 60000);
      lines.push(`  - ${shortId(s.id)}  seen=${ageMin}m  branch=${s.branch}${s.note ? `  "${s.note}"` : ''}`);
    }
  }

  const dirty = git(['status', '--porcelain'], root).split('\n').filter(Boolean).length;
  lines.push(`tree    ${dirty} changed file${dirty === 1 ? '' : 's'}`);

  if (claims.length) {
    lines.push('claims');
    for (const c of claims) {
      const left = Math.max(0, Math.ceil((Date.parse(c.expiresAt) - now()) / 1000));
      lines.push(`  - ${c.resource}  by=${shortId(c.owner)}  ${left}s left${c.reason ? `  "${c.reason}"` : ''}`);
    }
  } else {
    lines.push('claims  none');
  }

  const recent = readLog(dir, now() - 2 * 3600_000).slice(-6);
  if (recent.length) {
    lines.push('recent');
    for (const e of recent) {
      const t = iso(e.ts).slice(11, 16);
      lines.push(`  ${t} ${shortId(e.from)} ${e.kind}: ${String(e.text || '').slice(0, 100)}`);
    }
  }

  process.stdout.write(lines.join('\n') + '\n');
}

function cmdSay({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'say');
  const text = positional.slice(1).join(' ').trim();
  if (!text) die('coord say needs text.');
  touchSession(dir, id, root);
  appendLog(dir, { ts: now(), from: id, kind: String(flags.kind || 'note'), text: text.slice(0, 2000) });
  process.stdout.write('said\n');
}

function cmdInbox({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'inbox');
  touchSession(dir, id, root);
  const cur = readJson(cursorFile(dir, id), { ts: 0 });
  const entries = readLog(dir, cur.ts || 0).filter((e) => e.from !== id);
  const maxTs = entries.reduce((m, e) => Math.max(m, e.ts), cur.ts || 0);
  writeJson(cursorFile(dir, id), { ts: maxTs });
  if (!entries.length) {
    process.stdout.write('inbox  empty\n');
    return;
  }
  for (const e of entries) {
    process.stdout.write(`${iso(e.ts).slice(11, 16)} ${shortId(e.from)} [${e.kind}] ${e.text}\n`);
  }
}

function cmdClaim({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'claim');
  const resource = positional[1];
  if (!resource) die('coord claim needs a resource, e.g. "push:origin/main" or "edit:src/auth".');
  touchSession(dir, id, root);
  const claim = acquire(dir, resource, {
    owner: id,
    ttlMs: Number(flags.ttl || 0) * 1000 || TTL_DEFAULT_MS,
    reason: flags.reason ? String(flags.reason) : '',
    waitMs: Number(flags.wait || 0) * 1000,
  });
  appendLog(dir, { ts: now(), from: id, kind: 'claim', text: resource });
  process.stdout.write(`claimed ${resource} until ${claim.expiresAt}\n`);
}

function cmdRelease({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'release');
  const resource = positional[1];
  if (!resource) die('coord release needs a resource.');
  const ok = release(dir, resource, id);
  appendLog(dir, { ts: now(), from: id, kind: 'release', text: resource });
  process.stdout.write(ok ? `released ${resource}\n` : `no claim on ${resource}\n`);
}

function runGit(args, root) {
  const r = spawnSync('git', args, { cwd: root, stdio: 'inherit' });
  return r.status === null ? 1 : r.status;
}

function cmdPush({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'push');
  touchSession(dir, id, root);
  const remote = String(flags.remote || 'origin');
  const branch = String(flags.branch || currentBranch(root));
  const resource = `push:${remote}/${branch}`;
  acquire(dir, resource, {
    owner: id,
    ttlMs: 5 * 60_000,
    reason: 'git push',
    waitMs: Number(flags.wait || 0) * 1000,
  });
  appendLog(dir, { ts: now(), from: id, kind: 'push-start', text: `${remote}/${branch}` });
  try {
    const args = ['push', ...(flags.remote ? [remote] : []), ...positional.slice(1)];
    return runGit(args.length > 1 ? args : ['push'], root);
  } finally {
    release(dir, resource, id);
    appendLog(dir, { ts: now(), from: id, kind: 'push-end', text: `${remote}/${branch}` });
  }
}

function cmdCommit({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const id = requireId(flags, 'commit');
  touchSession(dir, id, root);
  acquire(dir, 'commit', { owner: id, ttlMs: 2 * 60_000, reason: 'git commit', waitMs: Number(flags.wait || 0) * 1000 });
  appendLog(dir, { ts: now(), from: id, kind: 'commit-start', text: '' });
  try {
    return runGit(['commit', ...positional.slice(1)], root);
  } finally {
    release(dir, 'commit', id);
    appendLog(dir, { ts: now(), from: id, kind: 'commit-end', text: '' });
  }
}

// ---------- guard: invoked by git hooks ----------

function cmdGuard({ positional, flags }) {
  const [what, ...rest] = positional.slice(1);
  const root = repoRoot(flags.root);
  const dir = store(root);
  const me = resolveId(flags) || `hook-${process.pid}`;

  if (what === 'push') {
    const stdin = process.stdin.isTTY ? '' : fs.readFileSync(0, 'utf8').trim();
    let refs = stdin
      .split('\n')
      .map((l) => l.split(/\s+/).filter(Boolean))
      .filter((p) => p.length >= 4)
      .map((p) => p[2].replace(/^refs\/heads\//, ''));
    // A sandboxed hook may not receive stdin; fall back to the checked-out branch
    // so the common case is still gated rather than silently allowed.
    if (!refs.length) refs = [currentBranch(root)];
    const remote = rest[0] || 'origin';
    const blocked = [];
    for (const ref of refs) {
      const resource = `push:${remote}/${ref}`;
      const held = readClaim(dir, resource);
      if (held && !claimIsStale(held) && held.owner !== me) blocked.push({ resource, held });
    }
    // a push is also unsafe while another session is mid-commit in this tree
    const commitHolder = readClaim(dir, 'commit');
    if (commitHolder && !claimIsStale(commitHolder) && commitHolder.owner !== me) {
      blocked.push({ resource: 'commit', held: commitHolder });
    }
    if (blocked.length) {
      process.stderr.write('dsh-coord: push blocked to avoid racing another session\n');
      for (const b of blocked) {
        const left = Math.max(0, Math.ceil((Date.parse(b.held.expiresAt) - now()) / 1000));
        process.stderr.write(`  ${b.resource} held by ${b.held.owner} (${left}s left)\n`);
      }
      process.stderr.write('Wait, or serialize with: node tools/dsh-coord/coord.mjs push --as <id> --wait 300\n');
      return 1;
    }
    return 0;
  }

  if (what === 'commit') {
    if (process.env.COORD_ALLOW_CONCURRENT === '1') return 0;
    const others = activeSessions(dir, { includeSelf: resolveId(flags) });
    const pushHolder = readClaim(dir, 'push:origin/' + currentBranch(root));
    const commitHolder = readClaim(dir, 'commit');
    const hard = [commitHolder, pushHolder].find((c) => c && !claimIsStale(c) && c.owner !== me);
    if (hard) {
      const left = Math.max(0, Math.ceil((Date.parse(hard.expiresAt) - now()) / 1000));
      process.stderr.write(`dsh-coord: commit blocked — ${hard.resource} held by ${hard.owner} (${left}s left).\n`);
      return 1;
    }
    if (others.length) {
      process.stderr.write(
        `dsh-coord: warning — ${others.length} other session(s) active: ${others.map((s) => shortId(s.id)).join(', ')}.\n` +
          '  "git commit -a" will capture their in-progress edits. Prefer a serialized commit:\n' +
          '  node tools/dsh-coord/coord.mjs commit --as <id> -m "..."\n' +
          '  Set COORD_ALLOW_CONCURRENT=1 to silence this warning.\n',
      );
    }
    return 0;
  }

  die(`unknown guard target: ${what || '(none)'}`);
}

function cmdDoctor({ positional, flags }) {
  const root = repoRoot(flags.root);
  const dir = store(root);
  const hooks = git(['rev-parse', '--git-path', 'hooks'], root) || path.join(root, '.git', 'hooks');
  const hooksDir = path.resolve(root, hooks);
  const out = {
    root,
    store: dir,
    branch: currentBranch(root),
    hooksDir,
    prePush: fs.existsSync(path.join(hooksDir, 'pre-push')),
    preCommit: fs.existsSync(path.join(hooksDir, 'pre-commit')),
    activeSessions: activeSessions(dir, { includeSelf: resolveId(flags) }).map((s) => s.id),
  };
  process.stdout.write(JSON.stringify(out, null, 2) + '\n');
}

// ---------- main ----------

const HELP = `dsh-coord — coordinate concurrent agent sessions in one checkout

  init [--as ID] [--note TEXT]     mint a session id and announce presence
  status [--as ID]                 who is active, what they hold, recent notes
  heartbeat [--as ID] [--note T]   refresh presence (do this between long turns)
  say TEXT [--as ID]               broadcast an intent note
  inbox [--as ID]                  read notes from other sessions since last read
  claim RESOURCE [--ttl S] [--wait S] [--reason T] [--as ID]
  release RESOURCE [--as ID]
  push [--wait S] -- <git push args>     serialized push
  commit [--wait S] -- <git commit args> serialized commit
  guard push|commit                hook entry point (reads stdin)
  doctor                           show paths and hook state

Resources: push:<remote>/<ref>, commit, edit:<path-prefix>
Common: --root DIR, --as ID; env COORD_ID, COORD_ROOT, COORD_DIR,
        COORD_SESSION_TTL_MS, COORD_CLAIM_TTL_MS, COORD_ALLOW_CONCURRENT=1
`;

const COMMANDS = {
  init: cmdInit,
  heartbeat: cmdHeartbeat,
  status: cmdStatus,
  say: cmdSay,
  inbox: cmdInbox,
  claim: cmdClaim,
  release: cmdRelease,
  push: cmdPush,
  commit: cmdCommit,
  guard: cmdGuard,
  doctor: cmdDoctor,
};

const argv = process.argv.slice(2);
const { positional, flags } = parseArgs(argv);
const name = positional[0];
if (!name || name === 'help' || flags.help) {
  process.stdout.write(HELP);
  process.exit(name ? 0 : 1);
}
const cmd = COMMANDS[name];
if (!cmd) die(`unknown command: ${name}\n\n${HELP}`);
const code = cmd({ positional, flags });
if (typeof code === 'number') process.exit(code);
