#!/usr/bin/env node
// End-to-end simulation of two concurrent sessions sharing one checkout.
//   node test-sim.mjs
//
// Captures child output through file descriptors instead of pipes, because the
// agent sandbox (and therefore this test) cannot open pipes for child processes.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const coord = path.join(here, 'coord.mjs');
const install = path.join(here, 'install.mjs');

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'coord-sim-'));
const scratch = fs.mkdtempSync(path.join(here, '.sim-'));
let failures = 0;

function capture(cmd, args, { input, cwd = tmp, env = {} } = {}) {
  const out = path.join(scratch, `cap-${Math.random().toString(16).slice(2)}.out`);
  const fd = fs.openSync(out, 'w');
  let inFd = 'ignore';
  let inPath = null;
  if (input !== undefined) {
    inPath = path.join(scratch, `in-${Math.random().toString(16).slice(2)}.txt`);
    fs.writeFileSync(inPath, input);
    inFd = fs.openSync(inPath, 'r');
  }
  try {
    const r = spawnSync(cmd, args, {
      cwd,
      stdio: [inFd, fd, fd],
      env: { ...process.env, ...env },
    });
    fs.closeSync(fd);
    return { status: r.error ? -1 : r.status, text: fs.readFileSync(out, 'utf8') };
  } finally {
    if (typeof inFd === 'number') fs.closeSync(inFd);
    if (inPath) fs.rmSync(inPath, { force: true });
    try {
      fs.unlinkSync(out);
    } catch {
      /* ignore */
    }
  }
}

function git(args, cwd = tmp) {
  const r = capture('git', args, { cwd });
  if (r.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${r.text}`);
  return r.text.trim();
}

function run(args, opts = {}) {
  return capture(process.execPath, [coord, ...args], {
    ...opts,
    env: { COORD_ROOT: tmp, COORD_TMP: scratch, ...(opts.env || {}) },
  });
}

function check(name, cond, detail = '') {
  if (!cond) failures++;
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${name}${detail && !cond ? `\n      ${detail.replace(/\n/g, '\n      ')}` : ''}`);
}

// --- fixture -------------------------------------------------------------
git(['init', '-q', '-b', 'main']);
git(['config', 'user.email', 'sim@example.test']);
git(['config', 'user.name', 'Sim']);
fs.writeFileSync(path.join(tmp, 'README.md'), 'fixture\n');
git(['add', '.']);
git(['commit', '-q', '-m', 'init']);

// --- presence ------------------------------------------------------------
const a = JSON.parse(run(['init', '--as', 'session-a', '--note', 'refactoring auth']).text);
const b = JSON.parse(run(['init', '--as', 'session-b', '--note', 'fixing tests']).text);
check('init mints distinct sessions', a.id !== b.id && !!a.store, JSON.stringify({ a, b }));

let st = run(['status', '--as', 'session-a']).text;
check('status shows the other session', st.includes('session-b') && st.includes('fixing tests'), st);

// --- claim contention ----------------------------------------------------
let r = run(['claim', 'push:origin/main', '--as', 'session-a', '--reason', 'about to push', '--ttl', '60']);
check('A claims push ref', r.status === 0, r.text);

r = run(['claim', 'push:origin/main', '--as', 'session-b', '--wait', '1', '--ttl', '60']);
check('B is refused while A holds the ref', r.status !== 0 && /held by session-a/.test(r.text), r.text);

st = run(['status', '--as', 'session-b']).text;
check('status surfaces the held claim', st.includes('push:origin/main') && st.includes('session-a'), st);

r = run(['release', 'push:origin/main', '--as', 'session-a']);
check('A releases', r.status === 0, r.text);

r = run(['claim', 'push:origin/main', '--as', 'session-b', '--ttl', '60']);
check('B claims after release', r.status === 0, r.text);

r = run(['release', 'push:origin/main', '--as', 'session-a']);
check('non-owner cannot release', r.status !== 0, r.text);
run(['release', 'push:origin/main', '--as', 'session-b']);

// --- stale lease takeover ------------------------------------------------
const claimsDir = path.join(tmp, '.dsh-coord', 'claims');
run(['claim', 'push:origin/main', '--as', 'dead-session', '--ttl', '60']);
const slug = fs.readdirSync(claimsDir).find((n) => n.endsWith('.json'));
fs.writeFileSync(
  path.join(claimsDir, slug),
  JSON.stringify({
    resource: 'push:origin/main',
    owner: 'dead-session',
    reason: 'crashed',
    createdAt: new Date(Date.now() - 600_000).toISOString(),
    expiresAt: new Date(Date.now() - 1000).toISOString(),
  }),
);
r = run(['claim', 'push:origin/main', '--as', 'session-b', '--ttl', '60']);
check('expired lease is taken over', r.status === 0, r.text);
run(['release', 'push:origin/main', '--as', 'session-b']);

// --- push guard (the hook path) -----------------------------------------
run(['claim', 'push:origin/main', '--as', 'session-a', '--ttl', '60']);
const pushLine =
  'refs/heads/main 1111111111111111111111111111111111111111 refs/heads/main 0000000000000000000000000000000000000000\n';
r = run(['guard', 'push', 'origin', 'git@example.test:repo.git'], { input: pushLine });
check('guard blocks while another session holds the ref', r.status === 1 && /blocked/.test(r.text), r.text);
run(['release', 'push:origin/main', '--as', 'session-a']);

r = run(['guard', 'push', 'origin', 'git@example.test:repo.git'], { input: pushLine });
check('guard allows a clean push', r.status === 0, r.text);

// --- commit guard --------------------------------------------------------
r = run(['guard', 'commit']);
check('commit warns about concurrent sessions but allows', r.status === 0 && /other session/.test(r.text), r.text);

// --- broadcast + inbox ---------------------------------------------------
run(['say', 'rebasing main, hold pushes', '--as', 'session-a']);
let inbox = run(['inbox', '--as', 'session-b']).text;
check('B receives A broadcast', inbox.includes('rebasing main'), inbox);
inbox = run(['inbox', '--as', 'session-b']).text;
check('inbox advances cursor', inbox.includes('empty'), inbox);

// --- serialized commit wrapper releases its lease -----------------------
fs.writeFileSync(path.join(tmp, 'x.txt'), 'x\n');
git(['add', 'x.txt']);
r = run(['commit', '--as', 'session-a', '--wait', '5', '-m', 'add x']);
check('serialized commit runs', r.status === 0, r.text);
const leftover = fs.existsSync(claimsDir) ? fs.readdirSync(claimsDir).filter((n) => n.endsWith('.json')) : [];
check('commit lease released afterwards', leftover.length === 0, leftover.join(', '));

// --- hooks install -------------------------------------------------------
r = capture(process.execPath, [install, '--root', tmp], { env: { COORD_TMP: scratch } });
check('installer reports hooks', r.status === 0 && /installed pre-push/.test(r.text), r.text);
const hook = fs.readFileSync(path.join(tmp, '.git', 'hooks', 'pre-push'), 'utf8');
check('pre-push hook calls the guard', hook.includes('guard push') && hook.includes('coord.mjs'), hook);

// --- token cost sanity ---------------------------------------------------
const statusOut = run(['status', '--as', 'session-a']).text;
check('status stays compact (< 1200 chars)', statusOut.length < 1200, `${statusOut.length} chars`);

console.log(`\n${failures === 0 ? 'ALL PASS' : `${failures} FAILURE(S)`}`);
fs.rmSync(tmp, { recursive: true, force: true });
fs.rmSync(scratch, { recursive: true, force: true });
process.exit(failures === 0 ? 0 : 1);
